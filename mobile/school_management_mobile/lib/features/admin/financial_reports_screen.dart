import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/api/api_error_message.dart';
import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../enrollment/models/enrollment_models.dart';
import '../promoteur/dashboard_formatters.dart';
import 'admin_finance_models.dart';
import 'daf_student_models.dart';

enum _ReportPeriod { day, week, month, custom }

class FinancialReportsScreen extends ConsumerStatefulWidget {
  const FinancialReportsScreen({super.key});

  @override
  ConsumerState<FinancialReportsScreen> createState() => _FinancialReportsScreenState();
}

class _FinancialReportsScreenState extends ConsumerState<FinancialReportsScreen>
    with SingleTickerProviderStateMixin {
  RealizedReceiptsReport? _report;
  AllocationCashFlowReport? _allocation;
  WithholdingReport? _withholding;
  List<FeeTypeCatalogItem> _feeTypes = [];
  EnrollmentStructureOptions? _structure;
  String? _academicYearId;

  FeeTypeCatalogItem? _selectedFeeType;
  EnrollmentSection? _selectedSection;
  EnrollmentClassOption? _selectedClass;
  _ReportPeriod _period = _ReportPeriod.month;
  DateTime _fromDate = DateTime(DateTime.now().year, DateTime.now().month, 1);
  DateTime _toDate = DateTime.now();
  int _selectedMonth = DateTime.now().month;
  int _selectedYear = DateTime.now().year;

  bool _loading = true;
  bool _refreshing = false;
  String? _error;
  late TabController _tabController;

  static const _monthLabels = [
    'Janvier', 'Février', 'Mars', 'Avril', 'Mai', 'Juin',
    'Juillet', 'Août', 'Septembre', 'Octobre', 'Novembre', 'Décembre',
  ];

  bool get _hasData => _report != null;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 7, vsync: this);
    _bootstrap();
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final prereq = await ref.read(dafStudentRepositoryProvider).getPrerequisites();
      final structure = await ref.read(enrollmentRepositoryProvider).getStructureOptions();
      final catalog = await ref.read(dafStudentRepositoryProvider).getFeeCatalog();
      final active = catalog.feeTypes.where((f) => f.isActive).toList();
      if (!mounted) return;
      setState(() {
        _academicYearId = prereq.currentAcademicYearId;
        _structure = structure;
        _feeTypes = active;
        _selectedFeeType = active.isNotEmpty ? active.first : null;
      });
      _applyPeriodDates();
      await _loadReport();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = resolveDashboardErrorMessage(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _applyPeriodDates({
    _ReportPeriod? period,
    int? month,
    int? year,
    DateTime? from,
    DateTime? to,
  }) {
    final p = period ?? _period;
    final m = month ?? _selectedMonth;
    final y = year ?? _selectedYear;
    final now = DateTime.now();
    switch (p) {
      case _ReportPeriod.day:
        _fromDate = DateTime(now.year, now.month, now.day);
        _toDate = _fromDate;
      case _ReportPeriod.week:
        final weekday = now.weekday;
        _fromDate = DateTime(now.year, now.month, now.day).subtract(Duration(days: weekday - 1));
        _toDate = _fromDate.add(const Duration(days: 6));
      case _ReportPeriod.month:
        _fromDate = DateTime(y, m, 1);
        _toDate = DateTime(y, m + 1, 0);
      case _ReportPeriod.custom:
        if (from != null) _fromDate = from;
        if (to != null) _toDate = to;
    }
  }

  Future<void> _loadReport() async {
    final feeType = _selectedFeeType;
    if (feeType == null) {
      setState(() {
        _report = null;
        _allocation = null;
        _withholding = null;
        _error = 'Sélectionnez un type de frais pour afficher le rapport.';
      });
      return;
    }

    if (_hasData) {
      setState(() => _refreshing = true);
    } else {
      setState(() => _loading = true);
    }
    setState(() => _error = null);

    try {
      final fmt = DateFormat('yyyy-MM-dd');
      final from = fmt.format(_fromDate);
      final to = fmt.format(_toDate);
      final repo = ref.read(adminFinanceRepositoryProvider);
      final yearId = _academicYearId;
      final sectionId = _selectedSection?.id;
      final classId = _selectedClass?.classRoomId;
      final feeTypeId = feeType.id;

      final results = await Future.wait([
        repo.getRealizedReceipts(
          fromDate: from,
          toDate: to,
          academicYearId: yearId,
          feeTypeId: feeTypeId,
          sectionId: sectionId,
          classRoomId: classId,
        ),
        repo.getAllocationCashFlow(
          fromDate: from,
          toDate: to,
          academicYearId: yearId,
          feeTypeId: feeTypeId,
          sectionId: sectionId,
          classRoomId: classId,
        ),
        repo.getWithholdingReport(
          fromDate: from,
          toDate: to,
          academicYearId: yearId,
          feeTypeId: feeTypeId,
          sectionId: sectionId,
          classRoomId: classId,
        ),
      ]);

      if (!mounted) return;
      setState(() {
        _report = results[0] as RealizedReceiptsReport;
        _allocation = results[1] as AllocationCashFlowReport;
        _withholding = results[2] as WithholdingReport;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = resolveDashboardErrorMessage(e));
    } finally {
      if (mounted) {
        setState(() {
          _loading = false;
          _refreshing = false;
        });
      }
    }
  }

  List<EnrollmentClassOption> _filteredClassesFor(EnrollmentSection? section) {
    final structure = _structure;
    if (structure == null) return [];
    final sectionId = section?.id;
    if (sectionId == null) return structure.classes;
    return structure.classes.where((c) => c.sectionId == sectionId).toList();
  }

  List<EnrollmentClassOption> get _filteredClasses => _filteredClassesFor(_selectedSection);

  String _periodLabel(_ReportPeriod period) => switch (period) {
        _ReportPeriod.day => 'Journalier',
        _ReportPeriod.week => 'Hebdo',
        _ReportPeriod.month => 'Mensuel',
        _ReportPeriod.custom => 'Période',
      };

  List<String> _activeFilterParts() {
    final parts = <String>[];
    if (_period == _ReportPeriod.custom) {
      final fmt = DateFormat('dd/MM/yyyy');
      parts.add('${fmt.format(_fromDate)} → ${fmt.format(_toDate)}');
    } else if (_period == _ReportPeriod.month) {
      parts.add('${_monthLabels[_selectedMonth - 1]} $_selectedYear');
    } else {
      parts.add(_periodLabel(_period));
    }
    parts.add(_selectedFeeType?.name ?? 'Type de frais');
    parts.add(_selectedSection?.name ?? 'Toutes sections');
    if (_selectedClass != null) {
      parts.add(_selectedClass!.fullDisplayName);
    }
    return parts;
  }

  String _primaryCurrency(RealizedReceiptsReport report) =>
      report.byCurrency.firstOrNull?.currency ?? 'CDF';

  String _formatDate(String raw) {
    final parsed = DateTime.tryParse(raw);
    if (parsed == null) return raw;
    return DateFormat('dd/MM/yyyy').format(parsed);
  }

  void _openFilterSheet() {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (context) => _FinancialReportsFilterSheet(
        period: _period,
        selectedMonth: _selectedMonth,
        selectedYear: _selectedYear,
        fromDate: _fromDate,
        toDate: _toDate,
        feeTypes: _feeTypes,
        selectedFeeType: _selectedFeeType,
        structure: _structure,
        selectedSection: _selectedSection,
        selectedClass: _selectedClass,
        filteredClasses: _filteredClasses,
        monthLabels: _monthLabels,
        onApply: ({
          required period,
          required selectedMonth,
          required selectedYear,
          required fromDate,
          required toDate,
          required feeType,
          required section,
          required classOption,
        }) {
          setState(() {
            _period = period;
            _selectedMonth = selectedMonth;
            _selectedYear = selectedYear;
            _selectedFeeType = feeType;
            _selectedSection = section;
            _selectedClass = classOption;
          });
          _applyPeriodDates(
            period: period,
            month: selectedMonth,
            year: selectedYear,
            from: fromDate,
            to: toDate,
          );
          _loadReport();
        },
        onReset: () {
          final now = DateTime.now();
          setState(() {
            _period = _ReportPeriod.month;
            _selectedMonth = now.month;
            _selectedYear = now.year;
            _selectedFeeType = _feeTypes.isNotEmpty ? _feeTypes.first : null;
            _selectedSection = null;
            _selectedClass = null;
          });
          _applyPeriodDates();
          _loadReport();
        },
      ),
    );
  }

  Widget _buildFilterSummaryBar() {
    return Material(
      color: Colors.white,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 8, 4, 8),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Wrap(
                    spacing: 6,
                    runSpacing: 4,
                    children: _activeFilterParts()
                        .map(
                          (label) => Container(
                            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                            decoration: BoxDecoration(
                              color: const Color(0xFFF1F5F9),
                              borderRadius: BorderRadius.circular(6),
                              border: Border.all(color: ErpColors.border),
                            ),
                            child: Text(
                              label,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: ErpColors.navy),
                            ),
                          ),
                        )
                        .toList(),
                  ),
                ),
                TextButton.icon(
                  onPressed: _openFilterSheet,
                  icon: const Icon(Icons.tune_rounded, size: 18),
                  label: const Text('Filtrer'),
                  style: TextButton.styleFrom(
                    foregroundColor: ErpColors.primary,
                    padding: const EdgeInsets.symmetric(horizontal: 8),
                    visualDensity: VisualDensity.compact,
                  ),
                ),
              ],
            ),
          ),
          if (_refreshing)
            const LinearProgressIndicator(minHeight: 2, color: ErpColors.primary),
          const Divider(height: 1, color: ErpColors.border),
        ],
      ),
    );
  }

  List<Widget> _scrollHeader(RealizedReceiptsReport report) => [
        _FinCompactSummary(
          report: report,
          currency: _primaryCurrency(report),
        ),
        if (_error != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
            child: Text(_error!, style: const TextStyle(color: ErpColors.danger, fontSize: 12)),
          ),
      ];

  @override
  Widget build(BuildContext context) {
    final report = _report;

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Rapports financiers', style: TextStyle(fontSize: 17)),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
        actions: [
          IconButton(
            icon: _refreshing
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2, color: ErpColors.primary),
                  )
                : const Icon(Icons.refresh_rounded),
            onPressed: _refreshing ? null : _loadReport,
          ),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(40),
          child: TabBar(
            controller: _tabController,
            isScrollable: true,
            tabAlignment: TabAlignment.start,
            labelPadding: const EdgeInsets.symmetric(horizontal: 12),
            labelStyle: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
            unselectedLabelStyle: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
            labelColor: ErpColors.primary,
            unselectedLabelColor: ErpColors.textSecondary,
            indicatorColor: ErpColors.primary,
            indicatorWeight: 2.5,
            tabs: const [
              Tab(text: 'Détail'),
              Tab(text: 'Journalier'),
              Tab(text: 'Par classe'),
              Tab(text: 'Par section'),
              Tab(text: 'Par type'),
              Tab(text: 'Répartitions'),
              Tab(text: 'Retenues'),
            ],
          ),
        ),
      ),
      body: _loading && !_hasData
          ? const Center(child: CircularProgressIndicator())
          : Column(
              children: [
                _buildFilterSummaryBar(),
                Expanded(
                  child: report == null
                      ? Center(
                          child: Padding(
                            padding: const EdgeInsets.all(24),
                            child: Text(
                              _error ?? 'Aucune donnée',
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                color: _error != null ? ErpColors.danger : ErpColors.textSecondary,
                              ),
                            ),
                          ),
                        )
                      : TabBarView(
                          controller: _tabController,
                          children: [
                            _DetailTab(
                              report: report,
                              formatDate: _formatDate,
                              header: _scrollHeader(report),
                            ),
                            _DailyTab(
                              report: report,
                              formatDate: _formatDate,
                              currency: _primaryCurrency(report),
                              header: _scrollHeader(report),
                            ),
                            _ByClassTab(
                              report: report,
                              currency: _primaryCurrency(report),
                              header: _scrollHeader(report),
                            ),
                            _BySectionTab(
                              report: report,
                              currency: _primaryCurrency(report),
                              header: _scrollHeader(report),
                            ),
                            _ByFeeTypeTab(report: report, header: _scrollHeader(report)),
                            _AllocationTab(
                              allocation: _allocation,
                              formatDate: _formatDate,
                              header: _scrollHeader(report),
                            ),
                            _WithholdingTab(
                              withholding: _withholding,
                              formatDate: _formatDate,
                              header: _scrollHeader(report),
                            ),
                          ],
                        ),
                ),
              ],
            ),
    );
  }
}

// ---------------------------------------------------------------------------
// Filtres — BottomSheet
// ---------------------------------------------------------------------------

class _FinancialReportsFilterSheet extends StatefulWidget {
  const _FinancialReportsFilterSheet({
    required this.period,
    required this.selectedMonth,
    required this.selectedYear,
    required this.fromDate,
    required this.toDate,
    required this.feeTypes,
    required this.selectedFeeType,
    required this.structure,
    required this.selectedSection,
    required this.selectedClass,
    required this.filteredClasses,
    required this.monthLabels,
    required this.onApply,
    required this.onReset,
  });

  final _ReportPeriod period;
  final int selectedMonth;
  final int selectedYear;
  final DateTime fromDate;
  final DateTime toDate;
  final List<FeeTypeCatalogItem> feeTypes;
  final FeeTypeCatalogItem? selectedFeeType;
  final EnrollmentStructureOptions? structure;
  final EnrollmentSection? selectedSection;
  final EnrollmentClassOption? selectedClass;
  final List<EnrollmentClassOption> filteredClasses;
  final List<String> monthLabels;
  final void Function({
    required _ReportPeriod period,
    required int selectedMonth,
    required int selectedYear,
    required DateTime fromDate,
    required DateTime toDate,
    required FeeTypeCatalogItem? feeType,
    required EnrollmentSection? section,
    required EnrollmentClassOption? classOption,
  }) onApply;
  final VoidCallback onReset;

  @override
  State<_FinancialReportsFilterSheet> createState() => _FinancialReportsFilterSheetState();
}

class _FinancialReportsFilterSheetState extends State<_FinancialReportsFilterSheet> {
  late _ReportPeriod _period;
  late int _month;
  late int _year;
  late DateTime _from;
  late DateTime _to;
  FeeTypeCatalogItem? _feeType;
  EnrollmentSection? _section;
  EnrollmentClassOption? _classOption;

  @override
  void initState() {
    super.initState();
    _period = widget.period;
    _month = widget.selectedMonth;
    _year = widget.selectedYear;
    _from = widget.fromDate;
    _to = widget.toDate;
    _feeType = widget.selectedFeeType;
    _section = widget.selectedSection;
    _classOption = widget.selectedClass;
  }

  List<EnrollmentClassOption> get _classes {
    final structure = widget.structure;
    if (structure == null) return [];
    final sectionId = _section?.id;
    if (sectionId == null) return structure.classes;
    return structure.classes.where((c) => c.sectionId == sectionId).toList();
  }

  @override
  Widget build(BuildContext context) {
    final dateFmt = DateFormat('dd/MM/yyyy');
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: bottomInset),
      child: DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.82,
        minChildSize: 0.45,
        maxChildSize: 0.95,
        builder: (context, scrollController) => Column(
          children: [
            Container(
              margin: const EdgeInsets.only(top: 10, bottom: 6),
              width: 36,
              height: 4,
              decoration: BoxDecoration(
                color: ErpColors.border,
                borderRadius: BorderRadius.circular(2),
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Row(
                children: [
                  const Expanded(
                    child: Text(
                      'Filtres du rapport',
                      style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700, color: ErpColors.navy),
                    ),
                  ),
                  IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.pop(context),
                  ),
                ],
              ),
            ),
            Expanded(
              child: ListView(
                controller: scrollController,
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                children: [
                  const Text('Période', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: ErpColors.textSecondary)),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      for (final entry in [
                        (_ReportPeriod.day, 'Journalier'),
                        (_ReportPeriod.week, 'Hebdo'),
                        (_ReportPeriod.month, 'Mensuel'),
                        (_ReportPeriod.custom, 'Période'),
                      ])
                        FilterChip(
                          label: Text(entry.$2),
                          selected: _period == entry.$1,
                          onSelected: (_) => setState(() => _period = entry.$1),
                          selectedColor: ErpColors.primary.withValues(alpha: 0.15),
                          checkmarkColor: ErpColors.primary,
                        ),
                    ],
                  ),
                  if (_period == _ReportPeriod.month) ...[
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: DropdownButtonFormField<int>(
                            initialValue: _month,
                            decoration: const InputDecoration(labelText: 'Mois', isDense: true),
                            items: List.generate(
                              12,
                              (i) => DropdownMenuItem(value: i + 1, child: Text(widget.monthLabels[i])),
                            ),
                            onChanged: (v) => setState(() => _month = v ?? _month),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: DropdownButtonFormField<int>(
                            initialValue: _year,
                            decoration: const InputDecoration(labelText: 'Année', isDense: true),
                            items: List.generate(7, (i) {
                              final y = DateTime.now().year - 5 + i;
                              return DropdownMenuItem(value: y, child: Text('$y'));
                            }),
                            onChanged: (v) => setState(() => _year = v ?? _year),
                          ),
                        ),
                      ],
                    ),
                  ],
                  if (_period == _ReportPeriod.custom) ...[
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton(
                            onPressed: () async {
                              final picked = await showDatePicker(
                                context: context,
                                initialDate: _from,
                                firstDate: DateTime(2020),
                                lastDate: DateTime.now().add(const Duration(days: 365)),
                              );
                              if (picked != null) setState(() => _from = picked);
                            },
                            child: Text('Du ${dateFmt.format(_from)}'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: OutlinedButton(
                            onPressed: () async {
                              final picked = await showDatePicker(
                                context: context,
                                initialDate: _to,
                                firstDate: DateTime(2020),
                                lastDate: DateTime.now().add(const Duration(days: 365)),
                              );
                              if (picked != null) setState(() => _to = picked);
                            },
                            child: Text('Au ${dateFmt.format(_to)}'),
                          ),
                        ),
                      ],
                    ),
                  ],
                  const SizedBox(height: 16),
                  if (widget.feeTypes.isNotEmpty)
                    DropdownButtonFormField<FeeTypeCatalogItem>(
                      initialValue: _feeType,
                      decoration: const InputDecoration(labelText: 'Type de frais *', isDense: true),
                      items: widget.feeTypes
                          .map((f) => DropdownMenuItem(value: f, child: Text(f.name)))
                          .toList(),
                      onChanged: (v) => setState(() => _feeType = v),
                    ),
                  if (widget.structure != null) ...[
                    const SizedBox(height: 10),
                    DropdownButtonFormField<EnrollmentSection?>(
                      initialValue: _section,
                      decoration: const InputDecoration(labelText: 'Section (optionnel)', isDense: true),
                      items: [
                        const DropdownMenuItem(value: null, child: Text('Toutes les sections')),
                        ...widget.structure!.sections
                            .map((s) => DropdownMenuItem(value: s, child: Text(s.name))),
                      ],
                      onChanged: (v) => setState(() {
                        _section = v;
                        _classOption = null;
                      }),
                    ),
                    const SizedBox(height: 10),
                    DropdownButtonFormField<EnrollmentClassOption?>(
                      initialValue: _classes.any((c) => c.classRoomId == _classOption?.classRoomId)
                          ? _classOption
                          : null,
                      decoration: const InputDecoration(labelText: 'Classe (optionnel)', isDense: true),
                      items: [
                        const DropdownMenuItem(value: null, child: Text('Toutes les classes')),
                        ..._classes.map((c) => DropdownMenuItem(value: c, child: Text(c.fullDisplayName))),
                      ],
                      onChanged: (v) => setState(() => _classOption = v),
                    ),
                  ],
                ],
              ),
            ),
            const Divider(height: 1),
            SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 12),
                child: Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        onPressed: () {
                          Navigator.pop(context);
                          widget.onReset();
                        },
                        child: const Text('Réinitialiser'),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: FilledButton(
                        onPressed: _feeType == null
                            ? null
                            : () {
                                Navigator.pop(context);
                                widget.onApply(
                                  period: _period,
                                  selectedMonth: _month,
                                  selectedYear: _year,
                                  fromDate: _from,
                                  toDate: _to,
                                  feeType: _feeType,
                                  section: _section,
                                  classOption: _classOption,
                                );
                              },
                        style: FilledButton.styleFrom(backgroundColor: ErpColors.primary),
                        child: const Text('Appliquer'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Synthèse compacte (scroll avec le contenu)
// ---------------------------------------------------------------------------

class _FinCompactSummary extends StatelessWidget {
  const _FinCompactSummary({required this.report, required this.currency});

  final RealizedReceiptsReport report;
  final String currency;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.fromLTRB(12, 10, 12, 4),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: ErpColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'TOTAL ENCAISSÉ',
            style: TextStyle(fontSize: 10, fontWeight: FontWeight.w700, color: ErpColors.textSecondary, letterSpacing: 0.4),
          ),
          const SizedBox(height: 4),
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Expanded(
                child: Text(
                  formatMoney(report.grandTotal, currency),
                  style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: ErpColors.primary),
                ),
              ),
              Text(
                '${report.paymentCount} encaissement${report.paymentCount > 1 ? 's' : ''}',
                style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
              ),
            ],
          ),
          if (report.byCurrency.length > 1) ...[
            const SizedBox(height: 6),
            ...report.byCurrency.map(
              (c) => Text(
                '${c.currency} : ${formatMoney(c.totalAmount, c.currency)} (${c.paymentCount})',
                style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Composants de liste compacts
// ---------------------------------------------------------------------------

class _FinSectionTitle extends StatelessWidget {
  const _FinSectionTitle(this.label);

  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 14, 16, 6),
      child: Text(
        label.toUpperCase(),
        style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w800, color: ErpColors.navy, letterSpacing: 0.5),
      ),
    );
  }
}

class _FinCompactRow extends StatelessWidget {
  const _FinCompactRow({
    required this.primary,
    this.secondary,
    required this.amount,
    required this.currency,
    this.showChevron = false,
  });

  final String primary;
  final String? secondary;
  final double amount;
  final String currency;
  final bool showChevron;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 9),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      primary,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                        color: ErpColors.navy,
                      ),
                    ),
                    if (secondary != null && secondary!.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Text(
                          secondary!,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Text(
                formatMoney(amount, currency),
                style: const TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w700,
                  color: ErpColors.navy,
                ),
              ),
              if (showChevron) ...[
                const SizedBox(width: 2),
                const Icon(Icons.chevron_right_rounded, size: 18, color: ErpColors.textSecondary),
              ],
            ],
          ),
        ),
        const Divider(height: 1, thickness: 1, indent: 16, color: ErpColors.border),
      ],
    );
  }
}

class _FinCompactMetaRow extends StatelessWidget {
  const _FinCompactMetaRow({
    required this.primary,
    required this.secondary,
    required this.amount,
    required this.currency,
  });

  final String primary;
  final String secondary;
  final double amount;
  final String currency;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 9),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(primary, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: ErpColors.navy)),
                    const SizedBox(height: 2),
                    Text(secondary, maxLines: 2, overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Text(formatMoney(amount, currency),
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: ErpColors.navy)),
            ],
          ),
        ),
        const Divider(height: 1, thickness: 1, indent: 16, color: ErpColors.border),
      ],
    );
  }
}

class _FinCompactTotalLine extends StatelessWidget {
  const _FinCompactTotalLine({
    required this.label,
    required this.amount,
    required this.currency,
  });

  final String label;
  final double amount;
  final String currency;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 4),
      child: Row(
        children: [
          Expanded(
            child: Text(label, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w800, color: ErpColors.navy)),
          ),
          Text(formatMoney(amount, currency),
              style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w800, color: ErpColors.primary)),
        ],
      ),
    );
  }
}

class _FinEmptyState extends StatelessWidget {
  const _FinEmptyState(this.message);

  final String message;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(32),
      child: Center(
        child: Text(message, textAlign: TextAlign.center, style: const TextStyle(color: ErpColors.textSecondary)),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Onglets
// ---------------------------------------------------------------------------

class _DetailTab extends StatelessWidget {
  const _DetailTab({required this.report, required this.formatDate, required this.header});

  final RealizedReceiptsReport report;
  final String Function(String) formatDate;
  final List<Widget> header;

  @override
  Widget build(BuildContext context) {
    final currency = report.byCurrency.firstOrNull?.currency ?? 'CDF';
    final empty = report.items.isEmpty && report.pivotRows.isEmpty;

    return ListView(
      padding: const EdgeInsets.only(bottom: 16),
      children: [
        ...header,
        if (empty)
          const _FinEmptyState('Aucun encaissement sur cette période')
        else ...[
          if (report.pivotRows.isNotEmpty) ...[
            const _FinSectionTitle('Synthèse par élève'),
            ...report.pivotRows.map(
              (row) => _FinCompactRow(
                primary: row.studentName,
                secondary: row.className,
                amount: row.rowTotal,
                currency: currency,
                showChevron: true,
              ),
            ),
          ],
          const _FinSectionTitle('Détail des encaissements'),
          ...report.items.map(
            (line) => _FinCompactMetaRow(
              primary: '${formatDate(line.paymentDate)} · Reçu ${line.receiptNumber}',
              secondary: line.studentName,
              amount: line.totalAmount,
              currency: line.currency,
            ),
          ),
        ],
      ],
    );
  }
}

class _DailyTab extends StatelessWidget {
  const _DailyTab({
    required this.report,
    required this.formatDate,
    required this.currency,
    required this.header,
  });

  final RealizedReceiptsReport report;
  final String Function(String) formatDate;
  final String currency;
  final List<Widget> header;

  Map<String, List<RealizedReceiptsDailyByClass>> _groupDailyByClass() {
    final map = <String, List<RealizedReceiptsDailyByClass>>{};
    for (final row in report.dailyByClass) {
      map.putIfAbsent(row.date, () => []).add(row);
    }
    return map;
  }

  int _paymentCountForDate(String date) {
    for (final b in report.dailyBuckets) {
      if (b.date == date) return b.paymentCount;
    }
    return 0;
  }

  double _totalForDate(String date) {
    for (final b in report.dailyBuckets) {
      if (b.date == date) return b.totalAmount;
    }
    final classes = _groupDailyByClass()[date];
    if (classes == null) return 0;
    return classes.fold<double>(0, (s, r) => s + r.totalAmount);
  }

  @override
  Widget build(BuildContext context) {
    final dailyGroups = _groupDailyByClass();
    final dates = <String>{
      ...report.dailyBuckets.map((b) => b.date),
      ...dailyGroups.keys,
    }.toList()
      ..sort((a, b) => b.compareTo(a));

    final empty = dates.isEmpty && report.dailyPivotRows.isEmpty;

    return ListView(
      padding: const EdgeInsets.only(bottom: 16),
      children: [
        ...header,
        if (empty)
          const _FinEmptyState('Aucune donnée journalière')
        else ...[
          if (dates.isNotEmpty) ...[
            const _FinSectionTitle('Par jour'),
            ...dates.map((date) {
              final dayTotal = _totalForDate(date);
              final count = _paymentCountForDate(date);
              final classes = dailyGroups[date] ?? [];
              return Theme(
                data: Theme.of(context).copyWith(dividerColor: ErpColors.border),
                child: ExpansionTile(
                  tilePadding: const EdgeInsets.symmetric(horizontal: 16),
                  childrenPadding: EdgeInsets.zero,
                  title: Text(formatDate(date), style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                  subtitle: Text(
                    '${formatMoney(dayTotal, currency)}${count > 0 ? ' · $count encaissement${count > 1 ? 's' : ''}' : ''}',
                    style: const TextStyle(fontSize: 11),
                  ),
                  children: classes.isEmpty
                      ? [
                          const Padding(
                            padding: EdgeInsets.fromLTRB(16, 0, 16, 10),
                            child: Text('Aucun détail par classe', style: TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
                          ),
                        ]
                      : classes
                          .map(
                            (r) => ListTile(
                              dense: true,
                              contentPadding: const EdgeInsets.symmetric(horizontal: 24),
                              visualDensity: VisualDensity.compact,
                              title: Text(r.className, style: const TextStyle(fontSize: 12)),
                              trailing: Text(formatMoney(r.totalAmount, currency),
                                  style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 12)),
                            ),
                          )
                          .toList(),
                ),
              );
            }),
          ],
          if (report.dailyPivotRows.isNotEmpty) ...[
            const _FinSectionTitle('Pivot journalier'),
            ...report.dailyPivotRows.map(
              (row) => _FinCompactRow(
                primary: row.studentName,
                secondary: '${formatDate(row.date)} · ${row.className}',
                amount: row.rowTotal,
                currency: currency,
              ),
            ),
          ],
        ],
      ],
    );
  }
}

class _ByClassTab extends StatelessWidget {
  const _ByClassTab({required this.report, required this.currency, required this.header});

  final RealizedReceiptsReport report;
  final String currency;
  final List<Widget> header;

  @override
  Widget build(BuildContext context) {
    final total = report.byClass.fold<double>(0, (s, r) => s + r.totalAmount);
    if (report.byClass.isEmpty) {
      return ListView(padding: const EdgeInsets.only(bottom: 16), children: [...header, const _FinEmptyState('Aucune donnée')]);
    }
    return ListView(
      padding: const EdgeInsets.only(bottom: 16),
      children: [
        ...header,
        const _FinSectionTitle('Par classe'),
        _FinCompactTotalLine(label: 'TOTAL PAR CLASSE', amount: total, currency: currency),
        ...report.byClass.map(
          (row) => _FinCompactRow(
            primary: row.className,
            secondary: '${row.sectionName} · ${row.paymentCount} paiement${row.paymentCount > 1 ? 's' : ''}',
            amount: row.totalAmount,
            currency: currency,
          ),
        ),
      ],
    );
  }
}

class _BySectionTab extends StatelessWidget {
  const _BySectionTab({required this.report, required this.currency, required this.header});

  final RealizedReceiptsReport report;
  final String currency;
  final List<Widget> header;

  @override
  Widget build(BuildContext context) {
    final total = report.bySection.fold<double>(0, (s, r) => s + r.totalAmount);
    if (report.bySection.isEmpty) {
      return ListView(padding: const EdgeInsets.only(bottom: 16), children: [...header, const _FinEmptyState('Aucune donnée')]);
    }
    return ListView(
      padding: const EdgeInsets.only(bottom: 16),
      children: [
        ...header,
        const _FinSectionTitle('Par section'),
        _FinCompactTotalLine(label: 'TOTAL PAR SECTION', amount: total, currency: currency),
        ...report.bySection.map(
          (row) => _FinCompactRow(
            primary: row.sectionName,
            secondary: '${row.paymentCount} paiement${row.paymentCount > 1 ? 's' : ''}',
            amount: row.totalAmount,
            currency: currency,
          ),
        ),
      ],
    );
  }
}

class _ByFeeTypeTab extends StatelessWidget {
  const _ByFeeTypeTab({required this.report, required this.header});

  final RealizedReceiptsReport report;
  final List<Widget> header;

  @override
  Widget build(BuildContext context) {
    if (report.byFeeType.isEmpty) {
      return ListView(padding: const EdgeInsets.only(bottom: 16), children: [...header, const _FinEmptyState('Aucune donnée')]);
    }
    return ListView(
      padding: const EdgeInsets.only(bottom: 16),
      children: [
        ...header,
        const _FinSectionTitle('Par type de frais'),
        ...report.byFeeType.map(
          (row) => _FinCompactRow(
            primary: row.feeTypeName,
            secondary: '${row.paymentCount} paiement${row.paymentCount > 1 ? 's' : ''}',
            amount: row.totalAmount,
            currency: row.currency,
          ),
        ),
      ],
    );
  }
}

class _AllocationTab extends StatelessWidget {
  const _AllocationTab({required this.allocation, required this.formatDate, required this.header});

  final AllocationCashFlowReport? allocation;
  final String Function(String) formatDate;
  final List<Widget> header;

  @override
  Widget build(BuildContext context) {
    final data = allocation;
    if (data == null) {
      return ListView(children: [...header, const Center(child: Padding(padding: EdgeInsets.all(24), child: CircularProgressIndicator()))]);
    }
    if (data.globalRows.isEmpty && data.dailyGroups.isEmpty && data.totalsByCurrency.isEmpty) {
      return ListView(padding: const EdgeInsets.only(bottom: 16), children: [...header, const _FinEmptyState('Aucune répartition sur cette période')]);
    }

    return ListView(
      padding: const EdgeInsets.only(bottom: 16),
      children: [
        ...header,
        const _FinSectionTitle('Répartitions'),
        if (data.globalRows.isNotEmpty)
          ...data.globalRows.map((row) => _AllocationAccountTile(row: row)),
        if (data.dailyGroups.isNotEmpty) ...[
          const _FinSectionTitle('Répartition journalière'),
          ...data.dailyGroups.map(
            (group) => Theme(
              data: Theme.of(context).copyWith(dividerColor: ErpColors.border),
              child: ExpansionTile(
                tilePadding: const EdgeInsets.symmetric(horizontal: 16),
                title: Text(formatDate(group.date), style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                children: group.rows.map((row) => _AllocationAccountTile(row: row, compact: true)).toList(),
              ),
            ),
          ),
        ],
        if (data.totalsByCurrency.isNotEmpty) ...[
          const _FinSectionTitle('Totaux par devise'),
          ...data.totalsByCurrency.map((row) => _AllocationAccountTile(row: row)),
        ],
      ],
    );
  }
}

class _AllocationAccountTile extends StatelessWidget {
  const _AllocationAccountTile({required this.row, this.compact = false});

  final AllocationCashFlowRow row;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final title = row.destinationName.isNotEmpty ? row.destinationName : row.destinationCode;
    return Theme(
      data: Theme.of(context).copyWith(dividerColor: ErpColors.border),
      child: ExpansionTile(
        tilePadding: EdgeInsets.symmetric(horizontal: compact ? 24 : 16),
        childrenPadding: const EdgeInsets.fromLTRB(24, 0, 16, 8),
        title: Text(title, style: TextStyle(fontWeight: FontWeight.w600, fontSize: compact ? 12 : 13)),
        subtitle: Text(formatMoney(row.periodeP, row.currencyCode), style: const TextStyle(fontSize: 11)),
        children: [
          _AllocationDetailLine(label: 'J-1', amount: row.periodJ1, currency: row.currencyCode),
          _AllocationDetailLine(label: 'Encaissements', amount: row.encaissement, currency: row.currencyCode),
          _AllocationDetailLine(label: 'Dépenses', amount: row.depenseP, currency: row.currencyCode),
          _AllocationDetailLine(label: 'Solde période', amount: row.periodeP, currency: row.currencyCode, bold: true),
        ],
      ),
    );
  }
}

class _AllocationDetailLine extends StatelessWidget {
  const _AllocationDetailLine({
    required this.label,
    required this.amount,
    required this.currency,
    this.bold = false,
  });

  final String label;
  final double amount;
  final String currency;
  final bool bold;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          Expanded(child: Text(label, style: TextStyle(fontSize: 11, fontWeight: bold ? FontWeight.w700 : FontWeight.w400))),
          Text(
            formatMoney(amount, currency),
            style: TextStyle(
              fontSize: 11,
              fontWeight: bold ? FontWeight.w700 : FontWeight.w500,
              color: bold ? ErpColors.primary : ErpColors.navy,
            ),
          ),
        ],
      ),
    );
  }
}

class _WithholdingTab extends StatelessWidget {
  const _WithholdingTab({required this.withholding, required this.formatDate, required this.header});

  final WithholdingReport? withholding;
  final String Function(String) formatDate;
  final List<Widget> header;

  @override
  Widget build(BuildContext context) {
    final data = withholding;
    if (data == null) {
      return ListView(children: [...header, const Center(child: Padding(padding: EdgeInsets.all(24), child: CircularProgressIndicator()))]);
    }
    if (data.groups.isEmpty) {
      return ListView(padding: const EdgeInsets.only(bottom: 16), children: [...header, const _FinEmptyState('Aucune retenue sur cette période')]);
    }

    return ListView(
      padding: const EdgeInsets.only(bottom: 16),
      children: [
        ...header,
        const _FinSectionTitle('Retenues'),
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
          child: Text(
            'Total : ${formatMoney(data.grandTotal, 'CDF')} · ${data.paymentCount} paiement${data.paymentCount > 1 ? 's' : ''}',
            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: ErpColors.navy),
          ),
        ),
        ...data.groups.map(
          (group) => Theme(
            data: Theme.of(context).copyWith(dividerColor: ErpColors.border),
            child: ExpansionTile(
              tilePadding: const EdgeInsets.symmetric(horizontal: 16),
              title: Text(
                group.withholdingTypeName.isNotEmpty ? group.withholdingTypeName : group.withholdingTypeCode,
                style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
              ),
              subtitle: Text(
                '${formatMoney(group.typeTotal, 'CDF')} · ${group.students.length} opération${group.students.length > 1 ? 's' : ''}',
                style: const TextStyle(fontSize: 11),
              ),
              children: group.students
                  .map(
                    (s) => Padding(
                      padding: const EdgeInsets.fromLTRB(24, 6, 16, 6),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(s.studentName, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
                                Text(formatDate(s.paymentDate), style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
                              ],
                            ),
                          ),
                          Text(formatMoney(s.amount, 'CDF'),
                              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700)),
                        ],
                      ),
                    ),
                  )
                  .toList(),
            ),
          ),
        ),
      ],
    );
  }
}
