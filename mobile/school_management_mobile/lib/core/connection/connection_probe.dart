import '../local_server_discovery/discovery_models.dart';
import '../local_server_discovery/local_server_discovery.dart';
import 'connection_mode.dart';

/// Adapter : conserve l'API ConnectionProbe en déléguant à LocalServerDiscovery.
class ConnectionProbe {
  Future<ConnectionSnapshot> probe({
    Duration localTimeout = const Duration(seconds: 2),
    Duration cloudTimeout = const Duration(seconds: 3),
    bool full = true,
  }) async {
    final result = full
        ? await LocalServerDiscovery.instance.rediscover()
        : await LocalServerDiscovery.instance.recheck();
    return _map(result);
  }

  ConnectionSnapshot _map(DiscoveryResult result) {
    switch (result.mode) {
      case DiscoveryMode.local:
        return ConnectionSnapshot(
          mode: ConnectionMode.local,
          baseUrl: result.baseUrl,
          message: result.message,
          hasInternet: true,
        );
      case DiscoveryMode.remote:
        return ConnectionSnapshot(
          mode: ConnectionMode.cloud,
          baseUrl: result.baseUrl,
          message: result.message,
          hasInternet: true,
        );
      case DiscoveryMode.detecting:
        return const ConnectionSnapshot(
          mode: ConnectionMode.detecting,
          message: 'Recherche du serveur…',
        );
      case DiscoveryMode.offline:
        return ConnectionSnapshot(
          mode: ConnectionMode.offline,
          hasInternet: false,
          message: result.message,
        );
    }
  }
}
