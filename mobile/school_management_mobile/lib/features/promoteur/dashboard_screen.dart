import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/date_symbol_data_local.dart';

import '../../core/api/api_error_message.dart';
import '../../core/auth/auth_storage.dart';
import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../router/app_router.dart';
import 'models/promoteur_dashboard_models.dart';
import 'widgets/promoteur_dashboard_widgets.dart';

class PromoteurDashboardScreen extends ConsumerStatefulWidget {
  const PromoteurDashboardScreen({super.key});

  @override
  ConsumerState<PromoteurDashboardScreen> createState() => _PromoteurDashboardScreenState();
}

class _PromoteurDashboardScreenState extends ConsumerState<PromoteurDashboardScreen> {
  PromoterDashboardOverview? _data;
  bool _loading = true;
  String? _error;
  String? _userName;
  bool _localeReady = false;
  String? _selectedFeeTypeId;

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

  Future<void> _load({bool force = true}) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final repo = ref.read(promoteurDashboardRepositoryProvider);
      if (force) repo.invalidateCache();
      final data = await repo.getOverview(
        forceRefresh: force,
        feeTypeId: _selectedFeeTypeId,
      );
      if (!mounted) return;
      setState(() {
        _data = data;
        _selectedFeeTypeId ??= data.selectedFeeTypeId;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = resolveDashboardErrorMessage(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _onFeeTypeChanged(String? feeTypeId) async {
    if (feeTypeId == null || feeTypeId == _selectedFeeTypeId) return;
    setState(() => _selectedFeeTypeId = feeTypeId);
    await _load(force: true);
  }

  void _openPayments(String scope) {
    final fee = _selectedFeeTypeId ?? _data?.selectedFeeTypeId;
    final feeQ = fee == null || fee.isEmpty ? '' : '&feeTypeId=$fee';
    context.push('/promoteur/payments?scope=$scope$feeQ');
  }

  void _openRevenueDetail(String scope) {
    final fee = _selectedFeeTypeId ?? _data?.selectedFeeTypeId;
    final currency = _data?.currency ?? 'CDF';
    final feeQ = fee == null || fee.isEmpty ? '' : '&feeTypeId=$fee';
    // Routes dédiées : jamais /promoteur/payments (liste élèves).
    final path = scope.toLowerCase() == 'year'
        ? '/promoteur/recette-annee'
        : '/promoteur/recette-mois';
    context.push(
      '$path?currency=${Uri.encodeComponent(currency)}$feeQ',
    );
  }

  void _openExpenses(String scope, {String? category}) {
    final q = category == null ? '' : '&category=${Uri.encodeComponent(category)}';
    context.push('/promoteur/expenses?scope=$scope$q');
  }

  void _openDebtors() {
    final fee = _selectedFeeTypeId ?? _data?.selectedFeeTypeId;
    final q = fee == null || fee.isEmpty ? '' : '?feeTypeId=$fee';
    context.push('/promoteur/debtors$q');
  }

  void _openFund(FundAllocationShare fund) => context.push(
        '/promoteur/funds/${fund.destinationId}?name=${Uri.encodeComponent(fund.name)}',
      );

  void _openStudents() => context.push('/promoteur/students');

  void _onAlert(DashboardAlert alert) {
    final hint = alert.actionHint;
    if (hint == 'debtors' || hint == 'receivables') {
      _openDebtors();
    } else if (hint == 'payments_today') {
      _openPayments('Today');
    } else if (hint == 'expenses_month') {
      _openExpenses('Month');
    } else if (hint == 'expenses_today') {
      _openExpenses('Today');
    }
  }

  @override
  Widget build(BuildContext context) {
    final data = _data;
    final currency = data?.currency ?? 'CDF';

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      body: SafeArea(
        child: !_localeReady || (_loading && data == null)
            ? const PromoterSkeleton()
            : RefreshIndicator(
                color: ErpColors.primary,
                onRefresh: () => _load(force: true),
                child: CustomScrollView(
                  physics: const AlwaysScrollableScrollPhysics(parent: BouncingScrollPhysics()),
                  slivers: [
                    SliverToBoxAdapter(
                      child: _Header(
                        userName: _userName ?? 'Promoteur',
                        schoolName: data?.schoolName ?? 'Établissement',
                        schoolLogoUrl: data?.schoolLogoUrl,
                        apiBaseUrl: ref.watch(apiClientProvider).baseUrl,
                        onLogout: () => logout(ref, context),
                      ),
                    ),
                    if (_error != null)
                      SliverPadding(
                        padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                        sliver: SliverToBoxAdapter(
                          child: Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: ErpColors.danger.withValues(alpha: 0.08),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: Text(_error!, style: const TextStyle(color: ErpColors.danger, fontSize: 12)),
                          ),
                        ),
                      ),
                    if (data != null) ...[
                      SliverPadding(
                        padding: const EdgeInsets.symmetric(horizontal: 16),
                        sliver: SliverToBoxAdapter(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              PilotCard(
                                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                                child: Row(
                                  children: [
                                    const Icon(Icons.payments_outlined, size: 18, color: ErpColors.primary),
                                    const SizedBox(width: 8),
                                    const Text(
                                      'Frais suivi',
                                      style: TextStyle(fontWeight: FontWeight.w700, fontSize: 12, color: ErpColors.navy),
                                    ),
                                    const SizedBox(width: 12),
                                    Expanded(
                                      child: DropdownButtonHideUnderline(
                                        child: DropdownButton<String>(
                                          isExpanded: true,
                                          value: data.availableFeeTypes.any((f) => f.id == (_selectedFeeTypeId ?? data.selectedFeeTypeId))
                                              ? (_selectedFeeTypeId ?? data.selectedFeeTypeId)
                                              : (data.availableFeeTypes.isEmpty ? null : data.availableFeeTypes.first.id),
                                          hint: Text(data.selectedFeeTypeName, overflow: TextOverflow.ellipsis),
                                          items: data.availableFeeTypes
                                              .map(
                                                (f) => DropdownMenuItem(
                                                  value: f.id,
                                                  child: Text('${f.name} (${f.currency})', overflow: TextOverflow.ellipsis),
                                                ),
                                              )
                                              .toList(),
                                          onChanged: _loading ? null : _onFeeTypeChanged,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              const SizedBox(height: 8),
                              const PilotSectionTitle('Indicateurs principaux', subtitle: 'Touchez une carte pour le détail'),
                              Row(
                                children: [
                                  Expanded(
                                    child: KpiMoneyCard(
                                      icon: Icons.today_rounded,
                                      label: data.kpis.todayRevenue.label,
                                      amount: data.kpis.todayRevenue.amount,
                                      currency: currency,
                                      changePercent: data.kpis.todayRevenue.changePercent,
                                      comparisonLabel: data.kpis.todayRevenue.comparisonLabel,
                                      accent: ErpColors.primary,
                                      onTap: () => _openPayments('Today'),
                                    ),
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: KpiMoneyCard(
                                      icon: Icons.calendar_month_rounded,
                                      label: data.kpis.monthRevenue.label,
                                      amount: data.kpis.monthRevenue.amount,
                                      currency: currency,
                                      changePercent: data.kpis.monthRevenue.changePercent,
                                      comparisonLabel: data.kpis.monthRevenue.comparisonLabel,
                                      accent: const Color(0xFF06B6D4),
                                      onTap: () => _openRevenueDetail('Month'),
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 10),
                              Row(
                                children: [
                                  Expanded(
                                    child: KpiMoneyCard(
                                      icon: Icons.insights_rounded,
                                      label: data.kpis.yearRevenue.label,
                                      amount: data.kpis.yearRevenue.amount,
                                      currency: currency,
                                      changePercent: data.kpis.yearRevenue.changePercent,
                                      comparisonLabel: data.kpis.yearRevenue.comparisonLabel,
                                      accent: ErpColors.success,
                                      onTap: () => _openRevenueDetail('Year'),
                                    ),
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: KpiStudentsCard(
                                      students: data.kpis.students,
                                      onTap: _openStudents,
                                    ),
                                  ),
                                ],
                              ),
                              const PilotSectionTitle('Évolution des recettes'),
                              RevenueLineChartCard(
                                title: '30 derniers jours',
                                points: data.dailyRevenueLast30Days,
                                currency: currency,
                              ),
                              const SizedBox(height: 12),
                              RevenueLineChartCard(
                                title: 'Année scolaire (mensuel)',
                                points: data.monthlyRevenueSchoolYear,
                                currency: currency,
                                color: const Color(0xFF0B1F47),
                              ),
                              const PilotSectionTitle('Dépenses'),
                              ExpenseSummaryCard(
                                expenses: data.expenses,
                                currency: currency,
                                onOpenScope: _openExpenses,
                                onCategoryTap: (c) => _openExpenses('Year', category: c.name),
                              ),
                              const PilotSectionTitle(
                                'Répartition des recettes',
                                subtitle: 'Comptes liés au frais suivi — J-1, J et dépenses',
                              ),
                              FundAllocationList(
                                funds: data.fundAllocations,
                                currency: currency,
                                onTap: _openFund,
                              ),
                              if (data.withholdings.isNotEmpty) ...[
                                const PilotSectionTitle(
                                  'Retenues',
                                  subtitle: 'Retenues appliquées sur le frais suivi',
                                ),
                                WithholdingsList(items: data.withholdings, currency: currency),
                              ],
                              const PilotSectionTitle(
                                'Situation financière',
                                subtitle: 'Recettes du frais suivi − dépenses de l’année scolaire',
                              ),
                              SituationHeroCard(situation: data.situation, currency: currency),
                              const PilotSectionTitle(
                                'Créances',
                                subtitle: 'À percevoir / Débiteurs = échéances dépassées · En ordre / Recouvrement = année',
                              ),
                              ReceivablesGrid(
                                receivables: data.receivables,
                                currency: currency,
                                onRemaining: _openDebtors,
                                onDebtors: _openDebtors,
                                onPaid: _openDebtors,
                                onRecovery: _openDebtors,
                              ),
                              const PilotSectionTitle('Alertes'),
                              AlertsList(alerts: data.alerts, onTap: _onAlert),
                              const SizedBox(height: 28),
                            ],
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

class _Header extends StatefulWidget {
  const _Header({
    required this.userName,
    required this.schoolName,
    this.schoolLogoUrl,
    required this.apiBaseUrl,
    required this.onLogout,
  });

  final String userName;
  final String schoolName;
  final String? schoolLogoUrl;
  final String apiBaseUrl;
  final VoidCallback onLogout;

  @override
  State<_Header> createState() => _HeaderState();
}

class _HeaderState extends State<_Header> {
  String? _token;

  @override
  void initState() {
    super.initState();
    AuthStorage.accessToken.then((t) {
      if (mounted) setState(() => _token = t);
    });
  }

  @override
  Widget build(BuildContext context) {
    final logoUrl = widget.schoolLogoUrl;
    final fullLogoUrl = (logoUrl == null || logoUrl.isEmpty)
        ? null
        : (logoUrl.startsWith('http') ? logoUrl : '${widget.apiBaseUrl.replaceAll(RegExp(r'/$'), '')}$logoUrl');

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
      child: Row(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(14),
            child: Container(
              width: 48,
              height: 48,
              color: Colors.white,
              child: fullLogoUrl == null || _token == null
                  ? Container(
                      decoration: BoxDecoration(
                        gradient: const LinearGradient(colors: [ErpColors.navy, ErpColors.primary]),
                        borderRadius: BorderRadius.circular(14),
                      ),
                      child: const Icon(Icons.school_rounded, color: Colors.white, size: 24),
                    )
                  : Image.network(
                      fullLogoUrl,
                      fit: BoxFit.contain,
                      headers: {'Authorization': 'Bearer $_token'},
                      errorBuilder: (_, __, ___) => Container(
                        decoration: BoxDecoration(
                          gradient: const LinearGradient(colors: [ErpColors.navy, ErpColors.primary]),
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: const Icon(Icons.school_rounded, color: Colors.white, size: 24),
                      ),
                    ),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Centre de pilotage',
                  style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800, color: ErpColors.navy),
                ),
                Text(
                  widget.schoolName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                ),
                Text(
                  'Bonjour, ${widget.userName}',
                  style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                ),
              ],
            ),
          ),
          PopupMenuButton<String>(
            onSelected: (v) {
              if (v == 'logout') widget.onLogout();
            },
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'logout', child: Text('Déconnexion')),
            ],
          ),
        ],
      ),
    );
  }
}
