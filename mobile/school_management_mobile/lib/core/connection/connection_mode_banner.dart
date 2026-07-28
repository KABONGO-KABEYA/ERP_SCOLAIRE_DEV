import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../theme/erp_theme.dart';
import 'connection_mode.dart';
import 'connection_mode_notifier.dart';

/// Bandeau permanent : Local / Distant / Mode Cache — tap = re-sonde immédiate.
class ConnectionModeBanner extends ConsumerStatefulWidget {
  const ConnectionModeBanner({super.key});

  @override
  ConsumerState<ConnectionModeBanner> createState() => _ConnectionModeBannerState();
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
    final (color, icon) = switch (snap.mode) {
      ConnectionMode.detecting => (ErpColors.warning, Icons.hourglass_top),
      ConnectionMode.local => (ErpColors.success, Icons.wifi),
      ConnectionMode.cloud => (const Color(0xFF2563EB), Icons.cloud_outlined),
      ConnectionMode.offline => (ErpColors.danger, Icons.offline_bolt_outlined),
    };

    return Material(
      color: color.withValues(alpha: 0.14),
      child: SafeArea(
        bottom: false,
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: detecting ? null : _onRefresh,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            child: Row(
              children: [
                if (detecting)
                  SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2, color: color),
                  )
                else
                  Icon(icon, size: 18, color: color),
                const SizedBox(width: 8),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        detecting ? 'Détection…' : snap.displayLabel,
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                          color: color,
                        ),
                      ),
                      Text(
                        detecting ? 'Recherche du serveur…' : snap.displaySubtitle,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 11,
                          color: ErpColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                Icon(
                  Icons.refresh,
                  size: 20,
                  color: detecting ? color.withValues(alpha: 0.4) : color,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
