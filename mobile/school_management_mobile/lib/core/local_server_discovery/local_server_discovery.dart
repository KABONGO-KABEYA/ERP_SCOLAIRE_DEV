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

  DiscoveryResult get current => _current;

  final _controller = StreamController<DiscoveryResult>.broadcast();
  Stream<DiscoveryResult> get changes => _controller.stream;

  Future<DiscoveryResult> discover({bool force = false}) {
    if (!force && _inFlight != null) return _inFlight!;
    final gen = ++_generation;
    _inFlight = _run(gen).whenComplete(() {
      if (gen == _generation) _inFlight = null;
    });
    return _inFlight!;
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
    if (last != null) candidates.add(last);

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
    debugPrint('[Discovery] Préfixes device: ${prefixes.join(', ')}');

    // 1) Dernière IP connue — uniquement si encore sur le même /24.
    debugPrint('[Discovery] Dernière IP connue');
    final last = await _loadLast();
    if (last != null && !_isVirtualBaseUrl(last) && !_isCloudBaseUrl(last, ctx)) {
      if (!_isSameSubnet(last, prefixes)) {
        debugPrint('[Discovery] lastKnown hors sous-réseau → ignoré ($last)');
        await _clearLast();
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
        if (local != null) return _publish(await _finalizeAccepted(local, ctx));
        await _clearLast();
      }
    }

    // 2) mDNS (mêmes sous-réseaux uniquement)
    debugPrint('[Discovery] Recherche mDNS...');
    final mdns = await _tryMdns(prefixes, ctx);
    if (gen != _generation) return _current;
    if (mdns != null) {
      await _saveLast(mdns.baseUrl!);
      debugPrint('[Discovery] Passage en serveur local (mDNS)');
      return _publish(await _finalizeAccepted(mdns, ctx));
    }

    // 3) Scan sous-réseau courant
    debugPrint('[Discovery] Scan réseau');
    final scanned = await _scanSubnet(prefixes, ctx);
    if (gen != _generation) return _current;
    if (scanned != null) {
      await _saveLast(scanned.baseUrl!);
      debugPrint('[Discovery] Passage en serveur local (scan)');
      return _publish(await _finalizeAccepted(scanned, ctx));
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

  Future<DiscoveryResult?> _tryMdns(
    List<String> localPrefixes,
    _BindingDiscoveryContext ctx,
  ) async {
    final client = MDnsClient();
    try {
      await client.start();
      final completer = Completer<DiscoveryResult?>();
      final seen = <String>{};

      Future<void> tryBase(String base) async {
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
      }

      Future<void> handlePtr(PtrResourceRecord ptr) async {
        await for (final srv in client.lookup<SrvResourceRecord>(
          ResourceRecordQuery.service(ptr.domainName),
        )) {
          await for (final ip in client.lookup<IPAddressResourceRecord>(
            ResourceRecordQuery.addressIPv4(srv.target),
          )) {
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
      }

      final sub = client
          .lookup<PtrResourceRecord>(
            ResourceRecordQuery.serverPointer(
              DiscoveryConstants.serviceTypeLocal,
            ),
          )
          .listen((ptr) => handlePtr(ptr), onError: (_) {});

      unawaited(() async {
        try {
          final list = await InternetAddress.lookup(
            DiscoveryConstants.hostName,
            type: InternetAddressType.IPv4,
          );
          for (final addr in list) {
            if (DiscoveryConstants.isLikelyVirtualHost(addr.address)) continue;
            final base =
                'http://${addr.address}:${DiscoveryConstants.apiPort}';
            await tryBase(base);
          }
        } catch (_) {}
      }());

      final result = await Future.any([
        completer.future,
        Future<DiscoveryResult?>.delayed(
          DiscoveryConstants.mdnsTimeout,
          () => null,
        ),
      ]);
      await sub.cancel();
      return result;
    } catch (e) {
      debugPrint('[Discovery] mDNS indisponible: $e');
      return null;
    } finally {
      client.stop();
    }
  }

  Future<DiscoveryResult?> _scanSubnet(
    List<String> prefixes,
    _BindingDiscoveryContext ctx,
  ) async {
    if (prefixes.isEmpty) return null;

    final candidates = <String>[];
    for (final prefix in prefixes) {
      for (var i = 1; i <= 254; i++) {
        candidates.add('http://$prefix.$i:${DiscoveryConstants.apiPort}');
      }
    }

    debugPrint('[Discovery] Scan de ${candidates.length} adresses');
    final completer = Completer<DiscoveryResult?>();
    var index = 0;
    final workers =
        List.generate(DiscoveryConstants.scanMaxParallelism, (_) async {
      while (!completer.isCompleted && index < candidates.length) {
        final i = index++;
        if (i >= candidates.length) break;
        final url = candidates[i];
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
      }
    });

    await Future.wait(workers);
    if (!completer.isCompleted) completer.complete(null);
    return completer.future;
  }

  Future<List<String>> _localPrefixes() async {
    final prefixes = <String>{};
    try {
      for (final iface in await NetworkInterface.list(
        type: InternetAddressType.IPv4,
        includeLinkLocal: false,
      )) {
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
    return ApiConfig.normalize(v);
  }

  Future<void> _saveLast(String baseUrl) async {
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
