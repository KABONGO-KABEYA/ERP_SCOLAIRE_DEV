import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../router/app_router.dart';
import 'dashboard_formatters.dart';
import 'models/promoteur_dashboard_models.dart';
import 'promoteur_dashboard_repository.dart';
import 'widgets/promoteur_dashboard_widgets.dart';

class PromoteurDashboardScreen extends ConsumerStatefulWidget {
  const PromoteurDashboardScreen({super.key});

  @override
  ConsumerState<PromoteurDashboardScreen> createState() => _PromoteurDashboardScreenState();
}

class _PromoteurDashboardScreenState extends ConsumerState<PromoteurDashboardScreen> {
  DashboardPeriod _period = DashboardPeriod.month;
  RevenueGranularity _granularity = RevenueGranularity.daily;
  PromoterDashboardOverview? _data;
  bool _loading = true;
  String? _error;
  String? _userName;
  bool _localeReady = false;

  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  Future<void> _bootstrap() async {
    await initializeDateFormatting('fr_FR');
    if (!mounted) return;
    setState(() => _localeReady = true);
    currentUserName().then((name) {
      if (mounted) setState(() => _userName = name);
    });
    await _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await ref.read(promoteurDashboardRepositoryProvider).getOverview(
            period: _period,
            granularity: _granularity,
          );
      if (!mounted) return;
      setState(() => _data = data);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _onPeriodChanged(DashboardPeriod period) async {
    setState(() => _period = period);
    await _load();
  }

  Future<void> _onGranularityChanged(RevenueGranularity granularity) async {
    setState(() => _granularity = granularity);
    await _load();
  }

  @override
  Widget build(BuildContext context) {
    final data = _data;
    final currency = data?.currency ?? 'CDF';
    final summary = data?.summary;

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      body: SafeArea(
        child: !_localeReady || (_loading && data == null)
            ? const PromoterSkeleton()
            : RefreshIndicator(
                color: ErpColors.primary,
                onRefresh: _load,
                child: CustomScrollView(
                  physics: const AlwaysScrollableScrollPhysics(parent: BouncingScrollPhysics()),
                  slivers: [
                    SliverToBoxAdapter(child: _Header(
                      userName: _userName ?? 'Promoteur',
                      schoolName: data?.schoolName ?? 'Établissement',
                      onLogout: () => logout(ref, context),
                    )),
                    SliverPadding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                      sliver: SliverToBoxAdapter(
                        child: PromoterPeriodSelector(
                          value: _period,
                          onChanged: _onPeriodChanged,
                        ),
                      ),
                    ),
                    if (_error != null)
                      SliverPadding(
                        padding: const EdgeInsets.symmetric(horizontal: 16),
                        sliver: SliverToBoxAdapter(
                          child: Container(
                            margin: const EdgeInsets.only(bottom: 12),
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: ErpColors.danger.withValues(alpha: 0.08),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: Text(_error!, style: const TextStyle(color: ErpColors.danger)),
                          ),
                        ),
                      ),
                    if (summary != null)
                      SliverPadding(
                        padding: const EdgeInsets.symmetric(horizontal: 16),
                        sliver: SliverToBoxAdapter(
                          child: Column(
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: PromoterStatCard(
                                      icon: Icons.payments_rounded,
                                      title: summary.periodRevenueLabel,
                                      value: formatMoney(summary.periodRevenue, currency),
                                      changePercent: summary.periodRevenueChangePercent,
                                      accent: ErpColors.primary,
                                    ),
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: PromoterStatCard(
                                      icon: Icons.calendar_month_rounded,
                                      title: summary.secondaryRevenueLabel,
                                      value: formatMoney(summary.secondaryRevenue, currency),
                                      changePercent: summary.secondaryRevenueChangePercent,
                                      accent: ErpColors.navy,
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 10),
                              Row(
                                children: [
                                  Expanded(
                                    child: PromoterStatCard(
                                      icon: Icons.school_rounded,
                                      title: 'Inscriptions',
                                      value: '${summary.newEnrollments}',
                                      subtitle: '${summary.activeStudents} élèves actifs',
                                      accent: ErpColors.success,
                                    ),
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: PromoterStatCard(
                                      icon: Icons.insights_rounded,
                                      title: 'Taux de réalisation',
                                      value: '${summary.realizationRate.toStringAsFixed(0)} %',
                                      accent: ErpColors.warning,
                                      child: ClipRRect(
                                        borderRadius: BorderRadius.circular(999),
                                        child: LinearProgressIndicator(
                                          value: (summary.realizationRate / 100).clamp(0, 1),
                                          minHeight: 7,
                                          backgroundColor: ErpColors.border,
                                          color: ErpColors.success,
                                        ),
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ),
                    if (data != null) ...[
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(16, 14, 16, 0),
                        sliver: SliverToBoxAdapter(
                          child: PromoterRevenueChart(
                            points: data.revenueSeries,
                            currency: currency,
                            granularity: _granularity,
                            onGranularityChanged: _onGranularityChanged,
                          ),
                        ),
                      ),
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(16, 14, 16, 0),
                        sliver: SliverToBoxAdapter(
                          child: PromoterDonutChart(
                            shares: data.feeTypeShares,
                            currency: currency,
                          ),
                        ),
                      ),
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(16, 14, 16, 0),
                        sliver: SliverToBoxAdapter(
                          child: PromoterFundAllocationList(
                            items: data.fundAllocations,
                            currency: currency,
                            onSeeAll: () {
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(content: Text('Détail des répartitions — bientôt disponible.')),
                              );
                            },
                          ),
                        ),
                      ),
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(16, 14, 16, 0),
                        sliver: SliverToBoxAdapter(
                          child: PromoterActivitiesList(
                            activities: data.recentActivities,
                            currency: currency,
                          ),
                        ),
                      ),
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(16, 14, 16, 0),
                        sliver: SliverToBoxAdapter(
                          child: PromoterAlertsList(alerts: data.alerts),
                        ),
                      ),
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(16, 14, 16, 0),
                        sliver: SliverToBoxAdapter(
                          child: PromoterTopClassesChart(
                            items: data.topClasses,
                            currency: currency,
                          ),
                        ),
                      ),
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(16, 14, 16, 0),
                        sliver: SliverToBoxAdapter(
                          child: PromoterTopFeeTypes(items: data.topFeeTypes),
                        ),
                      ),
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(16, 14, 16, 28),
                        sliver: SliverToBoxAdapter(
                          child: PromoterQuickStatsGrid(
                            stats: data.quickStats,
                            currency: currency,
                          ),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({
    required this.userName,
    required this.schoolName,
    required this.onLogout,
  });

  final String userName;
  final String schoolName;
  final VoidCallback onLogout;

  @override
  Widget build(BuildContext context) {
    final first = userName.trim().isEmpty ? 'P' : userName.trim()[0].toUpperCase();

    return Container(
      margin: const EdgeInsets.fromLTRB(16, 8, 16, 14),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [ErpColors.navy, ErpColors.primary],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(22),
        boxShadow: [
          BoxShadow(
            color: ErpColors.navy.withValues(alpha: 0.25),
            blurRadius: 18,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Bonjour, $userName',
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  schoolName,
                  style: TextStyle(
                    color: Colors.white.withValues(alpha: 0.9),
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  formatLongDate(DateTime.now()),
                  style: TextStyle(
                    color: Colors.white.withValues(alpha: 0.75),
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
          IconButton(
            onPressed: () {},
            icon: const Icon(Icons.notifications_none_rounded, color: Colors.white),
          ),
          const SizedBox(width: 4),
          PopupMenuButton<String>(
            onSelected: (v) {
              if (v == 'logout') onLogout();
            },
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'logout', child: Text('Déconnexion')),
            ],
            child: CircleAvatar(
              radius: 22,
              backgroundColor: Colors.white.withValues(alpha: 0.2),
              child: Text(
                first,
                style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 18),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
