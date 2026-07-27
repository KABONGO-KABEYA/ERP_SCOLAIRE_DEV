import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/erp_theme.dart';
import '../models/parent_models.dart';

class ParentSectionTitle extends StatelessWidget {
  const ParentSectionTitle(this.title, {super.key, this.action});

  final String title;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12, top: 4),
      child: Row(
        children: [
          Expanded(
            child: Text(
              title,
              style: const TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.w700,
                color: ErpColors.navy,
              ),
            ),
          ),
          if (action != null) action!,
        ],
      ),
    );
  }
}

class ParentChildSelector extends StatelessWidget {
  const ParentChildSelector({
    super.key,
    required this.children,
    required this.selectedId,
    required this.onChanged,
  });

  final List<ParentChild> children;
  final String? selectedId;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    if (children.length <= 1) return const SizedBox.shrink();

    return SizedBox(
      height: 44,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: children.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final child = children[index];
          final selected = child.studentId == selectedId;
          return ChoiceChip(
            selected: selected,
            label: Text(child.fullName.split(' ').first),
            onSelected: (_) => onChanged(child.studentId),
            selectedColor: ErpColors.primary.withValues(alpha: 0.15),
            labelStyle: TextStyle(
              color: selected ? ErpColors.primary : ErpColors.textPrimary,
              fontWeight: FontWeight.w600,
              fontSize: 13,
            ),
            side: BorderSide(
              color: selected ? ErpColors.primary : ErpColors.border,
            ),
            backgroundColor: Colors.white,
          );
        },
      ),
    );
  }
}

class ParentHeaderCard extends StatelessWidget {
  const ParentHeaderCard({
    super.key,
    required this.parentName,
    required this.schoolName,
    required this.child,
    this.photoBytes,
  });

  final String parentName;
  final String schoolName;
  final ParentChild? child;
  final Uint8List? photoBytes;

  @override
  Widget build(BuildContext context) {
    final initials = _initials(child?.fullName ?? parentName);
    return ErpCard(
      padding: const EdgeInsets.all(16),
      child: Row(
        children: [
          CircleAvatar(
            radius: 30,
            backgroundColor: ErpColors.primary.withValues(alpha: 0.12),
            backgroundImage:
                photoBytes != null ? MemoryImage(photoBytes!) : null,
            child: photoBytes == null
                ? Text(
                    initials,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 18,
                      color: ErpColors.primary,
                    ),
                  )
                : null,
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  parentName,
                  style: const TextStyle(
                    fontSize: 17,
                    fontWeight: FontWeight.w700,
                    color: ErpColors.navy,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  schoolName,
                  style: const TextStyle(fontSize: 13, color: ErpColors.textSecondary),
                ),
                if (child != null) ...[
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 6,
                    children: [
                      _MetaChip(icon: Icons.school_outlined, label: child!.fullName),
                      if (child!.className != null)
                        _MetaChip(icon: Icons.class_outlined, label: child!.className!),
                      _MetaChip(
                        icon: Icons.badge_outlined,
                        label: child!.registrationNumber,
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+'));
    if (parts.isEmpty || parts.first.isEmpty) return '?';
    if (parts.length == 1) return parts.first[0].toUpperCase();
    return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
  }
}

class _MetaChip extends StatelessWidget {
  const _MetaChip({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: ErpColors.pageBackground,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: ErpColors.textSecondary),
          const SizedBox(width: 4),
          Text(
            label,
            style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

class ParentPaymentSummaryCard extends StatelessWidget {
  const ParentPaymentSummaryCard({
    super.key,
    required this.summary,
    this.title = 'Situation des paiements',
    this.subtitle,
  });

  final ParentPaymentSummary summary;
  final String title;
  final String? subtitle;

  @override
  Widget build(BuildContext context) {
    final fmt = NumberFormat('#,##0.##');
    return ErpCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              fontSize: 15,
              fontWeight: FontWeight.w700,
              color: ErpColors.navy,
            ),
          ),
          if (subtitle != null && subtitle!.trim().isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(
              subtitle!,
              style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
            ),
          ],
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: _MoneyStat(
                  label: 'Total à payer',
                  value: '${fmt.format(summary.totalDue)} ${summary.currencyLabel}',
                ),
              ),
              Expanded(
                child: _MoneyStat(
                  label: 'Total payé',
                  value: '${fmt.format(summary.totalPaid)} ${summary.currencyLabel}',
                  valueColor: ErpColors.success,
                ),
              ),
              Expanded(
                child: _MoneyStat(
                  label: 'Reste à payer',
                  value: '${fmt.format(summary.balance)} ${summary.currencyLabel}',
                  valueColor: summary.balance > 0 ? ErpColors.danger : ErpColors.success,
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: summary.progress,
              minHeight: 10,
              backgroundColor: ErpColors.border,
              color: ErpColors.primary,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            '${(summary.progress * 100).toStringAsFixed(0)} % payé',
            style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
          ),
        ],
      ),
    );
  }
}

class ParentFeeTypeChips extends StatelessWidget {
  const ParentFeeTypeChips({
    super.key,
    required this.feeTypes,
    required this.selectedFeeTypeId,
    required this.onChanged,
    this.showAll = true,
  });

  final List<ParentFeeTypeSituation> feeTypes;
  final String? selectedFeeTypeId;
  final ValueChanged<String?> onChanged;
  final bool showAll;

  @override
  Widget build(BuildContext context) {
    if (feeTypes.isEmpty) return const SizedBox.shrink();
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          if (showAll) ...[
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: ChoiceChip(
                label: const Text('Tous'),
                selected: selectedFeeTypeId == null,
                onSelected: (_) => onChanged(null),
              ),
            ),
          ],
          for (final fee in feeTypes)
            Padding(
              padding: const EdgeInsets.only(right: 8),
              child: ChoiceChip(
                label: Text(fee.feeTypeName),
                selected: selectedFeeTypeId == fee.feeTypeId,
                onSelected: (_) => onChanged(fee.feeTypeId),
              ),
            ),
        ],
      ),
    );
  }
}

class ParentFeeInstallmentsCard extends StatelessWidget {
  const ParentFeeInstallmentsCard({super.key, required this.feeType});

  final ParentFeeTypeSituation feeType;

  @override
  Widget build(BuildContext context) {
    final fmt = NumberFormat('#,##0.##');
    if (feeType.installments.isEmpty) {
      return const ErpCard(
        child: Text('Aucune tranche applicable pour ce type de frais.'),
      );
    }

    return ErpCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Expanded(
                child: Text(
                  'Détail par tranche',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w700,
                    color: ErpColors.navy,
                  ),
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: feeType.isInOrder
                      ? ErpColors.success.withValues(alpha: 0.12)
                      : ErpColors.danger.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(
                  feeType.isInOrder ? 'En ordre' : 'Non en ordre',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.w700,
                    color: feeType.isInOrder ? ErpColors.success : ErpColors.danger,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          for (final line in feeType.installments) ...[
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 8),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${line.number}. ${line.installmentName}',
                    style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          'Dû ${fmt.format(line.amountExpected)} ${feeType.currencyLabel}',
                          style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                        ),
                      ),
                      Expanded(
                        child: Text(
                          'Payé ${fmt.format(line.amountPaid)} ${feeType.currencyLabel}',
                          style: const TextStyle(fontSize: 12, color: ErpColors.success),
                        ),
                      ),
                      Expanded(
                        child: Text(
                          'Reste ${fmt.format(line.remaining)} ${feeType.currencyLabel}',
                          textAlign: TextAlign.end,
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w700,
                            color: line.remaining > 0 ? ErpColors.danger : ErpColors.success,
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
            if (line != feeType.installments.last) const Divider(height: 1),
          ],
        ],
      ),
    );
  }
}

class _MoneyStat extends StatelessWidget {
  const _MoneyStat({
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
        const SizedBox(height: 4),
        Text(
          value,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w700,
            color: valueColor ?? ErpColors.textPrimary,
          ),
        ),
      ],
    );
  }
}

class ParentPaymentTile extends StatelessWidget {
  const ParentPaymentTile({
    super.key,
    required this.payment,
    this.onViewReceipt,
    this.onDownloadPdf,
  });

  final ParentPayment payment;
  final VoidCallback? onViewReceipt;
  final VoidCallback? onDownloadPdf;

  @override
  Widget build(BuildContext context) {
    final date = DateFormat('dd/MM/yyyy').format(payment.paymentDate.toLocal());
    return ErpCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  payment.feeLabel,
                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
                ),
              ),
              Text(
                '${payment.totalAmount.toStringAsFixed(2)} ${payment.currencyLabel}',
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  color: ErpColors.success,
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            '$date  ·  ${payment.receiptNumber}',
            style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: onViewReceipt,
                  icon: const Icon(Icons.receipt_long_outlined, size: 18),
                  label: const Text('Reçu'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: onDownloadPdf,
                  icon: const Icon(Icons.picture_as_pdf_outlined, size: 18),
                  label: const Text('PDF'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class ParentUnlockBanner extends StatelessWidget {
  const ParentUnlockBanner({super.key, required this.onActivate});

  final VoidCallback onActivate;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(18),
        color: ErpColors.navy,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.workspace_premium, color: Colors.amber),
              SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Débloquez toutes les fonctionnalités',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            'Accédez aux notes, bulletins, communications, notifications et au suivi complet de votre enfant.',
            style: TextStyle(
              color: Colors.white.withValues(alpha: 0.85),
              height: 1.4,
              fontSize: 13,
            ),
          ),
          const SizedBox(height: 14),
          Text(
            '1,50 USD / année scolaire',
            style: TextStyle(
              color: Colors.amber.shade200,
              fontWeight: FontWeight.w700,
              fontSize: 15,
            ),
          ),
          const SizedBox(height: 14),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              style: FilledButton.styleFrom(
                backgroundColor: Colors.white,
                foregroundColor: ErpColors.navy,
              ),
              onPressed: onActivate,
              child: const Text('Activer mon abonnement'),
            ),
          ),
        ],
      ),
    );
  }
}
