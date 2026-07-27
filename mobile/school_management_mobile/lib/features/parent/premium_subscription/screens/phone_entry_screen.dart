import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/theme/erp_theme.dart';
import '../models/premium_payment_models.dart';
import '../premium_payment_providers.dart';

class PremiumPhoneEntryScreen extends ConsumerStatefulWidget {
  const PremiumPhoneEntryScreen({super.key});

  @override
  ConsumerState<PremiumPhoneEntryScreen> createState() =>
      _PremiumPhoneEntryScreenState();
}

class _PremiumPhoneEntryScreenState extends ConsumerState<PremiumPhoneEntryScreen> {
  late final TextEditingController _controller;

  @override
  void initState() {
    super.initState();
    final state = ref.read(premiumPaymentProvider);
    final hint = state.method?.phonePrefixHint ?? '0';
    _controller = TextEditingController(
      text: state.phone.isNotEmpty ? state.phone : hint,
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(premiumPaymentProvider);
    final method = state.method;

    if (method == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Numéro')),
        body: const Center(child: Text('Choisissez d’abord un mode de paiement.')),
      );
    }

    final valid = method.isValidPhone(_controller.text);

    return Scaffold(
      appBar: AppBar(title: const Text('Numéro de paiement')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
        children: [
          Center(
            child: Hero(
              tag: 'pay-logo-${method.apiValue}',
              child: ClipRRect(
                borderRadius: BorderRadius.circular(16),
                child: Image.asset(
                  method.assetPath,
                  width: 96,
                  height: 96,
                  fit: BoxFit.contain,
                ),
              ),
            ),
          ),
          const SizedBox(height: 12),
          Text(
            method.label,
            textAlign: TextAlign.center,
            style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18),
          ),
          const SizedBox(height: 20),
          ErpCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Numéro de téléphone', style: TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: 8),
                TextField(
                  controller: _controller,
                  keyboardType: TextInputType.phone,
                  inputFormatters: [
                    FilteringTextInputFormatter.digitsOnly,
                    LengthLimitingTextInputFormatter(10),
                  ],
                  decoration: InputDecoration(
                    hintText: '${method.phonePrefixHint}xxxxxxx',
                    prefixIcon: const Icon(Icons.phone_android),
                    helperText: 'Format ${method.label} : ${method.phonePrefixHint}xxxxxxx',
                  ),
                  onChanged: (v) {
                    ref.read(premiumPaymentProvider.notifier).setPhone(v);
                    setState(() {});
                  },
                ),
              ],
            ),
          ),
          const SizedBox(height: 14),
          ErpCard(
            padding: const EdgeInsets.all(14),
            child: Column(
              children: [
                _InfoRow(label: 'Montant', value: '${state.displayAmount.toStringAsFixed(2)} ${state.currency}'),
                _InfoRow(label: 'Devise', value: state.currency),
                _InfoRow(label: 'Durée', value: state.displayDuration),
                _InfoRow(label: 'Formule', value: state.plan.label),
              ],
            ),
          ),
          const SizedBox(height: 22),
          SizedBox(
            width: double.infinity,
            height: 50,
            child: FilledButton(
              onPressed: !valid
                  ? null
                  : () {
                      ref.read(premiumPaymentProvider.notifier).setPhone(_controller.text);
                      context.push('/parent/subscription/confirm');
                    },
              child: const Text('Continuer'),
            ),
          ),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Expanded(child: Text(label, style: const TextStyle(color: ErpColors.textSecondary))),
          Text(value, style: const TextStyle(fontWeight: FontWeight.w700)),
        ],
      ),
    );
  }
}
