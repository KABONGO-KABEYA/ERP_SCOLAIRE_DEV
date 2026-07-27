import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers/app_providers.dart';
import '../../../core/theme/erp_theme.dart';
import '../models/parent_models.dart';
import '../offline/parent_offline_cache.dart';
import '../parent_providers.dart';
import '../widgets/parent_async_widgets.dart';
import '../widgets/parent_ui_widgets.dart';
import '../widgets/premium_feature_screen.dart';

/// Module Bulletins V2 — dossier isolé `features/parent/bulletins`.
class ParentBulletinsV2Screen extends ConsumerWidget {
  const ParentBulletinsV2Screen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unlocked =
        ref.watch(parentSubscriptionProvider).valueOrNull?.features.bulletins ??
            false;
    final selected = ref.watch(selectedChildProvider);
    ref.listen(parentChildrenProvider, (_, next) {
      next.whenData((c) => ensureChildSelected(ref, c));
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Bulletins')),
      body: !unlocked
          ? const PremiumFeatureScreen(featureTitle: 'Bulletins scolaires')
          : selected == null
              ? const Center(child: Text('Sélectionnez un enfant.'))
              : _BulletinsBody(studentId: selected.studentId),
    );
  }
}

class _BulletinsBody extends ConsumerWidget {
  const _BulletinsBody({required this.studentId});

  final String studentId;

  Future<void> _openPdf(
    BuildContext context,
    WidgetRef ref,
    ParentBulletin bulletin,
  ) async {
    try {
      await ref.read(parentRepositoryProvider).openBulletinPdf(studentId, bulletin);
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Impossible d\'ouvrir le bulletin : $e')),
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(parentBulletinsProvider(studentId));
    final children = ref.watch(parentChildrenProvider).valueOrNull ?? const [];
    final showOffline = parentHasOfflineCacheHit(
      ref.watch(parentOfflineCacheHitsProvider),
      [ParentCacheKeys.bulletins(studentId)],
    );

    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(parentBulletinsProvider(studentId)),
      child: async.when(
        loading: () => const ParentSkeletonList(itemCount: 3),
        error: (e, _) => ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(ErpSpacing.page),
          children: [
            ParentErrorState(
              message: 'Impossible de charger les bulletins.\n$e',
              onRetry: () => ref.invalidate(parentBulletinsProvider(studentId)),
            ),
          ],
        ),
        data: (bulletins) {
          return ListView(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
            children: [
              ParentChildSelector(
                children: children,
                selectedId: studentId,
                onChanged: (id) =>
                    ref.read(selectedChildIdProvider.notifier).state = id,
              ),
              if (children.length > 1) const SizedBox(height: 12),
              ParentOfflineBanner(visible: showOffline),
              if (bulletins.isEmpty)
                const ParentEmptyState(
                  title: 'Aucun bulletin',
                  subtitle: 'Aucun bulletin publié pour le moment.',
                  icon: Icons.description_outlined,
                )
              else
                ...bulletins.map(
                  (b) => Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: ErpCard(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: Text(
                                  b.periodName,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w700,
                                    fontSize: 16,
                                    color: ErpColors.navy,
                                  ),
                                ),
                              ),
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 10,
                                  vertical: 4,
                                ),
                                decoration: BoxDecoration(
                                  color: b.isPublished
                                      ? ErpColors.success.withValues(alpha: 0.12)
                                      : ErpColors.warning.withValues(alpha: 0.12),
                                  borderRadius: BorderRadius.circular(20),
                                ),
                                child: Text(
                                  b.isPublished ? 'Publié' : 'Brouillon',
                                  style: TextStyle(
                                    fontSize: 11,
                                    fontWeight: FontWeight.w700,
                                    color: b.isPublished
                                        ? ErpColors.success
                                        : ErpColors.warning,
                                  ),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 12),
                          Row(
                            children: [
                              Expanded(
                                child: _Mini(
                                  label: 'Moyenne',
                                  value: b.average.toStringAsFixed(2),
                                ),
                              ),
                              Expanded(
                                child: _Mini(
                                  label: 'Rang',
                                  value: '${b.rank}/${b.classSize > 0 ? b.classSize : '—'}',
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 10),
                          Row(
                            children: [
                              Expanded(
                                child: _Mini(
                                  label: 'Mention',
                                  value: b.mention?.isNotEmpty == true
                                      ? b.mention!
                                      : _mentionFromPercentage(b.percentage),
                                ),
                              ),
                              Expanded(
                                child: _Mini(
                                  label: 'Décision',
                                  value: b.decision?.isNotEmpty == true
                                      ? b.decision!
                                      : '—',
                                ),
                              ),
                            ],
                          ),
                          if (b.appreciation?.isNotEmpty == true) ...[
                            const SizedBox(height: 10),
                            Text(
                              b.appreciation!,
                              style: const TextStyle(
                                fontSize: 13,
                                color: ErpColors.textSecondary,
                              ),
                            ),
                          ],
                          const SizedBox(height: 14),
                          SizedBox(
                            width: double.infinity,
                            child: FilledButton.icon(
                              onPressed: b.isPublished
                                  ? () => _openPdf(context, ref, b)
                                  : null,
                              icon: const Icon(Icons.picture_as_pdf_outlined),
                              label: const Text('Voir le bulletin PDF'),
                            ),
                          ),
                        ],
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

  static String _mentionFromPercentage(double percentage) {
    if (percentage >= 80) return 'Grande distinction';
    if (percentage >= 70) return 'Distinction';
    if (percentage >= 60) return 'Satisfaction';
    if (percentage >= 50) return 'Passable';
    return 'À améliorer';
  }
}

class _Mini extends StatelessWidget {
  const _Mini({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
        const SizedBox(height: 2),
        Text(
          value,
          style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
        ),
      ],
    );
  }
}
