import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/theme/erp_theme.dart';
import '../models/parent_models.dart';
import '../offline/parent_offline_cache.dart';
import '../parent_providers.dart';
import '../widgets/parent_async_widgets.dart';
import '../widgets/parent_ui_widgets.dart';
import '../widgets/premium_feature_screen.dart';

/// Module Notes V2 — dossier isolé `features/parent/grades`.
class ParentGradesScreen extends ConsumerWidget {
  const ParentGradesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unlocked =
        ref.watch(parentSubscriptionProvider).valueOrNull?.features.notes ?? false;
    final selected = ref.watch(selectedChildProvider);
    ref.listen(parentChildrenProvider, (_, next) {
      next.whenData((c) => ensureChildSelected(ref, c));
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Notes')),
      body: !unlocked
          ? const PremiumFeatureScreen(featureTitle: 'Notes & moyennes')
          : selected == null
              ? const Center(child: Text('Sélectionnez un enfant.'))
              : _GradesBody(studentId: selected.studentId),
    );
  }
}

class _GradesBody extends ConsumerWidget {
  const _GradesBody({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(parentGradesProvider(studentId));
    final children = ref.watch(parentChildrenProvider).valueOrNull ?? const [];
    final showOffline = parentHasOfflineCacheHit(
      ref.watch(parentOfflineCacheHitsProvider),
      [ParentCacheKeys.grades(studentId)],
    );

    return RefreshIndicator(
      onRefresh: () async => ref.invalidate(parentGradesProvider(studentId)),
      child: async.when(
        loading: () => const ParentSkeletonList(itemCount: 4),
        error: (e, _) => ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(ErpSpacing.page),
          children: [
            ParentErrorState(
              message: 'Impossible de charger les notes.\n$e',
              onRetry: () => ref.invalidate(parentGradesProvider(studentId)),
            ),
          ],
        ),
        data: (grades) => ParentFadeSlide(
          child: ListView(
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
            if (grades.subjects.isEmpty)
              const ParentEmptyState(
                title: 'Aucune note publiée',
                subtitle: 'Les notes apparaîtront dès que l’école les publiera.',
                icon: Icons.school_outlined,
              )
            else ...[
            Row(
              children: [
                Expanded(
                  child: _StatCard(
                    label: 'Moyenne générale',
                    value: grades.generalAverage.toStringAsFixed(2),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: _StatCard(
                    label: 'Rang',
                    value: grades.classSize > 0
                        ? '${grades.rank}/${grades.classSize}'
                        : '—',
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            const ParentSectionTitle('Évolution'),
            ErpCard(
              child: SizedBox(
                height: 180,
                child: grades.evolution.isEmpty
                    ? const Center(
                        child: Text(
                          'Graphique disponible dès que les notes seront publiées.',
                          textAlign: TextAlign.center,
                        ),
                      )
                    : LineChart(
                        LineChartData(
                          gridData: const FlGridData(show: true, drawVerticalLine: false),
                          titlesData: const FlTitlesData(show: false),
                          borderData: FlBorderData(show: false),
                          lineBarsData: [
                            LineChartBarData(
                              spots: [
                                for (var i = 0; i < grades.evolution.length; i++)
                                  FlSpot(i.toDouble(), grades.evolution[i]),
                              ],
                              isCurved: true,
                              color: ErpColors.primary,
                              barWidth: 3,
                              dotData: const FlDotData(show: true),
                              belowBarData: BarAreaData(
                                show: true,
                                color: ErpColors.primary.withValues(alpha: 0.12),
                              ),
                            ),
                          ],
                        ),
                      ),
              ),
            ),
            const SizedBox(height: 16),
            const ParentSectionTitle('Matières'),
            ...grades.subjects.map(
              (s) => Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _SubjectCard(subject: s),
              ),
            ),
            ],
          ],
        ),
        ),
      ),
    );
  }
}

class _SubjectCard extends StatelessWidget {
  const _SubjectCard({required this.subject});

  final ParentGradeSubject subject;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      child: Theme(
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          tilePadding: EdgeInsets.zero,
          childrenPadding: const EdgeInsets.only(top: 4),
          title: Text(
            subject.name,
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
          ),
          subtitle: Text(
            'Moyenne ${subject.average.toStringAsFixed(1)} / ${subject.maxScore.toStringAsFixed(0)}',
            style: const TextStyle(color: ErpColors.primary, fontWeight: FontWeight.w600),
          ),
          children: [
            if (subject.interrogations.isEmpty &&
                subject.exams.isEmpty &&
                subject.works.isEmpty)
              const Padding(
                padding: EdgeInsets.only(bottom: 8),
                child: Text(
                  'Aucun détail d\'évaluation.',
                  style: TextStyle(color: ErpColors.textSecondary),
                ),
              ),
            ..._section('Interrogations', subject.interrogations),
            ..._section('Examens', subject.exams),
            ..._section('Travaux / devoirs', subject.works),
          ],
        ),
      ),
    );
  }

  List<Widget> _section(String title, List<ParentGradeItem> items) {
    if (items.isEmpty) return const [];
    return [
      Align(
        alignment: Alignment.centerLeft,
        child: Text(
          title,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: ErpColors.textSecondary,
          ),
        ),
      ),
      const SizedBox(height: 6),
      for (final item in items)
        Padding(
          padding: const EdgeInsets.only(bottom: 6),
          child: Row(
            children: [
              Expanded(child: Text(item.label)),
              Text(
                '${item.score.toStringAsFixed(1)} / ${item.maxScore.toStringAsFixed(0)}',
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
            ],
          ),
        ),
      const SizedBox(height: 8),
    ];
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
          const SizedBox(height: 6),
          Text(
            value,
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w700,
              color: ErpColors.navy,
            ),
          ),
        ],
      ),
    );
  }
}
