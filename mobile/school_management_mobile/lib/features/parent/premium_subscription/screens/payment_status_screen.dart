import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/theme/erp_theme.dart';
import '../models/premium_payment_models.dart';
import '../premium_payment_providers.dart';
import '../widgets/premium_payment_widgets.dart';

class PremiumPaymentStatusScreen extends ConsumerWidget {
  const PremiumPaymentStatusScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(premiumPaymentProvider);

    ref.listen(premiumPaymentProvider, (prev, next) {
      if (next.status == PremiumPaymentStatusKind.success &&
          prev?.status != PremiumPaymentStatusKind.success) {
        context.go('/parent/subscription/success');
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: const Text('Paiement'),
        automaticallyImplyLeading: !state.isSubmitting &&
            state.status != PremiumPaymentStatusKind.processing &&
            state.status != PremiumPaymentStatusKind.pending,
      ),
      body: Padding(
        padding: const EdgeInsets.all(ErpSpacing.page),
        child: Column(
          children: [
            const Spacer(),
            PremiumStatusIllustration(status: state.status),
            if (state.transactionNumber != null &&
                state.transactionNumber!.isNotEmpty) ...[
              const SizedBox(height: 18),
              Text(
                'Réf. ${state.transactionNumber}',
                style: const TextStyle(
                  fontSize: 12,
                  color: ErpColors.textSecondary,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
            if (state.failureReason != null &&
                state.status != PremiumPaymentStatusKind.success) ...[
              const SizedBox(height: 12),
              Text(
                state.failureReason!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: ErpColors.danger, fontSize: 13),
              ),
            ],
            const Spacer(),
            if (state.status == PremiumPaymentStatusKind.refused ||
                state.status == PremiumPaymentStatusKind.expired ||
                state.status == PremiumPaymentStatusKind.cancelled)
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: () => context.go('/parent/subscription/payment-method'),
                  child: const Text('Réessayer'),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
