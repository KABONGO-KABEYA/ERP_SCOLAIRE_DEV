import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/erp_theme.dart';
import '../../../core/widgets/erp_widgets.dart';
import '../parent_providers.dart';

/// Hub Scolarité : Notes + Bulletins + Présences (routes existantes conservées).
class ParentScolariteHubScreen extends ConsumerWidget {
  const ParentScolariteHubScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final features = ref.watch(parentSubscriptionProvider).valueOrNull?.features;

    return Scaffold(
      appBar: AppBar(title: const Text('Scolarité')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
        children: [
          const Text(
            'Consultez les résultats et le suivi pédagogique de votre enfant.',
            style: TextStyle(fontSize: 13, color: ErpColors.textSecondary, height: 1.4),
          ),
          const SizedBox(height: 16),
          _HubTile(
            icon: Icons.school_outlined,
            title: 'Notes',
            subtitle: 'Résultats par période et par cours',
            locked: !(features?.notes ?? false),
            onTap: () => context.push('/parent/notes'),
          ),
          const SizedBox(height: 10),
          _HubTile(
            icon: Icons.description_outlined,
            title: 'Bulletins',
            subtitle: 'Bulletins PDF officiels',
            locked: !(features?.bulletins ?? false),
            onTap: () => context.push('/parent/bulletins'),
          ),
          const SizedBox(height: 10),
          _HubTile(
            icon: Icons.event_available_outlined,
            title: 'Présences',
            subtitle: 'Absences et retards',
            locked: !(features?.attendance ?? false),
            onTap: () => context.push('/parent/attendance'),
          ),
          if (!(features?.notes ?? false) ||
              !(features?.bulletins ?? false) ||
              !(features?.attendance ?? false)) ...[
            const SizedBox(height: 20),
            ErpCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Row(
                    children: [
                      ErpLockChip(),
                      SizedBox(width: 8),
                      Text(
                        'Débloquez la scolarité complète',
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          color: ErpColors.navy,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Notes, bulletins et présences sont inclus dans l’abonnement Premium.',
                    style: TextStyle(fontSize: 13, color: ErpColors.textSecondary),
                  ),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: () => context.push('/parent/subscription'),
                    child: const Text('Voir Premium'),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// Hub Messages : Communications + Notifications.
class ParentMessagesHubScreen extends ConsumerWidget {
  const ParentMessagesHubScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final features = ref.watch(parentSubscriptionProvider).valueOrNull?.features;
    final unread = ref.watch(parentNotificationUnreadCountProvider).valueOrNull ??
        (ref.watch(parentNotificationInboxProvider).valueOrNull ?? const [])
            .where((n) => !n.isRead)
            .length;

    return Scaffold(
      appBar: AppBar(title: const Text('Messages')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
        children: [
          const Text(
            'Communications de l’école et alertes importantes.',
            style: TextStyle(fontSize: 13, color: ErpColors.textSecondary, height: 1.4),
          ),
          const SizedBox(height: 16),
          _HubTile(
            icon: Icons.forum_outlined,
            title: 'Communications',
            subtitle: 'Messages école ↔ parent',
            locked: !(features?.communications ?? false),
            onTap: () => context.push('/parent/communications'),
          ),
          const SizedBox(height: 10),
          _HubTile(
            icon: Icons.notifications_outlined,
            title: 'Notifications',
            subtitle: 'Alertes et rappels',
            locked: !(features?.notifications ?? false),
            badgeCount: unread,
            onTap: () => context.push('/parent/notifications'),
          ),
          if (!(features?.communications ?? false) ||
              !(features?.notifications ?? false)) ...[
            const SizedBox(height: 20),
            ErpCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Row(
                    children: [
                      ErpLockChip(),
                      SizedBox(width: 8),
                      Text(
                        'Messages Premium',
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          color: ErpColors.navy,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Activez Premium pour lire les communications et notifications.',
                    style: TextStyle(fontSize: 13, color: ErpColors.textSecondary),
                  ),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: () => context.push('/parent/subscription'),
                    child: const Text('Voir Premium'),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _HubTile extends StatelessWidget {
  const _HubTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
    this.locked = false,
    this.badgeCount = 0,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;
  final bool locked;
  final int badgeCount;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      padding: EdgeInsets.zero,
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        minVerticalPadding: 12,
        leading: Container(
          width: 44,
          height: 44,
          decoration: BoxDecoration(
            color: ErpColors.primary.withValues(alpha: 0.1),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Icon(icon, color: ErpColors.primary),
        ),
        title: Row(
          children: [
            Expanded(
              child: Text(
                title,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  color: ErpColors.textPrimary,
                ),
              ),
            ),
            if (locked) const ErpLockChip(compact: true),
            if (!locked && badgeCount > 0) ...[
              const SizedBox(width: 6),
              const ErpBadgeDot(size: 8),
            ],
          ],
        ),
        subtitle: Text(
          subtitle,
          style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
        ),
        trailing: const Icon(Icons.chevron_right, color: ErpColors.textSecondary),
        onTap: () {
          if (locked) {
            context.push('/parent/subscription');
            return;
          }
          onTap();
        },
      ),
    );
  }
}
