import 'package:flutter/material.dart';

import '../../../../core/theme/erp_theme.dart';
import '../models/premium_payment_models.dart';

class PremiumStatusIllustration extends StatelessWidget {
  const PremiumStatusIllustration({super.key, required this.status});

  final PremiumPaymentStatusKind status;

  @override
  Widget build(BuildContext context) {
    final (icon, color, title, subtitle) = switch (status) {
      PremiumPaymentStatusKind.pending => (
          Icons.hourglass_top_rounded,
          ErpColors.warning,
          'En attente',
          'Votre demande a été reçue. Confirmez sur votre téléphone.'
        ),
      PremiumPaymentStatusKind.processing => (
          Icons.sync_rounded,
          ErpColors.primary,
          'Paiement en cours',
          'Validation auprès de l’opérateur mobile money…'
        ),
      PremiumPaymentStatusKind.success => (
          Icons.verified_rounded,
          ErpColors.success,
          'Paiement réussi',
          'Votre abonnement Premium est activé.'
        ),
      PremiumPaymentStatusKind.refused => (
          Icons.cancel_rounded,
          ErpColors.danger,
          'Paiement refusé',
          'L’opérateur a refusé la transaction. Réessayez ou changez de numéro.'
        ),
      PremiumPaymentStatusKind.expired => (
          Icons.timer_off_rounded,
          ErpColors.warning,
          'Paiement expiré',
          'Le délai de confirmation est dépassé. Relancez le paiement.'
        ),
      PremiumPaymentStatusKind.cancelled => (
          Icons.block_rounded,
          ErpColors.textSecondary,
          'Paiement annulé',
          'La transaction a été annulée.'
        ),
      PremiumPaymentStatusKind.idle => (
          Icons.payments_outlined,
          ErpColors.primary,
          'Paiement',
          'Choisissez votre offre pour continuer.'
        ),
    };

    return AnimatedSwitcher(
      duration: const Duration(milliseconds: 350),
      switchInCurve: Curves.easeOutCubic,
      child: Column(
        key: ValueKey(status),
        children: [
          Container(
            width: 108,
            height: 108,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.12),
              shape: BoxShape.circle,
            ),
            child: status == PremiumPaymentStatusKind.processing
                ? Padding(
                    padding: const EdgeInsets.all(28),
                    child: CircularProgressIndicator(color: color, strokeWidth: 3),
                  )
                : Icon(icon, size: 52, color: color),
          ),
          const SizedBox(height: 18),
          Text(
            title,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w800,
              color: ErpColors.navy,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            subtitle,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: ErpColors.textSecondary,
              height: 1.4,
            ),
          ),
        ],
      ),
    );
  }
}

class PaymentMethodCard extends StatelessWidget {
  const PaymentMethodCard({
    super.key,
    required this.method,
    required this.selected,
    required this.onTap,
  });

  final PremiumPaymentMethodKind method;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 220),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(
              color: selected ? ErpColors.primary : ErpColors.border,
              width: selected ? 2 : 1,
            ),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withValues(alpha: selected ? 0.08 : 0.04),
                blurRadius: selected ? 16 : 10,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Row(
            children: [
              Hero(
                tag: 'pay-logo-${method.apiValue}',
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: Image.asset(
                    method.assetPath,
                    width: 56,
                    height: 56,
                    fit: BoxFit.contain,
                  ),
                ),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Text(
                  method.label,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 16,
                  ),
                ),
              ),
              Icon(
                selected ? Icons.radio_button_checked : Icons.chevron_right,
                color: selected ? ErpColors.primary : ErpColors.textSecondary,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
