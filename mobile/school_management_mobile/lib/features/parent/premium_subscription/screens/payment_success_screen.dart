import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/theme/erp_theme.dart';
import '../../parent_providers.dart';
import '../premium_payment_providers.dart';
import '../widgets/premium_payment_widgets.dart';
import '../models/premium_payment_models.dart';

class PremiumPaymentSuccessScreen extends ConsumerStatefulWidget {
  const PremiumPaymentSuccessScreen({super.key});

  @override
  ConsumerState<PremiumPaymentSuccessScreen> createState() =>
      _PremiumPaymentSuccessScreenState();
}

class _PremiumPaymentSuccessScreenState
    extends ConsumerState<PremiumPaymentSuccessScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _scale;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 700),
    );
    _scale = CurvedAnimation(parent: _controller, curve: Curves.elasticOut);
    _controller.forward();
    // Assure le refresh Premium.
    Future.microtask(() => ref.invalidate(parentSubscriptionProvider));
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(premiumPaymentProvider);

    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(ErpSpacing.page),
          child: Column(
            children: [
              const Spacer(),
              ScaleTransition(
                scale: _scale,
                child: const PremiumStatusIllustration(
                  status: PremiumPaymentStatusKind.success,
                ),
              ),
              const SizedBox(height: 10),
              const Text(
                'Abonnement Premium activé',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w900,
                  color: ErpColors.navy,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                'Toutes les fonctionnalités Premium sont déverrouillées immédiatement.',
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: ErpColors.textSecondary.withValues(alpha: 0.95),
                ),
              ),
              if (state.transactionNumber != null) ...[
                const SizedBox(height: 12),
                Text(
                  'Transaction ${state.transactionNumber}',
                  style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                ),
              ],
              const Spacer(),
              SizedBox(
                width: double.infinity,
                height: 50,
                child: FilledButton(
                  onPressed: () {
                    ref.read(premiumPaymentProvider.notifier).resetFlow();
                    context.go('/parent/home');
                  },
                  child: const Text('Retour à l’accueil'),
                ),
              ),
              const SizedBox(height: 8),
              TextButton(
                onPressed: () => context.go('/parent/subscription/history'),
                child: const Text('Voir l’historique'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
