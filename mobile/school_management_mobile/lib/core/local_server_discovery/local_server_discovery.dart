import 'dart:async';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:multicast_dns/multicast_dns.dart';

import '../config/api_config.dart';
import '../school_binding/school_binding.dart';
import '../school_binding/school_binding_gate.dart';
import '../cache/school_scoped_preferences.dart';
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
/// Convention : Mode Local = serveur école joignable **sur le même sous-réseau
/// privé (/24)** que le téléphone. Joignable hors Wi‑Fi école ≠ Local.
class LocalServerDiscovery {
  LocalServerDiscovery._();
  static final LocalServerDiscovery instance = LocalServerDiscovery._();

  DiscoveryResult _current = DiscoveryResult.detecting;
  Future<DiscoveryResult>? _inFlight;
  int _generation = 0;

  /// lastKnown ayant échoué pendant cette session (évite re-probes / boucles).
  final Set<String> _sessionIgnoredLastKnown = <String>{};

  DiscoveryResult get current => _current;

  final _controller = StreamController<DiscoveryResult>.broadcast();
  Stream<DiscoveryResult> get changes => _controller.stream;

  Future<DiscoveryResult> discover({bool force = false}) {
    if (!force && _inFlight != null) return _inFlight!;
    final gen = ++_generation;
    final future = _runBounded(gen);
    _inFlight = future;
    unawaited(future.whenComplete(() {
      if (identical(_inFlight, future)) _inFlight = null;
    }));
    return future;
  }

  Future<DiscoveryResult> rediscover() => discover(force: true);

  /// Recheck léger : confirme le Local actuel (même /24) ou bascule Distant/offline.
  /// Si le Local n'est plus éligible → découverte complète.
  Future<DiscoveryResult> recheck() async {
    if (await SchoolBindingGate.shouldRequireEstablishmentQr()) {
      debugPrint('[Discovery] Registre vide → pas de discovery (QR établissement requis)');
      return _publish(
        DiscoveryResult.offline(
          'Rejoignez un établissement avec le QR code de l\'école.',
        ),
      );
    }

    final ctx = await _loadBindingContext();
    final prefixes = await _localPrefixes();
    final candidates = <String>{};
    final current = _current.baseUrl;
    if (current != null && ApiConfig.isValidBaseUrl(current)) {
      candidates.add(ApiConfig.normalize(current));
    }
    final last = await _loadLast();
    if (last != null && !_sessionIgnoredLastKnown.contains(last)) {
      candidates.add(last);
    }

    for (final base in candidates) {
      if (_isVirtualBaseUrl(base) || _isCloudBaseUrl(base, ctx)) continue;
      debugPrint('[Discovery] Recheck léger $base');
      final health = await _probe(base, DiscoveryConstants.lastKnownTimeout);
      final local = _acceptLocal(
        base: base,
        health: health,
        source: DiscoverySource.lastKnown,
        devicePrefixes: prefixes,
        messagePrefix: 'Serveur local',
        ctx: ctx,
      );
      if (local != null) return _publish(await _finalizeAccepted(local, ctx));
      _ignoreLastKnownForSession(base);
      debugPrint(
        '[Discovery] Recheck refuse Local base=$base '
        'sameSubnet=${_isSameSubnet(base, prefixes)} '
        'health.server=${health?.server}',
      );
    }

    // Ancienne IP locale hors sous-réseau courant → ne plus la privilégier.
    if (last != null && !_isSameSubnet(last, prefixes)) {
      debugPrint('[Discovery] lastKnown hors sous-réseau → clear ($last)');
      await _clearLast();
    }

    final remote = await _tryRemote(ctx);
    if (remote != null) return _publish(await _finalizeAccepted(remote, ctx));

    debugPrint('[Discovery] Recheck échoué → découverte complète');
    return rediscover();
  }

  Future<DiscoveryResult> _runBounded(int gen) async {
    try {
      return await _run(gen).timeout(DiscoveryConstants.discoveryOverallTimeout);
    } on TimeoutException {
      debugPrint(
        '[Discovery] Budget global '
        '${DiscoveryConstants.discoveryOverallTimeout.inSeconds}s dépassé '
        '→ tentative cloud',
      );
      // Invalide le _run encore en cours pour qu'il n'écrase pas l'état UI.
      if (gen == _generation) {
        ++_generation;
      }
      final cloudGen = _generation;
      try {
        final ctx = await _loadBindingContext();
        final remote = await _tryRemote(ctx);
        if (cloudGen != _generation) return _current;
        if (remote != null) {
          return _publish(await _finalizeAccepted(remote, ctx));
        }
      } catch (e) {
        debugPrint('[Discovery] Fallback cloud après timeout: $e');
      }
      if (cloudGen != _generation) return _current;
      return _publish(DiscoveryResult.offline(
        'Délai de découverte dépassé — aucun serveur accessible.',
      ));
    }
  }

  Future<DiscoveryResult> _run(int gen) async {
    _publish(DiscoveryResult.detecting);
    if (await SchoolBindingGate.shouldRequireEstablishmentQr()) {
      debugPrint('[Discovery] Registre vide → pas de discovery (QR établissement requis)');
      return _publish(
        DiscoveryResult.offline(
          'Rejoignez un établissement avec le QR code de l\'école.',
        ),
      );
    }

    final ctx = await _loadBindingContext();
    final prefixes = await _localPrefixes();
    final lanAvailable = prefixes.isNotEmpty;
    debugPrint(
      '[Discovery] Préfixes device: ${prefixes.isEmpty ? '(aucun)' : prefixes.join(', ')} '
      'lanAvailable=$lanAvailable',
    );

    // 1) Dernière IP connue — uniquement si encore sur le même /24.
    debugPrint('[Discovery] Dernière IP connue');
    final last = await _loadLast();
    if (last != null &&
        !_isVirtualBaseUrl(last) &&
        !_isCloudBaseUrl(last, ctx) &&
        !_sessionIgnoredLastKnown.contains(last)) {
      if (!lanAvailable || !_isSameSubnet(last, prefixes)) {
        debugPrint('[Discovery] lastKnown hors sous-réseau / sans LAN → ignoré ($last)');
        _ignoreLastKnownForSession(last);
        if (!lanAvailable || !_isSameSubnet(last, prefixes)) {
          await _clearLast();
        }
      } else {
        debugPrint('[Discovery] Vérification Health $last');
        final health = await _probe(last, DiscoveryConstants.lastKnownTimeout);
        if (gen != _generation) return _current;
        final local = _acceptLocal(
          base: last,
          health: health,
          source: DiscoverySource.lastKnown,
          devicePrefixes: prefixes,
          messagePrefix: 'Serveur local (dernière IP)',
          ctx: ctx,
        );
        if (local != null) {
          _sessionIgnoredLastKnown.remove(last);
          return _publish(await _finalizeAccepted(local, ctx));
        }
        _ignoreLastKnownForSession(last);
        await _clearLast();
      }
    }

    // 1b) Candidats LAN compilés (`LOCAL_API_CANDIDATES` / `LOCAL_API_BASE_URL`)
    //     — jamais 127.0.0.1 (filtré dans ApiConfig).
    if (lanAvailable) {
      final configured = await _tryConfiguredLocals(prefixes, ctx);
      if (gen != _generation) return _current;
      if (configured != null) {
        await _saveLast(configured.baseUrl!);
        debugPrint('[Discovery] Passage en serveur local (config)');
        return _publish(await _finalizeAccepted(configured, ctx));
      }
    }

    // 2) mDNS (mêmes sous-réseaux uniquement) — skip si pas de LAN.
    if (lanAvailable) {
      debugPrint('[Discovery] Recherche mDNS...');
      final mdns = await _tryMdns(prefixes, ctx);
      if (gen != _generation) return _current;
      if (mdns != null) {
        await _saveLast(mdns.baseUrl!);
        debugPrint('[Discovery] Passage en serveur local (mDNS)');
        return _publish(await _finalizeAccepted(mdns, ctx));
      }
    } else {
      debugPrint('[Discovery] mDNS ignoré (réseau local indisponible)');
    }

    // 3) Scan sous-réseau — uniquement si LAN présent ; budget temps + plafond adresses.
    if (lanAvailable) {
      debugPrint('[Discovery] Scan réseau (budget limité)');
      final scanned = await _scanSubnet(prefixes, ctx);
      if (gen != _generation) return _current;
      if (scanned != null) {
        await _saveLast(scanned.baseUrl!);
        debugPrint('[Discovery] Passage en serveur local (scan)');
        return _publish(await _finalizeAccepted(scanned, ctx));
      }
    } else {
      debugPrint(
        '[Discovery] Scan subnet ignoré (pas de préfixe privé / LAN indisponible)',
      );
    }

    // 4) Cloud → Mode Distant
    if (gen != _generation) return _current;
    final remote = await _tryRemote(ctx);
    if (remote != null) {
      debugPrint('[Discovery] Passage en serveur distant');
      return _publish(await _finalizeAccepted(remote, ctx));
    }

    return _publish(DiscoveryResult.offline(
      'Aucun serveur local ni distant accessible.',
    ));
  }

  DiscoveryResult _publish(DiscoveryResult result) {
    _current = result;
    if (!_controller.isClosed) _controller.add(result);
    return result;
  }

  void _ignoreLastKnownForSession(String baseUrl) {
    _sessionIgnoredLastKnown.add(ApiConfig.normalize(baseUrl));
  }

  Future<DiscoveryResult?> _tryConfiguredLocals(
    List<String> localPrefixes,
    _BindingDiscoveryContext ctx,
  ) async {
    for (final raw in ApiConfig.localBaseUrlCandidates) {
      final base = ApiConfig.normalize(raw);
      if (_isVirtualBaseUrl(base) || _isCloudBaseUrl(base, ctx)) continue;
      if (!_isSameSubnet(base, localPrefixes)) continue;
      debugPrint('[Discovery] Candidat config $base');
      final health = await _probe(base, DiscoveryConstants.lastKnownTimeout);
      final local = _acceptLocal(
        base: base,
        health: health,
        source: DiscoverySource.lastKnown,
        devicePrefixes: localPrefixes,
        messagePrefix: 'Serveur local (config)',
        ctx: ctx,
      );
      if (local != null) return local;
    }
    return null;
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
      if (!SchoolDiscoveryPolicy.acceptsHealthForBinding(
        remoteHealth,
        ctx.binding!,
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
      message: 'Serveur distant — ${remoteHealth.school}',
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
    if (!_isSameSubnet(base, devicePrefixes)) {
      debugPrint(
        '[Discovery] Refuse Local (hors sous-réseau) $base '
        'devicePrefixes=${devicePrefixes.join(',')}',
      );
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
          if (!_isSameSubnet(base, localPrefixes)) {
            debugPrint('[Discovery] mDNS hors sous-réseau ignoré $base');
            return;
          }
          debugPrint('[Discovery] Service trouvé');
          debugPrint('[Discovery] Vérification Health $base');
          final health = await _probe(base, DiscoveryConstants.lastKnownTimeout);
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
            final base =
                'http://${addr.address}:${DiscoveryConstants.apiPort}';
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
    _BindingDiscoveryContext ctx,
  ) async {
    if (prefixes.isEmpty) return null;

    final scanPrefixes = _selectScanPrefixes(prefixes);
    if (scanPrefixes.isEmpty) return null;

    final candidates = _buildScanCandidates(scanPrefixes);
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

  /// Candidats limités : IPs config d'abord, puis échantillon du /24.
  List<String> _buildScanCandidates(List<String> scanPrefixes) {
    final seen = <String>{};
    final out = <String>[];

    void add(String url) {
      final n = ApiConfig.normalize(url);
      if (!ApiConfig.isValidBaseUrl(n)) return;
      final host = _hostOf(n);
      if (host == null || DiscoveryConstants.isLikelyVirtualHost(host)) return;
      if (seen.add(n)) out.add(n);
    }

    for (final c in ApiConfig.localBaseUrlCandidates) {
      final host = _hostOf(c);
      final prefix = host == null ? null : DiscoveryConstants.ipv4Prefix(host);
      if (prefix != null && scanPrefixes.contains(prefix)) add(c);
    }

    // Hosts fréquents d'abord (.1 gateway, .2, .100, …) puis pas de 1..254 exhaustif.
    const priorityHosts = <int>[
      1, 2, 10, 20, 30, 50, 100, 101, 110, 120, 137, 150, 200, 250, 254,
    ];
    for (final prefix in scanPrefixes) {
      for (final host in priorityHosts) {
        add('http://$prefix.$host:${DiscoveryConstants.apiPort}');
        if (out.length >= DiscoveryConstants.scanMaxAddresses) {
          return out;
        }
      }
    }

    // Compléter jusqu'au plafond sans balayer tout le /24.
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

  bool _isSameSubnet(String baseUrl, List<String> devicePrefixes) {
    if (devicePrefixes.isEmpty) return false;
    final host = _hostOf(baseUrl);
    if (host == null) return false;
    final prefix = DiscoveryConstants.ipv4Prefix(host);
    if (prefix == null) return false;
    return devicePrefixes.contains(prefix);
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
    return DiscoveryConstants.isLikelyVirtualHost(host);
  }

  Future<HealthInfo?> _probe(String baseUrl, Duration timeout) async {
    if (!ApiConfig.isValidBaseUrl(baseUrl)) return null;
    final host = _hostOf(baseUrl);
    if (host != null &&
        DiscoveryConstants.isLoopbackHost(host) &&
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

  Future<String?> _loadLast() async {
    final v = await SchoolScopedPreferences.getString(
      DiscoveryConstants.lastKnownPrefsKey,
    );
    if (v == null || !ApiConfig.isValidBaseUrl(v)) return null;
    final normalized = ApiConfig.normalize(v);
    final host = _hostOf(normalized);
    if (host != null && DiscoveryConstants.isLoopbackHost(host)) {
      debugPrint(
        '[Discovery] lastKnown loopback ignoré ($normalized) — '
        '127.0.0.1 = téléphone sur Android',
      );
      await _clearLast();
      return null;
    }
    return normalized;
  }

  Future<void> _saveLast(String baseUrl) async {
    final host = _hostOf(baseUrl);
    if (host != null && DiscoveryConstants.isLikelyVirtualHost(host)) return;
    await SchoolScopedPreferences.setString(
      DiscoveryConstants.lastKnownPrefsKey,
      ApiConfig.normalize(baseUrl),
    );
  }

  Future<void> _clearLast() async {
    await SchoolScopedPreferences.remove(DiscoveryConstants.lastKnownPrefsKey);
  }

  Future<_BindingDiscoveryContext> _loadBindingContext() async {
    final filter = await SchoolBindingGate.shouldFilterDiscoveryByBinding();
    if (!filter) {
      return const _BindingDiscoveryContext(filterByBinding: false);
    }
    final binding = await SchoolBindingGate.bindingRepository.load();
    if (binding == null || binding.schoolId.isEmpty) {
      debugPrint(
        '[Discovery] STRICT_SCHOOL_DISCOVERY sans binding valide → legacy',
      );
      return const _BindingDiscoveryContext(filterByBinding: false);
    }
    debugPrint(
      '[Discovery] Mode filtré schoolId=${binding.schoolId} '
      'cloud=${binding.cloudBaseUrl}',
    );
    return _BindingDiscoveryContext(filterByBinding: true, binding: binding);
  }

  Future<DiscoveryResult> _finalizeAccepted(
    DiscoveryResult result,
    _BindingDiscoveryContext ctx,
  ) async {
    if (!ctx.filterByBinding || ctx.binding == null || result.health == null) {
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
      repository: SchoolBindingGate.bindingRepository,
    );

    return result;
  }
}
