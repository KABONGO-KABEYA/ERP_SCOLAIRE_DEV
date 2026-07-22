import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import 'dashboard_formatters.dart';
import 'models/promoteur_dashboard_models.dart';
import 'promoteur_dashboard_repository.dart';

DashboardDetailScope _parseScope(String? raw) => switch (raw?.toLowerCase()) {
      'today' => DashboardDetailScope.today,
      'year' => DashboardDetailScope.year,
      _ => DashboardDetailScope.month,
    };

class PromoteurPaymentsDetailScreen extends ConsumerStatefulWidget {
  const PromoteurPaymentsDetailScreen({super.key, required this.scope});

  final String scope;

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
      final items = await ref.read(promoteurDashboardRepositoryProvider).getPayments(_scope);
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
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: _items?.length ?? 0,
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
      var items = await ref.read(promoteurDashboardRepositoryProvider).getExpenses(_scope);
      final cat = widget.category;
      if (cat != null && cat.isNotEmpty) {
        items = items.where((e) => e.category.toLowerCase() == cat.toLowerCase()).toList();
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

  @override
  Widget build(BuildContext context) {
    final title = widget.category == null || widget.category!.isEmpty
        ? 'Dépenses — ${_scope.label}'
        : '${widget.category} — ${_scope.label}';
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
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: _items?.length ?? 0,
                    separatorBuilder: (_, __) => const SizedBox(height: 8),
                    itemBuilder: (context, i) {
                      final e = _items![i];
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
                                  Text(e.label, style: const TextStyle(fontWeight: FontWeight.w700)),
                                  const SizedBox(height: 2),
                                  Text(
                                    '${e.category} · ${e.expenseDate.toIso8601String().split('T').first}',
                                    style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                                  ),
                                ],
                              ),
                            ),
                            Text(
                              formatMoney(e.amount, e.currency),
                              style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.danger),
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
        title: Text(data == null ? 'À percevoir' : 'À percevoir — ${data.feeTypeName}'),
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
                            data.academicYearLabel,
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
                          if (data.debtors.isNotEmpty) ...[
                            const SizedBox(height: 18),
                            Text(
                              'Élèves débiteurs (${data.debtors.length})',
                              style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15, color: ErpColors.navy),
                            ),
                            const SizedBox(height: 8),
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
                                        '${d.className} · Payé ${formatMoney(d.amountPaid, currency)} / ${formatMoney(d.amountDue, currency)}',
                                        style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                            ),
                          ],
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

class PromoteurStudentsDetailScreen extends ConsumerStatefulWidget {
  const PromoteurStudentsDetailScreen({super.key});

  @override
  ConsumerState<PromoteurStudentsDetailScreen> createState() => _PromoteurStudentsDetailScreenState();
}

class _PromoteurStudentsDetailScreenState extends ConsumerState<PromoteurStudentsDetailScreen> {
  EnrolledStudentsBySection? _data;
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
      final data = await ref.read(promoteurDashboardRepositoryProvider).getEnrolledStudents();
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
    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Élèves inscrits'),
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
                          Row(
                            children: [
                              Expanded(child: _StatTile(label: 'Total', value: '${data.totalStudents}', color: ErpColors.navy)),
                              const SizedBox(width: 8),
                              Expanded(child: _StatTile(label: 'Garçons', value: '${data.totalBoys}', color: ErpColors.primary)),
                              const SizedBox(width: 8),
                              Expanded(child: _StatTile(label: 'Filles', value: '${data.totalGirls}', color: const Color(0xFFEC4899))),
                            ],
                          ),
                          const SizedBox(height: 16),
                          ...data.sections.map((section) {
                            return Padding(
                              padding: const EdgeInsets.only(bottom: 14),
                              child: Container(
                                decoration: BoxDecoration(
                                  color: Colors.white,
                                  borderRadius: BorderRadius.circular(14),
                                  border: Border.all(color: ErpColors.border),
                                ),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.stretch,
                                  children: [
                                    Padding(
                                      padding: const EdgeInsets.fromLTRB(14, 12, 14, 8),
                                      child: Row(
                                        children: [
                                          Expanded(
                                            child: Text(
                                              section.sectionName,
                                              style: const TextStyle(
                                                fontWeight: FontWeight.w800,
                                                fontSize: 15,
                                                color: ErpColors.navy,
                                              ),
                                            ),
                                          ),
                                          Text(
                                            '${section.totalStudents} · ♂ ${section.boys} · ♀ ${section.girls}',
                                            style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                                          ),
                                        ],
                                      ),
                                    ),
                                    SingleChildScrollView(
                                      scrollDirection: Axis.horizontal,
                                      child: DataTable(
                                        headingRowHeight: 36,
                                        dataRowMinHeight: 36,
                                        dataRowMaxHeight: 44,
                                        columnSpacing: 20,
                                        horizontalMargin: 14,
                                        headingTextStyle: const TextStyle(
                                          fontSize: 11,
                                          fontWeight: FontWeight.w700,
                                          color: ErpColors.textSecondary,
                                        ),
                                        columns: const [
                                          DataColumn(label: Text('Classe')),
                                          DataColumn(label: Text('Total'), numeric: true),
                                          DataColumn(label: Text('Filles'), numeric: true),
                                          DataColumn(label: Text('Garçons'), numeric: true),
                                        ],
                                        rows: [
                                          ...section.classes.map(
                                            (c) => DataRow(
                                              cells: [
                                                DataCell(Text(c.className, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12))),
                                                DataCell(Text('${c.totalStudents}', style: const TextStyle(fontSize: 12))),
                                                DataCell(Text('${c.girls}', style: const TextStyle(fontSize: 12))),
                                                DataCell(Text('${c.boys}', style: const TextStyle(fontSize: 12))),
                                              ],
                                            ),
                                          ),
                                          DataRow(
                                            color: WidgetStatePropertyAll(ErpColors.primary.withValues(alpha: 0.06)),
                                            cells: [
                                              const DataCell(Text('Total section', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12))),
                                              DataCell(Text('${section.totalStudents}', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 12))),
                                              DataCell(Text('${section.girls}', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 12))),
                                              DataCell(Text('${section.boys}', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 12))),
                                            ],
                                          ),
                                        ],
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            );
                          }),
                        ],
                      ),
                    ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({required this.label, required this.value, required this.color});

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
          const SizedBox(height: 4),
          Text(value, style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: color)),
        ],
      ),
    );
  }
}
