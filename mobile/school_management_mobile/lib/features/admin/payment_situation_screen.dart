import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_error_message.dart';
import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../enrollment/models/enrollment_models.dart';
import '../promoteur/dashboard_formatters.dart';
import 'admin_finance_models.dart';
import 'daf_student_models.dart';

class PaymentSituationScreen extends ConsumerStatefulWidget {
  const PaymentSituationScreen({super.key});

  @override
  ConsumerState<PaymentSituationScreen> createState() => _PaymentSituationScreenState();
}

class _PaymentSituationScreenState extends ConsumerState<PaymentSituationScreen> {
  List<FeeTypeCatalogItem> _feeTypes = [];
  List<PricingCategoryOption> _pricingCategories = [];
  List<FeeTypeInstallment> _installments = [];
  EnrollmentStructureOptions? _structure;
  String? _academicYearId;

  FeeTypeCatalogItem? _feeType;
  int _scopeKind = 0;
  Set<String> _selectedInstallmentIds = {};
  int _situationFilter = 0;
  int _sortBy = 0;
  EnrollmentSection? _section;
  String? _studyOption;
  EnrollmentClassOption? _classOption;
  PricingCategoryOption? _pricingCategory;

  PaymentSituationReportResult? _result;
  bool _bootstrapping = true;
  bool _generating = false;
  bool _hasGenerated = false;
  String? _error;
  String? _statusMessage;

  static const _situationFilters = [
    (0, 'Tous les élèves'),
    (1, 'En ordre seulement'),
    (2, 'Non en ordre seulement'),
  ];

  static const _sortOptions = [
    (0, 'Nom'),
    (1, 'Matricule'),
    (2, 'Classe'),
    (3, 'Solde décroissant'),
  ];

  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  Future<void> _bootstrap() async {
    setState(() {
      _bootstrapping = true;
      _error = null;
    });
    try {
      final repo = ref.read(adminFinanceRepositoryProvider);
      final prereq = await ref.read(dafStudentRepositoryProvider).getPrerequisites();
      final structure = await ref.read(enrollmentRepositoryProvider).getStructureOptions();
      final catalog = await ref.read(dafStudentRepositoryProvider).getFeeCatalog();
      final pricingCategories = await repo.getPricingCategories();
      final active = catalog.feeTypes.where((f) => f.isActive).toList();
      final feeType = active.isNotEmpty ? active.first : null;
      List<FeeTypeInstallment> installments = [];
      if (feeType != null) {
        installments = await repo.getFeeTypeInstallments(feeType.id);
      }
      if (!mounted) return;
      setState(() {
        _academicYearId = prereq.currentAcademicYearId;
        _structure = structure;
        _feeTypes = active;
        _pricingCategories = pricingCategories.where((c) => c.isActive).toList();
        _feeType = feeType;
        _installments = installments;
        _statusMessage = 'Configurez les critères puis appuyez sur Générer.';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = resolveDashboardErrorMessage(e));
    } finally {
      if (mounted) setState(() => _bootstrapping = false);
    }
  }

  Future<void> _reloadStructure() async {
    try {
      final structure = await ref.read(enrollmentRepositoryProvider).getStructureOptions();
      if (!mounted) return;
      setState(() {
        _structure = structure;
        _section = null;
        _studyOption = null;
        _classOption = null;
        _result = null;
        _hasGenerated = false;
        _statusMessage = 'Structure rechargée — configurez les critères puis générez.';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = resolveDashboardErrorMessage(e));
    }
  }

  Future<List<FeeTypeInstallment>> _loadInstallments(String feeTypeId) =>
      ref.read(adminFinanceRepositoryProvider).getFeeTypeInstallments(feeTypeId);

  bool _sectionOrganizesOptions(List<EnrollmentClassOption> classes) {
    if (classes.isEmpty) return false;
    final options = classes
        .map((c) => c.studyOption?.trim())
        .where((o) => o != null && o.isNotEmpty)
        .map((o) => o!.toLowerCase())
        .toSet();
    return options.length > 1 ||
        (options.length == 1 && classes.any((c) => c.studyOption == null || c.studyOption!.trim().isEmpty));
  }

  List<EnrollmentClassOption> _sectionClasses(EnrollmentSection? section) {
    final structure = _structure;
    if (structure == null) return [];
    final selectable = structure.classes.where((c) => c.isSelectable);
    if (section == null) return selectable.toList();
    return selectable.where((c) => c.sectionId == section.id).toList();
  }

  bool get _isStudyOptionEnabled {
    if (_section == null) return false;
    return _sectionOrganizesOptions(_sectionClasses(_section));
  }

  String _situationLabel(int value) =>
      _situationFilters.firstWhere((f) => f.$1 == value, orElse: () => _situationFilters.first).$2;

  String _installmentScopeSummary() {
    if (_scopeKind != 1 || _selectedInstallmentIds.isEmpty) return '';
    final selected = _installments
        .where((i) => _selectedInstallmentIds.contains(i.feeInstallmentId))
        .toList()
      ..sort((a, b) => a.sortOrder.compareTo(b.sortOrder));
    if (selected.isEmpty) return 'Tranches sélectionnées';
    if (selected.length == 1) return selected.first.installmentName;
    final orders = selected.map((i) => i.sortOrder).toList()..sort();
    final consecutive = orders.last - orders.first + 1 == orders.length;
    if (consecutive) {
      return 'Tranches ${orders.first}–${orders.last}';
    }
    return selected.map((i) => i.installmentName).join(', ');
  }

  List<String> _activeFilterParts() {
    final parts = <String>[];
    parts.add(_feeType?.name ?? 'Type de frais');
    if (_scopeKind == 1) {
      final scope = _installmentScopeSummary();
      parts.add(scope.isNotEmpty ? scope : 'Tranches spécifiques');
    }
    parts.add(_section?.name ?? 'Toutes sections');
    if (_classOption != null) {
      parts.add(_classOption!.fullDisplayName);
    } else if (_isStudyOptionEnabled && _studyOption != null && _studyOption!.isNotEmpty) {
      parts.add(_studyOption!);
    }
    if (_pricingCategory != null) {
      parts.add(_pricingCategory!.name);
    }
    parts.add(_situationLabel(_situationFilter));
    return parts;
  }

  Future<void> _generate() async {
    final yearId = _academicYearId;
    final feeType = _feeType;
    if (yearId == null || yearId.isEmpty || feeType == null) {
      setState(() => _statusMessage = 'Année scolaire et type de frais sont obligatoires.');
      return;
    }
    if (_scopeKind == 1 && _selectedInstallmentIds.isEmpty) {
      setState(() => _statusMessage = 'Sélectionnez au moins une tranche.');
      return;
    }

    setState(() {
      _generating = true;
      _error = null;
    });
    try {
      final result = await ref.read(adminFinanceRepositoryProvider).getPaymentSituationReport(
            academicYearId: yearId,
            feeTypeId: feeType.id,
            scopeKind: _scopeKind,
            feeInstallmentIds: _scopeKind == 1 ? _selectedInstallmentIds.toList() : null,
            situationFilter: _situationFilter,
            sortBy: _sortBy,
            sectionId: _section?.id,
            classRoomId: _classOption?.classRoomId,
            studyOption: _isStudyOptionEnabled ? _studyOption : null,
            feePricingCategoryId: _pricingCategory?.id,
          );
      if (!mounted) return;
      setState(() {
        _result = result;
        _hasGenerated = true;
        _statusMessage = null;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = resolveDashboardErrorMessage(e);
        _statusMessage = null;
      });
    } finally {
      if (mounted) setState(() => _generating = false);
    }
  }

  void _resetFilters() {
    setState(() {
      _scopeKind = 0;
      _selectedInstallmentIds = {};
      _situationFilter = 0;
      _sortBy = 0;
      _section = null;
      _studyOption = null;
      _classOption = null;
      _pricingCategory = null;
      _feeType = _feeTypes.isNotEmpty ? _feeTypes.first : null;
      _result = null;
      _hasGenerated = false;
      _error = null;
      _statusMessage = 'Filtres réinitialisés — configurez les critères puis générez.';
    });
    final feeType = _feeType;
    if (feeType != null) {
      _loadInstallments(feeType.id).then((items) {
        if (mounted) setState(() => _installments = items);
      });
    }
  }

  void _openFilterSheet() {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (context) => _PaymentSituationFilterSheet(
        feeTypes: _feeTypes,
        pricingCategories: _pricingCategories,
        structure: _structure,
        initialFeeType: _feeType,
        initialScopeKind: _scopeKind,
        initialInstallmentIds: Set<String>.from(_selectedInstallmentIds),
        initialInstallments: _installments,
        initialSituationFilter: _situationFilter,
        initialSortBy: _sortBy,
        initialSection: _section,
        initialStudyOption: _studyOption,
        initialClass: _classOption,
        initialPricingCategory: _pricingCategory,
        loadInstallments: _loadInstallments,
        sectionOrganizesOptions: _sectionOrganizesOptions,
        onGenerate: ({
          required feeType,
          required scopeKind,
          required installmentIds,
          required installments,
          required situationFilter,
          required sortBy,
          required section,
          required studyOption,
          required classOption,
          required pricingCategory,
        }) {
          setState(() {
            _feeType = feeType;
            _scopeKind = scopeKind;
            _selectedInstallmentIds = installmentIds;
            _installments = installments;
            _situationFilter = situationFilter;
            _sortBy = sortBy;
            _section = section;
            _studyOption = studyOption;
            _classOption = classOption;
            _pricingCategory = pricingCategory;
          });
          _generate();
        },
        onReset: () {
          Navigator.pop(context);
          _resetFilters();
        },
      ),
    );
  }

  void _openStudentDetail(PaymentSituationPivotRow row, PaymentSituationReportResult result) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (context) => _PaymentSituationStudentDetailSheet(row: row, result: result),
    );
  }

  List<_SectionGroup> _buildGroups(PaymentSituationReportResult result) {
    final sectionMap = <String, Map<String, List<PaymentSituationPivotRow>>>{};
    final sectionOrder = <String>[];

    for (final row in result.pivotRows) {
      sectionMap.putIfAbsent(row.sectionName, () {
        sectionOrder.add(row.sectionName);
        return {};
      });
      final classMap = sectionMap[row.sectionName]!;
      classMap.putIfAbsent(row.className, () => []).add(row);
    }

    final sortedSections = sectionOrder.toSet().toList()
      ..sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));

    return sortedSections.map((sectionName) {
      final classMap = sectionMap[sectionName]!;
      final sortedClasses = classMap.keys.toList()
        ..sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));
      final classes = sortedClasses.map((className) {
        final students = classMap[className]!;
        final remaining = students.fold<double>(0, (s, r) => s + r.balance);
        return _ClassGroup(name: className, remaining: remaining, students: students);
      }).toList();
      final remaining = classes.fold<double>(0, (s, c) => s + c.remaining);
      return _SectionGroup(name: sectionName, remaining: remaining, classes: classes);
    }).toList();
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
                              style: const TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.w600,
                                color: ErpColors.navy,
                              ),
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
          if (_generating)
            const LinearProgressIndicator(minHeight: 2, color: ErpColors.primary),
          const Divider(height: 1, color: ErpColors.border),
        ],
      ),
    );
  }

  Widget _buildKpi(PaymentSituationReportResult result) {
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
          Wrap(
            spacing: 16,
            runSpacing: 4,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(
                '${result.totalCount} élève${result.totalCount > 1 ? 's' : ''}',
                style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: ErpColors.navy),
              ),
              Text(
                '${result.inOrderCount} en ordre',
                style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: ErpColors.success),
              ),
              Text(
                '${result.notInOrderCount} non en ordre',
                style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: ErpColors.danger),
              ),
            ],
          ),
          const SizedBox(height: 8),
          const Text(
            'RESTE À PAYER',
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w700,
              color: ErpColors.textSecondary,
              letterSpacing: 0.4,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            formatMoney(result.totalBalance, result.currency),
            style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: ErpColors.navy),
          ),
        ],
      ),
    );
  }

  Widget _buildResults(PaymentSituationReportResult result) {
    if (result.pivotRows.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text(
            'Aucun élève ne correspond aux critères sélectionnés.',
            textAlign: TextAlign.center,
            style: TextStyle(color: ErpColors.textSecondary),
          ),
        ),
      );
    }

    final groups = _buildGroups(result);
    return ListView.builder(
      padding: const EdgeInsets.fromLTRB(12, 0, 12, 24),
      itemCount: groups.length,
      itemBuilder: (context, index) {
        final section = groups[index];
        return _SectionExpansion(
          section: section,
          currency: result.currency,
          onStudentTap: (row) => _openStudentDetail(row, result),
        );
      },
    );
  }

  Widget _buildBody() {
    if (_bootstrapping) {
      return const Center(child: CircularProgressIndicator(color: ErpColors.primary));
    }

    final result = _result;

    if (result == null) {
      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Text(_error!, style: const TextStyle(color: ErpColors.danger, fontSize: 13)),
            ),
          Center(
            child: Text(
              _statusMessage ?? 'Configurez les critères puis appuyez sur Générer.',
              textAlign: TextAlign.center,
              style: TextStyle(
                color: _error != null ? ErpColors.textSecondary : ErpColors.textSecondary,
                fontSize: 14,
              ),
            ),
          ),
        ],
      );
    }

    return Column(
      children: [
        _buildKpi(result),
        if (_error != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
            child: Text(_error!, style: const TextStyle(color: ErpColors.danger, fontSize: 12)),
          ),
        Expanded(child: _buildResults(result)),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Situation des paiements', style: TextStyle(fontSize: 17)),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
        actions: [
          IconButton(
            icon: _generating
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2, color: ErpColors.primary),
                  )
                : const Icon(Icons.refresh_rounded),
            onPressed: _generating
                ? null
                : () {
                    if (_hasGenerated) {
                      _generate();
                    } else {
                      _reloadStructure();
                    }
                  },
          ),
        ],
      ),
      body: Column(
        children: [
          if (!_bootstrapping) _buildFilterSummaryBar(),
          Expanded(child: _buildBody()),
        ],
      ),
    );
  }
}

class _SectionGroup {
  const _SectionGroup({required this.name, required this.remaining, required this.classes});

  final String name;
  final double remaining;
  final List<_ClassGroup> classes;
}

class _ClassGroup {
  const _ClassGroup({required this.name, required this.remaining, required this.students});

  final String name;
  final double remaining;
  final List<PaymentSituationPivotRow> students;
}

class _SectionExpansion extends StatelessWidget {
  const _SectionExpansion({
    required this.section,
    required this.currency,
    required this.onStudentTap,
  });

  final _SectionGroup section;
  final String currency;
  final void Function(PaymentSituationPivotRow row) onStudentTap;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: ErpColors.border),
      ),
      child: Theme(
        data: Theme.of(context).copyWith(dividerColor: ErpColors.border),
        child: ExpansionTile(
          initiallyExpanded: false,
          tilePadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 0),
          childrenPadding: const EdgeInsets.only(bottom: 4),
          title: Text(
            section.name.toUpperCase(),
            style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w800, color: ErpColors.navy),
          ),
          trailing: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                formatMoney(section.remaining, currency),
                style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: ErpColors.danger),
              ),
              const SizedBox(width: 4),
              const Icon(Icons.expand_more, size: 20, color: ErpColors.textSecondary),
            ],
          ),
          children: section.classes
              .map((c) => _ClassExpansion(classGroup: c, currency: currency, onStudentTap: onStudentTap))
              .toList(),
        ),
      ),
    );
  }
}

class _ClassExpansion extends StatelessWidget {
  const _ClassExpansion({
    required this.classGroup,
    required this.currency,
    required this.onStudentTap,
  });

  final _ClassGroup classGroup;
  final String currency;
  final void Function(PaymentSituationPivotRow row) onStudentTap;

  @override
  Widget build(BuildContext context) {
    return ExpansionTile(
      initiallyExpanded: false,
      tilePadding: const EdgeInsets.only(left: 20, right: 12),
      childrenPadding: EdgeInsets.zero,
      title: Text(
        classGroup.name,
        style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: ErpColors.navy),
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            formatMoney(classGroup.remaining, currency),
            style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: ErpColors.danger),
          ),
          const SizedBox(width: 4),
          const Icon(Icons.expand_more, size: 18, color: ErpColors.textSecondary),
        ],
      ),
      children: classGroup.students
          .map((s) => _StudentRow(row: s, currency: currency, onTap: () => onStudentTap(s)))
          .toList(),
    );
  }
}

class _StudentRow extends StatelessWidget {
  const _StudentRow({required this.row, required this.currency, required this.onTap});

  final PaymentSituationPivotRow row;
  final String currency;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final subtitle = row.registrationNumber.trim().isNotEmpty
        ? '${row.className} · ${row.registrationNumber}'
        : row.className;

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(28, 8, 12, 8),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    row.fullName,
                    style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: ErpColors.navy),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    row.isInOrder ? '$subtitle · En ordre' : '$subtitle · Non en ordre',
                    style: TextStyle(
                      fontSize: 11,
                      color: row.isInOrder ? ErpColors.success : ErpColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            if (row.isInOrder)
              const Padding(
                padding: EdgeInsets.only(right: 4),
                child: Icon(Icons.check_circle_rounded, size: 20, color: ErpColors.success),
              )
            else
              Padding(
                padding: const EdgeInsets.only(right: 4),
                child: Text(
                  'Reste : ${formatMoney(row.balance, currency)}',
                  style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: ErpColors.danger),
                ),
              ),
            const Icon(Icons.chevron_right_rounded, size: 20, color: ErpColors.textSecondary),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Filtres — BottomSheet
// ---------------------------------------------------------------------------

class _PaymentSituationFilterSheet extends StatefulWidget {
  const _PaymentSituationFilterSheet({
    required this.feeTypes,
    required this.pricingCategories,
    required this.structure,
    required this.initialFeeType,
    required this.initialScopeKind,
    required this.initialInstallmentIds,
    required this.initialInstallments,
    required this.initialSituationFilter,
    required this.initialSortBy,
    required this.initialSection,
    required this.initialStudyOption,
    required this.initialClass,
    required this.initialPricingCategory,
    required this.loadInstallments,
    required this.sectionOrganizesOptions,
    required this.onGenerate,
    required this.onReset,
  });

  final List<FeeTypeCatalogItem> feeTypes;
  final List<PricingCategoryOption> pricingCategories;
  final EnrollmentStructureOptions? structure;
  final FeeTypeCatalogItem? initialFeeType;
  final int initialScopeKind;
  final Set<String> initialInstallmentIds;
  final List<FeeTypeInstallment> initialInstallments;
  final int initialSituationFilter;
  final int initialSortBy;
  final EnrollmentSection? initialSection;
  final String? initialStudyOption;
  final EnrollmentClassOption? initialClass;
  final PricingCategoryOption? initialPricingCategory;
  final Future<List<FeeTypeInstallment>> Function(String feeTypeId) loadInstallments;
  final bool Function(List<EnrollmentClassOption> classes) sectionOrganizesOptions;
  final void Function({
    required FeeTypeCatalogItem? feeType,
    required int scopeKind,
    required Set<String> installmentIds,
    required List<FeeTypeInstallment> installments,
    required int situationFilter,
    required int sortBy,
    required EnrollmentSection? section,
    required String? studyOption,
    required EnrollmentClassOption? classOption,
    required PricingCategoryOption? pricingCategory,
  }) onGenerate;
  final VoidCallback onReset;

  @override
  State<_PaymentSituationFilterSheet> createState() => _PaymentSituationFilterSheetState();
}

class _PaymentSituationFilterSheetState extends State<_PaymentSituationFilterSheet> {
  late FeeTypeCatalogItem? _feeType;
  late int _scopeKind;
  late Set<String> _installmentIds;
  late List<FeeTypeInstallment> _installments;
  late int _situationFilter;
  late int _sortBy;
  EnrollmentSection? _section;
  String? _studyOption;
  EnrollmentClassOption? _classOption;
  PricingCategoryOption? _pricingCategory;
  bool _loadingInstallments = false;
  String? _validationMessage;

  @override
  void initState() {
    super.initState();
    _feeType = widget.initialFeeType;
    _scopeKind = widget.initialScopeKind;
    _installmentIds = Set<String>.from(widget.initialInstallmentIds);
    _installments = List<FeeTypeInstallment>.from(widget.initialInstallments);
    _situationFilter = widget.initialSituationFilter;
    _sortBy = widget.initialSortBy;
    _section = widget.initialSection;
    _studyOption = widget.initialStudyOption;
    _classOption = widget.initialClass;
    _pricingCategory = widget.initialPricingCategory;
  }

  List<EnrollmentClassOption> _sectionClasses(EnrollmentSection? section) {
    final structure = widget.structure;
    if (structure == null) return [];
    final selectable = structure.classes.where((c) => c.isSelectable);
    if (section == null) return selectable.toList();
    return selectable.where((c) => c.sectionId == section.id).toList();
  }

  bool get _isStudyOptionEnabled =>
      _section != null && widget.sectionOrganizesOptions(_sectionClasses(_section));

  List<String> get _studyOptions {
    if (!_isStudyOptionEnabled) return [];
    return _sectionClasses(_section)
        .map((c) => c.studyOption?.trim())
        .where((o) => o != null && o.isNotEmpty)
        .map((o) => o!)
        .toSet()
        .toList()
      ..sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));
  }

  List<EnrollmentClassOption> get _filteredClasses {
    var query = _sectionClasses(_section);
    if (_isStudyOptionEnabled && _studyOption != null && _studyOption!.isNotEmpty) {
      query = query
          .where((c) => c.studyOption?.toLowerCase() == _studyOption!.toLowerCase())
          .toList();
    }
    return query..sort((a, b) => a.fullDisplayName.toLowerCase().compareTo(b.fullDisplayName.toLowerCase()));
  }

  Future<void> _onFeeTypeChanged(FeeTypeCatalogItem? feeType) async {
    setState(() {
      _feeType = feeType;
      _installmentIds = {};
      _loadingInstallments = true;
    });
    if (feeType == null) {
      setState(() {
        _installments = [];
        _loadingInstallments = false;
      });
      return;
    }
    try {
      final items = await widget.loadInstallments(feeType.id);
      if (!mounted) return;
      setState(() {
        _installments = items;
        _loadingInstallments = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _installments = [];
        _loadingInstallments = false;
      });
    }
  }

  void _onSectionChanged(EnrollmentSection? section) {
    setState(() {
      _section = section;
      _studyOption = null;
      _classOption = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.viewInsetsOf(context).bottom;

    return Padding(
      padding: EdgeInsets.only(bottom: bottomInset),
      child: DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.88,
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
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 16),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Critères de génération',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.w800, color: ErpColors.navy),
                ),
              ),
            ),
            if (_validationMessage != null)
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 6, 16, 0),
                child: Text(
                  _validationMessage!,
                  style: const TextStyle(fontSize: 12, color: ErpColors.danger),
                ),
              ),
            Expanded(
              child: ListView(
                controller: scrollController,
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
                children: [
                  if (widget.feeTypes.isNotEmpty)
                    DropdownButtonFormField<FeeTypeCatalogItem>(
                      value: _feeType,
                      decoration: const InputDecoration(labelText: 'Type de frais *', isDense: true),
                      items: widget.feeTypes
                          .map((f) => DropdownMenuItem(value: f, child: Text(f.name)))
                          .toList(),
                      onChanged: _onFeeTypeChanged,
                    ),
                  const SizedBox(height: 14),
                  const Text(
                    'Portée',
                    style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: ErpColors.navy),
                  ),
                  RadioListTile<int>(
                    contentPadding: EdgeInsets.zero,
                    dense: true,
                    title: const Text('Totalité du type de frais', style: TextStyle(fontSize: 13)),
                    value: 0,
                    groupValue: _scopeKind,
                    activeColor: ErpColors.primary,
                    onChanged: (v) => setState(() => _scopeKind = v ?? 0),
                  ),
                  RadioListTile<int>(
                    contentPadding: EdgeInsets.zero,
                    dense: true,
                    title: const Text('Tranche(s) spécifique(s)', style: TextStyle(fontSize: 13)),
                    value: 1,
                    groupValue: _scopeKind,
                    activeColor: ErpColors.primary,
                    onChanged: (v) => setState(() => _scopeKind = v ?? 1),
                  ),
                  if (_scopeKind == 1) ...[
                    if (_loadingInstallments)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 8),
                        child: LinearProgressIndicator(minHeight: 2, color: ErpColors.primary),
                      )
                    else if (_installments.isEmpty)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 4),
                        child: Text(
                          'Aucune tranche disponible pour ce type de frais.',
                          style: TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                        ),
                      )
                    else
                      ..._installments.map(
                        (item) => CheckboxListTile(
                          contentPadding: EdgeInsets.zero,
                          dense: true,
                          controlAffinity: ListTileControlAffinity.leading,
                          title: Text(item.installmentName, style: const TextStyle(fontSize: 13)),
                          value: _installmentIds.contains(item.feeInstallmentId),
                          activeColor: ErpColors.primary,
                          onChanged: (checked) {
                            setState(() {
                              if (checked == true) {
                                _installmentIds.add(item.feeInstallmentId);
                              } else {
                                _installmentIds.remove(item.feeInstallmentId);
                              }
                            });
                          },
                        ),
                      ),
                  ],
                  const SizedBox(height: 8),
                  DropdownButtonFormField<int>(
                    value: _situationFilter,
                    decoration: const InputDecoration(labelText: 'Élèves à afficher', isDense: true),
                    items: _PaymentSituationScreenState._situationFilters
                        .map((f) => DropdownMenuItem(value: f.$1, child: Text(f.$2)))
                        .toList(),
                    onChanged: (v) => setState(() => _situationFilter = v ?? 0),
                  ),
                  const SizedBox(height: 10),
                  DropdownButtonFormField<int>(
                    value: _sortBy,
                    decoration: const InputDecoration(labelText: 'Tri', isDense: true),
                    items: _PaymentSituationScreenState._sortOptions
                        .map((s) => DropdownMenuItem(value: s.$1, child: Text(s.$2)))
                        .toList(),
                    onChanged: (v) => setState(() => _sortBy = v ?? 0),
                  ),
                  if (widget.structure != null) ...[
                    const SizedBox(height: 10),
                    DropdownButtonFormField<EnrollmentSection?>(
                      value: _section,
                      decoration: const InputDecoration(labelText: 'Section / Site (optionnel)', isDense: true),
                      items: [
                        const DropdownMenuItem(value: null, child: Text('Toutes les sections')),
                        ...widget.structure!.sections
                            .map((s) => DropdownMenuItem(value: s, child: Text(s.name))),
                      ],
                      onChanged: _onSectionChanged,
                    ),
                  ],
                  if (_isStudyOptionEnabled) ...[
                    const SizedBox(height: 10),
                    DropdownButtonFormField<String?>(
                      value: _studyOptions.contains(_studyOption) ? _studyOption : null,
                      decoration: const InputDecoration(labelText: 'Option d\'études', isDense: true),
                      items: [
                        const DropdownMenuItem(value: null, child: Text('Toutes les options')),
                        ..._studyOptions.map((o) => DropdownMenuItem(value: o, child: Text(o))),
                      ],
                      onChanged: (v) => setState(() {
                        _studyOption = v;
                        _classOption = null;
                      }),
                    ),
                  ],
                  if (widget.structure != null) ...[
                    const SizedBox(height: 10),
                    DropdownButtonFormField<EnrollmentClassOption?>(
                      value: _filteredClasses.any((c) => c.classRoomId == _classOption?.classRoomId)
                          ? _classOption
                          : null,
                      decoration: const InputDecoration(labelText: 'Classe (optionnel)', isDense: true),
                      items: [
                        const DropdownMenuItem(value: null, child: Text('Toutes les classes')),
                        ..._filteredClasses
                            .map((c) => DropdownMenuItem(value: c, child: Text(c.fullDisplayName))),
                      ],
                      onChanged: (v) => setState(() => _classOption = v),
                    ),
                  ],
                  const SizedBox(height: 10),
                  DropdownButtonFormField<PricingCategoryOption?>(
                    value: _pricingCategory,
                    decoration: const InputDecoration(labelText: 'Catégorie tarifaire', isDense: true),
                    items: [
                      const DropdownMenuItem(value: null, child: Text('Toutes les catégories')),
                      ...widget.pricingCategories
                          .map((c) => DropdownMenuItem(value: c, child: Text(c.name))),
                    ],
                    onChanged: (v) => setState(() => _pricingCategory = v),
                  ),
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
                        onPressed: widget.onReset,
                        child: const Text('Réinitialiser'),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: FilledButton(
                        onPressed: _feeType == null
                            ? null
                            : () {
                                if (_scopeKind == 1 && _installmentIds.isEmpty) {
                                  setState(() => _validationMessage = 'Sélectionnez au moins une tranche.');
                                  return;
                                }
                                Navigator.pop(context);
                                widget.onGenerate(
                                  feeType: _feeType,
                                  scopeKind: _scopeKind,
                                  installmentIds: _installmentIds,
                                  installments: _installments,
                                  situationFilter: _situationFilter,
                                  sortBy: _sortBy,
                                  section: _section,
                                  studyOption: _isStudyOptionEnabled ? _studyOption : null,
                                  classOption: _classOption,
                                  pricingCategory: _pricingCategory,
                                );
                              },
                        style: FilledButton.styleFrom(backgroundColor: ErpColors.primary),
                        child: const Text('Générer'),
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
// Détail élève — BottomSheet
// ---------------------------------------------------------------------------

class _PaymentSituationStudentDetailSheet extends StatelessWidget {
  const _PaymentSituationStudentDetailSheet({required this.row, required this.result});

  final PaymentSituationPivotRow row;
  final PaymentSituationReportResult result;

  @override
  Widget build(BuildContext context) {
    final columns = result.installmentColumns;
    final currency = result.currency;

    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.75,
      minChildSize: 0.4,
      maxChildSize: 0.92,
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
          Expanded(
            child: ListView(
              controller: scrollController,
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
              children: [
                Text(
                  row.fullName,
                  style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800, color: ErpColors.navy),
                ),
                if (row.registrationNumber.trim().isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(
                    row.registrationNumber,
                    style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                  ),
                ],
                const SizedBox(height: 12),
                _detailRow('Classe', row.className),
                const SizedBox(height: 16),
                const Text(
                  'SITUATION FINANCIÈRE',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w700,
                    color: ErpColors.textSecondary,
                    letterSpacing: 0.4,
                  ),
                ),
                const SizedBox(height: 8),
                _amountLine('Montant prévu', row.amountExpected, currency),
                _amountLine('Montant payé', row.amountPaid, currency),
                _amountLine(
                  'Reste',
                  row.balance,
                  currency,
                  valueColor: row.isInOrder ? ErpColors.success : ErpColors.danger,
                  bold: true,
                ),
                const SizedBox(height: 16),
                const Divider(height: 1, color: ErpColors.border),
                const SizedBox(height: 12),
                const Text(
                  'TRANCHES',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w700,
                    color: ErpColors.textSecondary,
                    letterSpacing: 0.4,
                  ),
                ),
                const SizedBox(height: 8),
                ...List.generate(columns.length, (i) {
                  final col = columns[i];
                  final applicable =
                      i < row.installmentApplicable.length && row.installmentApplicable[i];
                  if (!applicable) {
                    return _installmentBlock(
                      name: col.installmentName,
                      child: const Text(
                        '— Non applicable —',
                        style: TextStyle(fontSize: 12, fontStyle: FontStyle.italic, color: ErpColors.textSecondary),
                      ),
                    );
                  }
                  final expected = i < row.installmentExpected.length ? row.installmentExpected[i] : 0.0;
                  final paid = i < row.installmentPaid.length ? row.installmentPaid[i] : 0.0;
                  final balance = i < row.installmentBalances.length ? row.installmentBalances[i] : 0.0;
                  final inOrder = balance <= 0;
                  return _installmentBlock(
                    name: col.installmentName,
                    child: Column(
                      children: [
                        _amountLine('Prévu', expected, currency),
                        _amountLine('Payé', paid, currency),
                        Row(
                          children: [
                            Expanded(child: _amountLine('Reste', balance, currency, compact: true)),
                            Icon(
                              inOrder ? Icons.check_circle_rounded : Icons.error_outline_rounded,
                              size: 18,
                              color: inOrder ? ErpColors.success : ErpColors.danger,
                            ),
                          ],
                        ),
                      ],
                    ),
                  );
                }),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _detailRow(String label, String value) => Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 72,
            child: Text(label, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
          ),
          Expanded(
            child: Text(value, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: ErpColors.navy)),
          ),
        ],
      );

  Widget _amountLine(
    String label,
    double amount,
    String currency, {
    Color? valueColor,
    bool bold = false,
    bool compact = false,
  }) {
    return Padding(
      padding: EdgeInsets.symmetric(vertical: compact ? 2 : 3),
      child: Row(
        children: [
          Expanded(
            flex: 2,
            child: Text(
              label,
              style: TextStyle(fontSize: compact ? 12 : 13, color: ErpColors.textSecondary),
            ),
          ),
          Expanded(
            flex: 3,
            child: Text(
              formatMoney(amount, currency),
              textAlign: TextAlign.right,
              style: TextStyle(
                fontSize: compact ? 12 : 13,
                fontWeight: bold ? FontWeight.w800 : FontWeight.w600,
                color: valueColor ?? ErpColors.navy,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _installmentBlock({required String name, required Widget child}) {
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: const Color(0xFFF8FAFC),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: ErpColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(name, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: ErpColors.navy)),
          const SizedBox(height: 6),
          child,
        ],
      ),
    );
  }
}
