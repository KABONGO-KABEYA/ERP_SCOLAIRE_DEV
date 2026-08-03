import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/erp_theme.dart';
import '../models/parent_models.dart';

/// Timeline des tranches — sans barre de progression (déjà sur le résumé).
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
            'TRANCHES — ${feeType.feeTypeName}',
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: 14,
              color: ErpColors.navy,
            ),
          ),
          const SizedBox(height: 14),
          for (final line in feeType.installments) ...[
            _TimelineStep(
              done: line.remaining <= 0 && line.amountExpected > 0,
              partial: line.amountPaid > 0 && line.remaining > 0,
              title: line.installmentName,
              paid: '${fmt.format(line.amountPaid)} ${feeType.currencyLabel}',
              due: '${fmt.format(line.amountExpected)} ${feeType.currencyLabel}',
              remaining: line.remaining,
              currencyLabel: feeType.currencyLabel,
            ),
            if (line != feeType.installments.last)
              const Padding(
                padding: EdgeInsets.only(left: 11, top: 2, bottom: 2),
                child: SizedBox(
                  height: 12,
                  child: VerticalDivider(
                    width: 2,
                    thickness: 2,
                    color: ErpColors.border,
                  ),
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
    required this.paid,
    required this.due,
    required this.remaining,
    required this.currencyLabel,
  });

  final bool done;
  final bool partial;
  final String title;
  final String paid;
  final String due;
  final double remaining;
  final String currencyLabel;

  @override
  Widget build(BuildContext context) {
    final color = done
        ? ErpColors.success
        : (partial ? ErpColors.warning : ErpColors.textSecondary);
    final icon = done
        ? Icons.check_circle
        : (partial ? Icons.timelapse : Icons.radio_button_unchecked);
    final fmt = NumberFormat('#,##0.##');

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 20, color: color),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: TextStyle(
                  fontWeight: FontWeight.w600,
                  fontSize: 13,
                  decoration: done ? TextDecoration.lineThrough : null,
                  color: done ? ErpColors.textSecondary : ErpColors.textPrimary,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                '$paid / $due',
                style: const TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: ErpColors.textPrimary,
                ),
              ),
              if (remaining > 0)
                Text(
                  'Reste ${fmt.format(remaining)} $currencyLabel',
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: ErpColors.danger,
                  ),
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
        SizedBox(
          height: 38,
          child: TextField(
            onChanged: onQueryChanged,
            style: const TextStyle(fontSize: 13),
            decoration: InputDecoration(
              hintText: 'Rechercher (type, reçu, montant…)',
              prefixIcon: const Icon(Icons.search, size: 18),
              isDense: true,
              contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              filled: true,
              fillColor: Colors.white,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(ErpSpacing.inputRadius),
                borderSide: const BorderSide(color: ErpColors.border),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(ErpSpacing.inputRadius),
                borderSide: const BorderSide(color: ErpColors.border),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(ErpSpacing.inputRadius),
                borderSide: const BorderSide(color: ErpColors.primary, width: 1.5),
              ),
            ),
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
                  child: FilterChip(
                    label: Text(period.$2),
                    selected: selectedPeriod == period.$1,
                    showCheckmark: false,
                    onSelected: (_) => onPeriodChanged?.call(period.$1),
                    visualDensity: VisualDensity.compact,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(ErpSpacing.chipRadius),
                    ),
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
