import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'connection_mode.dart';
import 'connection_probe.dart';

final connectionProbeProvider = Provider((ref) => ConnectionProbe());

final connectionModeProvider =
    StateNotifierProvider<ConnectionModeNotifier, ConnectionSnapshot>(
  (ref) => ConnectionModeNotifier(ref.watch(connectionProbeProvider)),
);

/// Détection automatique : même Wi‑Fi → Local → Distant → Mode Cache.
class ConnectionModeNotifier extends StateNotifier<ConnectionSnapshot> {
  ConnectionModeNotifier(this._probe) : super(ConnectionSnapshot.detecting) {
    refresh();
    _timer = Timer.periodic(const Duration(seconds: 45), (_) => refresh(silent: true));
    _connectivitySub = Connectivity().onConnectivityChanged.listen((_) {
      // Changement Wi‑Fi / 4G / hors ligne → re-sonde immédiate.
      refresh(silent: true);
    });
  }

  final ConnectionProbe _probe;
  Timer? _timer;
  StreamSubscription<List<ConnectivityResult>>? _connectivitySub;
  int _generation = 0;

  Future<void> refresh({bool silent = false}) async {
    final gen = ++_generation;
    if (!silent) {
      // Nouvel objet à chaque fois pour forcer le rebuild UI.
      state = const ConnectionSnapshot(
        mode: ConnectionMode.detecting,
        message: 'Recherche du serveur…',
      );
    }

    try {
      final next = await _probe.probe();
      // Une touche plus récente a déjà relancé une sonde.
      if (gen != _generation) return;
      state = next;
    } catch (e) {
      if (gen != _generation) return;
      state = ConnectionSnapshot(
        mode: ConnectionMode.offline,
        hasInternet: false,
        message: 'Erreur de détection : $e — Mode Cache si des données existent.',
      );
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    _connectivitySub?.cancel();
    super.dispose();
  }
}
