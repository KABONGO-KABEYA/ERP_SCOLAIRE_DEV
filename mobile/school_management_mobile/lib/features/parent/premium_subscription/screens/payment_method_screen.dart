import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/theme/erp_theme.dart';
import '../models/premium_payment_models.dart';
import '../premium_payment_providers.dart';
import '../widgets/premium_payment_widgets.dart';

class PremiumPaymentMethodScreen extends ConsumerWidget {
  const PremiumPaymentMethodScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(premiumPaymentProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Mode de paiement')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
        children: [
          const Text(
            'Choisissez votre opérateur',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: ErpColors.navy,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'Paiement mobile money sécurisé. Une seule méthode à la fois.',
            style: TextStyle(color: ErpColors.textSecondary),
          ),
          const SizedBox(height: 18),
          ...PremiumPaymentMethodKind.values.map(
            (m) => Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: PaymentMethodCard(
                method: m,
                selected: state.method == m,
                onTap: () =>
                    ref.read(premiumPaymentProvider.notifier).selectMethod(m),
              ),
            ),
          ),
          if (state.errorMessage != null) ...[
            const SizedBox(height: 8),
            Text(
              state.errorMessage!,
              style: const TextStyle(color: ErpColors.danger),
            ),
          ],
          const SizedBox(height: 20),
          SizedBox(
            width: double.infinity,
            height: 50,
            child: FilledButton(
              onPressed: state.method == null
                  ? null
                  : () => context.push('/parent/subscription/phone'),
              child: const Text('Continuer'),
            ),
          ),
        ],
      ),
    );
  }
}
