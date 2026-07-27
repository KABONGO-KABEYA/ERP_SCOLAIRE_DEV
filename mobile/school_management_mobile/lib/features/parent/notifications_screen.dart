import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/theme/erp_theme.dart';
import 'notifications/notification_service.dart';
import 'parent_providers.dart';
import 'widgets/parent_async_widgets.dart';
import 'widgets/parent_ui_widgets.dart';
import 'widgets/premium_feature_screen.dart';

class ParentNotificationsScreen extends ConsumerWidget {
  const ParentNotificationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unlocked = ref
            .watch(parentSubscriptionProvider)
            .valueOrNull
            ?.features
            .notifications ??
        false;

    return Scaffold(
      appBar: AppBar(title: const Text('Notifications')),
      body: !unlocked
          ? const PremiumFeatureScreen(featureTitle: 'Notifications')
          : const _Body(),
    );
  }
}

class _Body extends ConsumerWidget {
  const _Body();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(parentNotificationsProvider);
    final permission = ref.watch(parentPushPermissionProvider);
    final tokenAsync = ref.watch(parentFcmTokenProvider);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(parentNotificationsProvider);
        ref.invalidate(parentPushPermissionProvider);
        ref.invalidate(parentFcmTokenProvider);
      },
      child: async.when(
        loading: () => const ParentSkeletonList(itemCount: 4),
        error: (e, _) => ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(ErpSpacing.page),
          children: [
            ParentErrorState(
              message: 'Impossible de charger les notifications.\n$e',
              onRetry: () => ref.invalidate(parentNotificationsProvider),
            ),
          ],
        ),
        data: (items) {
          final status = permission.valueOrNull ??
              ParentPushPermissionStatus.unknown;
          final token = tokenAsync.valueOrNull;

          return ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
            children: [
              ErpCard(
                padding: const EdgeInsets.all(14),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Notifications push (FCM)',
                      style: TextStyle(fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      _permissionLabel(status),
                      style: const TextStyle(
                        fontSize: 12,
                        color: ErpColors.textSecondary,
                      ),
                    ),
                    if (token != null && token.isNotEmpty) ...[
                      const SizedBox(height: 6),
                      Text(
                        'Token prêt (${token.substring(0, token.length.clamp(0, 12))}…)',
                        style: const TextStyle(fontSize: 11),
                      ),
                    ] else ...[
                      const SizedBox(height: 6),
                      const Text(
                        'Architecture prête — Firebase Messaging à brancher.',
                        style: TextStyle(
                          fontSize: 12,
                          color: ErpColors.textSecondary,
                        ),
                      ),
                    ],
                    const SizedBox(height: 10),
                    OutlinedButton.icon(
                      onPressed: () async {
                        await ref
                            .read(parentNotificationInboxRepositoryProvider)
                            .ensurePermission();
                        ref.invalidate(parentPushPermissionProvider);
                        ref.invalidate(parentFcmTokenProvider);
                      },
                      icon: const Icon(Icons.notifications_active_outlined),
                      label: const Text('Activer les notifications'),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              const ParentSectionTitle('Boîte de notifications'),
              if (items.isEmpty)
                const ParentEmptyState(
                  title: 'Aucune notification',
                  subtitle: 'Les alertes de l’école apparaîtront ici.',
                  icon: Icons.notifications_none,
                )
              else
                ...items.map(
                  (n) => Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: ParentFadeSlide(
                      child: ErpCard(
                        padding: const EdgeInsets.all(14),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Icon(
                              n.isRead
                                  ? Icons.notifications_none
                                  : Icons.notifications_active,
                              color: n.isRead
                                  ? ErpColors.textSecondary
                                  : ErpColors.primary,
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    n.title,
                                    style: TextStyle(
                                      fontWeight: n.isRead
                                          ? FontWeight.w600
                                          : FontWeight.w700,
                                    ),
                                  ),
                                  const SizedBox(height: 4),
                                  Text(n.message),
                                  const SizedBox(height: 6),
                                  Text(
                                    DateFormat('dd/MM/yyyy HH:mm')
                                        .format(n.date.toLocal()),
                                    style: const TextStyle(
                                      fontSize: 12,
                                      color: ErpColors.textSecondary,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }

  String _permissionLabel(ParentPushPermissionStatus status) => switch (status) {
        ParentPushPermissionStatus.granted => 'Autorisation accordée',
        ParentPushPermissionStatus.denied => 'Autorisation refusée',
        ParentPushPermissionStatus.provisional => 'Autorisation provisoire',
        ParentPushPermissionStatus.unsupported =>
          'Push non branché (scaffolding local)',
        ParentPushPermissionStatus.unknown => 'Statut inconnu',
      };
}
