import 'dart:async';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:multicast_dns/multicast_dns.dart';

import '../config/api_config.dart';
import '../school_binding/school_binding.dart';
import '../school_binding/school_binding_gate.dart';
import '../cache/school_scoped_preferences.dart';
import '../school_binding/school_binding_repository.dart';
import '../connection/connection_context.dart';
import 'endpoint_stability_policy.dart';
import '../school_binding/server_instance_binding_sync.dart';
import '../school_binding/server_instance_recovery_service.dart';
import 'discovery_constants.dart';
import 'discovery_models.dart';
import 'school_discovery_policy.dart';

final class _BindingDiscoveryContext {
  const _BindingDiscoveryContext({
    required this.filterByBinding,
    this.binding,
  });

  final bool filterByBinding;
  final SchoolBinding? binding;
}

/// Porte d'entrée unique Mobile pour découvrir le serveur API.
///
/// Local = endpoint privé joignable dont l’identité correspond à l’école active.
/// La découverte scanne le LAN ; une adresse mémorisée peut traverser un LAN routé.
class LocalServerDiscovery {
  LocalServerDiscovery({
    SchoolBindingRepository? bindingRepository,
    Future<HealthInfo?> Function(String, Duration)? healthProbe,
    Future<List<String>> Function()? prefixProvider,
    Future<List<String>> Function()? candidateProvider,
    Future<String?> Function(String schoolId)? lastKnownLoader,
    Future<void> Function(String schoolId, String baseUrl)? lastKnownSaver,
    EndpointStabilityPolicy? stability,
  })  : _repository = bindingRepository,
        _healthProbe = healthProbe,
        _prefixProvider = prefixProvider,
        _candidateProvider = candidateProvider,
        _lastKnownLoader = lastKnownLoader,
        _lastKnownSaver = lastKnownSaver,
        _stability = stability ?? EndpointStabilityPolicy();

  static final LocalServerDiscovery instance = LocalServerDiscovery();
  final SchoolBindingRepository? _repository;
  SchoolBindingRepository get _bindings =>
      _repository ?? SchoolBindingGate.bindingRepository;
  final Future<HealthInfo?> Function(String, Duration)? _healthProbe;
  final Future<List<String>> Function()? _prefixProvider;
  final Future<List<String>> Function()? _candidateProvider;
  final Future<String?> Function(String)? _lastKnownLoader;
  final Future<void> Function(String, String)? _lastKnownSaver;
  final EndpointStabilityPolicy _stability;
  DiscoveryResult _current = DiscoveryResult.detecting;
  Future<DiscoveryResult>? _inFlight;
  int _generation = 0;
  DiscoveryResult get current => _current;
  final _controller = StreamController<DiscoveryResult>.broadcast();
  Stream<DiscoveryResult> get changes => _controller.stream;

  void reset() {
    ++_generation;
    _inFlight = null;
    _publish(DiscoveryResult.detecting);
  }

  Future<DiscoveryResult> discover({bool force = false}) {
    if (!force && _inFlight != null) return _inFlight!;
    final gen = ++_generation;
    final contextGeneration = ConnectionContext.generation;
    final future = _runBounded(gen, contextGeneration);
    _inFlight = future;
    unawaited(future.whenComplete(() {
      if (identical(_inFlight, future)) _inFlight = null;
    }));
    return future;
  }

  Future<DiscoveryResult> rediscover() => discover(force: true);
  // Always searches locally before cloud. An established local endpoint is
  // checked first, so this remains inexpensive while the local server is healthy.
  Future<DiscoveryResult> recheck() => discover();

  bool _isCurrent(int gen, int contextGeneration) =>
      gen == _generation && contextGeneration == ConnectionContext.generation;

  Future<bool> _isActive(
      _BindingDiscoveryContext ctx, int gen, int epoch) async {
    if (!_isCurrent(gen, epoch)) return false;
    final active = await _bindings.load();
    return _isCurrent(gen, epoch) &&
        active != null &&
        ctx.binding != null &&
        SchoolDiscoveryPolicy.schoolIdsMatch(
            active.schoolId, ctx.binding!.schoolId);
  }

  Future<DiscoveryResult> _runBounded(int gen, int epoch) async {
    try {
      return await _run(gen, epoch)
          .timeout(DiscoveryConstants.discoveryOverallTimeout);
    } on TimeoutException {
      if (!_isCurrent(gen, epoch)) return _current;
      // Invalidate unfinished mDNS/scan work before the final cloud check.
      final nextGen = ++_generation;
      final binding = await _bindings.load();
      if (binding == null || !_isCurrent(nextGen, epoch)) return _current;
      final ctx =
          _BindingDiscoveryContext(filterByBinding: true, binding: binding);
      final remote = await _tryRemote(ctx);
      if (!await _isActive(ctx, nextGen, epoch)) return _current;
      return _publish(remote ??
          DiscoveryResult.offline(
              'Aucun serveur de l’établissement actif n’est accessible.'));
    } catch (error) {
      if (!_isCurrent(gen, epoch)) return _current;
      debugPrint('[Discovery] Recherche interrompue: ${error.runtimeType}');
      return _publish(DiscoveryResult.offline(
          'Impossible de vérifier le serveur de l’établissement. Réessayez.'));
    }
  }

  Future<DiscoveryResult> _run(int gen, int epoch) async {
    final binding = await _bindings.load();
    if (!_isCurrent(gen, epoch)) return _current;
    if (binding == null || binding.schoolId.trim().isEmpty) {
      return _publish(DiscoveryResult.offline(
          'Sélectionnez un établissement avec son QR code.'));
    }
    final ctx =
        _BindingDiscoveryContext(filterByBinding: true, binding: binding);
    if (_current.schoolId != null &&
        !SchoolDiscoveryPolicy.schoolIdsMatch(
            _current.schoolId, binding.schoolId)) {
      _publish(DiscoveryResult.detecting);
    }
    final prefixes = await (_prefixProvider?.call() ?? _localPrefixes());
    final last = await _loadLast(binding.schoolId);
    final tried = <String>{};
    final established = _current.isLocal &&
        SchoolDiscoveryPolicy.schoolIdsMatch(
            _current.schoolId, binding.schoolId);
    final known = <String>{
      if (established && _current.baseUrl != null) _current.baseUrl!,
      if (last != null) last,
      ...ApiConfig.localBaseUrlCandidates,
    };

    Future<DiscoveryResult?> check(String raw, {bool retry = false}) async {
      if (!await _isActive(ctx, gen, epoch)) return null;
      final base = ApiConfig.normalize(raw);
      if (!ApiConfig.isValidBaseUrl(base) ||
          !tried.add(base) ||
          _isVirtualBaseUrl(base) ||
          _isCloudBaseUrl(base, ctx)) {
        return null;
      }
      final host = _hostOf(base);
      if (host == null ||
          (!DiscoveryConstants.isPrivateIpv4(host) &&
              !(kIsWeb && DiscoveryConstants.isLoopbackHost(host)))) {
        return null;
      }
      final health = await _stability.checkLocal<HealthInfo>(
        established: retry,
        isCurrent: () => _isCurrent(gen, epoch),
        probe: (timeout) async {
          final response = await _probe(base, timeout);
          if (response == null ||
              response.server.trim().toLowerCase() == 'cloud' ||
              !SchoolDiscoveryPolicy.acceptsHealthForBinding(
                  response, binding)) {
            return null;
          }
          return response;
        },
      );
      if (health == null) return null;
      return DiscoveryResult(
          mode: DiscoveryMode.local,
          source: DiscoverySource.lastKnown,
          baseUrl: base,
          schoolId: binding.schoolId,
          health: health,
          message: 'Serveur local — ${binding.schoolName}');
    }

    Future<DiscoveryResult?> finishLocal(DiscoveryResult? result) async {
      if (result == null || !await _isActive(ctx, gen, epoch)) return null;
      // Cloud -> local requires a second successful response from this endpoint.
      if (_current.isRemote) {
        final confirmation = await _stability.confirmReturn<HealthInfo>(
          first: result.health!,
          isCurrent: () => _isCurrent(gen, epoch),
          probe: () async {
            final h = await _probe(
                result.baseUrl!, DiscoveryConstants.lastKnownTimeout);
            return h != null &&
                    h.server.trim().toLowerCase() != 'cloud' &&
                    SchoolDiscoveryPolicy.acceptsHealthForBinding(h, binding) &&
                    SchoolDiscoveryPolicy.normalizeInstanceId(
                            h.identity?.serverInstanceId) ==
                        SchoolDiscoveryPolicy.normalizeInstanceId(
                            result.health?.identity?.serverInstanceId)
                ? h
                : null;
          },
        );
        if (confirmation == null) return null;
      }
      if (!await _isActive(ctx, gen, epoch)) return null;
      await _saveLast(result.baseUrl!, binding.schoolId);
      if (!await _isActive(ctx, gen, epoch)) return null;
      final accepted = await _finalizeAccepted(result, ctx);
      if (!await _isActive(ctx, gen, epoch)) return null;
      return _publish(accepted);
    }

    for (final base in known) {
      final result = await finishLocal(
          await check(base, retry: established && base == _current.baseUrl));
      if (result != null) return result;
      if (!await _isActive(ctx, gen, epoch)) return _current;
    }
    if (_candidateProvider != null) {
      for (final base in await _candidateProvider()) {
        final result = await finishLocal(await check(base));
        if (result != null) return result;
      }
    } else if (prefixes.isNotEmpty) {
      final mdns = await finishLocal(await _tryMdns(prefixes, ctx));
      if (mdns != null) return mdns;
      if (!await _isActive(ctx, gen, epoch)) return _current;
      final scan = await finishLocal(
          await _scanSubnet(prefixes, ctx, hintBaseUrls: [...known]));
      if (scan != null) return scan;
    }
    if (!await _isActive(ctx, gen, epoch)) return _current;
    final remote = await _tryRemote(ctx);
    if (!await _isActive(ctx, gen, epoch)) return _current;
    return _publish(remote ??
        DiscoveryResult.offline(
            'Aucun serveur de ${binding.schoolName} n’est accessible.'));
  }

  DiscoveryResult _publish(DiscoveryResult result) {
    _current = result;
    if (!_controller.isClosed) _controller.add(result);
    return result;
  }

  Future<DiscoveryResult?> _tryRemote(_BindingDiscoveryContext ctx) async {
    final String remote;
    if (ctx.filterByBinding && ctx.binding != null) {
      final fromBinding =
          SchoolDiscoveryPolicy.cloudBaseUrlForBinding(ctx.binding!);
      if (fromBinding == null) {
        debugPrint(
          '[Discovery] cloudBaseUrl binding invalide — distant ignoré',
        );
        return null;
      }
      remote = fromBinding;
    } else {
      remote = ApiConfig.effectiveCloudBaseUrl ??
          DiscoveryConstants.defaultRemoteBaseUrl;
    }
    if (DiscoveryConstants.isLoopbackHost(_hostOf(remote) ?? '')) {
      debugPrint(
        '[Discovery] URL cloud loopback ignorée ($remote) — '
        'sur Android 127.0.0.1 = le téléphone (sauf adb reverse debug)',
      );
      return null;
    }
    debugPrint('[Discovery] Vérification Health distant $remote');
    final remoteHealth =
        await _probe(remote, DiscoveryConstants.lastKnownTimeout);
    if (remoteHealth == null) return null;
    if (ctx.filterByBinding && ctx.binding != null) {
      if (!SchoolDiscoveryPolicy.acceptsRemoteHealthForBinding(
        remoteHealth,
        ctx.binding!,
        remote,
      )) {
        debugPrint(
          '[Discovery] Refuse distant (schoolId) attendu=${ctx.binding!.schoolId} '
          'health=${remoteHealth.identity?.schoolId}',
        );
        return null;
      }
    }
    return DiscoveryResult(
      mode: DiscoveryMode.remote,
      source: DiscoverySource.remote,
      baseUrl: ApiConfig.normalize(remote),
      schoolId: ctx.binding?.schoolId,
      health: HealthInfo(
        status: 'ok',
        server: 'cloud',
        school: remoteHealth.school,
        version: remoteHealth.version,
        time: remoteHealth.time,
        apiVersion: remoteHealth.apiVersion,
        protocolVersion: remoteHealth.protocolVersion,
        identity: remoteHealth.identity,
        serverSignature: remoteHealth.serverSignature,
      ),
      message:
          'Serveur distant — ${ctx.binding?.schoolName ?? remoteHealth.school}',
    );
  }

  /// Accepte Local seulement : probe OK + même /24 + pas cloud + health ≠ cloud.
  DiscoveryResult? _acceptLocal({
    required String base,
    required HealthInfo? health,
    required DiscoverySource source,
    required List<String> devicePrefixes,
    required String messagePrefix,
    required _BindingDiscoveryContext ctx,
  }) {
    if (health == null) return null;
    if (_isCloudBaseUrl(base, ctx)) {
      debugPrint('[Discovery] Refuse Local (URL cloud) $base');
      return null;
    }
    final serverKind = health.server.trim().toLowerCase();
    if (serverKind == 'cloud') {
      debugPrint('[Discovery] Refuse Local (health.server=cloud) $base');
      return null;
    }
    final hostAddress = _hostOf(base);
    if (hostAddress == null || !DiscoveryConstants.isPrivateIpv4(hostAddress)) {
      return null;
    }
    if (ctx.filterByBinding && ctx.binding != null) {
      if (!SchoolDiscoveryPolicy.acceptsHealthForBinding(
        health,
        ctx.binding!,
      )) {
        debugPrint(
          '[Discovery] Refuse Local (schoolId) base=$base '
          'attendu=${ctx.binding!.schoolId} '
          'health=${health.identity?.schoolId}',
        );
        return null;
      }
    }
    final host = _hostOf(base) ?? '?';
    final prefix = DiscoveryConstants.ipv4Prefix(host);
    debugPrint(
      '[Discovery] Accept Local base=$base host=$host '
      'prefix=$prefix health.server=${health.server}',
    );
    return DiscoveryResult(
      mode: DiscoveryMode.local,
      source: source,
      baseUrl: ApiConfig.normalize(base),
      schoolId: ctx.binding?.schoolId,
      health: health,
      message: '$messagePrefix — ${health.school}',
    );
  }

  /// mDNS entièrement isolé : aucune SocketException / erreur stream ne doit
  /// remonter en Unhandled Exception. Échec = null → suite (scan / cloud).
  Future<DiscoveryResult?> _tryMdns(
    List<String> localPrefixes,
    _BindingDiscoveryContext ctx,
  ) async {
    if (localPrefixes.isEmpty) return null;

    DiscoveryResult? result;
    Object? zoneError;

    await runZonedGuarded(() async {
      result = await _tryMdnsBody(localPrefixes, ctx);
    }, (error, stack) {
      zoneError = error;
      debugPrint('[Discovery] mDNS zone error (avalé): $error');
    });

    if (zoneError != null && result == null) {
      debugPrint('[Discovery] mDNS échec réseau → null');
    }
    return result;
  }

  Future<DiscoveryResult?> _tryMdnsBody(
    List<String> localPrefixes,
    _BindingDiscoveryContext ctx,
  ) async {
    MDnsClient? client;
    StreamSubscription<PtrResourceRecord>? sub;
    try {
      client = MDnsClient();
      try {
        await client.start().timeout(DiscoveryConstants.mdnsStartTimeout);
      } on SocketException catch (e) {
        debugPrint('[Discovery] mDNS start SocketException: $e');
        return null;
      } on TimeoutException {
        debugPrint('[Discovery] mDNS start timeout');
        return null;
      } on OSError catch (e) {
        debugPrint('[Discovery] mDNS start OSError: $e');
        return null;
      }

      final completer = Completer<DiscoveryResult?>();
      final seen = <String>{};

      Future<void> tryBase(String base) async {
        try {
          if (!seen.add(base) || completer.isCompleted) return;
          debugPrint('[Discovery] Service trouvé');
          debugPrint('[Discovery] Vérification Health $base');
          final health =
              await _probe(base, DiscoveryConstants.lastKnownTimeout);
          final local = _acceptLocal(
            base: base,
            health: health,
            source: DiscoverySource.mdns,
            devicePrefixes: localPrefixes,
            messagePrefix: 'Serveur local découvert (mDNS)',
            ctx: ctx,
          );
          if (local != null && !completer.isCompleted) {
            completer.complete(local);
          }
        } catch (e) {
          debugPrint('[Discovery] mDNS tryBase: $e');
        }
      }

      Future<void> handlePtr(PtrResourceRecord ptr) async {
        try {
          await for (final srv in client!
              .lookup<SrvResourceRecord>(
            ResourceRecordQuery.service(ptr.domainName),
          )
              .handleError((Object e) {
            debugPrint('[Discovery] mDNS SRV stream: $e');
          })) {
            await for (final ip in client
                .lookup<IPAddressResourceRecord>(
              ResourceRecordQuery.addressIPv4(srv.target),
            )
                .handleError((Object e) {
              debugPrint('[Discovery] mDNS A stream: $e');
            })) {
              if (completer.isCompleted) return;
              final host = ip.address.address;
              if (DiscoveryConstants.isLikelyVirtualHost(host)) {
                debugPrint('[Discovery] IP virtuelle ignorée $host');
                continue;
              }
              final port =
                  srv.port == 0 ? DiscoveryConstants.apiPort : srv.port;
              await tryBase('http://$host:$port');
            }
          }
        } catch (e) {
          debugPrint('[Discovery] mDNS handlePtr: $e');
        }
      }

      sub = client
          .lookup<PtrResourceRecord>(
        ResourceRecordQuery.serverPointer(
          DiscoveryConstants.serviceTypeLocal,
        ),
      )
          .listen(
        (ptr) {
          unawaited(handlePtr(ptr));
        },
        onError: (Object e) {
          debugPrint('[Discovery] mDNS PTR stream: $e');
        },
        cancelOnError: false,
      );

      unawaited(() async {
        try {
          final list = await InternetAddress.lookup(
            DiscoveryConstants.hostName,
            type: InternetAddressType.IPv4,
          ).timeout(DiscoveryConstants.mdnsTimeout);
          for (final addr in list) {
            if (DiscoveryConstants.isLikelyVirtualHost(addr.address)) continue;
            final base = 'http://${addr.address}:${DiscoveryConstants.apiPort}';
            await tryBase(base);
          }
        } catch (e) {
          debugPrint('[Discovery] mDNS hostname lookup: $e');
        }
      }());

      final settled = await Future.any([
        completer.future,
        Future<DiscoveryResult?>.delayed(
          DiscoveryConstants.mdnsTimeout,
          () => null,
        ),
      ]);
      return settled;
    } on SocketException catch (e) {
      debugPrint('[Discovery] mDNS SocketException: $e');
      return null;
    } on OSError catch (e) {
      debugPrint('[Discovery] mDNS OSError: $e');
      return null;
    } catch (e) {
      debugPrint('[Discovery] mDNS indisponible: $e');
      return null;
    } finally {
      try {
        await sub?.cancel();
      } catch (_) {}
      try {
        client?.stop();
      } catch (e) {
        debugPrint('[Discovery] mDNS stop: $e');
      }
    }
  }

  Future<DiscoveryResult?> _scanSubnet(
    List<String> prefixes,
    _BindingDiscoveryContext ctx, {
    List<String> hintBaseUrls = const [],
  }) async {
    if (prefixes.isEmpty) return null;

    final scanPrefixes = _selectScanPrefixes(prefixes);
    if (scanPrefixes.isEmpty) return null;

    final candidates = await _buildScanCandidates(scanPrefixes, hintBaseUrls);
    if (candidates.isEmpty) return null;

    debugPrint(
      '[Discovery] Scan de ${candidates.length} adresses '
      '(préfixes=${scanPrefixes.join(', ')}, '
      'max=${DiscoveryConstants.scanMaxAddresses}, '
      'timeout=${DiscoveryConstants.scanOverallTimeout.inSeconds}s)',
    );

    final completer = Completer<DiscoveryResult?>();
    var index = 0;
    final stopwatch = Stopwatch()..start();

    final workers =
        List.generate(DiscoveryConstants.scanMaxParallelism, (_) async {
      while (!completer.isCompleted && index < candidates.length) {
        if (stopwatch.elapsed >= DiscoveryConstants.scanOverallTimeout) {
          debugPrint('[Discovery] Scan timeout → abandon');
          if (!completer.isCompleted) completer.complete(null);
          return;
        }
        final i = index++;
        if (i >= candidates.length) break;
        final url = candidates[i];
        try {
          final health = await _probe(url, DiscoveryConstants.scanProbeTimeout);
          final local = _acceptLocal(
            base: url,
            health: health,
            source: DiscoverySource.subnetScan,
            devicePrefixes: prefixes,
            messagePrefix: 'Serveur local trouvé par scan',
            ctx: ctx,
          );
          if (local != null && !completer.isCompleted) {
            debugPrint('[Discovery] Serveur trouvé $url');
            completer.complete(local);
          }
        } catch (e) {
          debugPrint('[Discovery] Scan probe $url: $e');
        }
      }
    });

    try {
      await Future.wait(workers).timeout(DiscoveryConstants.scanOverallTimeout);
    } on TimeoutException {
      debugPrint('[Discovery] Scan Future.wait timeout');
    } catch (e) {
      debugPrint('[Discovery] Scan erreur: $e');
    }

    if (!completer.isCompleted) completer.complete(null);
    return completer.future;
  }

  /// Un seul préfixe /24 prioritaire (évite 4×254 probes).
  List<String> _selectScanPrefixes(List<String> prefixes) {
    final scored = [...prefixes]..sort((a, b) {
        int rank(String p) {
          if (p.startsWith('192.168.')) return 0;
          if (p.startsWith('10.')) return 1;
          return 2;
        }

        return rank(a).compareTo(rank(b));
      });
    return scored.take(DiscoveryConstants.scanMaxPrefixes).toList();
  }

  /// Candidats limités : hints (lastKnown/config) d'abord, puis échantillon du /24.
  Future<List<String>> _buildScanCandidates(
    List<String> scanPrefixes,
    List<String> hintBaseUrls,
  ) async {
    final seen = <String>{};
    final out = <String>[];

    void add(String url) {
      final n = ApiConfig.normalize(url);
      if (!ApiConfig.isValidBaseUrl(n)) return;
      final host = _hostOf(n);
      if (host == null || DiscoveryConstants.isLikelyVirtualHost(host)) return;
      if (seen.add(n)) out.add(n);
    }

    // 1) Accélérateurs connus (lastKnown, dart-define) — même /24 uniquement.
    for (final hint in hintBaseUrls) {
      final host = _hostOf(hint);
      final prefix = host == null ? null : DiscoveryConstants.ipv4Prefix(host);
      if (prefix != null && scanPrefixes.contains(prefix)) add(hint);
    }

    for (final c in ApiConfig.localBaseUrlCandidates) {
      final host = _hostOf(c);
      final prefix = host == null ? null : DiscoveryConstants.ipv4Prefix(host);
      if (prefix != null && scanPrefixes.contains(prefix)) add(c);
    }

    // 2) Dernier octet de lastKnown + hosts fréquents, sans IP magique fixe.
    final priorityHosts = <int>{
      1,
      2,
      10,
      20,
      30,
      50,
      100,
      101,
      110,
      120,
      137,
      150,
      200,
      250,
      254,
    };
    for (final hint in hintBaseUrls) {
      final host = _hostOf(hint);
      if (host == null) continue;
      final parts = host.split('.');
      if (parts.length == 4) {
        final lastOctet = int.tryParse(parts[3]);
        if (lastOctet != null && lastOctet >= 1 && lastOctet <= 254) {
          priorityHosts.add(lastOctet);
        }
      }
    }

    for (final prefix in scanPrefixes) {
      for (final host in priorityHosts.toList()..sort()) {
        add('http://$prefix.$host:${DiscoveryConstants.apiPort}');
        if (out.length >= DiscoveryConstants.scanMaxAddresses) {
          return out;
        }
      }
    }

    // 3) Compléter jusqu'au plafond sans balayer tout le /24.
    for (final prefix in scanPrefixes) {
      for (var i = 1; i <= 254; i++) {
        if (priorityHosts.contains(i)) continue;
        add('http://$prefix.$i:${DiscoveryConstants.apiPort}');
        if (out.length >= DiscoveryConstants.scanMaxAddresses) {
          return out;
        }
      }
    }
    return out;
  }

  Future<List<String>> _localPrefixes() async {
    final prefixes = <String>{};
    try {
      for (final iface in await NetworkInterface.list(
        type: InternetAddressType.IPv4,
        includeLinkLocal: false,
      ).timeout(const Duration(seconds: 2))) {
        for (final addr in iface.addresses) {
          if (DiscoveryConstants.isLikelyVirtualHost(addr.address)) continue;
          if (!DiscoveryConstants.isPrivateIpv4(addr.address)) continue;
          final prefix = DiscoveryConstants.ipv4Prefix(addr.address);
          if (prefix != null) prefixes.add(prefix);
        }
      }
    } catch (e) {
      debugPrint('[Discovery] Interfaces réseau: $e');
    }
    return prefixes.toList();
  }

  bool _isCloudBaseUrl(String baseUrl, _BindingDiscoveryContext ctx) {
    if (ctx.filterByBinding && ctx.binding != null) {
      final bindingCloud =
          SchoolDiscoveryPolicy.cloudBaseUrlForBinding(ctx.binding!);
      if (bindingCloud != null) {
        try {
          final a = Uri.parse(ApiConfig.normalize(baseUrl));
          final b = Uri.parse(bindingCloud);
          return a.host.toLowerCase() == b.host.toLowerCase() &&
              (a.hasPort ? a.port : _defaultPort(a.scheme)) ==
                  (b.hasPort ? b.port : _defaultPort(b.scheme));
        } catch (_) {
          return false;
        }
      }
    }
    final cloud = ApiConfig.effectiveCloudBaseUrl ??
        DiscoveryConstants.defaultRemoteBaseUrl;
    try {
      final a = Uri.parse(ApiConfig.normalize(baseUrl));
      final b = Uri.parse(ApiConfig.normalize(cloud));
      return a.host.toLowerCase() == b.host.toLowerCase() &&
          (a.hasPort ? a.port : _defaultPort(a.scheme)) ==
              (b.hasPort ? b.port : _defaultPort(b.scheme));
    } catch (_) {
      return false;
    }
  }

  int _defaultPort(String scheme) => scheme == 'https' ? 443 : 80;

  String? _hostOf(String baseUrl) {
    try {
      return Uri.parse(ApiConfig.normalize(baseUrl)).host;
    } catch (_) {
      return null;
    }
  }

  bool _isVirtualBaseUrl(String baseUrl) {
    final host = _hostOf(baseUrl);
    if (host == null) return false;
    return DiscoveryConstants.isLoopbackHost(host) &&
        !(kIsWeb || ApiConfig.allowUsbLoopback);
  }

  Future<HealthInfo?> _probe(String baseUrl, Duration timeout) async {
    if (!ApiConfig.isValidBaseUrl(baseUrl)) return null;
    if (_healthProbe != null) return _healthProbe(baseUrl, timeout);
    final host = _hostOf(baseUrl);
    if (host != null &&
        DiscoveryConstants.isLoopbackHost(host) &&
        !kIsWeb &&
        !ApiConfig.allowUsbLoopback) {
      return null;
    }
    try {
      final dio = Dio(BaseOptions(
        baseUrl: ApiConfig.normalize(baseUrl),
        connectTimeout: timeout,
        receiveTimeout: timeout,
        validateStatus: (c) => c != null && c >= 200 && c < 300,
      ));
      final response = await dio.get<dynamic>(DiscoveryConstants.healthPath);
      final data = response.data;
      if (data is Map) {
        final map = Map<String, dynamic>.from(data);
        final status = (map['status'] ?? '').toString().toLowerCase();
        if (status == 'ok' || status == 'healthy') {
          return HealthInfo.fromJson(map);
        }
      }
      // Réponse non JSON / inattendue : ne pas forcer server=local.
      return null;
    } catch (_) {
      return null;
    }
  }

  Future<String?> _loadLast(String schoolId) async {
    if (_lastKnownLoader != null) return _lastKnownLoader(schoolId);
    final v = await SchoolScopedPreferences.getString(
      DiscoveryConstants.lastKnownPrefsKey,
      schoolId: schoolId,
    );
    if (v == null || !ApiConfig.isValidBaseUrl(v)) return null;
    final normalized = ApiConfig.normalize(v);
    final host = _hostOf(normalized);
    if (host != null && DiscoveryConstants.isLoopbackHost(host)) {
      debugPrint(
        '[Discovery] lastKnown loopback ignoré ($normalized) — '
        '127.0.0.1 = téléphone sur Android',
      );
      return null;
    }
    return normalized;
  }

  Future<void> _saveLast(String baseUrl, String schoolId) async {
    if (_lastKnownSaver != null) return _lastKnownSaver(schoolId, baseUrl);
    final host = _hostOf(baseUrl);
    if (host != null && DiscoveryConstants.isLikelyVirtualHost(host)) return;
    await SchoolScopedPreferences.setString(
      DiscoveryConstants.lastKnownPrefsKey,
      ApiConfig.normalize(baseUrl),
      schoolId: schoolId,
    );
  }

  Future<DiscoveryResult> _finalizeAccepted(
    DiscoveryResult result,
    _BindingDiscoveryContext ctx,
  ) async {
    if (result.isRemote ||
        !ctx.filterByBinding ||
        ctx.binding == null ||
        result.health == null) {
      return result;
    }

    final change = SchoolDiscoveryPolicy.detectInstanceChange(
      ctx.binding!,
      result.health!,
    );

    if (change.detected) {
      final recovery = await ServerInstanceRecoveryService.handleInstanceChange(
        binding: ctx.binding!,
        change: change,
        health: result.health!,
        apiBaseUrl: result.baseUrl,
      );
      return DiscoveryResult(
        mode: result.mode,
        source: result.source,
        baseUrl: result.baseUrl,
        schoolId: ctx.binding?.schoolId,
        health: result.health,
        message: recovery.message ?? result.message,
        serverInstanceIdChanged: recovery.requiresReauthentication,
        previousServerInstanceId: change.previousInstanceId,
        observedServerInstanceId: change.observedInstanceId,
      );
    }

    await ServerInstanceBindingSync.syncFromHealth(
      binding: ctx.binding!,
      health: result.health!,
      repository: _bindings,
    );

    return result;
  }
}
