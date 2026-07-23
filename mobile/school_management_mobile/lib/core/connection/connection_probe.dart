import 'package:dio/dio.dart';

import '../api/dio_factory.dart';
import '../config/api_config.dart';
import 'connection_mode.dart';

/// Sonde HTTP : Local (plusieurs candidats) → Cloud → diagnostic Internet.
class ConnectionProbe {
  Future<ConnectionSnapshot> probe({
    Duration localTimeout = const Duration(seconds: 2),
    Duration cloudTimeout = const Duration(seconds: 3),
  }) async {
    final localCandidates = _localCandidates();
    final cloudUrl = ApiConfig.effectiveCloudBaseUrl;

    final localHit = await _firstHealthy(localCandidates, localTimeout);
    if (localHit != null) {
      return ConnectionSnapshot(
        mode: ConnectionMode.local,
        baseUrl: localHit,
        message: 'Serveur local accessible ($localHit).',
        hasInternet: true,
      );
    }

    if (cloudUrl != null && await _isHealthy(cloudUrl, cloudTimeout)) {
      return ConnectionSnapshot(
        mode: ConnectionMode.cloud,
        baseUrl: cloudUrl,
        message: 'Serveur local hors portée — bascule Cloud (lecture seule).',
        hasInternet: true,
      );
    }

    final online = await _hasInternetAccess();
    if (!online) {
      return const ConnectionSnapshot(
        mode: ConnectionMode.offline,
        hasInternet: false,
        message: 'Pas de connexion Internet. Activez le Wi‑Fi ou les données mobiles.',
      );
    }

    final tested = localCandidates.isEmpty
        ? '(aucune URL locale valide)'
        : localCandidates.join(', ');

    if (cloudUrl == null) {
      return ConnectionSnapshot(
        mode: ConnectionMode.offline,
        hasInternet: true,
        message:
            'Internet OK, mais le serveur école ne répond pas '
            '(testé : $tested). '
            'Vérifiez que l\'API tourne et que vous êtes sur le même Wi‑Fi.',
      );
    }

    return const ConnectionSnapshot(
      mode: ConnectionMode.offline,
      hasInternet: true,
      message:
          'Internet OK, mais ni le serveur local ni le Cloud ne répondent. '
          'Réessayez dans un instant.',
    );
  }

  List<String> _localCandidates() {
    // Uniquement l'URL locale configurée (IP LAN école).
    // Pas de fallback 127.0.0.1 / tunnel USB : hors Wi‑Fi école → Cloud.
    final primary = ApiConfig.effectiveLocalBaseUrl;
    if (!ApiConfig.isValidBaseUrl(primary)) return const [];
    return [primary];
  }

  Future<String?> _firstHealthy(List<String> urls, Duration timeout) async {
    if (urls.isEmpty) return null;
    final results = await Future.wait(
      urls.map((u) async => (u, await _isHealthy(u, timeout))),
    );
    for (final (url, ok) in results) {
      if (ok) return url;
    }
    return null;
  }

  Future<bool> _isHealthy(String baseUrl, Duration timeout) async {
    if (!ApiConfig.isValidBaseUrl(baseUrl)) return false;
    try {
      final dio = createApiDio(baseUrl);
      dio.options.connectTimeout = timeout;
      dio.options.receiveTimeout = timeout;
      final response = await dio.get<dynamic>('/api/v1/health');
      return response.statusCode != null &&
          response.statusCode! >= 200 &&
          response.statusCode! < 300;
    } on DioException {
      return false;
    } catch (_) {
      return false;
    }
  }

  Future<bool> _hasInternetAccess() async {
    final dio = Dio(
      BaseOptions(
        connectTimeout: const Duration(seconds: 2),
        receiveTimeout: const Duration(seconds: 2),
        followRedirects: false,
        validateStatus: (code) => code != null && code < 500,
      ),
    );
    try {
      final response = await dio.get<dynamic>('https://clients3.google.com/generate_204');
      return response.statusCode == 204 ||
          (response.statusCode != null && response.statusCode! < 400);
    } on DioException {
      try {
        final fallback = await dio.head<dynamic>('https://www.cloudflare.com');
        return fallback.statusCode != null && fallback.statusCode! < 500;
      } catch (_) {
        return false;
      }
    } catch (_) {
      return false;
    }
  }
}
