import 'dart:async';

import '../local_server_discovery/discovery_constants.dart';
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
    try {
      final result = await (full
              ? LocalServerDiscovery.instance.rediscover()
              : LocalServerDiscovery.instance.recheck())
          .timeout(DiscoveryConstants.discoveryUiTimeout);
      return _map(result);
    } on TimeoutException {
      // Ne jamais laisser refresh() bloqué : état propre → Mode Cache / offline.
      return const ConnectionSnapshot(
        mode: ConnectionMode.offline,
        hasInternet: false,
        message:
            'Délai de détection dépassé — Mode Cache si des données existent.',
      );
    } catch (e) {
      return ConnectionSnapshot(
        mode: ConnectionMode.offline,
        hasInternet: false,
        message: 'Erreur de détection : $e — Mode Cache si des données existent.',
      );
    }
  }

  ConnectionSnapshot _map(DiscoveryResult result) {
    switch (result.mode) {
      case DiscoveryMode.local:
        return ConnectionSnapshot(
          mode: ConnectionMode.local,
          baseUrl: result.baseUrl,
          message: result.message,
          hasInternet: true,
          requiresReauthentication: result.serverInstanceIdChanged,
        );
      case DiscoveryMode.remote:
        return ConnectionSnapshot(
          mode: ConnectionMode.cloud,
          baseUrl: result.baseUrl,
          message: result.message,
          hasInternet: true,
          requiresReauthentication: result.serverInstanceIdChanged,
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
