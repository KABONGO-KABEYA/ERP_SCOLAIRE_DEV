import 'dart:async';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:multicast_dns/multicast_dns.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../config/api_config.dart';
import 'discovery_constants.dart';
import 'discovery_models.dart';

/// Porte d'entrée unique Mobile pour découvrir le serveur API.
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

  Future<DiscoveryResult> _run(int gen) async {
    _publish(DiscoveryResult.detecting);

    debugPrint('[Discovery] Recherche mDNS...');
    final mdns = await _tryMdns();
    if (gen != _generation) return _current;
    if (mdns != null) {
      await _saveLast(mdns.baseUrl!);
      debugPrint('[Discovery] Passage en serveur local');
      return _publish(mdns);
    }

    debugPrint('[Discovery] Dernière IP connue');
    final last = await _loadLast();
    if (last != null) {
      debugPrint('[Discovery] Vérification Health $last');
      final health = await _probe(last, DiscoveryConstants.lastKnownTimeout);
      if (gen != _generation) return _current;
      if (health != null) {
        return _publish(DiscoveryResult(
          mode: DiscoveryMode.local,
          source: DiscoverySource.lastKnown,
          baseUrl: ApiConfig.normalize(last),
          health: health,
          message: 'Serveur local (dernière IP) — ${health.school}',
        ));
      }
      await _clearLast();
    }

    debugPrint('[Discovery] Scan réseau');
    final scanned = await _scanSubnet();
    if (gen != _generation) return _current;
    if (scanned != null) {
      await _saveLast(scanned.baseUrl!);
      debugPrint('[Discovery] Passage en serveur local');
      return _publish(scanned);
    }

    final remote = ApiConfig.effectiveCloudBaseUrl ??
        DiscoveryConstants.defaultRemoteBaseUrl;
    debugPrint('[Discovery] Vérification Health distant $remote');
    final remoteHealth =
        await _probe(remote, DiscoveryConstants.lastKnownTimeout);
    if (gen != _generation) return _current;
    if (remoteHealth != null) {
      debugPrint('[Discovery] Passage en serveur distant');
      return _publish(DiscoveryResult(
        mode: DiscoveryMode.remote,
        source: DiscoverySource.remote,
        baseUrl: ApiConfig.normalize(remote),
        health: HealthInfo(
          status: 'ok',
          server: 'cloud',
          school: remoteHealth.school,
          version: remoteHealth.version,
          time: remoteHealth.time,
        ),
        message: 'Serveur distant — ${remoteHealth.school}',
      ));
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

  Future<DiscoveryResult?> _tryMdns() async {
    final client = MDnsClient();
    try {
      await client.start();
      final completer = Completer<DiscoveryResult?>();
      final seen = <String>{};

      Future<void> handlePtr(PtrResourceRecord ptr) async {
        await for (final srv in client.lookup<SrvResourceRecord>(
          ResourceRecordQuery.service(ptr.domainName),
        )) {
          await for (final ip in client.lookup<IPAddressResourceRecord>(
            ResourceRecordQuery.addressIPv4(srv.target),
          )) {
            final port =
                srv.port == 0 ? DiscoveryConstants.apiPort : srv.port;
            final base = 'http://${ip.address.address}:$port';
            if (!seen.add(base)) continue;
            debugPrint('[Discovery] Service trouvé');
            debugPrint('[Discovery] Vérification Health $base');
            final health =
                await _probe(base, DiscoveryConstants.lastKnownTimeout);
            if (health != null && !completer.isCompleted) {
              completer.complete(DiscoveryResult(
                mode: DiscoveryMode.local,
                source: DiscoverySource.mdns,
                baseUrl: ApiConfig.normalize(base),
                health: health,
                message: 'Serveur local découvert (mDNS) — ${health.school}',
              ));
            }
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
            final base =
                'http://${addr.address}:${DiscoveryConstants.apiPort}';
            final health =
                await _probe(base, DiscoveryConstants.lastKnownTimeout);
            if (health != null && !completer.isCompleted) {
              completer.complete(DiscoveryResult(
                mode: DiscoveryMode.local,
                source: DiscoverySource.mdns,
                baseUrl: ApiConfig.normalize(base),
                health: health,
                message: 'Serveur local via ${DiscoveryConstants.hostName}',
              ));
            }
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

  Future<DiscoveryResult?> _scanSubnet() async {
    final prefixes = await _localPrefixes();
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
        if (health != null && !completer.isCompleted) {
          debugPrint('[Discovery] Serveur trouvé $url');
          completer.complete(DiscoveryResult(
            mode: DiscoveryMode.local,
            source: DiscoverySource.subnetScan,
            baseUrl: ApiConfig.normalize(url),
            health: health,
            message: 'Serveur local trouvé par scan — $url',
          ));
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
          final parts = addr.address.split('.');
          if (parts.length != 4) continue;
          final a = int.tryParse(parts[0]) ?? -1;
          final b = int.tryParse(parts[1]) ?? -1;
          final private = a == 10 ||
              (a == 172 && b >= 16 && b <= 31) ||
              (a == 192 && b == 168);
          if (!private) continue;
          prefixes.add('${parts[0]}.${parts[1]}.${parts[2]}');
        }
      }
    } catch (e) {
      debugPrint('[Discovery] Interfaces réseau: $e');
    }
    return prefixes.toList();
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
      if (data is Map<String, dynamic>) {
        final status = (data['status'] ?? '').toString().toLowerCase();
        if (status == 'ok' || status == 'healthy') {
          return HealthInfo.fromJson(data);
        }
      }
      return HealthInfo(
        status: 'ok',
        server: 'local',
        school: 'École',
        version: '1.0.0',
        time: DateTime.now().toUtc(),
      );
    } catch (_) {
      return null;
    }
  }

  Future<String?> _loadLast() async {
    final prefs = await SharedPreferences.getInstance();
    final v = prefs.getString(DiscoveryConstants.lastKnownPrefsKey);
    if (v == null || !ApiConfig.isValidBaseUrl(v)) return null;
    return ApiConfig.normalize(v);
  }

  Future<void> _saveLast(String baseUrl) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(
      DiscoveryConstants.lastKnownPrefsKey,
      ApiConfig.normalize(baseUrl),
    );
  }

  Future<void> _clearLast() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(DiscoveryConstants.lastKnownPrefsKey);
  }
}
