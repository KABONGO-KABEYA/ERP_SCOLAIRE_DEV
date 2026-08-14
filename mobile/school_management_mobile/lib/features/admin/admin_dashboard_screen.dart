import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_error_message.dart';
import '../../core/auth/auth_storage.dart';
import '../../core/auth/permission_policy.dart';
import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../router/app_router.dart';
import '../direction/models/direction_models.dart';
import '../promoteur/models/promoteur_dashboard_models.dart';
import '../promoteur/widgets/promoteur_dashboard_widgets.dart';

class AdminDashboardScreen extends ConsumerStatefulWidget {
  const AdminDashboardScreen({super.key});

  @override
  ConsumerState<AdminDashboardScreen> createState() => _AdminDashboardScreenState();
}

class _AdminDashboardScreenState extends ConsumerState<AdminDashboardScreen> {
  PromoterDashboardOverview? _overview;
  DashboardStats? _stats;
  bool _loading = true;
  String? _error;
  String? _userName;

  List<String> _permissions = [];

  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  Future<void> _bootstrap() async {
    currentUserName().then((name) {
      if (mounted) setState(() => _userName = name);
    });
    final perms = await AuthStorage.permissions;
    if (mounted) setState(() => _permissions = perms);
    await _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final overview = await ref.read(promoteurDashboardRepositoryProvider).getOverview(forceRefresh: true);
      final stats = await ref.read(directionRepositoryProvider).getDashboard();
      if (!mounted) return;
      setState(() {
        _overview = overview;
        _stats = stats;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = resolveDashboardErrorMessage(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _openStudents() => context.push('/admin/students');

  void _openPersonnel() => context.push('/admin/personnel');

  void _openCollected() {
    if (PermissionPolicy.canViewFinancialReports(_permissions)) {
      context.push('/admin/financial-reports');
      return;
    }
    context.push('/promoteur/recette-annee?currency=${Uri.encodeComponent(_overview?.currency ?? 'CDF')}');
  }

  void _openExpenses() => context.push('/admin/expenses?scope=Month');

  void _openFinancialReports() => context.push('/admin/financial-reports');

  void _openPaymentSituations() => context.push('/admin/payment-situations');

  void _openPricingCategories() => context.push('/admin/pricing-categories');

  void _openPresence() => context.push('/admin/presence');

  @override
  Widget build(BuildContext context) {
    final overview = _overview;
    final stats = _stats;
    final currency = overview?.currency ?? 'CDF';
    final totalAttendance = (overview?.quickStats.presentStudents ?? 0) + (overview?.quickStats.absentStudents ?? 0);
    final presencePct = totalAttendance == 0
        ? '—'
        : '${(100.0 * (overview?.quickStats.presentStudents ?? 0) / totalAttendance).toStringAsFixed(1)} %';

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Tableau de bord'),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        actions: [
          IconButton(icon: const Icon(Icons.refresh_rounded), onPressed: _load),
          IconButton(icon: const Icon(Icons.logout_rounded), onPressed: () => logout(ref, context)),
        ],
      ),
      body: _loading && overview == null
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  Text(
                    'Bonjour, ${_userName ?? 'Utilisateur'}',
                    style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700, color: ErpColors.navy),
                  ),
                  if (overview != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 4, bottom: 12),
                      child: Text(
                        overview.schoolName,
                        style: const TextStyle(color: ErpColors.textSecondary),
                      ),
                    ),
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Text(_error!, style: const TextStyle(color: ErpColors.danger, fontSize: 12)),
                    ),
                  const PilotSectionTitle('Indicateurs principaux', subtitle: 'Touchez une carte pour le détail'),
                  Row(
                    children: [
                      Expanded(
                        child: KpiStudentsCard(
                          students: overview?.kpis.students ??
                              const PromoterStudentsKpi(total: 0, boys: 0, girls: 0, newThisPeriod: 0),
                          onTap: _openStudents,
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: PilotCard(
                          onTap: _openPersonnel,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Icon(Icons.badge_outlined, color: ErpColors.success),
                              const SizedBox(height: 10),
                              const Text('Personnel', style: TextStyle(fontSize: 11, color: ErpColors.textSecondary, fontWeight: FontWeight.w600)),
                              Text('${stats?.totalTeachers ?? 0}', style: const TextStyle(fontSize: 28, fontWeight: FontWeight.w700, color: ErpColors.navy)),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(
                        child: PilotCard(
                          onTap: _openStudents,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Icon(Icons.class_outlined, color: Color(0xFF1E3A8A)),
                              const SizedBox(height: 10),
                              const Text('Classes actives', style: TextStyle(fontSize: 11, color: ErpColors.textSecondary, fontWeight: FontWeight.w600)),
                              Text('${stats?.totalClassRooms ?? 0}', style: const TextStyle(fontSize: 28, fontWeight: FontWeight.w700, color: ErpColors.navy)),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: KpiMoneyCard(
                          icon: Icons.payments_outlined,
                          label: 'Total encaissé',
                          amount: overview?.kpis.yearRevenue.amount ?? 0,
                          currency: currency,
                          changePercent: overview?.kpis.yearRevenue.changePercent ?? 0,
                          comparisonLabel: overview?.kpis.yearRevenue.comparisonLabel ?? '',
                          accent: ErpColors.primary,
                          onTap: _openCollected,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(
                        child: KpiMoneyCard(
                          icon: Icons.receipt_long_outlined,
                          label: 'Dépenses',
                          amount: overview?.expenses.month ?? 0,
                          currency: currency,
                          changePercent: 0,
                          comparisonLabel: 'Ce mois',
                          accent: ErpColors.warning,
                          onTap: _openExpenses,
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: PilotCard(
                          onTap: _openPresence,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Icon(Icons.how_to_reg_outlined, color: ErpColors.success),
                              const SizedBox(height: 10),
                              const Text('Présence', style: TextStyle(fontSize: 11, color: ErpColors.textSecondary, fontWeight: FontWeight.w600)),
                              Text(presencePct, style: const TextStyle(fontSize: 28, fontWeight: FontWeight.w700, color: ErpColors.navy)),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  const PilotSectionTitle('Module financier', subtitle: 'Consultation et pilotage'),
                  if (PermissionPolicy.canViewFinancialReports(_permissions))
                    _FinanceModuleTile(
                      icon: Icons.analytics_outlined,
                      title: 'Rapports financiers',
                      subtitle: 'Recettes, répartitions et retenues',
                      onTap: _openFinancialReports,
                    ),
                  if (PermissionPolicy.canViewFinancialReports(_permissions))
                    _FinanceModuleTile(
                      icon: Icons.account_balance_wallet_outlined,
                      title: 'Situation des paiements',
                      subtitle: 'État des soldes par section et classe',
                      onTap: _openPaymentSituations,
                    ),
                  if (PermissionPolicy.canAssignPricingCategories(_permissions))
                    _FinanceModuleTile(
                      icon: Icons.sell_outlined,
                      title: 'Catégories tarifaires',
                      subtitle: 'Attribution des catégories aux élèves',
                      onTap: _openPricingCategories,
                    ),
                  if (PermissionPolicy.canViewExpenses(_permissions))
                    _FinanceModuleTile(
                      icon: Icons.receipt_long_outlined,
                      title: 'Dépenses',
                      subtitle: 'Consultation des dépenses (lecture seule)',
                      onTap: _openExpenses,
                    ),
                ],
              ),
            ),
    );
  }
}

class _FinanceModuleTile extends StatelessWidget {
  const _FinanceModuleTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Material(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Icon(icon, color: ErpColors.primary),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(title, style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.navy)),
                      const SizedBox(height: 4),
                      Text(subtitle, style: const TextStyle(color: ErpColors.textSecondary, fontSize: 12)),
                    ],
                  ),
                ),
                const Icon(Icons.chevron_right_rounded, color: ErpColors.textSecondary),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class AdminModulePlaceholderScreen extends StatelessWidget {
  const AdminModulePlaceholderScreen({super.key, required this.title, required this.message});

  final String title;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title), backgroundColor: Colors.white, foregroundColor: ErpColors.navy),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.construction_outlined, size: 56, color: ErpColors.primary),
              const SizedBox(height: 16),
              Text(title, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700)),
              const SizedBox(height: 8),
              Text(message, textAlign: TextAlign.center, style: const TextStyle(color: ErpColors.textSecondary)),
            ],
          ),
        ),
      ),
    );
  }
}
