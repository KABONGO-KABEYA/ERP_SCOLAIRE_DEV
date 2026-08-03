import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/theme/erp_theme.dart';
import 'notifications/notification_service.dart';
import 'widgets/parent_async_widgets.dart';
import 'widgets/parent_ui_widgets.dart';
import 'widgets/premium_feature_screen.dart';
import 'parent_providers.dart';

class ParentNotificationsScreen extends ConsumerWidget {
  const ParentNotificationsScreen({super.key});

  static const _filters = <(String?, String)>[
    (null, 'Toutes'),
    ('Payment', 'Paiement'),
    ('Bulletin', 'Bulletin'),
    ('Grades', 'Notes'),
    ('Discipline', 'Discipline'),
    ('Merit', 'Mérite'),
    ('Communication', 'Communication'),
    ('Attendance', 'Présence'),
    ('Administration', 'Administration'),
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unlocked = ref
            .watch(parentSubscriptionProvider)
            .valueOrNull
            ?.features
            .notifications ??
        false;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: unlocked
            ? [
                TextButton(
                  onPressed: () async {
                    await ref
                        .read(parentNotificationInboxRepositoryProvider)
                        .markAllRead();
                    ref.invalidate(parentNotificationInboxProvider);
                  },
                  child: const Text('Tout lu'),
                ),
              ]
            : null,
      ),
      body: !unlocked
          ? const PremiumFeatureScreen(featureTitle: 'Notifications')
          : const _Body(),
    );
  }
}

class _Body extends ConsumerStatefulWidget {
  const _Body();

  @override
  ConsumerState<_Body> createState() => _BodyState();
}

class _BodyState extends ConsumerState<_Body> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  String _relativeDate(DateTime date) {
    final local = date.toLocal();
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final day = DateTime(local.year, local.month, local.day);
    final time = DateFormat('HH:mm').format(local);
    if (day == today) return 'Aujourd\'hui · $time';
    if (day == today.subtract(const Duration(days: 1))) {
      return 'Hier · $time';
    }
    return DateFormat('dd/MM/yyyy · HH:mm').format(local);
  }

  @override
  Widget build(BuildContext context) {
    ref.watch(parentNotificationPollingProvider);
    final async = ref.watch(parentNotificationInboxProvider);
    final selectedCategory = ref.watch(parentNotificationCategoryFilterProvider);
    final permission = ref.watch(parentPushPermissionProvider);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(parentNotificationInboxProvider);
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
              onRetry: () => ref.invalidate(parentNotificationInboxProvider),
            ),
          ],
        ),
        data: (items) {
          final status = permission.valueOrNull ??
              ParentPushPermissionStatus.unknown;

          return ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
            children: [
              TextField(
                controller: _searchController,
                decoration: InputDecoration(
                  hintText: 'Rechercher une notification…',
                  prefixIcon: const Icon(Icons.search),
                  suffixIcon: _searchController.text.isEmpty
                      ? null
                      : IconButton(
                          icon: const Icon(Icons.clear),
                          onPressed: () {
                            _searchController.clear();
                            ref
                                .read(parentNotificationSearchProvider.notifier)
                                .state = '';
                            setState(() {});
                          },
                        ),
                ),
                onChanged: (v) {
                  ref.read(parentNotificationSearchProvider.notifier).state = v;
                  setState(() {});
                },
              ),
              const SizedBox(height: 12),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    for (final (value, label) in ParentNotificationsScreen._filters) ...[
                      Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: FilterChip(
                          label: Text(label),
                          selected: selectedCategory == value,
                          onSelected: (_) {
                            ref
                                .read(
                                  parentNotificationCategoryFilterProvider
                                      .notifier,
                                )
                                .state = value;
                          },
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              const SizedBox(height: 12),
              ErpCard(
                padding: const EdgeInsets.all(14),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Icon(
                          Icons.notifications_active_outlined,
                          color: status == ParentPushPermissionStatus.granted
                              ? ErpColors.primary
                              : ErpColors.textSecondary,
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Text(
                            status == ParentPushPermissionStatus.granted
                                ? 'Une pastille « alertes actives » reste en barre de statut : les notifications arrivent même app en arrière-plan (toutes les ~8 s).'
                                : status == ParentPushPermissionStatus.denied
                                    ? 'Autorisation refusée — activez les notifications dans les réglages Android.'
                                    : 'Autorisez les notifications pour recevoir les alertes hors de l’écran de l’app.',
                            style: const TextStyle(
                              fontSize: 12,
                              color: ErpColors.textSecondary,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 10),
                    OutlinedButton.icon(
                      onPressed: () async {
                        await ref
                            .read(parentNotificationServiceProvider)
                            .requestPermission();
                        ref.invalidate(parentPushPermissionProvider);
                      },
                      icon: const Icon(Icons.notifications_active_outlined),
                      label: const Text('Activer les notifications'),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              const ParentSectionTitle('Historique'),
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
                      child: Material(
                        color: Colors.transparent,
                        child: InkWell(
                          borderRadius:
                              BorderRadius.circular(ErpSpacing.cardRadius),
                          onTap: () async {
                            if (!n.isRead) {
                              await ref
                                  .read(
                                    parentNotificationInboxRepositoryProvider,
                                  )
                                  .markRead(n.id);
                              ref.invalidate(parentNotificationInboxProvider);
                            }
                          },
                          child: ErpCard(
                            padding: const EdgeInsets.all(14),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Stack(
                                  children: [
                                    CircleAvatar(
                                      radius: 20,
                                      backgroundColor: n.isRead
                                          ? ErpColors.border
                                              .withValues(alpha: 0.4)
                                          : ErpColors.primary
                                              .withValues(alpha: 0.12),
                                      child: Icon(
                                        n.categoryIcon,
                                        color: n.isRead
                                            ? ErpColors.textSecondary
                                            : ErpColors.primary,
                                        size: 20,
                                      ),
                                    ),
                                    if (!n.isRead)
                                      Positioned(
                                        right: 0,
                                        top: 0,
                                        child: Container(
                                          width: 10,
                                          height: 10,
                                          decoration: const BoxDecoration(
                                            color: ErpColors.danger,
                                            shape: BoxShape.circle,
                                          ),
                                        ),
                                      ),
                                  ],
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Row(
                                        children: [
                                          Expanded(
                                            child: Text(
                                              n.title,
                                              style: TextStyle(
                                                fontWeight: n.isRead
                                                    ? FontWeight.w600
                                                    : FontWeight.w700,
                                              ),
                                            ),
                                          ),
                                          Text(
                                            n.categoryLabel,
                                            style: const TextStyle(
                                              fontSize: 11,
                                              color: ErpColors.textSecondary,
                                            ),
                                          ),
                                        ],
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        n.message,
                                        style: TextStyle(
                                          height: 1.35,
                                          color: n.isRead
                                              ? ErpColors.textSecondary
                                              : ErpColors.textPrimary,
                                        ),
                                      ),
                                      const SizedBox(height: 6),
                                      Text(
                                        _relativeDate(n.date),
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
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}
