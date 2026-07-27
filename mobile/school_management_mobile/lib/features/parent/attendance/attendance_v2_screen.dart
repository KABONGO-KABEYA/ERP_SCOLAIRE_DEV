import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/erp_theme.dart';
import '../models/parent_models.dart';
import '../offline/parent_offline_cache.dart';
import '../parent_providers.dart';
import '../widgets/parent_async_widgets.dart';
import '../widgets/parent_ui_widgets.dart';
import '../widgets/premium_feature_screen.dart';

/// Module Présences V2 — calendrier mensuel coloré + stats.
class ParentAttendanceV2Screen extends ConsumerWidget {
  const ParentAttendanceV2Screen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unlocked = ref
            .watch(parentSubscriptionProvider)
            .valueOrNull
            ?.features
            .attendance ??
        false;
    final selected = ref.watch(selectedChildProvider);
    ref.listen(parentChildrenProvider, (_, next) {
      next.whenData((c) => ensureChildSelected(ref, c));
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Présences')),
      body: !unlocked
          ? const PremiumFeatureScreen(featureTitle: 'Présences & absences')
          : selected == null
              ? const Center(child: Text('Sélectionnez un enfant.'))
              : _AttendanceBody(studentId: selected.studentId),
    );
  }
}

class _AttendanceBody extends ConsumerStatefulWidget {
  const _AttendanceBody({required this.studentId});

  final String studentId;

  @override
  ConsumerState<_AttendanceBody> createState() => _AttendanceBodyState();
}

class _AttendanceBodyState extends ConsumerState<_AttendanceBody> {
  late DateTime _month;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _month = DateTime(now.year, now.month);
  }

  void _shiftMonth(int delta) {
    setState(() {
      _month = DateTime(_month.year, _month.month + delta);
    });
  }

  @override
  Widget build(BuildContext context) {
    final async = ref.watch(parentAttendanceProvider(widget.studentId));
    final children = ref.watch(parentChildrenProvider).valueOrNull ?? const [];
    final showOffline = parentHasOfflineCacheHit(
      ref.watch(parentOfflineCacheHitsProvider),
      [ParentCacheKeys.attendance(widget.studentId)],
    );

    return RefreshIndicator(
      onRefresh: () async =>
          ref.invalidate(parentAttendanceProvider(widget.studentId)),
      child: async.when(
        loading: () => const ParentSkeletonList(itemCount: 3),
        error: (e, _) => ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(ErpSpacing.page),
          children: [
            ParentErrorState(
              message: 'Impossible de charger les présences.\n$e',
              onRetry: () =>
                  ref.invalidate(parentAttendanceProvider(widget.studentId)),
            ),
          ],
        ),
        data: (days) {
          final byDate = <DateTime, ParentAttendanceDay>{
            for (final d in days)
              DateTime(d.date.year, d.date.month, d.date.day): d,
          };
          final monthDays = days.where(
            (d) => d.date.year == _month.year && d.date.month == _month.month,
          );
          final present = monthDays.where((d) => d.status == 'present').length;
          final absent = monthDays.where((d) => d.status == 'absent').length;
          final late = monthDays.where((d) => d.status == 'late').length;
          final total = present + absent + late;
          final rate = total == 0 ? 0.0 : present / total;

          return ListView(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
            children: [
              ParentChildSelector(
                children: children,
                selectedId: widget.studentId,
                onChanged: (id) =>
                    ref.read(selectedChildIdProvider.notifier).state = id,
              ),
              if (children.length > 1) const SizedBox(height: 12),
              ParentOfflineBanner(visible: showOffline),
              Row(
                children: [
                  Expanded(
                    child: _StatChip(
                      label: 'Présents',
                      value: '$present',
                      color: ErpColors.success,
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: _StatChip(
                      label: 'Absents',
                      value: '$absent',
                      color: ErpColors.danger,
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: _StatChip(
                      label: 'Retards',
                      value: '$late',
                      color: ErpColors.warning,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              ErpCard(
                padding: const EdgeInsets.all(14),
                child: Row(
                  children: [
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            'Taux de présence (mois)',
                            style: TextStyle(
                              fontSize: 12,
                              color: ErpColors.textSecondary,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '${(rate * 100).toStringAsFixed(0)} %',
                            style: const TextStyle(
                              fontSize: 22,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ],
                      ),
                    ),
                    SizedBox(
                      width: 56,
                      height: 56,
                      child: CircularProgressIndicator(
                        value: rate,
                        strokeWidth: 6,
                        backgroundColor: ErpColors.primary.withValues(alpha: 0.12),
                        color: ErpColors.success,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              ErpCard(
                padding: const EdgeInsets.fromLTRB(12, 12, 12, 16),
                child: Column(
                  children: [
                    Row(
                      children: [
                        IconButton(
                          onPressed: () => _shiftMonth(-1),
                          icon: const Icon(Icons.chevron_left),
                        ),
                        Expanded(
                          child: Text(
                            DateFormat('MMMM yyyy').format(_month),
                            textAlign: TextAlign.center,
                            style: const TextStyle(
                              fontWeight: FontWeight.w700,
                              fontSize: 16,
                            ),
                          ),
                        ),
                        IconButton(
                          onPressed: () => _shiftMonth(1),
                          icon: const Icon(Icons.chevron_right),
                        ),
                      ],
                    ),
                    const SizedBox(height: 8),
                    const _WeekHeader(),
                    const SizedBox(height: 6),
                    _MonthGrid(month: _month, byDate: byDate),
                    const SizedBox(height: 12),
                    const _Legend(),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              const ParentSectionTitle('Détail du mois'),
              if (monthDays.isEmpty)
                const ErpCard(
                  child: Text(
                    'Aucun pointage pour ce mois. Les présences apparaîtront dès que l’école les publiera.',
                  ),
                )
              else
                ...monthDays
                    .toList()
                    .reversed
                    .map((d) => _DayTile(day: d)),
            ],
          );
        },
      ),
    );
  }
}

class _StatChip extends StatelessWidget {
  const _StatChip({
    required this.label,
    required this.value,
    required this.color,
  });

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      padding: const EdgeInsets.all(12),
      child: Column(
        children: [
          Text(
            value,
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w700,
              color: color,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
          ),
        ],
      ),
    );
  }
}

class _WeekHeader extends StatelessWidget {
  const _WeekHeader();

  @override
  Widget build(BuildContext context) {
    const labels = ['L', 'M', 'M', 'J', 'V', 'S', 'D'];
    return Row(
      children: labels
          .map(
            (l) => Expanded(
              child: Text(
                l,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                  color: ErpColors.textSecondary,
                ),
              ),
            ),
          )
          .toList(),
    );
  }
}

class _MonthGrid extends StatelessWidget {
  const _MonthGrid({required this.month, required this.byDate});

  final DateTime month;
  final Map<DateTime, ParentAttendanceDay> byDate;

  @override
  Widget build(BuildContext context) {
    final first = DateTime(month.year, month.month, 1);
    final daysInMonth = DateTime(month.year, month.month + 1, 0).day;
    // Monday=1 … Sunday=7 → offset 0…6
    final startOffset = (first.weekday + 6) % 7;
    final cells = startOffset + daysInMonth;
    final rows = (cells / 7).ceil();

    return Column(
      children: List.generate(rows, (row) {
        return Padding(
          padding: const EdgeInsets.only(bottom: 4),
          child: Row(
            children: List.generate(7, (col) {
              final index = row * 7 + col;
              final dayNum = index - startOffset + 1;
              if (dayNum < 1 || dayNum > daysInMonth) {
                return const Expanded(child: SizedBox(height: 36));
              }
              final date = DateTime(month.year, month.month, dayNum);
              final entry = byDate[date];
              final color = _statusColor(entry?.status);
              return Expanded(
                child: Tooltip(
                  message: entry == null
                      ? DateFormat('dd/MM').format(date)
                      : '${_statusLabel(entry.status)} · ${DateFormat('dd/MM').format(date)}',
                  child: Container(
                    height: 36,
                    margin: const EdgeInsets.symmetric(horizontal: 2),
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: entry == null ? 0.06 : 0.22),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(
                        color: color.withValues(alpha: entry == null ? 0.15 : 0.55),
                      ),
                    ),
                    alignment: Alignment.center,
                    child: Text(
                      '$dayNum',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: entry == null
                            ? ErpColors.textSecondary
                            : color.withValues(alpha: 1),
                      ),
                    ),
                  ),
                ),
              );
            }),
          ),
        );
      }),
    );
  }

  static Color _statusColor(String? status) => switch (status) {
        'absent' => ErpColors.danger,
        'late' => ErpColors.warning,
        'present' => ErpColors.success,
        _ => ErpColors.textSecondary,
      };

  static String _statusLabel(String status) => switch (status) {
        'absent' => 'Absent',
        'late' => 'Retard',
        _ => 'Présent',
      };
}

class _Legend extends StatelessWidget {
  const _Legend();

  @override
  Widget build(BuildContext context) {
    return const Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        _LegendDot(color: ErpColors.success, label: 'Présent'),
        SizedBox(width: 12),
        _LegendDot(color: ErpColors.warning, label: 'Retard'),
        SizedBox(width: 12),
        _LegendDot(color: ErpColors.danger, label: 'Absent'),
      ],
    );
  }
}

class _LegendDot extends StatelessWidget {
  const _LegendDot({required this.color, required this.label});

  final Color color;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 10,
          height: 10,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: 4),
        Text(label, style: const TextStyle(fontSize: 11)),
      ],
    );
  }
}

class _DayTile extends StatelessWidget {
  const _DayTile({required this.day});

  final ParentAttendanceDay day;

  @override
  Widget build(BuildContext context) {
    final (label, color, icon) = switch (day.status) {
      'absent' => ('Absence', ErpColors.danger, Icons.cancel_outlined),
      'late' => ('Retard', ErpColors.warning, Icons.schedule),
      _ => ('Présent', ErpColors.success, Icons.check_circle_outline),
    };

    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: ErpCard(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        child: Row(
          children: [
            Icon(icon, color: color),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    DateFormat('EEEE dd/MM/yyyy').format(day.date),
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                  if (day.note != null && day.note!.isNotEmpty)
                    Text(
                      day.note!,
                      style: const TextStyle(
                        fontSize: 12,
                        color: ErpColors.textSecondary,
                      ),
                    ),
                ],
              ),
            ),
            Text(
              label,
              style: TextStyle(color: color, fontWeight: FontWeight.w700),
            ),
          ],
        ),
      ),
    );
  }
}
