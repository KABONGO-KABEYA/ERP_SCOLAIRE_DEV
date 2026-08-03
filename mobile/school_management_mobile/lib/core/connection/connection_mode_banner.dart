import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../theme/erp_theme.dart';
import '../widgets/erp_widgets.dart';
import 'connection_mode.dart';
import 'connection_mode_notifier.dart';

/// Barre de statut permanente Local / Distant / Cache — tap = re-sonde.
class ConnectionModeBanner extends ConsumerStatefulWidget {
  const ConnectionModeBanner({super.key});

  @override
  ConsumerState<ConnectionModeBanner> createState() =>
      _ConnectionModeBannerState();
}

class _ConnectionModeBannerState extends ConsumerState<ConnectionModeBanner> {
  bool _tapping = false;

  Future<void> _onRefresh() async {
    if (_tapping) return;
    setState(() => _tapping = true);
    try {
      await ref.read(connectionModeProvider.notifier).refresh();
    } finally {
      if (mounted) setState(() => _tapping = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final snap = ref.watch(connectionModeProvider);
    final detecting = snap.mode == ConnectionMode.detecting || _tapping;

    final (color, icon, label) = switch (snap.mode) {
      ConnectionMode.detecting => (
          ErpColors.warning,
          Icons.hourglass_top,
          'Détection du serveur…',
        ),
      ConnectionMode.local => (
          ErpColors.success,
          Icons.wifi,
          'Mode local — synchronisé',
        ),
      ConnectionMode.cloud => (
          ErpColors.primary,
          Icons.cloud_outlined,
          'Mode distant',
        ),
      ConnectionMode.offline => (
          ErpColors.danger,
          Icons.cloud_off_outlined,
          'Mode cache — hors ligne',
        ),
    };

    return ErpBanner(
      label: detecting ? 'Détection du serveur…' : label,
      icon: icon,
      color: color,
      busy: detecting,
      onTap: _onRefresh,
      trailing: detecting
          ? null
          : Icon(
              Icons.refresh,
              size: 14,
              color: ErpColors.textSecondary.withValues(alpha: 0.9),
            ),
    );
  }
}
