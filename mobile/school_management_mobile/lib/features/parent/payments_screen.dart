import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import 'models/parent_models.dart';
import 'offline/parent_offline_cache.dart';
import 'parent_providers.dart';
import 'widgets/parent_async_widgets.dart';
import 'widgets/parent_payments_premium_widgets.dart';
import 'widgets/parent_ui_widgets.dart';

class ParentPaymentsScreen extends ConsumerWidget {
  const ParentPaymentsScreen({super.key});

  Future<void> _openReceipt(
    BuildContext context,
    WidgetRef ref,
    ParentPayment payment,
  ) async {
    try {
      await ref.read(parentRepositoryProvider).openPaymentReceipt(payment);
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Impossible d\'ouvrir le reçu : $e')),
      );
    }
  }

  Future<void> _exportZip(
    BuildContext context,
    WidgetRef ref,
    List<ParentPayment> payments,
  ) async {
    final messenger = ScaffoldMessenger.of(context);
    messenger.showSnackBar(
      const SnackBar(content: Text('Préparation du ZIP des reçus…')),
    );
    try {
      await ref.read(parentReceiptZipServiceProvider).downloadAndOpenZip(payments);
      messenger.hideCurrentSnackBar();
      messenger.showSnackBar(
        const SnackBar(content: Text('ZIP des reçus prêt.')),
      );
    } catch (e) {
      messenger.hideCurrentSnackBar();
      messenger.showSnackBar(
        SnackBar(content: Text('Export impossible : $e')),
      );
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final childrenAsync = ref.watch(parentChildrenProvider);
    final selected = ref.watch(selectedChildProvider);
    final selectedFeeTypeId = ref.watch(selectedFeeTypeIdProvider);
    final searchQuery = ref.watch(parentPaymentsSearchQueryProvider);
    final periodFilter = ref.watch(parentPaymentsPeriodFilterProvider);

    ref.listen(parentChildrenProvider, (_, next) {
      next.whenData((children) => ensureChildSelected(ref, children));
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Paiements')),
      body: childrenAsync.when(
        loading: () => const ParentSkeletonList(itemCount: 4),
        error: (e, _) => ListView(
          padding: const EdgeInsets.all(ErpSpacing.page),
          children: [
            ParentErrorState(
              message: 'Impossible de charger les paiements.\n$e',
            ),
          ],
        ),
        data: (children) {
          if (children.isEmpty) {
            return ListView(
              padding: const EdgeInsets.all(ErpSpacing.page),
              children: const [
                ParentEmptyState(
                  title: 'Aucun enfant associé',
                  icon: Icons.family_restroom_outlined,
                ),
              ],
            );
          }
          final studentId = selected?.studentId ?? children.first.studentId;
          final situationsAsync = ref.watch(parentFeeSituationsProvider(studentId));
          final paymentsAsync = ref.watch(parentPaymentsProvider(studentId));
          final showOffline = parentHasOfflineCacheHit(
            ref.watch(parentOfflineCacheHitsProvider),
            [
              ParentCacheKeys.payments(studentId),
              ParentCacheKeys.feeSituations(studentId),
            ],
          );

          return RefreshIndicator(
            onRefresh: () async {
              ref.invalidate(parentFeeSituationsProvider(studentId));
              ref.invalidate(parentPaymentSummaryProvider(studentId));
              ref.invalidate(parentPaymentsProvider(studentId));
            },
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
              children: [
                ParentChildSelector(
                  children: children,
                  selectedId: studentId,
                  onChanged: (id) {
                    ref.read(selectedChildIdProvider.notifier).state = id;
                    ref.read(selectedFeeTypeIdProvider.notifier).state = null;
                    ref.read(parentPaymentsSearchQueryProvider.notifier).state = '';
                    ref.read(parentPaymentsPeriodFilterProvider.notifier).state = null;
                  },
                ),
                if (children.length > 1) const SizedBox(height: 12),
                ParentOfflineBanner(visible: showOffline),
                situationsAsync.when(
                  loading: () => const ErpCard(
                    child: SizedBox(
                      height: 90,
                      child: Center(child: CircularProgressIndicator()),
                    ),
                  ),
                  error: (e, _) => ErpCard(child: Text('Erreur situation : $e')),
                  data: (situations) {
                    if (situations.feeTypes.isEmpty) {
                      return const ErpCard(
                        child: Text(
                          'Aucun type de frais applicable pour cet enfant.',
                        ),
                      );
                    }

                    ParentFeeTypeSituation? selectedFee;
                    if (selectedFeeTypeId != null) {
                      for (final fee in situations.feeTypes) {
                        if (fee.feeTypeId == selectedFeeTypeId) {
                          selectedFee = fee;
                          break;
                        }
                      }
                    }

                    final timelineFee = selectedFee ?? situations.feeTypes.first;
                    final summary = selectedFee?.asSummary ?? situations.overallSummary;
                    final title = selectedFee == null
                        ? 'Situation des paiements'
                        : selectedFee.feeTypeName;
                    final subtitle = selectedFee == null
                        ? 'Année ${situations.academicYearLabel} · tous les types'
                        : 'Année ${situations.academicYearLabel}';

                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const ParentSectionTitle('Type de frais'),
                        ParentFeeTypeChips(
                          feeTypes: situations.feeTypes,
                          selectedFeeTypeId: selectedFeeTypeId,
                          onChanged: (id) =>
                              ref.read(selectedFeeTypeIdProvider.notifier).state = id,
                        ),
                        const SizedBox(height: 14),
                        ParentPaymentSummaryCard(
                          summary: summary,
                          title: title,
                          subtitle: subtitle,
                        ),
                        const SizedBox(height: 12),
                        ParentInstallmentTimeline(feeType: timelineFee),
                        if (selectedFee != null) ...[
                          const SizedBox(height: 12),
                          ParentFeeInstallmentsCard(feeType: selectedFee),
                        ] else ...[
                          const SizedBox(height: 12),
                          for (final fee in situations.feeTypes) ...[
                            _FeeTypeOverviewTile(
                              fee: fee,
                              onTap: () => ref
                                  .read(selectedFeeTypeIdProvider.notifier)
                                  .state = fee.feeTypeId,
                            ),
                            const SizedBox(height: 8),
                          ],
                        ],
                      ],
                    );
                  },
                ),
                const SizedBox(height: 18),
                ParentSectionTitle(
                  'Historique des paiements',
                  action: paymentsAsync.maybeWhen(
                    data: (payments) => TextButton.icon(
                      onPressed: payments.isEmpty
                          ? null
                          : () => _exportZip(context, ref, payments),
                      icon: const Icon(Icons.folder_zip_outlined, size: 18),
                      label: const Text('ZIP'),
                    ),
                    orElse: () => null,
                  ),
                ),
                ParentPaymentsSearchBar(
                  query: searchQuery,
                  selectedPeriod: periodFilter,
                  onQueryChanged: (v) =>
                      ref.read(parentPaymentsSearchQueryProvider.notifier).state = v,
                  onPeriodChanged: (v) =>
                      ref.read(parentPaymentsPeriodFilterProvider.notifier).state = v,
                ),
                const SizedBox(height: 12),
                paymentsAsync.when(
                  loading: () => const Center(child: CircularProgressIndicator()),
                  error: (e, _) => Text('Erreur : $e'),
                  data: (payments) {
                    final filtered = filterParentPayments(
                      payments: payments,
                      feeTypeId: selectedFeeTypeId,
                      query: searchQuery,
                      periodDays: periodFilter,
                    );
                    if (filtered.isEmpty) {
                      return const ErpCard(
                        child: Text('Aucun paiement enregistré.'),
                      );
                    }
                    return Column(
                      children: [
                        for (final p in filtered) ...[
                          ParentPaymentTile(
                            payment: p,
                            onViewReceipt: () => _openReceipt(context, ref, p),
                            onDownloadPdf: () => _openReceipt(context, ref, p),
                          ),
                          const SizedBox(height: 10),
                        ],
                      ],
                    );
                  },
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _FeeTypeOverviewTile extends StatelessWidget {
  const _FeeTypeOverviewTile({required this.fee, required this.onTap});

  final ParentFeeTypeSituation fee;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      padding: const EdgeInsets.all(14),
      child: InkWell(
        onTap: onTap,
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    fee.feeTypeName,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                      color: ErpColors.navy,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'À payer ${fee.amountExpected.toStringAsFixed(0)} ${fee.currencyLabel}'
                    ' · Payé ${fee.amountPaid.toStringAsFixed(0)} ${fee.currencyLabel}'
                    ' · Reste ${fee.balance.toStringAsFixed(0)} ${fee.currencyLabel}',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                  ),
                ],
              ),
            ),
            Icon(
              fee.isInOrder ? Icons.check_circle : Icons.chevron_right,
              color: fee.isInOrder ? ErpColors.success : ErpColors.textSecondary,
            ),
          ],
        ),
      ),
    );
  }
}
