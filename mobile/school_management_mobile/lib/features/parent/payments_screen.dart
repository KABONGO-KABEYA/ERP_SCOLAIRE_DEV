import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../core/widgets/erp_widgets.dart';
import 'models/parent_models.dart';
import 'offline/parent_offline_cache.dart';
import 'parent_providers.dart';
import 'widgets/parent_async_widgets.dart';
import 'widgets/parent_payments_premium_widgets.dart';
import 'widgets/parent_ui_widgets.dart';

/// Sentinel : l'utilisateur a choisi « Tous » (≠ sélection pas encore initialisée).
const _kAllFeeTypes = '__all__';

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
                  loading: () => const ErpLoadingState(),
                  error: (e, _) => ErpErrorState(
                    message: 'Erreur situation : $e',
                    onRetry: () =>
                        ref.invalidate(parentFeeSituationsProvider(studentId)),
                  ),
                  data: (situations) {
                    final fees = situations.feeTypes
                        .where((f) => f.feeTypeName.trim().isNotEmpty)
                        .toList();
                    if (fees.isEmpty) {
                      return const ErpEmptyState(
                        title: 'Aucun type de frais',
                        description:
                            'Aucun type de frais applicable pour cet enfant.',
                        icon: Icons.payments_outlined,
                      );
                    }

                    ParentFeeTypeSituation? selectedFee;
                    final wantsAll = selectedFeeTypeId == _kAllFeeTypes;
                    if (!wantsAll && selectedFeeTypeId != null) {
                      for (final fee in fees) {
                        if (fee.feeTypeId == selectedFeeTypeId) {
                          selectedFee = fee;
                          break;
                        }
                      }
                    }

                    // Première ouverture / enfant changé : frais principal école.
                    final defaultId = situations.resolveDefaultFeeTypeId();
                    if (!wantsAll &&
                        selectedFee == null &&
                        selectedFeeTypeId == null &&
                        defaultId != null) {
                      for (final fee in fees) {
                        if (fee.feeTypeId == defaultId) {
                          selectedFee = fee;
                          break;
                        }
                      }
                      WidgetsBinding.instance.addPostFrameCallback((_) {
                        if (!context.mounted) return;
                        if (ref.read(selectedFeeTypeIdProvider) == null) {
                          ref.read(selectedFeeTypeIdProvider.notifier).state =
                              defaultId;
                        }
                      });
                    }

                    final summary =
                        selectedFee?.asSummary ?? situations.overallSummary;
                    final chipSelectedId = wantsAll
                        ? null
                        : (selectedFee?.feeTypeId ?? selectedFeeTypeId);

                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const ParentSectionTitle('Type de frais'),
                        ParentFeeTypeChips(
                          feeTypes: fees,
                          selectedFeeTypeId: chipSelectedId,
                          showAll: true,
                          onChanged: (id) {
                            ref.read(selectedFeeTypeIdProvider.notifier).state =
                                id ?? _kAllFeeTypes;
                          },
                        ),
                        const SizedBox(height: 14),
                        ParentPaymentSummaryCard(
                          summary: summary,
                          title: selectedFee?.feeTypeName ??
                              'Situation des paiements',
                          subtitle: selectedFee == null
                              ? 'Année ${situations.academicYearLabel} · tous les types'
                              : 'Année ${situations.academicYearLabel}',
                          showProgress: true,
                        ),
                        if (selectedFee != null &&
                            selectedFee.installments.isNotEmpty) ...[
                          const SizedBox(height: 12),
                          ParentInstallmentTimeline(feeType: selectedFee),
                        ],
                        if (selectedFee == null) ...[
                          const SizedBox(height: 16),
                          const ParentSectionTitle('Situations de frais'),
                          for (final fee in fees) ...[
                            _FeeSituationCard(
                              fee: fee,
                              selected: false,
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
                const SizedBox(height: 20),
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
                  loading: () => const ErpLoadingState(),
                  error: (e, _) => ErpErrorState(message: 'Erreur : $e'),
                  data: (payments) {
                    final filtered = filterParentPayments(
                      payments: payments,
                      feeTypeId: (selectedFeeTypeId == null ||
                              selectedFeeTypeId == _kAllFeeTypes)
                          ? null
                          : selectedFeeTypeId,
                      query: searchQuery,
                      periodDays: periodFilter,
                    );
                    if (filtered.isEmpty) {
                      return const ErpEmptyState(
                        title: 'Aucun paiement',
                        description: 'Aucun paiement enregistré pour ce filtre.',
                        icon: Icons.receipt_long_outlined,
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
                          const SizedBox(height: 8),
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

class _FeeSituationCard extends StatelessWidget {
  const _FeeSituationCard({
    required this.fee,
    required this.selected,
    required this.onTap,
  });

  final ParentFeeTypeSituation fee;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final fmt = NumberFormat('#,##0.##');
    final remainingColor =
        fee.balance > 0 ? ErpColors.danger : ErpColors.success;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: ErpColors.card,
            borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
            border: Border.all(
              color: selected ? ErpColors.primary : ErpColors.border,
              width: selected ? 1.5 : 1,
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      fee.feeTypeName,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w700,
                        color: ErpColors.navy,
                      ),
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: (fee.isInOrder ? ErpColors.success : ErpColors.warning)
                          .withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(20),
                    ),
                    child: Text(
                      fee.isInOrder ? 'En ordre' : 'À régler',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: fee.isInOrder ? ErpColors.success : ErpColors.warning,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: _AmountCol(
                      label: 'Dû',
                      value: '${fmt.format(fee.amountExpected)} ${fee.currencyLabel}',
                    ),
                  ),
                  Expanded(
                    child: _AmountCol(
                      label: 'Payé',
                      value: '${fmt.format(fee.amountPaid)} ${fee.currencyLabel}',
                      valueColor: ErpColors.success,
                    ),
                  ),
                  Expanded(
                    child: _AmountCol(
                      label: 'Reste',
                      value: '${fmt.format(fee.balance)} ${fee.currencyLabel}',
                      valueColor: remainingColor,
                      alignEnd: true,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _AmountCol extends StatelessWidget {
  const _AmountCol({
    required this.label,
    required this.value,
    this.valueColor,
    this.alignEnd = false,
  });

  final String label;
  final String value;
  final Color? valueColor;
  final bool alignEnd;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment:
          alignEnd ? CrossAxisAlignment.end : CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
        ),
        const SizedBox(height: 2),
        Text(
          value,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w700,
            color: valueColor ?? ErpColors.textPrimary,
          ),
        ),
      ],
    );
  }
}
