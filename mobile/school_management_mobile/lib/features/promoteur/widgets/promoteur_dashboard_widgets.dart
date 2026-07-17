import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';

import '../../../core/theme/erp_theme.dart';
import '../promoteur_dashboard_repository.dart';
import '../dashboard_formatters.dart';
import '../models/promoteur_dashboard_models.dart';

class PromoterPeriodSelector extends StatelessWidget {
  const PromoterPeriodSelector({
    super.key,
    required this.value,
    required this.onChanged,
  });

  final DashboardPeriod value;
  final ValueChanged<DashboardPeriod> onChanged;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 40,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: DashboardPeriod.values.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final period = DashboardPeriod.values[index];
          final selected = period == value;
          return ChoiceChip(
            label: Text(period.label),
            selected: selected,
            onSelected: (_) => onChanged(period),
            selectedColor: ErpColors.primary,
            labelStyle: TextStyle(
              color: selected ? Colors.white : ErpColors.textPrimary,
              fontWeight: FontWeight.w600,
              fontSize: 12,
            ),
            backgroundColor: Colors.white,
            side: BorderSide(color: selected ? ErpColors.primary : ErpColors.border),
            showCheckmark: false,
          );
        },
      ),
    );
  }
}

class PromoterStatCard extends StatelessWidget {
  const PromoterStatCard({
    super.key,
    required this.icon,
    required this.title,
    required this.value,
    this.subtitle,
    this.changePercent,
    this.child,
    this.accent = ErpColors.primary,
  });

  final IconData icon;
  final String title;
  final String value;
  final String? subtitle;
  final double? changePercent;
  final Widget? child;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    final change = changePercent;
    final up = (change ?? 0) >= 0;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        boxShadow: [
          BoxShadow(
            color: ErpColors.navy.withValues(alpha: 0.06),
            blurRadius: 16,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 34,
                height: 34,
                decoration: BoxDecoration(
                  color: accent.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(icon, size: 18, color: accent),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  title,
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: ErpColors.textSecondary,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            value,
            style: const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: ErpColors.navy,
            ),
          ),
          if (subtitle != null) ...[
            const SizedBox(height: 4),
            Text(subtitle!, style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
          ],
          if (change != null) ...[
            const SizedBox(height: 6),
            Row(
              children: [
                Icon(
                  up ? Icons.trending_up_rounded : Icons.trending_down_rounded,
                  size: 16,
                  color: up ? ErpColors.success : ErpColors.danger,
                ),
                const SizedBox(width: 4),
                Text(
                  formatPercent(change),
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: up ? ErpColors.success : ErpColors.danger,
                  ),
                ),
              ],
            ),
          ],
          if (child != null) ...[
            const SizedBox(height: 10),
            child!,
          ],
        ],
      ),
    );
  }
}

class PromoterSectionCard extends StatelessWidget {
  const PromoterSectionCard({
    super.key,
    required this.title,
    required this.child,
    this.trailing,
  });

  final String title;
  final Widget child;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        boxShadow: [
          BoxShadow(
            color: ErpColors.navy.withValues(alpha: 0.05),
            blurRadius: 18,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  title,
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                    color: ErpColors.navy,
                  ),
                ),
              ),
              if (trailing != null) trailing!,
            ],
          ),
          const SizedBox(height: 14),
          child,
        ],
      ),
    );
  }
}

class PromoterRevenueChart extends StatelessWidget {
  const PromoterRevenueChart({
    super.key,
    required this.points,
    required this.currency,
    required this.granularity,
    required this.onGranularityChanged,
  });

  final List<RevenuePoint> points;
  final String currency;
  final RevenueGranularity granularity;
  final ValueChanged<RevenueGranularity> onGranularityChanged;

  @override
  Widget build(BuildContext context) {
    final maxY = points.fold<double>(0, (m, p) => p.amount > m ? p.amount : m);
    final chartMax = maxY <= 0 ? 100.0 : maxY * 1.2;

    return PromoterSectionCard(
      title: 'Évolution des recettes',
      trailing: DropdownButtonHideUnderline(
        child: DropdownButton<RevenueGranularity>(
          value: granularity,
          items: RevenueGranularity.values
              .map((g) => DropdownMenuItem(value: g, child: Text(g.label, style: const TextStyle(fontSize: 12))))
              .toList(),
          onChanged: (v) {
            if (v != null) onGranularityChanged(v);
          },
        ),
      ),
      child: SizedBox(
        height: 220,
        child: points.isEmpty
            ? const Center(child: Text('Aucune donnée sur la période.'))
            : LineChart(
                LineChartData(
                  minY: 0,
                  maxY: chartMax,
                  gridData: FlGridData(
                    show: true,
                    drawVerticalLine: false,
                    getDrawingHorizontalLine: (v) => FlLine(
                      color: ErpColors.border.withValues(alpha: 0.8),
                      strokeWidth: 1,
                    ),
                  ),
                  borderData: FlBorderData(show: false),
                  titlesData: FlTitlesData(
                    topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                    rightTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                    leftTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true,
                        reservedSize: 42,
                        getTitlesWidget: (value, meta) => Text(
                          value >= 1000 ? '${(value / 1000).toStringAsFixed(0)}k' : value.toStringAsFixed(0),
                          style: const TextStyle(fontSize: 10, color: ErpColors.textSecondary),
                        ),
                      ),
                    ),
                    bottomTitles: AxisTitles(
                      sideTitles: SideTitles(
                        showTitles: true,
                        interval: points.length <= 8 ? 1 : (points.length / 5).ceilToDouble(),
                        getTitlesWidget: (value, meta) {
                          final i = value.toInt();
                          if (i < 0 || i >= points.length) return const SizedBox.shrink();
                          return Padding(
                            padding: const EdgeInsets.only(top: 6),
                            child: Text(points[i].label, style: const TextStyle(fontSize: 10, color: ErpColors.textSecondary)),
                          );
                        },
                      ),
                    ),
                  ),
                  lineTouchData: LineTouchData(
                    touchTooltipData: LineTouchTooltipData(
                      getTooltipItems: (spots) => spots
                          .map(
                            (s) => LineTooltipItem(
                              formatMoney(s.y, currency),
                              const TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 12),
                            ),
                          )
                          .toList(),
                    ),
                  ),
                  lineBarsData: [
                    LineChartBarData(
                      spots: [
                        for (var i = 0; i < points.length; i++) FlSpot(i.toDouble(), points[i].amount),
                      ],
                      isCurved: true,
                      color: ErpColors.primary,
                      barWidth: 3,
                      dotData: const FlDotData(show: false),
                      belowBarData: BarAreaData(
                        show: true,
                        color: ErpColors.primary.withValues(alpha: 0.15),
                      ),
                    ),
                  ],
                ),
                duration: const Duration(milliseconds: 650),
              ),
      ),
    );
  }
}

class PromoterDonutChart extends StatefulWidget {
  const PromoterDonutChart({
    super.key,
    required this.shares,
    required this.currency,
  });

  final List<NamedAmountShare> shares;
  final String currency;

  @override
  State<PromoterDonutChart> createState() => _PromoterDonutChartState();
}

class _PromoterDonutChartState extends State<PromoterDonutChart> {
  int? _touched;

  @override
  Widget build(BuildContext context) {
    final shares = widget.shares;
    final selected = _touched != null && _touched! < shares.length ? shares[_touched!] : null;

    return PromoterSectionCard(
      title: 'Répartition des recettes',
      child: Column(
        children: [
          SizedBox(
            height: 200,
            child: shares.isEmpty
                ? const Center(child: Text('Aucune répartition disponible.'))
                : Stack(
                    alignment: Alignment.center,
                    children: [
                      PieChart(
                        PieChartData(
                          sectionsSpace: 2,
                          centerSpaceRadius: 52,
                          pieTouchData: PieTouchData(
                            touchCallback: (event, response) {
                              setState(() {
                                if (!event.isInterestedForInteractions ||
                                    response?.touchedSection == null) {
                                  _touched = null;
                                  return;
                                }
                                _touched = response!.touchedSection!.touchedSectionIndex;
                              });
                            },
                          ),
                          sections: [
                            for (var i = 0; i < shares.length; i++)
                              PieChartSectionData(
                                color: parseHexColor(shares[i].colorHex),
                                value: shares[i].amount <= 0 ? 0.01 : shares[i].amount,
                                title: '${shares[i].percentage.toStringAsFixed(0)}%',
                                radius: _touched == i ? 58 : 48,
                                titleStyle: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 11,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                          ],
                        ),
                        duration: const Duration(milliseconds: 500),
                      ),
                      if (selected != null)
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 48),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                selected.name,
                                textAlign: TextAlign.center,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600),
                              ),
                              Text(
                                formatMoney(selected.amount, widget.currency),
                                style: const TextStyle(
                                  fontSize: 13,
                                  fontWeight: FontWeight.w800,
                                  color: ErpColors.navy,
                                ),
                              ),
                            ],
                          ),
                        ),
                    ],
                  ),
          ),
          const SizedBox(height: 8),
          ...shares.take(6).map(
                (s) => Padding(
                  padding: const EdgeInsets.symmetric(vertical: 4),
                  child: Row(
                    children: [
                      Container(
                        width: 10,
                        height: 10,
                        decoration: BoxDecoration(
                          color: parseHexColor(s.colorHex),
                          shape: BoxShape.circle,
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(child: Text(s.name, style: const TextStyle(fontSize: 12))),
                      Text(
                        '${s.percentage.toStringAsFixed(0)} %',
                        style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
                      ),
                    ],
                  ),
                ),
              ),
        ],
      ),
    );
  }
}

class PromoterFundAllocationList extends StatelessWidget {
  const PromoterFundAllocationList({
    super.key,
    required this.items,
    required this.currency,
    this.onSeeAll,
  });

  final List<FundAllocationShare> items;
  final String currency;
  final VoidCallback? onSeeAll;

  @override
  Widget build(BuildContext context) {
    return PromoterSectionCard(
      title: 'Répartition automatique des fonds',
      trailing: TextButton(
        onPressed: onSeeAll,
        child: const Text('Voir toutes', style: TextStyle(fontSize: 12)),
      ),
      child: items.isEmpty
          ? const Text('Aucune répartition enregistrée sur la période.')
          : Column(
              children: [
                for (final item in items) ...[
                  Row(
                    children: [
                      Expanded(
                        child: Text(item.name, style: const TextStyle(fontWeight: FontWeight.w700)),
                      ),
                      Text('${item.percentage.toStringAsFixed(0)} %'),
                    ],
                  ),
                  const SizedBox(height: 4),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(999),
                    child: LinearProgressIndicator(
                      value: (item.percentage / 100).clamp(0, 1),
                      minHeight: 8,
                      backgroundColor: ErpColors.border,
                      color: ErpColors.primary,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Align(
                    alignment: Alignment.centerRight,
                    child: Text(
                      formatMoney(item.amount, currency),
                      style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                    ),
                  ),
                  const SizedBox(height: 12),
                ],
              ],
            ),
    );
  }
}

class PromoterActivitiesList extends StatelessWidget {
  const PromoterActivitiesList({
    super.key,
    required this.activities,
    required this.currency,
  });

  final List<DashboardActivity> activities;
  final String currency;

  @override
  Widget build(BuildContext context) {
    return PromoterSectionCard(
      title: 'Activités récentes',
      child: activities.isEmpty
          ? const Text('Aucune activité récente.')
          : Column(
              children: [
                for (final a in activities)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Row(
                      children: [
                        Container(
                          width: 40,
                          height: 40,
                          decoration: BoxDecoration(
                            color: (a.kind == 'Payment' ? ErpColors.success : ErpColors.primary)
                                .withValues(alpha: 0.12),
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Icon(
                            a.kind == 'Payment' ? Icons.payments_rounded : Icons.person_add_alt_1_rounded,
                            color: a.kind == 'Payment' ? ErpColors.success : ErpColors.primary,
                            size: 20,
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(a.title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
                              Text(a.subtitle, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
                            ],
                          ),
                        ),
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.end,
                          children: [
                            Text(formatTime(a.occurredAtUtc), style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
                            if (a.amount != null)
                              Text(
                                formatMoney(a.amount!, a.currency ?? currency),
                                style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 12),
                              ),
                          ],
                        ),
                      ],
                    ),
                  ),
              ],
            ),
    );
  }
}

class PromoterAlertsList extends StatelessWidget {
  const PromoterAlertsList({super.key, required this.alerts});

  final List<DashboardAlert> alerts;

  @override
  Widget build(BuildContext context) {
    return PromoterSectionCard(
      title: 'Alertes',
      child: Column(
        children: [
          for (final alert in alerts)
            Container(
              width: double.infinity,
              margin: const EdgeInsets.only(bottom: 8),
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: alertColor(alert.severity).withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: alertColor(alert.severity).withValues(alpha: 0.25)),
              ),
              child: Row(
                children: [
                  Icon(Icons.notifications_active_rounded, color: alertColor(alert.severity), size: 18),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      alert.message,
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                        color: alertColor(alert.severity),
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class PromoterTopClassesChart extends StatelessWidget {
  const PromoterTopClassesChart({
    super.key,
    required this.items,
    required this.currency,
  });

  final List<ClassRevenueRank> items;
  final String currency;

  @override
  Widget build(BuildContext context) {
    final max = items.fold<double>(0, (m, e) => e.amount > m ? e.amount : m);

    return PromoterSectionCard(
      title: 'Classement des classes',
      child: items.isEmpty
          ? const Text('Aucune recette par classe.')
          : Column(
              children: [
                for (final item in items) ...[
                  Row(
                    children: [
                      SizedBox(
                        width: 22,
                        child: Text('${item.rank}.', style: const TextStyle(fontWeight: FontWeight.w800)),
                      ),
                      Expanded(child: Text(item.className, style: const TextStyle(fontWeight: FontWeight.w600))),
                      Text(formatMoney(item.amount, currency), style: const TextStyle(fontWeight: FontWeight.w700)),
                    ],
                  ),
                  const SizedBox(height: 6),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(999),
                    child: LinearProgressIndicator(
                      value: max <= 0 ? 0 : (item.amount / max).clamp(0, 1),
                      minHeight: 10,
                      backgroundColor: ErpColors.border,
                      color: ErpColors.navy,
                    ),
                  ),
                  const SizedBox(height: 12),
                ],
              ],
            ),
    );
  }
}

class PromoterTopFeeTypes extends StatelessWidget {
  const PromoterTopFeeTypes({super.key, required this.items});

  final List<NamedAmountShare> items;

  @override
  Widget build(BuildContext context) {
    return PromoterSectionCard(
      title: 'Top des types de frais',
      child: items.isEmpty
          ? const Text('Aucun type de frais encaissé.')
          : Column(
              children: [
                for (final item in items)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 6),
                    child: Row(
                      children: [
                        Expanded(child: Text(item.name)),
                        Text(
                          '${item.percentage.toStringAsFixed(0)} %',
                          style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.navy),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
    );
  }
}

class PromoterQuickStatsGrid extends StatelessWidget {
  const PromoterQuickStatsGrid({
    super.key,
    required this.stats,
    required this.currency,
  });

  final PromoterQuickStats stats;
  final String currency;

  @override
  Widget build(BuildContext context) {
    final tiles = [
      ('Présents', '${stats.presentStudents}', Icons.check_circle_outline),
      ('Absents', '${stats.absentStudents}', Icons.cancel_outlined),
      ('Paiements du jour', '${stats.paymentsToday}', Icons.receipt_long_outlined),
      ('Reçus imprimés', '${stats.receiptsPrinted}', Icons.print_outlined),
      ('Reste à percevoir', formatMoney(stats.remainingToCollect, currency), Icons.hourglass_bottom_rounded),
      ('Total réparti', formatMoney(stats.totalAllocated, currency), Icons.pie_chart_outline),
    ];

    return PromoterSectionCard(
      title: 'Informations rapides',
      child: GridView.builder(
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        itemCount: tiles.length,
        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 2,
          mainAxisSpacing: 10,
          crossAxisSpacing: 10,
          childAspectRatio: 1.55,
        ),
        itemBuilder: (context, index) {
          final t = tiles[index];
          return Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: ErpColors.pageBackground,
              borderRadius: BorderRadius.circular(14),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(t.$3, size: 18, color: ErpColors.primary),
                const Spacer(),
                Text(t.$2, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 14, color: ErpColors.navy)),
                Text(t.$1, style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
              ],
            ),
          );
        },
      ),
    );
  }
}

class PromoterSkeleton extends StatelessWidget {
  const PromoterSkeleton({super.key});

  @override
  Widget build(BuildContext context) {
    Widget box({double h = 90}) => Container(
          height: h,
          margin: const EdgeInsets.only(bottom: 12),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(18),
          ),
          child: const Center(
            child: SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
          ),
        );

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        box(h: 120),
        box(h: 48),
        Row(children: [Expanded(child: box()), const SizedBox(width: 10), Expanded(child: box())]),
        Row(children: [Expanded(child: box()), const SizedBox(width: 10), Expanded(child: box())]),
        box(h: 240),
        box(h: 260),
      ],
    );
  }
}
