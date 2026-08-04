import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/config/binding_migration_config.dart';
import '../../../core/config/strict_discovery_rollout_policy.dart';
import '../../../core/school_binding/jwt_binding_migration_service.dart';
import '../../../core/school_binding/school_binding_gate.dart';

/// Bannière migration JWT / rappel activation QR (§4.11).
class ParentMigrationBanner extends StatelessWidget {
  const ParentMigrationBanner({super.key});

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<_BannerState>(
      future: _load(),
      builder: (context, snapshot) {
        final state = snapshot.data;
        if (state == null || !state.visible) {
          return const SizedBox.shrink();
        }

        return Padding(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
          child: Card(
            color: state.urgent
                ? Colors.orange.shade50
                : Theme.of(context).colorScheme.surfaceContainerHighest,
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    state.urgent ? Icons.warning_amber_rounded : Icons.info_outline,
                    size: 22,
                  ),
                  const SizedBox(width: 12),
                  Expanded(child: Text(state.message)),
                  if (state.showActivate)
                    TextButton(
                      onPressed: () => context.push('/parent/activate'),
                      child: const Text('QR'),
                    ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  static Future<_BannerState> _load() async {
    final binding = await SchoolBindingGate.bindingRepository.load();
    final preferActivate =
        await SchoolBindingGate.shouldPreferActivationEntryForParent();

    if (preferActivate) {
      return const _BannerState(
        visible: true,
        message:
            'Activation requise : scannez le QR fourni par l\'école pour lier cet appareil.',
        showActivate: true,
        urgent: true,
      );
    }

    if (BindingMigrationPolicy.isMigrationEndingSoon) {
      final days = BindingMigrationPolicy.daysUntilMigrationEndUtc ?? 0;
      return _BannerState(
        visible: true,
        message:
            'Dans $days jour(s), la connexion sans QR ne sera plus possible. Activez votre appareil.',
        showActivate: true,
        urgent: true,
      );
    }

    if (BindingMigrationPolicy.effectiveAllowJwtBindingMigration &&
        binding != null &&
        await JwtBindingMigrationService.isJwtMigratedBinding(binding)) {
      return const _BannerState(
        visible: true,
        message:
            'Compte migré automatiquement — scannez le QR de l\'école pour une activation officielle.',
        showActivate: true,
        urgent: false,
      );
    }

    if (StrictDiscoveryRolloutPolicy.shouldEnableStrictDiscoveryInProductionBuild &&
        !BindingMigrationPolicy.isStrictSchoolDiscoveryEnabled) {
      return _BannerState(
        visible: true,
        message: StrictDiscoveryRolloutPolicy.rolloutHint,
        showActivate: false,
        urgent: false,
      );
    }

    return const _BannerState(visible: false, message: '');
  }
}

class _BannerState {
  const _BannerState({
    required this.visible,
    required this.message,
    this.showActivate = false,
    this.urgent = false,
  });

  final bool visible;
  final String message;
  final bool showActivate;
  final bool urgent;
}
