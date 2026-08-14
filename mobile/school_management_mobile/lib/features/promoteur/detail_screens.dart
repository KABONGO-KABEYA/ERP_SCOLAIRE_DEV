import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:intl/intl.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../admin/daf_student_analytics_screens.dart';
import 'dashboard_formatters.dart';
import 'models/promoteur_dashboard_models.dart';
import 'promoteur_dashboard_repository.dart';
import 'widgets/promoteur_dashboard_widgets.dart';

DashboardDetailScope _parseScope(String? raw) => switch (raw?.toLowerCase()) {
      'today' => DashboardDetailScope.today,
      'year' => DashboardDetailScope.year,
      _ => DashboardDetailScope.month,
    };

class PromoteurPaymentsDetailScreen extends ConsumerStatefulWidget {
  const PromoteurPaymentsDetailScreen({super.key, required this.scope, this.feeTypeId});

  final String scope;
  final String? feeTypeId;

  @override
  ConsumerState<PromoteurPaymentsDetailScreen> createState() => _PromoteurPaymentsDetailScreenState();
}

class _PromoteurPaymentsDetailScreenState extends ConsumerState<PromoteurPaymentsDetailScreen> {
  late final DashboardDetailScope _scope = _parseScope(widget.scope);
  List<DashboardPaymentLine>? _items;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final items = await ref.read(promoteurDashboardRepositoryProvider).getPayments(
            _scope,
            feeTypeId: widget.feeTypeId,
          );
      if (!mounted) return;
      setState(() => _items = items);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final dateFmt = DateFormat('dd/MM/yyyy HH:mm');
    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: Text('Encaissements — ${_scope.label}'),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: ErpColors.danger)))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: (_items == null || _items!.isEmpty)
                      ? ListView(
                          physics: const AlwaysScrollableScrollPhysics(),
                          children: const [
                            SizedBox(height: 120),
                            Center(child: Text('Aucun encaissement sur cette période.')),
                          ],
                        )
                      : ListView.separated(
                          padding: const EdgeInsets.all(16),
                          itemCount: _items!.length,
                          separatorBuilder: (_, __) => const SizedBox(height: 8),
                          itemBuilder: (context, i) {
                            final p = _items![i];
                            return Container(
                              padding: const EdgeInsets.all(14),
                              decoration: BoxDecoration(
                                color: Colors.white,
                                borderRadius: BorderRadius.circular(14),
                              ),
                              child: Row(
                                children: [
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Text(p.studentName, style: const TextStyle(fontWeight: FontWeight.w700)),
                                        const SizedBox(height: 2),
                                        Text(
                                          '${p.reference} · ${dateFmt.format(p.paymentDateUtc.toLocal())}',
                                          style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                                        ),
                                      ],
                                    ),
                                  ),
                                  Text(
                                    formatMoney(p.amount, p.currency),
                                    style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.navy),
                                  ),
                                ],
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}

/// Recette du mois = jours avec perception ; Recette annuelle = mois avec perception.
class PromoteurRevenueDetailScreen extends ConsumerStatefulWidget {
  const PromoteurRevenueDetailScreen({
    super.key,
    required this.scope,
    this.feeTypeId,
    this.currency = 'CDF',
  });

  final String scope;
  final String? feeTypeId;
  final String currency;

  @override
  ConsumerState<PromoteurRevenueDetailScreen> createState() => _PromoteurRevenueDetailScreenState();
}

class _PromoteurRevenueDetailScreenState extends ConsumerState<PromoteurRevenueDetailScreen> {
  late final DashboardDetailScope _scope = _parseScope(widget.scope);
  List<RevenuePoint>? _points;
  String? _error;
  bool _loading = true;

  bool get _isYear => _scope == DashboardDetailScope.year;

  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  Future<void> _bootstrap() async {
    await initializeDateFormatting('fr_FR');
    if (!mounted) return;
    await _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final points = await ref.read(promoteurDashboardRepositoryProvider).getRevenueDetail(
            _scope,
            feeTypeId: widget.feeTypeId,
          );
      if (!mounted) return;
      // API filtre déjà les zéros ; garde-fou affichage.
      setState(() => _points = points.where((p) => p.amount > 0).toList());
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final pointsAsc = _points ?? const <RevenuePoint>[];
    final pointsDesc = pointsAsc.reversed.toList();
    final total = pointsAsc.fold<double>(0, (s, p) => s + p.amount);
    final currency = widget.currency;
    final title = _isYear ? 'Recette annuelle' : 'Recette du mois';
    final detailTitle = _isYear ? 'Perceptions par mois' : 'Perceptions par jour';
    final chartTitle = _isYear ? 'Recettes mensuelles' : 'Recettes journalières';

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: Text(title),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: ErpColors.danger)))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
                    children: [
                      Container(
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: Row(
                          children: [
                            Expanded(
                              child: Text(
                                _isYear ? 'Total année scolaire' : 'Total du mois',
                                style: const TextStyle(fontWeight: FontWeight.w600, color: ErpColors.textSecondary),
                              ),
                            ),
                            Text(
                              formatMoney(total, currency),
                              style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: ErpColors.navy),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 14),
                      if (pointsAsc.isEmpty)
                        Container(
                          width: double.infinity,
                          padding: const EdgeInsets.all(24),
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(14),
                          ),
                          child: Text(
                            _isYear
                                ? 'Aucune perception enregistrée sur l’année scolaire.'
                                : 'Aucune perception enregistrée ce mois-ci.',
                            textAlign: TextAlign.center,
                            style: const TextStyle(color: ErpColors.textSecondary),
                          ),
                        )
                      else ...[
                        RevenueLineChartCard(
                          title: chartTitle,
                          points: pointsAsc,
                          currency: currency,
                          color: const Color(0xFF0B1F47),
                        ),
                        const SizedBox(height: 16),
                        Text(
                          detailTitle,
                          style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: ErpColors.navy),
                        ),
                        const SizedBox(height: 8),
                        ...pointsDesc.map(
                          (p) => _RevenuePeriodTile(
                            label: p.label,
                            amount: p.amount,
                            currency: currency,
                            detailLabel: _isYear ? 'Total perçu ce mois' : 'Total perçu ce jour',
                            accent: ErpColors.primary,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
    );
  }
}

class _RevenuePeriodTile extends StatelessWidget {
  const _RevenuePeriodTile({
    required this.label,
    required this.amount,
    required this.currency,
    required this.detailLabel,
    required this.accent,
  });

  final String label;
  final double amount;
  final String currency;
  final String detailLabel;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: const BorderSide(color: ErpColors.border),
      ),
      child: Theme(
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          initiallyExpanded: false,
          controlAffinity: ListTileControlAffinity.leading,
          tilePadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
          childrenPadding: const EdgeInsets.fromLTRB(10, 0, 10, 10),
          title: Text(
            label,
            style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.navy),
          ),
          trailing: Text(
            formatMoney(amount, currency),
            style: TextStyle(fontWeight: FontWeight.w800, color: accent),
          ),
          children: [
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: ErpColors.border),
              ),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      detailLabel,
                      style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13),
                    ),
                  ),
                  Text(
                    formatMoney(amount, currency),
                    style: TextStyle(fontWeight: FontWeight.w800, color: accent, fontSize: 13),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class PromoteurExpensesDetailScreen extends ConsumerStatefulWidget {
  const PromoteurExpensesDetailScreen({super.key, required this.scope, this.category});

  final String scope;
  final String? category;

  @override
  ConsumerState<PromoteurExpensesDetailScreen> createState() => _PromoteurExpensesDetailScreenState();
}

class _PromoteurExpensesDetailScreenState extends ConsumerState<PromoteurExpensesDetailScreen> {
  late final DashboardDetailScope _scope = _parseScope(widget.scope);
  List<DashboardExpenseLine>? _items;
  String? _error;
  bool _loading = true;
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
    await _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      var items = await ref.read(promoteurDashboardRepositoryProvider).getExpenses(_scope);
      var cat = widget.category;
      if (cat != null && cat.isNotEmpty) {
        items = items
            .where((e) => e.accountTypeName.toLowerCase() == cat.toLowerCase())
            .toList();
      }
      if (!mounted) return;
      setState(() => _items = items);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  String _dayKey(DateTime d) => DateFormat('yyyy-MM-dd').format(d);
  String _monthKey(DateTime d) => DateFormat('yyyy-MM').format(d);
  String _dayLabel(DateTime d) => DateFormat('dd/MM/yyyy').format(d);
  String _monthLabel(DateTime d) {
    final raw = DateFormat('MMMM', 'fr_FR').format(d);
    return raw.isEmpty ? raw : '${raw[0].toUpperCase()}${raw.substring(1)}';
  }

  String _dayLabelShort(DateTime d) => DateFormat('dd/MM').format(d);

  @override
  Widget build(BuildContext context) {
    final title = widget.category == null || widget.category!.isEmpty
        ? 'Dépenses — ${_scope.label}'
        : '${widget.category} — ${_scope.label}';
    final items = _items ?? const <DashboardExpenseLine>[];
    final total = items.fold<double>(0, (s, e) => s + e.amount);
    final currency = items.isEmpty ? 'CDF' : items.first.currency;
    final filteredByAccount = widget.category != null && widget.category!.isNotEmpty;

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: Text(title),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
      ),
      body: !_localeReady || _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: ErpColors.danger)))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
                    children: [
                      Container(
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: Row(
                          children: [
                            const Expanded(
                              child: Text(
                                'Total',
                                style: TextStyle(fontWeight: FontWeight.w600, color: ErpColors.textSecondary),
                              ),
                            ),
                            Text(
                              formatMoney(total, currency),
                              style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.danger),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 12),
                      if (items.isEmpty)
                        Container(
                          width: double.infinity,
                          padding: const EdgeInsets.all(28),
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(14),
                          ),
                          child: const Text(
                            'Aucune dépense enregistrée sur cette période.',
                            textAlign: TextAlign.center,
                            style: TextStyle(color: ErpColors.textSecondary),
                          ),
                        )
                      else if (filteredByAccount)
                        ..._buildPeriodHierarchy(items)
                      else
                        ..._buildAccountTypeHierarchy(items),
                    ],
                  ),
                ),
    );
  }

  List<Widget> _buildAccountTypeHierarchy(List<DashboardExpenseLine> items) {
    final byAccount = <String, List<DashboardExpenseLine>>{};
    for (final e in items) {
      byAccount.putIfAbsent(e.accountTypeName, () => []).add(e);
    }
    final accountKeys = byAccount.keys.toList()..sort();

    return accountKeys.map((account) {
      final accountItems = byAccount[account]!;
      final accountTotal = accountItems.fold<double>(0, (s, e) => s + e.amount);
      return Card(
        margin: const EdgeInsets.only(bottom: 10),
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: const BorderSide(color: ErpColors.border),
        ),
        child: Theme(
          data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
          child: ExpansionTile(
            initiallyExpanded: false,
            controlAffinity: ListTileControlAffinity.leading,
            tilePadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            childrenPadding: const EdgeInsets.fromLTRB(8, 0, 8, 10),
            title: Text(
              account,
              style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.navy),
            ),
            trailing: Text(
              formatMoney(accountTotal, accountItems.first.currency),
              style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.danger),
            ),
            children: _buildPeriodHierarchy(accountItems),
          ),
        ),
      );
    }).toList();
  }

  List<Widget> _buildPeriodHierarchy(List<DashboardExpenseLine> items) {
    if (_scope == DashboardDetailScope.year) {
      return _buildYearHierarchy(items);
    }
    if (_scope == DashboardDetailScope.month) {
      return _buildMonthHierarchy(items);
    }
    return items
        .map((e) => Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: _ExpenseDetailTile(expense: e),
            ))
        .toList();
  }

  List<Widget> _buildYearHierarchy(List<DashboardExpenseLine> items) {
    final byMonth = <String, List<DashboardExpenseLine>>{};
    for (final e in items) {
      byMonth.putIfAbsent(_monthKey(e.expenseDate), () => []).add(e);
    }
    final monthKeys = byMonth.keys.toList()..sort((a, b) => b.compareTo(a));

    return monthKeys.map((monthKey) {
      final monthItems = byMonth[monthKey]!;
      monthItems.sort((a, b) => b.expenseDate.compareTo(a.expenseDate));
      final monthTotal = monthItems.fold<double>(0, (s, e) => s + e.amount);
      final byDay = <String, List<DashboardExpenseLine>>{};
      for (final e in monthItems) {
        byDay.putIfAbsent(_dayKey(e.expenseDate), () => []).add(e);
      }
      final dayKeys = byDay.keys.toList()..sort((a, b) => b.compareTo(a));

      return Card(
        margin: const EdgeInsets.only(bottom: 10),
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: const BorderSide(color: ErpColors.border),
        ),
        child: Theme(
          data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
          child: ExpansionTile(
            initiallyExpanded: false,
            controlAffinity: ListTileControlAffinity.leading,
            tilePadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            childrenPadding: const EdgeInsets.fromLTRB(8, 0, 8, 10),
            title: Text(
              _monthLabel(monthItems.first.expenseDate),
              style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.navy),
            ),
            trailing: Text(
              formatMoney(monthTotal, monthItems.first.currency),
              style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.danger),
            ),
            children: dayKeys.map((dayKey) {
              final dayItems = byDay[dayKey]!;
              final dayTotal = dayItems.fold<double>(0, (s, e) => s + e.amount);
              return Card(
                margin: const EdgeInsets.only(bottom: 6),
                elevation: 0,
                color: ErpColors.pageBackground,
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                child: ExpansionTile(
                  initiallyExpanded: false,
                  controlAffinity: ListTileControlAffinity.leading,
                  tilePadding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
                  childrenPadding: const EdgeInsets.fromLTRB(8, 0, 8, 8),
                  title: Text(
                    _dayLabelShort(dayItems.first.expenseDate),
                    style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                  ),
                  trailing: Text(
                    formatMoney(dayTotal, dayItems.first.currency),
                    style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.danger, fontSize: 13),
                  ),
                  children: dayItems
                      .map((e) => Padding(
                            padding: const EdgeInsets.only(bottom: 6),
                            child: _ExpenseDetailTile(expense: e),
                          ))
                      .toList(),
                ),
              );
            }).toList(),
          ),
        ),
      );
    }).toList();
  }

  List<Widget> _buildMonthHierarchy(List<DashboardExpenseLine> items) {
    final byDay = <String, List<DashboardExpenseLine>>{};
    for (final e in items) {
      byDay.putIfAbsent(_dayKey(e.expenseDate), () => []).add(e);
    }
    final dayKeys = byDay.keys.toList()..sort((a, b) => b.compareTo(a));

    return dayKeys.map((dayKey) {
      final dayItems = byDay[dayKey]!;
      final dayTotal = dayItems.fold<double>(0, (s, e) => s + e.amount);
      return Card(
        margin: const EdgeInsets.only(bottom: 10),
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: const BorderSide(color: ErpColors.border),
        ),
        child: Theme(
          data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
          child: ExpansionTile(
            initiallyExpanded: false,
            controlAffinity: ListTileControlAffinity.leading,
            tilePadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            childrenPadding: const EdgeInsets.fromLTRB(10, 0, 10, 10),
            title: Text(
              _dayLabel(dayItems.first.expenseDate),
              style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.navy),
            ),
            trailing: Text(
              formatMoney(dayTotal, dayItems.first.currency),
              style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.danger),
            ),
            children: dayItems
                .map((e) => Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: _ExpenseDetailTile(expense: e),
                    ))
                .toList(),
          ),
        ),
      );
    }).toList();
  }
}

class _ExpenseDetailTile extends StatelessWidget {
  const _ExpenseDetailTile({required this.expense});

  final DashboardExpenseLine expense;

  @override
  Widget build(BuildContext context) {
    final dateFmt = DateFormat('dd/MM/yyyy');
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: ErpColors.border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(expense.label, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
                const SizedBox(height: 2),
                Text(
                  dateFmt.format(expense.expenseDate),
                  style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                ),
                const SizedBox(height: 2),
                Text(
                  expense.accountTypeName,
                  style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary, fontWeight: FontWeight.w600),
                ),
                if (expense.reference.isNotEmpty)
                  Text(
                    expense.reference,
                    style: const TextStyle(fontSize: 10, color: ErpColors.textSecondary),
                  ),
              ],
            ),
          ),
          Text(
            formatMoney(expense.amount, expense.currency),
            style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.danger, fontSize: 13),
          ),
        ],
      ),
    );
  }
}

class PromoteurDebtorsDetailScreen extends ConsumerStatefulWidget {
  const PromoteurDebtorsDetailScreen({super.key, this.feeTypeId});

  final String? feeTypeId;

  @override
  ConsumerState<PromoteurDebtorsDetailScreen> createState() => _PromoteurDebtorsDetailScreenState();
}

class _PromoteurDebtorsDetailScreenState extends ConsumerState<PromoteurDebtorsDetailScreen> {
  FeeReceivablesBreakdown? _data;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await ref
          .read(promoteurDashboardRepositoryProvider)
          .getReceivablesBreakdown(feeTypeId: widget.feeTypeId);
      if (!mounted) return;
      setState(() => _data = data);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final data = _data;
    final currency = data?.currency ?? 'CDF';

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: Text(data == null ? 'Débiteurs' : 'Débiteurs — ${data.feeTypeName}'),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: ErpColors.danger)))
              : data == null
                  ? const Center(child: Text('Aucune donnée'))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView(
                        padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
                        children: [
                          Text(
                            '${data.academicYearLabel} · Tranches dont l’échéance est dépassée',
                            style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                          ),
                          const SizedBox(height: 10),
                          _ReceivablesTotalsCard(
                            expected: data.totalExpected,
                            paid: data.totalPaid,
                            remaining: data.totalRemaining,
                            currency: currency,
                          ),
                          const SizedBox(height: 18),
                          const Text(
                            'Par tranche',
                            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: ErpColors.navy),
                          ),
                          const SizedBox(height: 8),
                          _InstallmentTable(rows: data.byInstallment, currency: currency),
                          const SizedBox(height: 18),
                          const Text(
                            'Par compte de répartition',
                            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: ErpColors.navy),
                          ),
                          const SizedBox(height: 8),
                          _DestinationTable(rows: data.byDestination, currency: currency),
                          const SizedBox(height: 18),
                          Text(
                            data.debtors.isEmpty
                                ? 'Aucun élève en retard d’échéance'
                                : 'Élèves débiteurs (${data.debtors.length})',
                            style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: ErpColors.navy),
                          ),
                          const SizedBox(height: 8),
                          if (data.debtors.isEmpty)
                            Container(
                              width: double.infinity,
                              padding: const EdgeInsets.all(14),
                              decoration: BoxDecoration(
                                color: Colors.white,
                                borderRadius: BorderRadius.circular(14),
                              ),
                              child: const Text(
                                'Aucun élève avec une tranche échue non soldée.',
                                style: TextStyle(color: ErpColors.textSecondary),
                              ),
                            )
                          else
                            ...data.debtors.map(
                              (d) => Padding(
                                padding: const EdgeInsets.only(bottom: 8),
                                child: Container(
                                  padding: const EdgeInsets.all(14),
                                  decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(14),
                                  ),
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Row(
                                        children: [
                                          Expanded(
                                            child: Text(d.studentName, style: const TextStyle(fontWeight: FontWeight.w700)),
                                          ),
                                          Text(
                                            formatMoney(d.remaining, currency),
                                            style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.warning),
                                          ),
                                        ],
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        '${d.className} · Retard ${formatMoney(d.remaining, currency)}'
                                        ' (payé ${formatMoney(d.amountPaid, currency)} / ${formatMoney(d.amountDue, currency)})',
                                        style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                            ),
                        ],
                      ),
                    ),
    );
  }
}

class _ReceivablesTotalsCard extends StatelessWidget {
  const _ReceivablesTotalsCard({
    required this.expected,
    required this.paid,
    required this.remaining,
    required this.currency,
  });

  final double expected;
  final double paid;
  final double remaining;
  final String currency;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        boxShadow: [
          BoxShadow(color: ErpColors.navy.withValues(alpha: 0.06), blurRadius: 12, offset: const Offset(0, 4)),
        ],
      ),
      child: Row(
        children: [
          Expanded(child: _TotalCell(label: 'Attendu', value: formatMoney(expected, currency))),
          Expanded(child: _TotalCell(label: 'Perçu', value: formatMoney(paid, currency), color: ErpColors.success)),
          Expanded(child: _TotalCell(label: 'Reste', value: formatMoney(remaining, currency), color: ErpColors.warning)),
        ],
      ),
    );
  }
}

class _TotalCell extends StatelessWidget {
  const _TotalCell({required this.label, required this.value, this.color});

  final String label;
  final String value;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
        const SizedBox(height: 4),
        Text(value, style: TextStyle(fontWeight: FontWeight.w800, fontSize: 13, color: color ?? ErpColors.navy)),
      ],
    );
  }
}

class _InstallmentTable extends StatelessWidget {
  const _InstallmentTable({required this.rows, required this.currency});

  final List<FeeInstallmentReceivable> rows;
  final String currency;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) {
      return const _EmptyTableHint('Aucune tranche tarifaire pour ce frais.');
    }

    return _ScrollableDataTable(
      columns: const ['Tranche', 'Attendu', 'Perçu', 'Reste'],
      rows: [
        ...rows.map(
          (r) => [
            r.installmentName,
            formatMoney(r.amountExpected, currency),
            formatMoney(r.amountPaid, currency),
            formatMoney(r.remaining, currency),
          ],
        ),
        [
          'Total',
          formatMoney(rows.fold<double>(0, (s, r) => s + r.amountExpected), currency),
          formatMoney(rows.fold<double>(0, (s, r) => s + r.amountPaid), currency),
          formatMoney(rows.fold<double>(0, (s, r) => s + r.remaining), currency),
        ],
      ],
      emphasizeLastRow: true,
    );
  }
}

class _DestinationTable extends StatelessWidget {
  const _DestinationTable({required this.rows, required this.currency});

  final List<FeeDestinationReceivable> rows;
  final String currency;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) {
      return const _EmptyTableHint('Aucun compte de répartition configuré.');
    }

    return _ScrollableDataTable(
      columns: const ['Compte', '%', 'Attendu', 'Encaissé', 'Reste'],
      rows: [
        ...rows.map(
          (r) => [
            r.destinationName,
            '${r.percentage.toStringAsFixed(r.percentage == r.percentage.roundToDouble() ? 0 : 1)} %',
            formatMoney(r.amountExpected, currency),
            formatMoney(r.amountCollected, currency),
            formatMoney(r.remaining, currency),
          ],
        ),
        [
          'Total',
          '100 %',
          formatMoney(rows.fold<double>(0, (s, r) => s + r.amountExpected), currency),
          formatMoney(rows.fold<double>(0, (s, r) => s + r.amountCollected), currency),
          formatMoney(rows.fold<double>(0, (s, r) => s + r.remaining), currency),
        ],
      ],
      emphasizeLastRow: true,
    );
  }
}

class _EmptyTableHint extends StatelessWidget {
  const _EmptyTableHint(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(text, style: const TextStyle(color: ErpColors.textSecondary)),
    );
  }
}

class _ScrollableDataTable extends StatelessWidget {
  const _ScrollableDataTable({
    required this.columns,
    required this.rows,
    this.emphasizeLastRow = false,
  });

  final List<String> columns;
  final List<List<String>> rows;
  final bool emphasizeLastRow;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: ErpColors.border),
      ),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          headingRowHeight: 40,
          dataRowMinHeight: 40,
          dataRowMaxHeight: 48,
          columnSpacing: 18,
          horizontalMargin: 12,
          headingTextStyle: const TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.w700,
            color: ErpColors.textSecondary,
          ),
          columns: columns.map((c) => DataColumn(label: Text(c))).toList(),
          rows: [
            for (var i = 0; i < rows.length; i++)
              DataRow(
                color: emphasizeLastRow && i == rows.length - 1
                    ? WidgetStatePropertyAll(ErpColors.primary.withValues(alpha: 0.06))
                    : null,
                cells: [
                  for (var j = 0; j < rows[i].length; j++)
                    DataCell(
                      Text(
                        rows[i][j],
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: (j == 0 || (emphasizeLastRow && i == rows.length - 1))
                              ? FontWeight.w700
                              : FontWeight.w500,
                          color: j == rows[i].length - 1 ? ErpColors.warning : ErpColors.textPrimary,
                        ),
                      ),
                    ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

class PromoteurFundMovementsScreen extends ConsumerStatefulWidget {
  const PromoteurFundMovementsScreen({
    super.key,
    required this.destinationId,
    required this.name,
  });

  final String destinationId;
  final String name;

  @override
  ConsumerState<PromoteurFundMovementsScreen> createState() => _PromoteurFundMovementsScreenState();
}

class _PromoteurFundMovementsScreenState extends ConsumerState<PromoteurFundMovementsScreen> {
  List<DashboardFundMovement>? _items;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final items = await ref.read(promoteurDashboardRepositoryProvider).getFundMovements(widget.destinationId);
      if (!mounted) return;
      setState(() => _items = items);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final dateFmt = DateFormat('dd/MM/yyyy HH:mm');
    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: Text(widget.name),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: ErpColors.danger)))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: _items?.length ?? 0,
                    separatorBuilder: (_, __) => const SizedBox(height: 8),
                    itemBuilder: (context, i) {
                      final m = _items![i];
                      return Container(
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: Row(
                          children: [
                            Expanded(
                              child: Text(
                                dateFmt.format(m.allocatedAtUtc.toLocal()),
                                style: const TextStyle(fontWeight: FontWeight.w600),
                              ),
                            ),
                            Text(
                              formatMoney(m.amount, m.currency),
                              style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.primary),
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                ),
    );
  }
}

class PromoteurStudentsDetailScreen extends StatelessWidget {
  const PromoteurStudentsDetailScreen({super.key});

  @override
  Widget build(BuildContext context) => const EnrolledStudentsAnalyticsScreen();
}
