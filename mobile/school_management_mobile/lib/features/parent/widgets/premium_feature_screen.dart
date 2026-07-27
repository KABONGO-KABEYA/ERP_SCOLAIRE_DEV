import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../core/theme/erp_theme.dart';

/// Écran verrouillage Premium — réutilisable pour tous les modules payants.
class PremiumFeatureScreen extends StatelessWidget {
  const PremiumFeatureScreen({
    super.key,
    this.featureTitle,
    this.onActivate,
  });

  final String? featureTitle;
  final VoidCallback? onActivate;

  static const priceLabel = '1,50 USD / année scolaire';

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 24, 20, 32),
      children: [
        const SizedBox(height: 12),
        Center(
          child: Container(
            width: 96,
            height: 96,
            decoration: BoxDecoration(
              color: ErpColors.primary.withValues(alpha: 0.1),
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.workspace_premium_rounded,
              size: 48,
              color: ErpColors.primary,
            ),
          ),
        ),
        const SizedBox(height: 24),
        Text(
          featureTitle ?? 'Fonctionnalité Premium',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                fontWeight: FontWeight.w700,
                color: ErpColors.navy,
              ),
        ),
        const SizedBox(height: 10),
        Text(
          'Cette fonctionnalité est réservée aux abonnés Premium.',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(height: 1.45),
        ),
        const SizedBox(height: 20),
        ErpCard(
          child: Column(
            children: [
              Text(
                priceLabel,
                style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: ErpColors.primary,
                      fontWeight: FontWeight.w700,
                    ),
              ),
              const SizedBox(height: 16),
              const _BenefitRow(text: 'Notes, moyennes et classement'),
              const _BenefitRow(text: 'Bulletins PDF à télécharger'),
              const _BenefitRow(text: 'Communications et circulaires'),
              const _BenefitRow(text: 'Notifications en temps réel'),
              const _BenefitRow(text: 'Présences, absences et retards'),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: onActivate ??
                      () => context.push('/parent/subscription'),
                  icon: const Icon(Icons.workspace_premium_rounded),
                  label: const Text('Passer Premium'),
                ),
              ),
              const SizedBox(height: 8),
              Text(
                'Airtel Money · Orange Money · M-Pesa',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      fontSize: 12,
                    ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _BenefitRow extends StatelessWidget {
  const _BenefitRow({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        children: [
          Icon(Icons.check_circle, color: ErpColors.success.withValues(alpha: 0.9), size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              style: Theme.of(context).textTheme.bodyLarge,
            ),
          ),
        ],
      ),
    );
  }
}
