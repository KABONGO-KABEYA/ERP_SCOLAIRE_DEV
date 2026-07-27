import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/erp_theme.dart';
import '../premium/dashboard_insights.dart';
import 'parent_ui_widgets.dart';

/// Cartes Dashboard V2 — design system ErpCard existant.
class ParentDashboardInsightCards extends StatelessWidget {
  const ParentDashboardInsightCards({
    super.key,
    this.grades,
    this.attendance,
    this.communications,
    this.nextDue,
    this.onOpenGrades,
    this.onOpenAttendance,
    this.onOpenCommunications,
    this.onOpenPayments,
  });

  final ParentGradesInsight? grades;
  final ParentAttendanceInsight? attendance;
  final ParentCommunicationsInsight? communications;
  final ParentNextDueInsight? nextDue;
  final VoidCallback? onOpenGrades;
  final VoidCallback? onOpenAttendance;
  final VoidCallback? onOpenCommunications;
  final VoidCallback? onOpenPayments;

  @override
  Widget build(BuildContext context) {
    final cards = <Widget>[
      if (grades != null && grades!.hasData)
        _InsightCard(
          title: 'Résultats scolaires',
          icon: Icons.school_outlined,
          onTap: onOpenGrades,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: _MiniStat(
                      label: 'Moyenne',
                      value: grades!.generalAverage.toStringAsFixed(1),
                    ),
                  ),
                  Expanded(
                    child: _MiniStat(
                      label: 'Rang',
                      value: grades!.rank > 0
                          ? '${grades!.rank}${grades!.classSize > 0 ? '/${grades!.classSize}' : ''}'
                          : '—',
                    ),
                  ),
                ],
              ),
              if (grades!.lastSubject != null) ...[
                const SizedBox(height: 10),
                Text(
                  'Dernière note : ${grades!.lastScore?.toStringAsFixed(1) ?? '—'}'
                  '${grades!.lastMaxScore != null ? '/${grades!.lastMaxScore!.toStringAsFixed(0)}' : ''}'
                  ' · ${grades!.lastSubject}'
                  '${grades!.lastLabel != null ? ' (${grades!.lastLabel})' : ''}',
                  style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                ),
              ],
            ],
          ),
        ),
      if (attendance != null)
        _InsightCard(
          title: 'Présences',
          icon: Icons.event_available_outlined,
          onTap: onOpenAttendance,
          child: Row(
            children: [
              Expanded(
                child: _MiniStat(
                  label: "Aujourd'hui",
                  value: attendance!.presentToday ? 'Présent' : '—',
                  valueColor: attendance!.presentToday ? ErpColors.success : null,
                ),
              ),
              Expanded(
                child: _MiniStat(
                  label: 'Retards / mois',
                  value: '${attendance!.lateThisMonth}',
                  valueColor: attendance!.lateThisMonth > 0 ? ErpColors.warning : null,
                ),
              ),
              Expanded(
                child: _MiniStat(
                  label: 'Absences / mois',
                  value: '${attendance!.absentThisMonth}',
                  valueColor: attendance!.absentThisMonth > 0 ? ErpColors.danger : null,
                ),
              ),
            ],
          ),
        ),
      if (communications != null)
        _InsightCard(
          title: 'Communications',
          icon: Icons.forum_outlined,
          onTap: onOpenCommunications,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${communications!.unreadCount} non lu${communications!.unreadCount > 1 ? 's' : ''}',
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  color: ErpColors.navy,
                ),
              ),
              if (communications!.lastTitle != null) ...[
                const SizedBox(height: 6),
                Text(
                  communications!.lastTitle!,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 13),
                ),
                if (communications!.lastDate != null)
                  Text(
                    DateFormat('dd/MM/yyyy').format(communications!.lastDate!),
                    style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                  ),
              ] else
                const Text(
                  'Aucun message pour le moment.',
                  style: TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                ),
            ],
          ),
        ),
      if (nextDue != null)
        _InsightCard(
          title: 'Échéances',
          icon: Icons.schedule_outlined,
          onTap: onOpenPayments,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                nextDue!.installmentName,
                style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.navy),
              ),
              const SizedBox(height: 4),
              Text(
                '${NumberFormat('#,##0.##').format(nextDue!.amount)} ${nextDue!.currencyLabel}'
                ' · ${nextDue!.feeTypeName}',
                style: const TextStyle(fontSize: 13),
              ),
              const SizedBox(height: 4),
              Text(
                nextDue!.daysRemaining <= 0
                    ? 'Échéance atteinte'
                    : 'Dans ${nextDue!.daysRemaining} jour${nextDue!.daysRemaining > 1 ? 's' : ''}',
                style: TextStyle(
                  fontSize: 12,
                  color: nextDue!.daysRemaining <= 7 ? ErpColors.danger : ErpColors.textSecondary,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
    ];

    if (cards.isEmpty) return const SizedBox.shrink();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const ParentSectionTitle('Suivi intelligent'),
        for (var i = 0; i < cards.length; i++) ...[
          cards[i],
          if (i < cards.length - 1) const SizedBox(height: 10),
        ],
      ],
    );
  }
}

class _InsightCard extends StatelessWidget {
  const _InsightCard({
    required this.title,
    required this.icon,
    required this.child,
    this.onTap,
  });

  final String title;
  final IconData icon;
  final Widget child;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      padding: const EdgeInsets.all(14),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, size: 18, color: ErpColors.primary),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                      color: ErpColors.navy,
                    ),
                  ),
                ),
                if (onTap != null)
                  const Icon(Icons.chevron_right, color: ErpColors.textSecondary),
              ],
            ),
            const SizedBox(height: 12),
            child,
          ],
        ),
      ),
    );
  }
}

class _MiniStat extends StatelessWidget {
  const _MiniStat({
    required this.label,
    required this.value,
    this.valueColor,
  });

  final String label;
  final String value;
  final Color? valueColor;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
        const SizedBox(height: 2),
        Text(
          value,
          style: TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w700,
            color: valueColor ?? ErpColors.textPrimary,
          ),
        ),
      ],
    );
  }
}
