import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/theme/erp_theme.dart';
import '../models/premium_payment_models.dart';
import '../premium_payment_providers.dart';

class PremiumPaymentConfirmScreen extends ConsumerWidget {
  const PremiumPaymentConfirmScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(premiumPaymentProvider);
    final method = state.method;

    return Scaffold(
      appBar: AppBar(title: const Text('Confirmation')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
        children: [
          const Text(
            'Vérifiez avant de payer',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: ErpColors.navy,
            ),
          ),
          const SizedBox(height: 14),
          ErpCard(
            child: Column(
              children: [
                _Row(label: 'Abonnement', value: 'Premium'),
                _Row(label: 'Durée', value: state.displayDuration),
                _Row(
                  label: 'Montant',
                  value: '${state.displayAmount.toStringAsFixed(2)} ${state.currency}',
                ),
                _Row(label: 'Mode de paiement', value: method?.label ?? '—'),
                _Row(label: 'Numéro', value: state.phone),
              ],
            ),
          ),
          const SizedBox(height: 12),
          const Text(
            'Un message USSD / push vous sera envoyé par l’opérateur pour valider.',
            style: TextStyle(color: ErpColors.textSecondary, fontSize: 13),
          ),
          if (state.errorMessage != null) ...[
            const SizedBox(height: 10),
            Text(state.errorMessage!, style: const TextStyle(color: ErpColors.danger)),
          ],
          const SizedBox(height: 24),
          SizedBox(
            width: double.infinity,
            height: 52,
            child: FilledButton(
              onPressed: state.isSubmitting
                  ? null
                  : () async {
                      context.push('/parent/subscription/status');
                      final ok = await ref
                          .read(premiumPaymentProvider.notifier)
                          .confirmAndPay();
                      if (!context.mounted) return;
                      if (ok) {
                        context.go('/parent/subscription/success');
                      }
                    },
              child: state.isSubmitting
                  ? const SizedBox(
                      width: 22,
                      height: 22,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                    )
                  : const Text('Confirmer le paiement'),
            ),
          ),
        ],
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 130,
            child: Text(label, style: const TextStyle(color: ErpColors.textSecondary)),
          ),
          Expanded(
            child: Text(value, style: const TextStyle(fontWeight: FontWeight.w700)),
          ),
        ],
      ),
    );
  }
}
