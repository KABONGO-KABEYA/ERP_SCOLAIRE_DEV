import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/erp_theme.dart';
import '../models/parent_models.dart';

/// Timeline des tranches (Paiements Premium).
class ParentInstallmentTimeline extends StatelessWidget {
  const ParentInstallmentTimeline({super.key, required this.feeType});

  final ParentFeeTypeSituation feeType;

  @override
  Widget build(BuildContext context) {
    if (feeType.installments.isEmpty) return const SizedBox.shrink();
    final fmt = NumberFormat('#,##0.##');

    return ErpCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Progression — ${feeType.feeTypeName}',
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: 14,
              color: ErpColors.navy,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            '${(feeType.progress * 100).toStringAsFixed(0)} % payé',
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: ErpColors.primary,
            ),
          ),
          const SizedBox(height: 8),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: feeType.progress,
              minHeight: 10,
              backgroundColor: ErpColors.border,
              color: ErpColors.primary,
            ),
          ),
          const SizedBox(height: 16),
          for (final line in feeType.installments) ...[
            _TimelineStep(
              done: line.remaining <= 0 && line.amountExpected > 0,
              partial: line.amountPaid > 0 && line.remaining > 0,
              title: line.installmentName,
              subtitle:
                  '${fmt.format(line.amountPaid)} / ${fmt.format(line.amountExpected)} ${feeType.currencyLabel}',
            ),
            if (line != feeType.installments.last)
              const Padding(
                padding: EdgeInsets.only(left: 11),
                child: SizedBox(
                  height: 14,
                  child: VerticalDivider(width: 2, thickness: 2, color: ErpColors.border),
                ),
              ),
          ],
        ],
      ),
    );
  }
}

class _TimelineStep extends StatelessWidget {
  const _TimelineStep({
    required this.done,
    required this.partial,
    required this.title,
    required this.subtitle,
  });

  final bool done;
  final bool partial;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    final color = done
        ? ErpColors.success
        : (partial ? ErpColors.warning : ErpColors.textSecondary);
    final icon = done
        ? Icons.check_circle
        : (partial ? Icons.timelapse : Icons.radio_button_unchecked);

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 22, color: color),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: TextStyle(
                  fontWeight: FontWeight.w600,
                  decoration: done ? TextDecoration.lineThrough : null,
                  color: done ? ErpColors.textSecondary : ErpColors.textPrimary,
                ),
              ),
              Text(
                subtitle,
                style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Barre de recherche / filtres historique paiements.
class ParentPaymentsSearchBar extends StatelessWidget {
  const ParentPaymentsSearchBar({
    super.key,
    required this.query,
    required this.onQueryChanged,
    this.selectedPeriod,
    this.onPeriodChanged,
  });

  final String query;
  final ValueChanged<String> onQueryChanged;
  final String? selectedPeriod;
  final ValueChanged<String?>? onPeriodChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextField(
          onChanged: onQueryChanged,
          decoration: InputDecoration(
            hintText: 'Rechercher (type, reçu, montant…)',
            prefixIcon: const Icon(Icons.search),
            filled: true,
            fillColor: Colors.white,
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: ErpColors.border),
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: ErpColors.border),
            ),
            contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          ),
        ),
        const SizedBox(height: 10),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              for (final period in const [
                (null, 'Toutes périodes'),
                ('30', '30 jours'),
                ('90', '90 jours'),
                ('365', 'Année'),
              ])
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: ChoiceChip(
                    label: Text(period.$2),
                    selected: selectedPeriod == period.$1,
                    onSelected: (_) => onPeriodChanged?.call(period.$1),
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }
}

List<ParentPayment> filterParentPayments({
  required List<ParentPayment> payments,
  String? feeTypeId,
  String query = '',
  String? periodDays,
}) {
  var list = payments;
  if (feeTypeId != null && feeTypeId.isNotEmpty) {
    list = list.where((p) => p.feeTypeId == feeTypeId).toList();
  }
  final q = query.trim().toLowerCase();
  if (q.isNotEmpty) {
    list = list.where((p) {
      final hay = [
        p.feeLabel,
        p.receiptNumber,
        p.totalAmount.toStringAsFixed(2),
        p.totalAmount.toStringAsFixed(0),
        p.statusLabel,
      ].join(' ').toLowerCase();
      return hay.contains(q);
    }).toList();
  }
  if (periodDays != null) {
    final days = int.tryParse(periodDays);
    if (days != null) {
      final from = DateTime.now().subtract(Duration(days: days));
      list = list.where((p) => !p.paymentDate.isBefore(from)).toList();
    }
  }
  return list;
}
