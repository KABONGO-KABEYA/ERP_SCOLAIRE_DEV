import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/theme/erp_theme.dart';
import '../../parent_providers.dart';
import '../../widgets/parent_async_widgets.dart';
import '../../widgets/parent_ui_widgets.dart';
import '../models/premium_payment_models.dart';
import '../premium_payment_providers.dart';

/// Écran offre Premium — comparatif Free / Premium + plans.
class PremiumOfferScreen extends ConsumerWidget {
  const PremiumOfferScreen({super.key});

  static const _monthlyVsAnnualSaving = 4.50; // 12*0.50 - 1.50

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(parentSubscriptionProvider);
    final checkout = ref.watch(premiumPaymentProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Abonnement Premium'),
        actions: [
          IconButton(
            tooltip: 'Historique',
            onPressed: () => context.push('/parent/subscription/history'),
            icon: const Icon(Icons.receipt_long_outlined),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(parentSubscriptionProvider);
          await ref.read(parentSubscriptionProvider.future);
        },
        child: async.when(
          loading: () => const ParentSkeletonList(itemCount: 4),
          error: (e, _) => ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(ErpSpacing.page),
            children: [
              ParentErrorState(
                message: 'Impossible de charger l’abonnement.\n$e',
                onRetry: () => ref.invalidate(parentSubscriptionProvider),
              ),
            ],
          ),
          data: (sub) {
            if (sub.isActive) {
              return _ActivePremiumView(subPlan: sub.plan, expiry: sub.expiryDate ?? sub.subscription.expiresAt);
            }
            return ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
              children: [
                const _PremiumHero(),
                const SizedBox(height: 18),
                const ParentSectionTitle('Choisissez votre formule'),
                _PlanCard(
                  selected: checkout.plan == PremiumPlanKind.monthly,
                  title: 'Mensuel',
                  price: PremiumPlanKind.monthly.priceLabel,
                  subtitle: 'Flexible, sans engagement long',
                  onTap: () => ref
                      .read(premiumPaymentProvider.notifier)
                      .selectPlan(PremiumPlanKind.monthly),
                ),
                const SizedBox(height: 10),
                _PlanCard(
                  selected: checkout.plan == PremiumPlanKind.annual,
                  title: 'Annuel',
                  price: PremiumPlanKind.annual.priceLabel,
                  subtitle:
                      'Économisez ${_monthlyVsAnnualSaving.toStringAsFixed(2)} USD vs 12 mois',
                  badge: 'Meilleure offre',
                  onTap: () => ref
                      .read(premiumPaymentProvider.notifier)
                      .selectPlan(PremiumPlanKind.annual),
                ),
                const SizedBox(height: 20),
                const ParentSectionTitle('Comparatif'),
                const _ComparisonTable(),
                const SizedBox(height: 24),
                SizedBox(
                  width: double.infinity,
                  height: 52,
                  child: FilledButton(
                    onPressed: () {
                      ref.read(premiumPaymentProvider.notifier).selectPlan(checkout.plan);
                      context.push('/parent/subscription/payment-method');
                    },
                    child: const Text(
                      'Passer Premium',
                      style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
                    ),
                  ),
                ),
                const SizedBox(height: 10),
                TextButton(
                  onPressed: () => context.push('/parent/subscription/history'),
                  child: const Text('Voir l’historique des abonnements'),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _PremiumHero extends StatelessWidget {
  const _PremiumHero();

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      padding: const EdgeInsets.fromLTRB(18, 22, 18, 22),
      child: Column(
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            decoration: BoxDecoration(
              color: ErpColors.primary.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(20),
            ),
            child: const Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Icons.workspace_premium_rounded, color: ErpColors.primary, size: 18),
                SizedBox(width: 6),
                Text(
                  'Premium',
                  style: TextStyle(
                    color: ErpColors.primary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Container(
            width: 88,
            height: 88,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              gradient: LinearGradient(
                colors: [
                  ErpColors.primary.withValues(alpha: 0.18),
                  ErpColors.navy.withValues(alpha: 0.08),
                ],
              ),
            ),
            child: const Icon(
              Icons.school_rounded,
              size: 42,
              color: ErpColors.navy,
            ),
          ),
          const SizedBox(height: 14),
          const Text(
            'Suivi scolaire complet',
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w800,
              color: ErpColors.navy,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'Notes, bulletins, présences et alertes — comme une app bancaire : simple, claire, sécurisée.',
            textAlign: TextAlign.center,
            style: TextStyle(color: ErpColors.textSecondary, height: 1.4),
          ),
        ],
      ),
    );
  }
}

class _PlanCard extends StatelessWidget {
  const _PlanCard({
    required this.selected,
    required this.title,
    required this.price,
    required this.subtitle,
    required this.onTap,
    this.badge,
  });

  final bool selected;
  final String title;
  final String price;
  final String subtitle;
  final String? badge;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: selected ? ErpColors.primary : ErpColors.border,
            width: selected ? 2 : 1,
          ),
        ),
        child: Row(
          children: [
            Icon(
              selected ? Icons.radio_button_checked : Icons.radio_button_off,
              color: selected ? ErpColors.primary : ErpColors.textSecondary,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Text(title, style: const TextStyle(fontWeight: FontWeight.w800)),
                      if (badge != null) ...[
                        const SizedBox(width: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                          decoration: BoxDecoration(
                            color: ErpColors.success.withValues(alpha: 0.12),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(
                            badge!,
                            style: const TextStyle(
                              fontSize: 11,
                              color: ErpColors.success,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(price, style: const TextStyle(color: ErpColors.primary, fontWeight: FontWeight.w700)),
                  Text(subtitle, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ComparisonTable extends StatelessWidget {
  const _ComparisonTable();

  @override
  Widget build(BuildContext context) {
    const free = ['Paiements', 'Reçus', 'Profil'];
    const premium = [
      'Notes',
      'Bulletins',
      'Présences',
      'Communications',
      'Emploi du temps',
      'Devoirs',
      'Notifications temps réel',
    ];

    return ErpCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        children: [
          const Row(
            children: [
              Expanded(
                child: Text('Gratuit', style: TextStyle(fontWeight: FontWeight.w800)),
              ),
              Expanded(
                child: Text(
                  'Premium',
                  textAlign: TextAlign.right,
                  style: TextStyle(fontWeight: FontWeight.w800, color: ErpColors.primary),
                ),
              ),
            ],
          ),
          const Divider(height: 20),
          ...free.map((f) => _row(f, false)),
          ...premium.map((f) => _row(f, true)),
        ],
      ),
    );
  }

  Widget _row(String label, bool premiumOnly) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Expanded(child: Text(label)),
          Icon(
            premiumOnly ? Icons.lock_outline : Icons.check_circle,
            size: 18,
            color: premiumOnly ? ErpColors.textSecondary : ErpColors.success,
          ),
          const SizedBox(width: 18),
          const Icon(Icons.check_circle, size: 18, color: ErpColors.success),
        ],
      ),
    );
  }
}

class _ActivePremiumView extends StatelessWidget {
  const _ActivePremiumView({required this.subPlan, this.expiry});

  final String subPlan;
  final DateTime? expiry;

  @override
  Widget build(BuildContext context) {
    final expiryLabel =
        expiry == null ? '—' : DateFormat('dd/MM/yyyy').format(expiry!);
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(ErpSpacing.page),
      children: [
        ErpCard(
          child: Column(
            children: [
              const Icon(Icons.verified, color: ErpColors.success, size: 52),
              const SizedBox(height: 12),
              Text(
                'Plan $subPlan',
                style: const TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                  color: ErpColors.navy,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                'Actif jusqu’au $expiryLabel',
                style: const TextStyle(color: ErpColors.textSecondary),
              ),
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: () => context.push('/parent/subscription/history'),
                icon: const Icon(Icons.history),
                label: const Text('Historique & factures'),
              ),
              const SizedBox(height: 8),
              TextButton(
                onPressed: () => context.push('/parent/subscription/payment-method'),
                child: const Text('Renouveler / prolonger'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
