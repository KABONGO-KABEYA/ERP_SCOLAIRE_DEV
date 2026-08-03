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

  static const _ink = Color(0xFF0F172A);
  static const _primary = Color(0xFF2952CC);
  static const _borderStrong = Color(0xFFCBD5E1);
  static const _pageBg = Color(0xFFF1F5F9);
  static const _textSecondary = Color(0xFF64748B);

  @override
  Widget build(BuildContext context) {
    if (children.isEmpty) return const SizedBox.shrink();

    return SizedBox(
      height: 36,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: children.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final child = children[index];
          final selected = child.studentId == selectedId;
          return _ChildPill(
            child: child,
            selected: selected,
            onTap: () => onChanged(child.studentId),
          );
        },
      ),
    );
  }
}

class _ChildPill extends StatelessWidget {
  const _ChildPill({
    required this.child,
    required this.selected,
    required this.onTap,
  });

  final ParentChild child;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final firstName = _firstName(child.fullName);
    final classLabel = (child.className ?? '').trim();
    final label = classLabel.isEmpty ? firstName : '$firstName — $classLabel';

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(6),
        child: Ink(
          decoration: BoxDecoration(
            color: selected ? ParentChildSelector._ink : Colors.white,
            borderRadius: BorderRadius.circular(6),
            border: selected
                ? null
                : Border.all(color: ParentChildSelector._borderStrong),
          ),
          padding: const EdgeInsets.fromLTRB(6, 6, 6, 6),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 24,
                height: 24,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: selected
                      ? ParentChildSelector._primary
                      : ParentChildSelector._pageBg,
                  borderRadius: BorderRadius.circular(4),
                ),
                child: Text(
                  _initials(child.fullName),
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w600,
                    height: 1,
                    color: selected
                        ? Colors.white
                        : ParentChildSelector._textSecondary,
                  ),
                ),
              ),
              const SizedBox(width: 8),
              Text(
                label,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w500,
                  color: selected ? Colors.white : ParentChildSelector._ink,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static String _firstName(String fullName) {
    final parts =
        fullName.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '—';
    return parts.first;
  }

  static String _initials(String fullName) {
    final parts =
        fullName.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first.substring(0, 1) + parts.last.substring(0, 1)).toUpperCase();
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
    this.showProgress = true,
  });

  final ParentPaymentSummary summary;
  final String title;
  final String? subtitle;
  final bool showProgress;

  @override
  Widget build(BuildContext context) {
    final fmt = NumberFormat('#,##0.##');
    final remaining = summary.balance;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: ErpColors.navy,
        borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: Colors.white.withValues(alpha: 0.75),
            ),
          ),
          if (subtitle != null && subtitle!.trim().isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(
              subtitle!,
              style: TextStyle(
                fontSize: 12,
                color: Colors.white.withValues(alpha: 0.55),
              ),
            ),
          ],
          const SizedBox(height: 12),
          Text(
            '${fmt.format(remaining)} ${summary.currencyLabel}',
            style: const TextStyle(
              fontSize: 28,
              fontWeight: FontWeight.w700,
              letterSpacing: -0.2,
              color: Colors.white,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            remaining > 0 ? 'Reste à payer' : 'Solde à jour',
            style: TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w600,
              color: remaining > 0 ? ErpColors.warning : ErpColors.accentGreen,
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: _NavyMoneyStat(
                  label: 'À payer',
                  value: '${fmt.format(summary.totalDue)} ${summary.currencyLabel}',
                ),
              ),
              Expanded(
                child: _NavyMoneyStat(
                  label: 'Payé',
                  value: '${fmt.format(summary.totalPaid)} ${summary.currencyLabel}',
                ),
              ),
            ],
          ),
          if (showProgress) ...[
            const SizedBox(height: 14),
            ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: LinearProgressIndicator(
                value: summary.progress,
                minHeight: 8,
                backgroundColor: Colors.white.withValues(alpha: 0.15),
                color: ErpColors.accentGold,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              '${(summary.progress * 100).toStringAsFixed(0)} % payé',
              style: TextStyle(
                fontSize: 12,
                color: Colors.white.withValues(alpha: 0.7),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _NavyMoneyStat extends StatelessWidget {
  const _NavyMoneyStat({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: TextStyle(fontSize: 11, color: Colors.white.withValues(alpha: 0.6)),
        ),
        const SizedBox(height: 2),
        Text(
          value,
          style: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w700,
            color: Colors.white,
          ),
        ),
      ],
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
    final fees = feeTypes.where((f) => f.feeTypeName.trim().isNotEmpty).toList();
    if (fees.isEmpty) return const SizedBox.shrink();

    Widget chip({
      required String label,
      required bool selected,
      required VoidCallback onTap,
    }) {
      return Padding(
        padding: const EdgeInsets.only(right: 8),
        child: FilterChip(
          label: Text(label),
          selected: selected,
          showCheckmark: false,
          onSelected: (_) => onTap(),
          selectedColor: ErpColors.primary.withValues(alpha: 0.12),
          labelStyle: TextStyle(
            color: selected ? ErpColors.primary : ErpColors.textPrimary,
            fontWeight: FontWeight.w600,
            fontSize: 13,
          ),
          side: BorderSide(color: selected ? ErpColors.primary : ErpColors.border),
          backgroundColor: Colors.white,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        ),
      );
    }

    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          if (showAll)
            chip(
              label: 'Tous',
              selected: selectedFeeTypeId == null,
              onTap: () => onChanged(null),
            ),
          for (final fee in fees)
            chip(
              label: fee.feeTypeName,
              selected: selectedFeeTypeId == fee.feeTypeId,
              onTap: () => onChanged(fee.feeTypeId),
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
              Icon(Icons.workspace_premium, color: ErpColors.accentGold),
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
          const Text(
            '1,50 USD / année scolaire',
            style: TextStyle(
              color: ErpColors.accentGold,
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
