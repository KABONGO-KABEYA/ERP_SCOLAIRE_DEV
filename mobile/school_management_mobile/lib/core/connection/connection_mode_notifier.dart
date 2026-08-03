import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart';
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
    _timer = Timer.periodic(_periodicInterval, (_) => refresh(silent: true));
    _connectivitySub = Connectivity().onConnectivityChanged.listen((_) {
      _debounce?.cancel();
      _debounce = Timer(_connectivityDebounce, () {
        debugPrint('[Discovery] Changement de réseau → rediscovery complète');
        // Full : lastKnown hors /24 ne doit pas garder un faux Mode Local.
        refresh(silent: false);
      });
    });
  }

  static const _periodicInterval = Duration(seconds: 60);
  static const _connectivityDebounce = Duration(seconds: 3);

  final ConnectionProbe _probe;
  Timer? _timer;
  Timer? _debounce;
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
      // silent = recheck léger (IP courante, même /24) ; sinon découverte complète.
      final next = await _probe.probe(full: !silent);
      if (gen != _generation) return;
      if (_sameSnapshot(state, next)) return;
      state = next;
      debugPrint(
        '[Discovery] Mode UI=${next.mode.name} baseUrl=${next.baseUrl}',
      );
    } catch (e) {
      if (gen != _generation) return;
      state = ConnectionSnapshot(
        mode: ConnectionMode.offline,
        hasInternet: false,
        message: 'Erreur de détection : $e — Mode Cache si des données existent.',
      );
    }
  }

  static bool _sameSnapshot(ConnectionSnapshot a, ConnectionSnapshot b) =>
      a.mode == b.mode &&
      a.baseUrl == b.baseUrl &&
      a.hasInternet == b.hasInternet;

  @override
  void dispose() {
    _timer?.cancel();
    _debounce?.cancel();
    _connectivitySub?.cancel();
    super.dispose();
  }
}
