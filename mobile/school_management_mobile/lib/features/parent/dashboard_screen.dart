import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_error_message.dart';
import '../../core/auth/auth_storage.dart';
import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../core/widgets/erp_widgets.dart';
import 'models/parent_models.dart';
import 'offline/parent_offline_cache.dart';
import 'parent_providers.dart';
import 'parent_shell_screen.dart';
import 'premium/dashboard_insights.dart';
import 'widgets/parent_async_widgets.dart';
import 'widgets/parent_dashboard_insight_cards.dart';
import 'widgets/parent_ui_widgets.dart';

class ParentDashboardScreen extends ConsumerStatefulWidget {
  const ParentDashboardScreen({super.key});

  @override
  ConsumerState<ParentDashboardScreen> createState() =>
      _ParentDashboardScreenState();
}

class _ParentDashboardScreenState extends ConsumerState<ParentDashboardScreen> {
  String _parentName = 'Parent';
  String _schoolName = 'Établissement';

  @override
  void initState() {
    super.initState();
    _loadHeader();
  }

  Future<void> _loadHeader() async {
    final name = await AuthStorage.userName;
    String? school;
    try {
      school = await ref.read(parentAccountRepositoryProvider).getSchoolName();
    } catch (_) {}
    if (!mounted) return;
    setState(() {
      _parentName = name ?? 'Parent';
      _schoolName = school ?? 'Établissement';
    });
  }

  Future<void> _refresh() async {
    ref.invalidate(parentChildrenProvider);
    ref.invalidate(parentSubscriptionProvider);
    final child = ref.read(selectedChildProvider);
    if (child != null) {
      final id = child.studentId;
      ref.invalidate(parentPaymentsProvider(id));
      ref.invalidate(parentPaymentSummaryProvider(id));
      ref.invalidate(parentFeeSituationsProvider(id));
      ref.invalidate(parentChildPhotoProvider(id));
      ref.invalidate(parentGradesProvider(id));
      ref.invalidate(parentAttendanceProvider(id));
      ref.invalidate(parentCommunicationsProvider(id));
    }
    await _loadHeader();
  }

  Future<void> _openReceipt(BuildContext context, ParentPayment payment) async {
    try {
      await ref.read(parentRepositoryProvider).openPaymentReceipt(payment);
    } catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Impossible d\'ouvrir le reçu : $e')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final childrenAsync = ref.watch(parentChildrenProvider);
    final subscriptionAsync = ref.watch(parentSubscriptionProvider);
    final selected = ref.watch(selectedChildProvider);

    ref.listen(parentChildrenProvider, (_, next) {
      next.whenData((children) => ensureChildSelected(ref, children));
    });

    final titleSchool = selected?.schoolName?.trim().isNotEmpty == true
        ? selected!.schoolName!
        : _schoolName;

    return Scaffold(
      appBar: AppBar(
        title: Text(titleSchool),
        actions: [
          IconButton(
            tooltip: 'Présences',
            onPressed: () => context.push('/parent/attendance'),
            icon: const Icon(Icons.event_available_outlined),
          ),
          IconButton(
            tooltip: 'Abonnement',
            onPressed: () => context.push('/parent/subscription'),
            icon: const Icon(Icons.workspace_premium_outlined),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: childrenAsync.when(
          loading: () => const ParentSkeletonList(itemCount: 5),
          error: (e, _) => ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(ErpSpacing.page),
            children: [
              ParentErrorState(
                message: resolveDashboardErrorMessage(e),
                onRetry: _refresh,
              ),
            ],
          ),
          data: (children) {
            if (children.isEmpty) {
              return ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(ErpSpacing.page),
                children: const [
                  ParentEmptyState(
                    title: 'Aucun enfant associé',
                    subtitle:
                        'Aucun enfant n’est lié à ce compte pour le moment.',
                    icon: Icons.family_restroom_outlined,
                  ),
                ],
              );
            }

            final studentId = selected?.studentId ?? children.first.studentId;
            final paymentsAsync = ref.watch(parentPaymentsProvider(studentId));
            final photoAsync = ref.watch(parentChildPhotoProvider(studentId));
            final gradesAsync = ref.watch(parentGradesProvider(studentId));
            final attendanceAsync = ref.watch(parentAttendanceProvider(studentId));
            final communicationsAsync = ref.watch(parentCommunicationsProvider(studentId));
            final feeSituationsAsync = ref.watch(parentFeeSituationsProvider(studentId));
            final isPremium = subscriptionAsync.valueOrNull?.isActive ?? false;
            final activeChild = selected ?? children.first;
            final cacheHits = ref.watch(parentOfflineCacheHitsProvider);
            final showOffline = parentHasOfflineCacheHit(cacheHits, [
              ParentCacheKeys.children(),
              ParentCacheKeys.payments(studentId),
              ParentCacheKeys.paymentSummary(studentId),
              ParentCacheKeys.feeSituations(studentId),
              ParentCacheKeys.grades(studentId),
              ParentCacheKeys.attendance(studentId),
              ParentCacheKeys.communications(studentId),
            ]);

            final gradesInsight = ParentGradesInsight.fromOverview(
              gradesAsync.valueOrNull ?? ParentGradesOverview.empty(),
            );
            final attendanceInsight = ParentAttendanceInsight.fromDays(
              attendanceAsync.valueOrNull ?? const [],
            );
            final communicationsInsight = ParentCommunicationsInsight.fromItems(
              (communicationsAsync.valueOrNull ?? const [])
                  .map(
                    (i) => i.copyWith(
                      isRead: i.isRead ||
                          ref.watch(parentReadCommunicationIdsProvider).contains(i.id),
                    ),
                  )
                  .toList(),
            );
            final nextDueInsight = ParentNextDueInsight.fromFeeSituations(
              feeSituationsAsync.valueOrNull ?? ParentFeeSituations.empty,
            );

            return ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
              children: [
                ParentOfflineBanner(visible: showOffline),
                ParentHeaderCard(
                  parentName: _parentName,
                  schoolName: activeChild.schoolName ?? titleSchool,
                  child: activeChild,
                  photoBytes: photoAsync.valueOrNull,
                ),
                const SizedBox(height: 16),
                ParentChildSelector(
                  children: children,
                  selectedId: studentId,
                  onChanged: (id) {
                    ref.read(selectedChildIdProvider.notifier).state = id;
                  },
                ),
                if (children.isNotEmpty) const SizedBox(height: 12),
                feeSituationsAsync.when(
                  loading: () => const ErpCard(
                    child: SizedBox(
                      height: 80,
                      child: Center(child: CircularProgressIndicator()),
                    ),
                  ),
                  error: (_, __) => const SizedBox.shrink(),
                  data: (situations) {
                    final defaultId = situations.resolveDefaultFeeTypeId();
                    ParentFeeTypeSituation? defaultFee;
                    if (defaultId != null) {
                      for (final fee in situations.feeTypes) {
                        if (fee.feeTypeId == defaultId) {
                          defaultFee = fee;
                          break;
                        }
                      }
                    }
                    return ParentPaymentSummaryCard(
                      summary:
                          defaultFee?.asSummary ?? situations.overallSummary,
                      title: defaultFee?.feeTypeName ??
                          'Situation des paiements',
                      subtitle: 'Année ${situations.academicYearLabel}',
                    );
                  },
                ),
                const SizedBox(height: 16),
                ParentDashboardInsightCards(
                  grades: gradesInsight,
                  attendance: attendanceInsight,
                  communications: communicationsInsight,
                  nextDue: nextDueInsight,
                  onOpenGrades: () => context.push('/parent/notes'),
                  onOpenAttendance: () => context.push('/parent/attendance'),
                  onOpenCommunications: () => context.push('/parent/communications'),
                  onOpenPayments: () => goParentBranch(context, 1),
                ),
                const SizedBox(height: 16),
                ParentSectionTitle(
                  'Derniers paiements',
                  action: TextButton(
                    onPressed: () => goParentBranch(context, 1),
                    child: const Text('Voir tout'),
                  ),
                ),
                paymentsAsync.when(
                  loading: () => const Center(child: CircularProgressIndicator()),
                  error: (_, __) => const Text('Historique indisponible.'),
                  data: (payments) {
                    if (payments.isEmpty) {
                      return const ErpCard(
                        child: Text('Aucun paiement enregistré pour cet enfant.'),
                      );
                    }
                    final recent = payments.take(3).toList();
                    return Column(
                      children: [
                        for (final p in recent) ...[
                          ParentPaymentTile(
                            payment: p,
                            onViewReceipt: () => _openReceipt(context, p),
                            onDownloadPdf: () => _openReceipt(context, p),
                          ),
                          const SizedBox(height: 10),
                        ],
                      ],
                    );
                  },
                ),
                if (!isPremium) ...[
                  const SizedBox(height: 8),
                  ParentUnlockBanner(
                    onActivate: () => context.push('/parent/subscription'),
                  ),
                ],
                const SizedBox(height: 16),
                ParentSectionTitle('Accès rapide'),
                Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: [
                    _QuickAccessTile(
                      icon: Icons.school_outlined,
                      label: 'Notes',
                      locked: !(subscriptionAsync.valueOrNull?.features.notes ?? false),
                      onTap: () => context.push('/parent/notes'),
                      onLockedTap: () => context.push('/parent/subscription'),
                    ),
                    _QuickAccessTile(
                      icon: Icons.description_outlined,
                      label: 'Bulletins',
                      locked: !(subscriptionAsync.valueOrNull?.features.bulletins ?? false),
                      onTap: () => context.push('/parent/bulletins'),
                      onLockedTap: () => context.push('/parent/subscription'),
                    ),
                    _QuickAccessTile(
                      icon: Icons.forum_outlined,
                      label: 'Messages',
                      locked: !(subscriptionAsync.valueOrNull?.features.communications ?? false),
                      onTap: () => goParentBranch(context, 3),
                      onLockedTap: () => context.push('/parent/subscription'),
                    ),
                    _QuickAccessTile(
                      icon: Icons.event_available_outlined,
                      label: 'Présences',
                      locked: !(subscriptionAsync.valueOrNull?.features.attendance ?? false),
                      onTap: () => context.push('/parent/attendance'),
                      onLockedTap: () => context.push('/parent/subscription'),
                    ),
                  ],
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _QuickAccessTile extends StatelessWidget {
  const _QuickAccessTile({
    required this.icon,
    required this.label,
    required this.onTap,
    required this.onLockedTap,
    this.locked = false,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final VoidCallback onLockedTap;
  final bool locked;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: const BorderSide(color: ErpColors.border),
      ),
      child: InkWell(
        onTap: locked ? onLockedTap : onTap,
        borderRadius: BorderRadius.circular(12),
        child: ConstrainedBox(
          constraints: const BoxConstraints(minWidth: 148, minHeight: ErpSpacing.minTap),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(icon, size: 20, color: ErpColors.primary),
                const SizedBox(width: 8),
                Text(
                  label,
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 13,
                    color: ErpColors.textPrimary,
                  ),
                ),
                if (locked) ...[
                  const SizedBox(width: 8),
                  const ErpLockChip(compact: true),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
