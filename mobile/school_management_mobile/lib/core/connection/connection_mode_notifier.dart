import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'connection_mode.dart';
import 'connection_probe.dart';

final connectionProbeProvider = Provider((ref) => ConnectionProbe());

final connectionModeProvider =
    StateNotifierProvider<ConnectionModeNotifier, ConnectionSnapshot>(
  (ref) => ConnectionModeNotifier(ref.watch(connectionProbeProvider)),
);

/// Détection automatique Local → Cloud → Hors ligne.
class ConnectionModeNotifier extends StateNotifier<ConnectionSnapshot> {
  ConnectionModeNotifier(this._probe) : super(ConnectionSnapshot.detecting) {
    refresh();
    _timer = Timer.periodic(const Duration(seconds: 45), (_) => refresh(silent: true));
  }

  final ConnectionProbe _probe;
  Timer? _timer;
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
        message: 'Erreur de détection : $e',
      );
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}
