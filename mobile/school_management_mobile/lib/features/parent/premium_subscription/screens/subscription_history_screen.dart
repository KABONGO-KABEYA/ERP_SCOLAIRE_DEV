import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

import '../../../../core/theme/erp_theme.dart';
import '../models/premium_payment_models.dart';
import '../premium_payment_providers.dart';
import '../../widgets/parent_async_widgets.dart';

class PremiumSubscriptionHistoryScreen extends ConsumerWidget {
  const PremiumSubscriptionHistoryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(premiumPaymentHistoryProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Historique des abonnements')),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(premiumPaymentHistoryProvider);
          await ref.read(premiumPaymentHistoryProvider.future);
        },
        child: async.when(
          loading: () => const ParentSkeletonList(itemCount: 4),
          error: (e, _) => ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(ErpSpacing.page),
            children: [
              ParentErrorState(
                message: 'Impossible de charger l’historique.\n$e',
                onRetry: () => ref.invalidate(premiumPaymentHistoryProvider),
              ),
            ],
          ),
          data: (items) {
            if (items.isEmpty) {
              return ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(ErpSpacing.page),
                children: const [
                  ParentEmptyState(
                    title: 'Aucun paiement',
                    subtitle: 'Vos souscriptions Premium apparaîtront ici.',
                    icon: Icons.receipt_long_outlined,
                  ),
                ],
              );
            }

            return ListView.separated(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
              itemCount: items.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final item = items[index];
                return ErpCard(
                  padding: const EdgeInsets.all(14),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              DateFormat('dd/MM/yyyy HH:mm').format(item.date.toLocal()),
                              style: const TextStyle(fontWeight: FontWeight.w800),
                            ),
                          ),
                          _StatusChip(status: item.status),
                        ],
                      ),
                      const SizedBox(height: 8),
                      Text('${item.amount.toStringAsFixed(2)} ${item.currency} · ${item.durationLabel}'),
                      const SizedBox(height: 4),
                      Text(
                        '${_methodLabel(item.paymentMethod)} · ${item.phoneNumber}',
                        style: const TextStyle(color: ErpColors.textSecondary, fontSize: 13),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'N° ${item.transactionNumber}',
                        style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                      ),
                      if (item.invoiceAvailable) ...[
                        const SizedBox(height: 10),
                        Align(
                          alignment: Alignment.centerRight,
                          child: OutlinedButton.icon(
                            onPressed: () => _openInvoice(context, ref, item.id),
                            icon: const Icon(Icons.picture_as_pdf_outlined, size: 18),
                            label: const Text('Facture PDF'),
                          ),
                        ),
                      ],
                    ],
                  ),
                );
              },
            );
          },
        ),
      ),
    );
  }

  String _methodLabel(String raw) {
    for (final m in PremiumPaymentMethodKind.values) {
      if (m.apiValue == raw.toLowerCase()) return m.label;
    }
    return raw;
  }

  Future<void> _openInvoice(
    BuildContext context,
    WidgetRef ref,
    String paymentId,
  ) async {
    try {
      final bytes =
          await ref.read(premiumPaymentServiceProvider).invoicePdf(paymentId);
      final dir = await getTemporaryDirectory();
      final file = File('${dir.path}/facture-premium-$paymentId.pdf');
      await file.writeAsBytes(bytes, flush: true);
      await OpenFilex.open(file.path);
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Facture indisponible : $e')),
      );
    }
  }
}

class _StatusChip extends StatelessWidget {
  const _StatusChip({required this.status});

  final PremiumPaymentStatusKind status;

  @override
  Widget build(BuildContext context) {
    final (label, color) = switch (status) {
      PremiumPaymentStatusKind.success => ('Réussi', ErpColors.success),
      PremiumPaymentStatusKind.refused => ('Refusé', ErpColors.danger),
      PremiumPaymentStatusKind.expired => ('Expiré', ErpColors.warning),
      PremiumPaymentStatusKind.cancelled => ('Annulé', ErpColors.textSecondary),
      PremiumPaymentStatusKind.processing => ('En cours', ErpColors.primary),
      PremiumPaymentStatusKind.pending => ('En attente', ErpColors.warning),
      PremiumPaymentStatusKind.idle => ('—', ErpColors.textSecondary),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        label,
        style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 11),
      ),
    );
  }
}
