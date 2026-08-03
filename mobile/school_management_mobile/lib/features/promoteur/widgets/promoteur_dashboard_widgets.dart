import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';

import '../../../core/theme/erp_theme.dart';
import '../dashboard_formatters.dart';
import '../models/promoteur_dashboard_models.dart';

class PilotSectionTitle extends StatelessWidget {
  const PilotSectionTitle(this.title, {super.key, this.subtitle});

  final String title;
  final String? subtitle;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12, top: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: ErpColors.navy,
              letterSpacing: -0.2,
            ),
          ),
          if (subtitle != null) ...[
            const SizedBox(height: 2),
            Text(subtitle!, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
          ],
        ],
      ),
    );
  }
}

class PilotCard extends StatelessWidget {
  const PilotCard({super.key, required this.child, this.onTap, this.padding});

  final Widget child;
  final VoidCallback? onTap;
  final EdgeInsets? padding;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      onTap: onTap,
      padding: padding ?? const EdgeInsets.all(14),
      child: child,
    );
  }
}

class KpiMoneyCard extends StatelessWidget {
  const KpiMoneyCard({
    super.key,
    required this.icon,
    required this.label,
    required this.amount,
    required this.currency,
    required this.changePercent,
    required this.comparisonLabel,
    required this.accent,
    this.onTap,
  });

  final IconData icon;
  final String label;
  final double amount;
  final String currency;
  final double changePercent;
  final String comparisonLabel;
  final Color accent;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final up = changePercent >= 0;
    return PilotCard(
      onTap: onTap,
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
              const Spacer(),
              Icon(Icons.chevron_right_rounded, size: 18, color: ErpColors.textSecondary.withValues(alpha: 0.6)),
            ],
          ),
          const SizedBox(height: 10),
          Text(label, style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary, fontWeight: FontWeight.w600)),
          const SizedBox(height: 4),
          Text(
            formatMoney(amount, currency),
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w600,
              letterSpacing: -0.3,
              color: ErpColors.navy,
              height: 1.15,
            ),
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              Icon(
                up ? Icons.trending_up_rounded : Icons.trending_down_rounded,
                size: 14,
                color: up ? ErpColors.success : ErpColors.danger,
              ),
              const SizedBox(width: 4),
              Expanded(
                child: Text(
                  '${formatPercent(changePercent)} $comparisonLabel',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w600,
                    color: up ? ErpColors.success : ErpColors.danger,
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class KpiStudentsCard extends StatelessWidget {
  const KpiStudentsCard({
    super.key,
    required this.students,
    this.onTap,
  });

  final PromoterStudentsKpi students;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return PilotCard(
      onTap: onTap,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 34,
                height: 34,
                decoration: BoxDecoration(
                  color: ErpColors.primary.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: const Icon(Icons.school_rounded, size: 18, color: ErpColors.primary),
              ),
              const Spacer(),
              Icon(Icons.chevron_right_rounded, size: 18, color: ErpColors.textSecondary.withValues(alpha: 0.6)),
            ],
          ),
          const SizedBox(height: 10),
          const Text('Élèves inscrits', style: TextStyle(fontSize: 11, color: ErpColors.textSecondary, fontWeight: FontWeight.w600)),
          const SizedBox(height: 4),
          Text(
            '${students.total}',
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w600,
              letterSpacing: -0.3,
              color: ErpColors.navy,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            '♂ ${students.boys}  ·  ♀ ${students.girls}',
            style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary, fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

class RevenueLineChartCard extends StatelessWidget {
  const RevenueLineChartCard({
    super.key,
    required this.title,
    required this.points,
    required this.currency,
    this.color = ErpColors.primary,
  });

  final String title;
  final List<RevenuePoint> points;
  final String currency;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final maxY = points.fold<double>(0, (m, p) => p.amount > m ? p.amount : m);
    final spots = <FlSpot>[];
    for (var i = 0; i < points.length; i++) {
      spots.add(FlSpot(i.toDouble(), points[i].amount));
    }

    return PilotCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: ErpColors.navy)),
          const SizedBox(height: 12),
          SizedBox(
            height: 160,
            child: spots.isEmpty
                ? const Center(child: Text('Aucune donnée', style: TextStyle(color: ErpColors.textSecondary)))
                : LineChart(
                    LineChartData(
                      minY: 0,
                      maxY: maxY <= 0 ? 1 : maxY * 1.15,
                      gridData: FlGridData(
                        show: true,
                        drawVerticalLine: false,
                        getDrawingHorizontalLine: (_) => FlLine(
                          color: ErpColors.border.withValues(alpha: 0.8),
                          strokeWidth: 1,
                        ),
                      ),
                      borderData: FlBorderData(show: false),
                      titlesData: FlTitlesData(
                        topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                        rightTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                        leftTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                        bottomTitles: AxisTitles(
                          sideTitles: SideTitles(
                            showTitles: true,
                            interval: (points.length / 4).clamp(1, 10).toDouble(),
                            getTitlesWidget: (value, meta) {
                              final i = value.round();
                              if (i < 0 || i >= points.length) return const SizedBox.shrink();
                              return Padding(
                                padding: const EdgeInsets.only(top: 6),
                                child: Text(points[i].label, style: const TextStyle(fontSize: 9, color: ErpColors.textSecondary)),
                              );
                            },
                          ),
                        ),
                      ),
                      lineTouchData: LineTouchData(
                        touchTooltipData: LineTouchTooltipData(
                          getTooltipItems: (touched) => touched
                              .map(
                                (t) => LineTooltipItem(
                                  formatMoney(t.y, currency),
                                  const TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 11),
                                ),
                              )
                              .toList(),
                        ),
                      ),
                      lineBarsData: [
                        LineChartBarData(
                          spots: spots,
                          isCurved: true,
                          color: color,
                          barWidth: 2.5,
                          dotData: const FlDotData(show: false),
                          belowBarData: BarAreaData(
                            show: true,
                            color: color.withValues(alpha: 0.12),
                          ),
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

class ExpenseSummaryCard extends StatelessWidget {
  const ExpenseSummaryCard({
    super.key,
    required this.expenses,
    required this.currency,
    required this.onOpenScope,
    required this.onCategoryTap,
  });

  final PromoterExpensesBoard expenses;
  final String currency;
  final void Function(String scope) onOpenScope;
  final void Function(NamedAmountShare category) onCategoryTap;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Row(
          children: [
            Expanded(
              child: _MiniMetric(
                label: "Aujourd'hui",
                value: formatMoney(expenses.today, currency),
                onTap: () => onOpenScope('Today'),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _MiniMetric(
                label: 'Ce mois',
                value: formatMoney(expenses.month, currency),
                onTap: () => onOpenScope('Month'),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _MiniMetric(
                label: 'Année',
                value: formatMoney(expenses.year, currency),
                onTap: () => onOpenScope('Year'),
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        PilotCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Répartition par catégorie', style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: ErpColors.navy)),
              const SizedBox(height: 10),
              if (expenses.byCategory.isEmpty)
                const Text('Aucune dépense enregistrée', style: TextStyle(color: ErpColors.textSecondary, fontSize: 12))
              else
                ...expenses.byCategory.map((c) {
                  final color = parseHexColor(c.colorHex);
                  return Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: InkWell(
                      onTap: () => onCategoryTap(c),
                      borderRadius: BorderRadius.circular(10),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: Text(c.name, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12)),
                              ),
                              Text(formatMoney(c.amount, currency), style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 12)),
                            ],
                          ),
                          const SizedBox(height: 4),
                          ClipRRect(
                            borderRadius: BorderRadius.circular(4),
                            child: LinearProgressIndicator(
                              value: (c.percentage / 100).clamp(0, 1),
                              minHeight: 6,
                              backgroundColor: color.withValues(alpha: 0.12),
                              color: color,
                            ),
                          ),
                        ],
                      ),
                    ),
                  );
                }),
            ],
          ),
        ),
      ],
    );
  }
}

class FundAllocationList extends StatelessWidget {
  const FundAllocationList({
    super.key,
    required this.funds,
    required this.currency,
    required this.onTap,
  });

  final List<FundAllocationShare> funds;
  final String currency;
  final void Function(FundAllocationShare fund) onTap;

  @override
  Widget build(BuildContext context) {
    if (funds.isEmpty) {
      return const PilotCard(
        child: Text(
          'Aucun compte lié à ce frais (configurez la répartition ou encaissez).',
          style: TextStyle(color: ErpColors.textSecondary),
        ),
      );
    }

    return Column(
      children: [
        const PilotCard(
          padding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          child: Text(
            'J-1 = solde avant aujourd’hui · J = encaissement du jour · Dépense = sorties du jour',
            style: TextStyle(fontSize: 11, color: ErpColors.textSecondary, height: 1.35),
          ),
        ),
        const SizedBox(height: 10),
        ...funds.map((f) {
          final color = parseHexColor(f.colorHex);
          return Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: PilotCard(
              onTap: () => onTap(f),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        width: 8,
                        height: 8,
                        decoration: BoxDecoration(color: color, shape: BoxShape.circle),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          f.name,
                          style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: ErpColors.navy),
                        ),
                      ),
                      Text(
                        formatMoney(f.solde, currency),
                        style: TextStyle(
                          fontWeight: FontWeight.w800,
                          fontSize: 13,
                          color: f.solde >= 0 ? ErpColors.navy : ErpColors.danger,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(child: _CashFlowCell(label: 'J-1', value: formatMoney(f.periodJ1, currency))),
                      Expanded(child: _CashFlowCell(label: 'J', value: formatMoney(f.encaissementJ, currency), accent: ErpColors.success)),
                      Expanded(child: _CashFlowCell(label: 'Dépense', value: formatMoney(f.depenseJ, currency), accent: ErpColors.danger)),
                    ],
                  ),
                  if (f.percentage > 0) ...[
                    const SizedBox(height: 8),
                    Text(
                      '${f.percentage.toStringAsFixed(1)} % des encaissements du jour',
                      style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                    ),
                  ],
                ],
              ),
            ),
          );
        }),
      ],
    );
  }
}

class _CashFlowCell extends StatelessWidget {
  const _CashFlowCell({required this.label, required this.value, this.accent});

  final String label;
  final String value;
  final Color? accent;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 10, color: ErpColors.textSecondary, fontWeight: FontWeight.w600)),
        const SizedBox(height: 2),
        Text(
          value,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: accent ?? ErpColors.textPrimary,
          ),
        ),
      ],
    );
  }
}

class WithholdingsList extends StatelessWidget {
  const WithholdingsList({
    super.key,
    required this.items,
    required this.currency,
  });

  final List<PromoterWithholdingShare> items;
  final String currency;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) {
      return const PilotCard(
        child: Text('Aucune retenue sur ce frais.', style: TextStyle(color: ErpColors.textSecondary)),
      );
    }

    return Column(
      children: items.map((w) {
        return Padding(
          padding: const EdgeInsets.only(bottom: 10),
          child: PilotCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(w.name, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: ErpColors.navy)),
                const SizedBox(height: 10),
                Row(
                  children: [
                    Expanded(child: _CashFlowCell(label: 'Aujourd’hui', value: formatMoney(w.amountToday, currency))),
                    Expanded(child: _CashFlowCell(label: 'Mois', value: formatMoney(w.amountMonth, currency))),
                    Expanded(child: _CashFlowCell(label: 'Année', value: formatMoney(w.amountYear, currency), accent: ErpColors.primary)),
                  ],
                ),
              ],
            ),
          ),
        );
      }).toList(),
    );
  }
}

class SituationHeroCard extends StatelessWidget {
  const SituationHeroCard({
    super.key,
    required this.situation,
    required this.currency,
  });

  final PromoterSituation situation;
  final String currency;

  bool get _hasData =>
      situation.totalRevenue != 0 || situation.totalExpenses != 0 || situation.availableBalance != 0;

  @override
  Widget build(BuildContext context) {
    if (!_hasData) {
      return Container(
        width: double.infinity,
        padding: const EdgeInsets.all(18),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: ErpColors.border),
        ),
        child: const Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Situation financière',
              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: ErpColors.navy),
            ),
            SizedBox(height: 10),
            Text(
              'Aucune donnée financière disponible pour l’année scolaire.',
              style: TextStyle(color: ErpColors.textSecondary, fontSize: 13),
            ),
          ],
        ),
      );
    }

    final positive = situation.availableBalance >= 0;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(20),
        gradient: LinearGradient(
          colors: positive
              ? [ErpColors.navy, const Color(0xFF1D4ED8)]
              : [const Color(0xFF7F1D1D), ErpColors.danger],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        boxShadow: [
          BoxShadow(
            color: ErpColors.navy.withValues(alpha: 0.25),
            blurRadius: 20,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Situation financière',
            style: TextStyle(color: Colors.white70, fontWeight: FontWeight.w600, fontSize: 12),
          ),
          const SizedBox(height: 8),
          Text(
            formatMoney(situation.availableBalance, currency),
            style: const TextStyle(color: Colors.white, fontSize: 26, fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 4),
          const Text(
            'Solde disponible',
            style: TextStyle(color: Colors.white70, fontSize: 12),
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: _HeroStat(
                  label: 'Recettes annuelles',
                  value: formatMoney(situation.totalRevenue, currency),
                ),
              ),
              Expanded(
                child: _HeroStat(
                  label: 'Dépenses annuelles',
                  value: formatMoney(situation.totalExpenses, currency),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class ReceivablesGrid extends StatelessWidget {
  const ReceivablesGrid({
    super.key,
    required this.receivables,
    required this.currency,
    required this.onRemaining,
    required this.onDebtors,
    required this.onPaid,
    required this.onRecovery,
  });

  final PromoterReceivables receivables;
  final String currency;
  final VoidCallback onRemaining;
  final VoidCallback onDebtors;
  final VoidCallback onPaid;
  final VoidCallback onRecovery;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Row(
          children: [
            Expanded(
              child: _MiniMetric(
                label: 'À percevoir',
                value: formatMoney(receivables.remainingToCollect, currency),
                onTap: onRemaining,
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _MiniMetric(
                label: 'Débiteurs',
                value: '${receivables.debtorStudents}',
                onTap: onDebtors,
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: _MiniMetric(
                label: 'En ordre',
                value: '${receivables.fullyPaidStudents}',
                onTap: onPaid,
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _MiniMetric(
                label: 'Recouvrement',
                value: '${receivables.recoveryPercent.toStringAsFixed(1)} %',
                onTap: onRecovery,
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class AlertsList extends StatelessWidget {
  const AlertsList({
    super.key,
    required this.alerts,
    required this.onTap,
  });

  final List<DashboardAlert> alerts;
  final void Function(DashboardAlert alert) onTap;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: alerts.map((a) {
        final color = alertColor(a.severity);
        return Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: PilotCard(
            onTap: a.actionHint == null ? null : () => onTap(a),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  width: 36,
                  height: 36,
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: Icon(Icons.notifications_active_rounded, color: color, size: 18),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(a.title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13, color: ErpColors.navy)),
                      const SizedBox(height: 2),
                      Text(a.message, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
                    ],
                  ),
                ),
                if (a.actionHint != null)
                  Icon(Icons.chevron_right_rounded, color: ErpColors.textSecondary.withValues(alpha: 0.5)),
              ],
            ),
          ),
        );
      }).toList(),
    );
  }
}

class PromoterSkeleton extends StatelessWidget {
  const PromoterSkeleton({super.key});

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: List.generate(
        6,
        (i) => Container(
          height: i == 0 ? 100 : 80,
          margin: const EdgeInsets.only(bottom: 12),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
          ),
        ),
      ),
    );
  }
}

class _MiniMetric extends StatelessWidget {
  const _MiniMetric({required this.label, required this.value, this.onTap});

  final String label;
  final String value;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return PilotCard(
      onTap: onTap,
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary, fontWeight: FontWeight.w600)),
          const SizedBox(height: 6),
          Text(value, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w800, color: ErpColors.navy)),
        ],
      ),
    );
  }
}

class _HeroStat extends StatelessWidget {
  const _HeroStat({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(color: Colors.white60, fontSize: 11)),
        const SizedBox(height: 2),
        Text(value, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 13)),
      ],
    );
  }
}
