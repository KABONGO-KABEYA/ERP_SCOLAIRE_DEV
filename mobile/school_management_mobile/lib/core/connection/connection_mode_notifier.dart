import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../local_server_discovery/discovery_constants.dart';
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
    // Ne bloque pas le premier frame. `detecting` initial uniquement au bootstrap.
    unawaited(refresh(silent: false));
    _timer = Timer.periodic(
      _periodicInterval,
      (_) => unawaited(refresh(silent: true)),
    );
    _connectivitySub = Connectivity().onConnectivityChanged.listen((_) {
      _debounce?.cancel();
      _debounce = Timer(_connectivityDebounce, () {
        // Déjà un mode utilisable → rediscovery complète SANS repasser par detecting.
        final silentUi = _hasSettledMode;
        debugPrint(
          '[Discovery] Changement de réseau → rediscovery '
          '(uiSilent=$silentUi, mode=${state.mode.name})',
        );
        unawaited(refresh(silent: silentUi, full: true));
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
  Future<void>? _inFlight;

  bool get _hasSettledMode =>
      state.mode == ConnectionMode.local ||
      state.mode == ConnectionMode.cloud ||
      state.mode == ConnectionMode.offline;

  /// [silent] : ne force pas `detecting` (conserve l'UI).
  /// [full] : découverte complète (sinon recheck léger).
  ///
  /// Au bootstrap (`mode == detecting`), un refresh non silencieux laisse
  /// `detecting` jusqu'au premier résultat. Ensuite : jamais `cloud→detecting→cloud`.
  Future<void> refresh({bool silent = false, bool? full}) async {
    if (_inFlight != null) {
      debugPrint('[Discovery] refresh déjà en cours — ignore le doublon');
      return _inFlight!;
    }
    final probeFull = full ?? !silent;
    _inFlight = _refreshBody(silent: silent, full: probeFull).whenComplete(() {
      _inFlight = null;
    });
    return _inFlight!;
  }

  Future<void> _refreshBody({required bool silent, required bool full}) async {
    final gen = ++_generation;

    // `detecting` uniquement tant qu'aucun mode settled n'existe (démarrage).
    if (!silent && !_hasSettledMode && state.mode == ConnectionMode.detecting) {
      state = const ConnectionSnapshot(
        mode: ConnectionMode.detecting,
        message: 'Recherche du serveur…',
      );
    }

    try {
      final next = await _probe
          .probe(full: full)
          .timeout(DiscoveryConstants.discoveryUiTimeout);
      if (gen != _generation) return;
      if (_sameSnapshot(state, next)) return;
      state = next;
      debugPrint(
        '[Discovery] Mode UI=${next.mode.name} baseUrl=${next.baseUrl}',
      );
    } on TimeoutException {
      if (gen != _generation) return;
      // Ne pas écraser un mode online déjà usable par un timeout de rediscovery.
      if (_hasSettledMode && state.mode.isOnline) {
        debugPrint(
          '[Discovery] refresh() timeout — conservation mode=${state.mode.name}',
        );
        return;
      }
      state = const ConnectionSnapshot(
        mode: ConnectionMode.offline,
        hasInternet: false,
        message:
            'Délai de détection dépassé — Mode Cache si des données existent.',
      );
      debugPrint('[Discovery] refresh() UI timeout → offline');
    } catch (e) {
      if (gen != _generation) return;
      if (_hasSettledMode && state.mode.isOnline) {
        debugPrint(
          '[Discovery] refresh() erreur — conservation mode=${state.mode.name}: $e',
        );
        return;
      }
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
      a.hasInternet == b.hasInternet &&
      a.requiresReauthentication == b.requiresReauthentication;

  @override
  void dispose() {
    _timer?.cancel();
    _debounce?.cancel();
    _connectivitySub?.cancel();
    super.dispose();
  }
}
