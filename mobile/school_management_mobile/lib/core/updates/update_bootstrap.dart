import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../connection/connection_mode_notifier.dart';
import 'update_dialog.dart';
import 'update_manager.dart';

/// Vérifie les mises à jour au démarrage, au retour réseau, et toutes les 6 heures.
class UpdateBootstrap extends ConsumerStatefulWidget {
  const UpdateBootstrap({super.key, required this.child});

  final Widget child;

  @override
  ConsumerState<UpdateBootstrap> createState() => _UpdateBootstrapState();
}

class _UpdateBootstrapState extends ConsumerState<UpdateBootstrap> {
  final _manager = UpdateManager();
  Timer? _timer;
  StreamSubscription<List<ConnectivityResult>>? _connectivitySub;
  var _started = false;
  var _wasOffline = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _start());
  }

  void _start() {
    if (_started) return;
    _started = true;
    unawaited(_check());
    _timer = Timer.periodic(const Duration(hours: 6), (_) => unawaited(_check()));
    _connectivitySub = Connectivity().onConnectivityChanged.listen((results) {
      final offline = results.every((r) => r == ConnectivityResult.none);
      if (_wasOffline && !offline) {
        unawaited(_check());
      }
      _wasOffline = offline;
    });
  }

  Future<void> _check() async {
    if (!mounted) return;
    final connection = ref.read(connectionModeProvider);
    final outcome = await _manager.checkSilently(baseUrl: connection.baseUrl);
    if (!mounted || outcome == null) return;
    await showUpdateDialogIfNeeded(context, outcome, _manager);
  }

  @override
  void dispose() {
    _timer?.cancel();
    _connectivitySub?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}
