import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../promoteur/dashboard_formatters.dart';
import '../promoteur/models/promoteur_dashboard_models.dart';
import '../secretary/models/secretary_student_models.dart';
import 'daf_student_models.dart';
import 'daf_student_repository.dart';

/// Drill-down hiérarchique : régime → classe → élève (Dashboard DAF / Promoteur).
class EnrolledStudentsAnalyticsScreen extends ConsumerStatefulWidget {
  const EnrolledStudentsAnalyticsScreen({super.key});

  @override
  ConsumerState<EnrolledStudentsAnalyticsScreen> createState() => _EnrolledStudentsAnalyticsScreenState();
}

class _RegimeGroup {
  _RegimeGroup(this.name);

  final String name;
  int totalStudents = 0;
  int totalBoys = 0;
  int totalGirls = 0;
  final List<_ClassGroup> classes = [];
}

class _ClassGroup {
  _ClassGroup({
    required this.classRoomId,
    required this.className,
    required this.sectionName,
    required this.totalStudents,
    required this.boys,
    required this.girls,
  });

  final String classRoomId;
  final String className;
  final String sectionName;
  int totalStudents;
  int boys;
  int girls;
  List<dynamic>? students;
  bool loadingStudents = false;
  String? loadError;
}

class _EnrolledStudentsAnalyticsScreenState extends ConsumerState<EnrolledStudentsAnalyticsScreen> {
  EnrolledStudentsBySection? _data;
  String? _academicYearId;
  String? _yearLabel;
  String? _error;
  String? _lastUpdated;
  int _activeClassesCount = 0;
  bool _loading = true;
  final List<_RegimeGroup> _regimes = [];

  final _searchController = TextEditingController();
  Timer? _searchDebounce;
  bool _isSearchMode = false;
  bool _isSearching = false;
  List<StudentSummary> _searchResults = [];
  String? _searchStatusMessage;

  @override
  void initState() {
    super.initState();
    _searchController.addListener(_onSearchTextChanged);
    _load();
  }

  @override
  void dispose() {
    _searchDebounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _onSearchTextChanged() {
    final active = _searchController.text.trim().isNotEmpty;
    setState(() {
      _isSearchMode = active;
      if (!active) {
        _searchResults = [];
        _searchStatusMessage = null;
        _isSearching = false;
      }
    });
    if (!active) return;
    _searchDebounce?.cancel();
    _searchDebounce = Timer(const Duration(milliseconds: 400), _executeSearch);
  }

  Future<void> _executeSearch() async {
    final term = _searchController.text.trim();
    if (term.isEmpty) return;
    final yearId = _academicYearId;
    if (yearId == null || yearId.isEmpty) {
      setState(() {
        _searchStatusMessage = 'Année scolaire introuvable.';
        _searchResults = [];
      });
      return;
    }

    setState(() {
      _isSearching = true;
      _searchStatusMessage = null;
    });
    try {
      final page = await ref.read(dafStudentRepositoryProvider).searchEnrolledStudents(
            academicYearId: yearId,
            search: term,
          );
      if (!mounted) return;
      final enrolled = page.items
          .where((s) => s.isEnrolledCurrentYear || (s.currentYearClassName?.isNotEmpty ?? false))
          .toList()
        ..sort((a, b) => a.lastName.toLowerCase().compareTo(b.lastName.toLowerCase()));
      setState(() {
        _searchResults = enrolled;
        _searchStatusMessage =
            enrolled.isEmpty ? 'Aucun élève inscrit ne correspond à cette recherche.' : null;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _searchResults = [];
        _searchStatusMessage = e.toString();
      });
    } finally {
      if (mounted) setState(() => _isSearching = false);
    }
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _regimes.clear();
    });
    try {
      final prereq = await ref.read(dafStudentRepositoryProvider).getPrerequisites();
      final yearId = prereq.currentAcademicYearId;
      if (yearId == null || yearId.isEmpty) {
        throw StateError('Année scolaire courante introuvable.');
      }
      final data = await ref.read(promoteurDashboardRepositoryProvider).getEnrolledStudents();
      if (!mounted) return;
      _academicYearId = yearId;
      _yearLabel = prereq.currentAcademicYearLabel ?? 'Année courante';
      _data = data;
      _buildRegimeGroups(data);
      _activeClassesCount = _regimes.fold<int>(0, (sum, r) => sum + r.classes.length);
      _lastUpdated = DateFormat('dd/MM/yyyy HH:mm').format(DateTime.now());
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _buildRegimeGroups(EnrolledStudentsBySection data) {
    final map = <String, _RegimeGroup>{};
    for (final section in data.sections) {
      final regimeName = resolveStudentRegime(section.sectionName);
      final regime = map.putIfAbsent(regimeName, () => _RegimeGroup(regimeName));
      regime.totalStudents += section.totalStudents;
      regime.totalBoys += section.boys;
      regime.totalGirls += section.girls;

      for (final cls in section.classes) {
        final existing = regime.classes.where((c) => c.classRoomId == cls.classRoomId).firstOrNull;
        if (existing != null) {
          existing.totalStudents += cls.totalStudents;
          existing.boys += cls.boys;
          existing.girls += cls.girls;
        } else {
          regime.classes.add(_ClassGroup(
            classRoomId: cls.classRoomId,
            className: cls.className,
            sectionName: section.sectionName,
            totalStudents: cls.totalStudents,
            boys: cls.boys,
            girls: cls.girls,
          ));
        }
      }
    }

    final sorted = map.values.toList()..sort((a, b) => regimeSortKey(a.name).compareTo(regimeSortKey(b.name)));
    for (final regime in sorted) {
      regime.classes.sort((a, b) => a.className.compareTo(b.className));
    }
    _regimes.addAll(sorted);
  }

  Future<void> _loadClassStudents(_ClassGroup cls) async {
    if (cls.students != null || cls.loadingStudents) return;
    final yearId = _academicYearId;
    if (yearId == null) return;

    setState(() {
      cls.loadingStudents = true;
      cls.loadError = null;
    });
    try {
      final page = await ref.read(dafStudentRepositoryProvider).searchStudentsByClass(
            academicYearId: yearId,
            classRoomId: cls.classRoomId,
          );
      if (!mounted) return;
      setState(() => cls.students = page.items);
    } catch (e) {
      if (!mounted) return;
      setState(() => cls.loadError = e.toString());
    } finally {
      if (mounted) setState(() => cls.loadingStudents = false);
    }
  }

  void _openStudent(String studentId) {
    context.push('/admin/students/$studentId/consultation');
  }

  Widget _buildSearchResultTile(StudentSummary student) {
    final dob = DateTime.tryParse(student.dateOfBirth);
    final dobLabel = dob != null ? DateFormat('dd/MM/yyyy').format(dob) : student.dateOfBirth;
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Material(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        child: InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: () => _openStudent(student.id),
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 18,
                  backgroundColor: student.gender == 2 ? const Color(0xFFFDF2F8) : const Color(0xFFEFF6FF),
                  child: Text(
                    student.gender == 2 ? 'F' : 'M',
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      color: student.gender == 2 ? const Color(0xFFDB2777) : ErpColors.primary,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(student.fullName, style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.navy)),
                      Text(
                        '${student.registrationNumber} • ${student.currentYearClassName ?? '—'} • $dobLabel',
                        style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                      ),
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

  String _percent(int part, int total) =>
      total == 0 ? '—' : '${(100.0 * part / total).toStringAsFixed(2)} %';

  String _averagePerClass(EnrolledStudentsBySection data) =>
      _activeClassesCount == 0 ? '—' : (data.totalStudents / _activeClassesCount).toStringAsFixed(1);

  ({Color bg, Color border, Color title}) _regimeColors(String name) => switch (name) {
        'Maternelle' => (
            bg: const Color(0xFFFDF2F8),
            border: const Color(0xFFFBCFE8),
            title: const Color(0xFFBE185D),
          ),
        'Primaire' => (
            bg: const Color(0xFFEFF6FF),
            border: const Color(0xFFBFDBFE),
            title: const Color(0xFF1E3A8A),
          ),
        _ => (
            bg: const Color(0xFFECFDF5),
            border: const Color(0xFFA7F3D0),
            title: const Color(0xFF047857),
          ),
      };

  @override
  Widget build(BuildContext context) {
    final data = _data;
    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Élèves inscrits — analyse'),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
        actions: [
          IconButton(icon: const Icon(Icons.refresh_rounded), onPressed: _loading ? null : _load),
        ],
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
                          const Text(
                            'Regroupement par régime scolaire puis par classe. Consultez le détail jusqu\'à la fiche de l\'élève et sa situation financière.',
                            style: TextStyle(fontSize: 13, color: ErpColors.textSecondary, height: 1.35),
                          ),
                          const SizedBox(height: 12),
                          Row(
                            children: [
                              Expanded(
                                child: Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                                  decoration: BoxDecoration(
                                    color: Colors.white,
                                    borderRadius: BorderRadius.circular(12),
                                    border: Border.all(color: ErpColors.border),
                                  ),
                                  child: Row(
                                    children: [
                                      const Icon(Icons.calendar_month_outlined, size: 18, color: ErpColors.primary),
                                      const SizedBox(width: 8),
                                      Expanded(
                                        child: Column(
                                          crossAxisAlignment: CrossAxisAlignment.start,
                                          children: [
                                            const Text('Année scolaire', style: TextStyle(fontSize: 10, color: ErpColors.textSecondary, fontWeight: FontWeight.w600)),
                                            Text(_yearLabel ?? '—', style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.navy)),
                                          ],
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                              const SizedBox(width: 8),
                              FilledButton.icon(
                                onPressed: _loading ? null : _load,
                                icon: const Icon(Icons.refresh_rounded, size: 18),
                                label: const Text('Actualiser'),
                                style: FilledButton.styleFrom(
                                  backgroundColor: ErpColors.primary,
                                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 12),
                          TextField(
                            controller: _searchController,
                            decoration: InputDecoration(
                              hintText: 'Rechercher un élève inscrit (nom, matricule…)',
                              prefixIcon: const Icon(Icons.search),
                              suffixIcon: _searchController.text.isNotEmpty
                                  ? IconButton(
                                      icon: const Icon(Icons.clear),
                                      onPressed: () => _searchController.clear(),
                                    )
                                  : null,
                              filled: true,
                              fillColor: Colors.white,
                              border: OutlineInputBorder(
                                borderRadius: BorderRadius.circular(12),
                                borderSide: const BorderSide(color: ErpColors.border),
                              ),
                            ),
                          ),
                          if (_isSearchMode) ...[
                            const SizedBox(height: 12),
                            if (_isSearching)
                              const Center(child: Padding(padding: EdgeInsets.all(12), child: CircularProgressIndicator()))
                            else if (_searchStatusMessage != null)
                              Text(_searchStatusMessage!, style: const TextStyle(color: ErpColors.textSecondary, fontSize: 13))
                            else
                              Text(
                                '${_searchResults.length} élève(s) trouvé(s)',
                                style: const TextStyle(fontWeight: FontWeight.w600, color: ErpColors.navy),
                              ),
                            const SizedBox(height: 8),
                            ..._searchResults.map(_buildSearchResultTile),
                          ] else ...[
                          const SizedBox(height: 4),
                          SingleChildScrollView(
                            scrollDirection: Axis.horizontal,
                            child: Row(
                              children: [
                                _KpiCard.primary(label: 'TOTAL ÉLÈVES', value: '${data.totalStudents}', icon: Icons.groups_outlined),
                                _KpiCard.accent(label: 'GARÇONS', value: '${data.totalBoys}', sub: _percent(data.totalBoys, data.totalStudents), accent: ErpColors.primary, icon: Icons.male),
                                _KpiCard.accent(label: 'FILLES', value: '${data.totalGirls}', sub: _percent(data.totalGirls, data.totalStudents), accent: const Color(0xFFDB2777), icon: Icons.female),
                                _KpiCard.accent(label: 'MOYENNE / CLASSE', value: _averagePerClass(data), accent: ErpColors.success, icon: Icons.show_chart),
                                _KpiCard.accent(label: 'CLASSES ACTIVES', value: '$_activeClassesCount', accent: const Color(0xFF7C3AED), icon: Icons.class_outlined),
                              ],
                            ),
                          ),
                          const SizedBox(height: 18),
                          const Text(
                            'Régimes scolaires',
                            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: ErpColors.navy),
                          ),
                          const SizedBox(height: 8),
                          ..._regimes.map(_buildRegimeTile),
                          ],
                          if (_lastUpdated != null)
                            Padding(
                              padding: const EdgeInsets.only(top: 8),
                              child: Text(
                                'Dernière mise à jour : $_lastUpdated',
                                style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                                textAlign: TextAlign.right,
                              ),
                            ),
                        ],
                      ),
                    ),
    );
  }

  Widget _buildRegimeTile(_RegimeGroup regime) {
    final colors = _regimeColors(regime.name);
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        decoration: BoxDecoration(
          color: colors.bg,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: colors.border),
        ),
        child: Theme(
          data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
          child: ExpansionTile(
            tilePadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
            title: Text(
              regime.name.toUpperCase(),
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: colors.title),
            ),
            subtitle: Wrap(
              spacing: 6,
              runSpacing: 4,
              children: [
                _Badge(text: '${regime.totalStudents} élève(s)'),
                _Badge(text: '${regime.totalBoys} garçon(s)'),
                _Badge(text: '${regime.totalGirls} fille(s)'),
                _Badge(text: '${regime.classes.length}', bold: true),
              ],
            ),
            children: [
              Container(
                color: const Color(0xFFF8FAFC),
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                child: const Row(
                  children: [
                    Expanded(flex: 3, child: Text('Classe', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: ErpColors.textSecondary))),
                    Expanded(flex: 2, child: Text('Section', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: ErpColors.textSecondary))),
                    SizedBox(width: 36, child: Text('G', textAlign: TextAlign.right, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: ErpColors.textSecondary))),
                    SizedBox(width: 36, child: Text('F', textAlign: TextAlign.right, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: ErpColors.textSecondary))),
                    SizedBox(width: 36, child: Text('T', textAlign: TextAlign.right, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: ErpColors.textSecondary))),
                  ],
                ),
              ),
              ...regime.classes.map((cls) => _buildClassTile(cls)),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildClassTile(_ClassGroup cls) {
    return Container(
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: ErpColors.border)),
      ),
      child: Theme(
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          tilePadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 0),
          onExpansionChanged: (expanded) {
            if (expanded) _loadClassStudents(cls);
          },
          title: Row(
            children: [
              Expanded(flex: 3, child: Text(cls.className, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13))),
              Expanded(flex: 2, child: Text(cls.sectionName, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary))),
              SizedBox(width: 36, child: Text('${cls.boys}', textAlign: TextAlign.right, style: const TextStyle(fontSize: 12))),
              SizedBox(width: 36, child: Text('${cls.girls}', textAlign: TextAlign.right, style: const TextStyle(fontSize: 12))),
              SizedBox(width: 36, child: Text('${cls.totalStudents}', textAlign: TextAlign.right, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700))),
            ],
          ),
          children: [
            Container(
              margin: const EdgeInsets.fromLTRB(12, 0, 12, 12),
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: const Color(0xFFF8FAFC),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: ErpColors.border),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          cls.className.toUpperCase(),
                          style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 13, color: ErpColors.navy),
                        ),
                      ),
                      Text('${cls.totalStudents} élève(s)', style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary)),
                    ],
                  ),
                  if (cls.loadingStudents)
                    const Padding(
                      padding: EdgeInsets.all(12),
                      child: Center(child: SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2))),
                    )
                  else if (cls.loadError != null)
                    Padding(
                      padding: const EdgeInsets.all(8),
                      child: Text(cls.loadError!, style: const TextStyle(color: ErpColors.danger, fontSize: 12)),
                    )
                  else if (cls.students == null || cls.students!.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(8),
                      child: Text('Aucun élève', style: TextStyle(color: ErpColors.textSecondary, fontSize: 12)),
                    )
                  else
                    ...cls.students!.asMap().entries.map((entry) {
                      final s = entry.value;
                      final index = entry.key + 1;
                      return Container(
                        margin: const EdgeInsets.only(top: 8),
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(color: ErpColors.border),
                        ),
                        child: Row(
                          children: [
                            SizedBox(width: 22, child: Text('$index', style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary))),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(s.fullName, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
                                  Text(
                                    '${s.registrationNumber} · ${s.genderLabel}${s.dateOfBirth.isNotEmpty ? ' · ${DateFormat('dd/MM/yyyy').format(DateTime.tryParse(s.dateOfBirth) ?? DateTime.now())}' : ''}',
                                    style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                                  ),
                                ],
                              ),
                            ),
                            IconButton(
                              visualDensity: VisualDensity.compact,
                              icon: const Icon(Icons.visibility_outlined, color: ErpColors.primary, size: 20),
                              tooltip: 'Consulter',
                              onPressed: () => _openStudent(s.id),
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
      ),
    );
  }
}

class _KpiCard extends StatelessWidget {
  const _KpiCard._({
    required this.label,
    required this.value,
    required this.icon,
    this.sub,
    this.primary = false,
    this.accent,
  });

  factory _KpiCard.primary({required String label, required String value, required IconData icon}) =>
      _KpiCard._(label: label, value: value, icon: icon, primary: true);

  factory _KpiCard.accent({
    required String label,
    required String value,
    required Color accent,
    required IconData icon,
    String? sub,
  }) =>
      _KpiCard._(label: label, value: value, icon: icon, accent: accent, sub: sub);

  final String label;
  final String value;
  final String? sub;
  final IconData icon;
  final bool primary;
  final Color? accent;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 148,
      margin: const EdgeInsets.only(right: 10),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: primary ? ErpColors.navy : Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: primary ? ErpColors.navy : ErpColors.border),
        boxShadow: primary
            ? [BoxShadow(color: ErpColors.navy.withValues(alpha: 0.18), blurRadius: 10, offset: const Offset(0, 4))]
            : [BoxShadow(color: ErpColors.navy.withValues(alpha: 0.04), blurRadius: 8, offset: const Offset(0, 2))],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: primary ? Colors.white70 : accent),
          const SizedBox(height: 8),
          Text(
            label,
            style: TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w700,
              color: primary ? Colors.white70 : ErpColors.textSecondary,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            value,
            style: TextStyle(
              fontSize: 28,
              fontWeight: FontWeight.w800,
              color: primary ? Colors.white : ErpColors.navy,
            ),
          ),
          if (sub != null)
            Text(
              sub!,
              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: primary ? Colors.white70 : accent),
            ),
        ],
      ),
    );
  }
}

class _Badge extends StatelessWidget {
  const _Badge({required this.text, this.bold = false});

  final String text;
  final bool bold;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: ErpColors.border),
      ),
      child: Text(
        text,
        style: TextStyle(fontSize: 11, color: ErpColors.textSecondary, fontWeight: bold ? FontWeight.w700 : FontWeight.w500),
      ),
    );
  }
}

class DafStudentConsultationScreen extends ConsumerStatefulWidget {
  const DafStudentConsultationScreen({super.key, required this.studentId});

  final String studentId;

  @override
  ConsumerState<DafStudentConsultationScreen> createState() => _DafStudentConsultationScreenState();
}

class _FinancialGroup {
  _FinancialGroup({
    required this.feeTypeName,
    required this.categoryName,
    required this.currency,
  });

  final String feeTypeName;
  final String categoryName;
  final String currency;
  final List<InstallmentPlanLine> lines = [];
}

class _DafStudentConsultationScreenState extends ConsumerState<DafStudentConsultationScreen> {
  bool _loading = true;
  String? _error;
  StudentDossierPayload? _dossier;
  Map<String, dynamic>? _profileEnrollment;
  StudentFinancialSummary? _summary;
  final List<_FinancialGroup> _financialGroups = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _financialGroups.clear();
    });
    try {
      final repo = ref.read(dafStudentRepositoryProvider);
      final prereq = await repo.getPrerequisites();
      final yearId = prereq.currentAcademicYearId;
      if (yearId == null || yearId.isEmpty) {
        throw StateError('Année scolaire courante introuvable.');
      }

      final dossier = await repo.getStudentDossier(widget.studentId);
      final profile = await repo.getStudentProfile(widget.studentId);
      final summary = await repo.getFinancialSummary(widget.studentId, yearId);
      final situations = await repo.getPaymentSituations(studentId: widget.studentId, academicYearId: yearId);
      final catalog = await repo.getFeeCatalog();

      final enrollments = profile.enrollments;
      final current = enrollments.where((e) => e.isCurrentYear && e.isActive).firstOrNull ??
          enrollments.firstOrNull;

      final groups = <_FinancialGroup>[];
      final categoryFallback = situations.items.firstOrNull?.feePricingCategoryName ?? '—';

      for (final feeType in catalog.feeTypes.where((f) => f.isActive)) {
        try {
          final plan = await repo.getInstallmentPlan(dossier.enrollmentId, feeType.id);
          if (plan.lines.isEmpty) continue;
          final situation = situations.items.where((s) => s.feeTypeId == feeType.id).firstOrNull;
          final group = _FinancialGroup(
            feeTypeName: feeType.name,
            categoryName: situation?.feePricingCategoryName ?? categoryFallback,
            currency: plan.currency,
          );
          group.lines.addAll(plan.lines);
          groups.add(group);
        } catch (_) {
          // Type de frais sans tranches pour cette inscription.
        }
      }

      if (groups.isEmpty && situations.items.isNotEmpty) {
        for (final item in situations.items) {
          final group = _FinancialGroup(
            feeTypeName: item.feeTypeName,
            categoryName: item.feePricingCategoryName,
            currency: item.currency,
          );
          group.lines.add(InstallmentPlanLine(
            installmentName: 'Global',
            amountExpected: item.amountExpected,
            amountPaid: item.amountPaid,
            remaining: item.balance,
          ));
          groups.add(group);
        }
      }

      if (!mounted) return;
      setState(() {
        _dossier = dossier;
        _profileEnrollment = current != null
            ? {
                'academicYearLabel': current.academicYearLabel,
                'sectionName': current.sectionName,
                'classDisplayName': current.classDisplayName,
                'enrollmentDate': current.enrollmentDate,
              }
            : null;
        _summary = summary;
        _financialGroups.addAll(groups);
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  String _formatDate(String? raw) {
    if (raw == null || raw.isEmpty) return '—';
    final dt = DateTime.tryParse(raw);
    if (dt == null) return raw;
    return DateFormat('dd/MM/yyyy').format(dt.toLocal());
  }

  String? _readString(Map<String, dynamic>? map, String key) {
    if (map == null) return null;
    final value = map[key];
    if (value == null) return null;
    final text = '$value'.trim();
    return text.isEmpty ? null : text;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Fiche élève'),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        elevation: 0,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!, style: const TextStyle(color: ErpColors.danger)))
              : _dossier == null
                  ? const Center(child: Text('Élève introuvable'))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView(
                        padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
                        children: [
                          _buildIdentityHeader(),
                          const SizedBox(height: 12),
                          _buildSection('Identification', _identificationItems()),
                          _buildSection('Adresse', _addressItems()),
                          _buildSection('Scolarité', _schoolItems()),
                          _buildGuardiansSection(),
                          _buildSection('Informations médicales', _medicalItems()),
                          _buildSection('Documents', _documentItems()),
                          const SizedBox(height: 8),
                          _buildFinancialSection(),
                        ],
                      ),
                    ),
    );
  }

  Widget _buildIdentityHeader() {
    final d = _dossier!.dossier;
    final lastName = _readString(d, 'lastName') ?? '';
    final middleName = _readString(d, 'middleName') ?? '';
    final firstName = _readString(d, 'firstName') ?? '';
    final fullName = [lastName, middleName, firstName].where((p) => p.isNotEmpty).join(' ');
    final photoPath = _readString(d, 'photoPath');

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: ErpColors.border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            radius: 32,
            backgroundColor: ErpColors.primary.withValues(alpha: 0.12),
            backgroundImage: photoPath != null && photoPath.isNotEmpty ? NetworkImage(photoPath) : null,
            child: photoPath == null || photoPath.isEmpty
                ? const Icon(Icons.person, color: ErpColors.primary, size: 32)
                : null,
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(fullName, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: ErpColors.navy)),
                const SizedBox(height: 4),
                Text(
                  'Matricule : ${_dossier!.registrationNumber}',
                  style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  List<MapEntry<String, String>> _identificationItems() {
    final d = _dossier!.dossier;
    final gender = _readString(d, 'gender');
    return [
      MapEntry('Matricule', _dossier!.registrationNumber),
      MapEntry('Nom', _readString(d, 'lastName') ?? '—'),
      MapEntry('Postnom', _readString(d, 'middleName') ?? '—'),
      MapEntry('Prénom', _readString(d, 'firstName') ?? '—'),
      MapEntry('Sexe', gender == '2' || gender?.toLowerCase() == 'feminin' ? 'Féminin' : 'Masculin'),
      MapEntry('Date de naissance', _formatDate(_readString(d, 'dateOfBirth'))),
      MapEntry('Lieu de naissance', _readString(d, 'placeOfBirth') ?? '—'),
      MapEntry('Nationalité', _readString(d, 'nationality') ?? '—'),
      MapEntry('Téléphone', _readString(d, 'phone') ?? '—'),
      MapEntry('Email', _readString(d, 'email') ?? '—'),
    ];
  }

  List<MapEntry<String, String>> _addressItems() {
    final addr = _dossier!.dossier['residenceAddress'];
    if (addr is! Map) return const [MapEntry('—', 'Non renseignée')];
    final a = Map<String, dynamic>.from(addr);
    return [
      MapEntry('Quartier', _readString(a, 'neighborhood') ?? '—'),
      MapEntry('Avenue', _readString(a, 'avenue') ?? '—'),
      MapEntry('Numéro', _readString(a, 'houseNumber') ?? '—'),
    ];
  }

  List<MapEntry<String, String>> _schoolItems() {
    final sc = _dossier!.dossier['scolarite'];
    final scMap = sc is Map ? Map<String, dynamic>.from(sc) : <String, dynamic>{};
    final enr = _profileEnrollment;
    return [
      MapEntry('Année scolaire', enr?['academicYearLabel']?.toString() ?? '—'),
      MapEntry('Section', enr?['sectionName']?.toString() ?? '—'),
      MapEntry('Classe', enr?['classDisplayName']?.toString() ?? '—'),
      MapEntry('Date d\'inscription', _formatDate(enr?['enrollmentDate']?.toString())),
      MapEntry('École précédente', _readString(scMap, 'previousSchool') ?? '—'),
      MapEntry('Code élève précédent', _readString(scMap, 'previousStudentCode') ?? '—'),
    ];
  }

  List<MapEntry<String, String>> _medicalItems() {
    final med = _dossier!.dossier['medical'];
    if (med is! Map) return const [];
    final m = Map<String, dynamic>.from(med);
    return [
      MapEntry('Groupe sanguin', _readString(m, 'bloodGroup') ?? '—'),
      MapEntry('Allergies', _readString(m, 'allergies') ?? '—'),
      MapEntry('Maladies chroniques', _readString(m, 'chronicDiseases') ?? '—'),
      MapEntry('Handicap', _readString(m, 'disability') ?? '—'),
      MapEntry('Médecin', _readString(m, 'doctorName') ?? '—'),
      MapEntry('Centre médical', _readString(m, 'medicalCenter') ?? '—'),
    ];
  }

  List<MapEntry<String, String>> _documentItems() {
    final docs = _dossier!.dossier['documents'];
    if (docs is! List || docs.isEmpty) return const [MapEntry('—', 'Aucun document')];
    return docs.whereType<Map>().map((raw) {
      final doc = Map<String, dynamic>.from(raw);
      return MapEntry(
        _readString(doc, 'documentType') ?? 'Document',
        _readString(doc, 'status') ?? '—',
      );
    }).toList();
  }

  Widget _buildSection(String title, List<MapEntry<String, String>> items) {
    if (items.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: ErpColors.border),
        ),
        child: Theme(
          data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
          child: ExpansionTile(
            initiallyExpanded: title == 'Identification' || title == 'Scolarité',
            title: Text(title, style: const TextStyle(fontWeight: FontWeight.w800, color: ErpColors.navy)),
            children: items
                .map(
                  (item) => ListTile(
                    dense: true,
                    title: Text(item.key, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
                    subtitle: Text(item.value, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
                  ),
                )
                .toList(),
          ),
        ),
      ),
    );
  }

  Widget _buildGuardiansSection() {
    final guardians = _dossier!.dossier['guardians'];
    if (guardians is! List || guardians.isEmpty) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: ErpColors.border),
        ),
        child: Theme(
          data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
          child: ExpansionTile(
            title: const Text('Responsables', style: TextStyle(fontWeight: FontWeight.w800, color: ErpColors.navy)),
            children: guardians.whereType<Map>().map((raw) {
              final g = Map<String, dynamic>.from(raw);
              final name = '${_readString(g, 'lastName') ?? ''} ${_readString(g, 'firstName') ?? ''}'.trim();
              return ListTile(
                dense: true,
                title: Text(name.isEmpty ? 'Responsable' : name, style: const TextStyle(fontWeight: FontWeight.w700)),
                subtitle: Text(
                  [
                    if (_readString(g, 'relationship') != null) _readString(g, 'relationship'),
                    if (_readString(g, 'phone') != null) _readString(g, 'phone'),
                    if (_readString(g, 'email') != null) _readString(g, 'email'),
                    if (_readString(g, 'profession') != null) _readString(g, 'profession'),
                  ].whereType<String>().join(' · '),
                  style: const TextStyle(fontSize: 12),
                ),
              );
            }).toList(),
          ),
        ),
      ),
    );
  }

  Widget _buildFinancialSection() {
    final summary = _summary;
    if (summary == null) return const SizedBox.shrink();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'Situation financière',
          style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: ErpColors.navy),
        ),
        const SizedBox(height: 8),
        _ReceivablesTotalsCard(
          expected: summary.totalDue,
          paid: summary.totalPaid,
          remaining: summary.balance,
          currency: summary.currency,
        ),
        const SizedBox(height: 12),
        ..._financialGroups.map((group) => _buildFinancialGroupCard(group)),
        if (_financialGroups.isEmpty)
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: ErpColors.border),
            ),
            child: const Text(
              'Aucune situation financière disponible pour cette année.',
              style: TextStyle(color: ErpColors.textSecondary, fontSize: 12),
            ),
          ),
      ],
    );
  }

  Widget _buildFinancialGroupCard(_FinancialGroup group) {
    final expected = group.lines.fold<double>(0, (s, l) => s + l.amountExpected);
    final paid = group.lines.fold<double>(0, (s, l) => s + l.amountPaid);
    final remaining = group.lines.fold<double>(0, (s, l) => s + l.remaining);

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: ErpColors.border),
        ),
        child: Theme(
          data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
          child: ExpansionTile(
            title: Text(group.feeTypeName, style: const TextStyle(fontWeight: FontWeight.w800)),
            subtitle: Text(
              'Catégorie : ${group.categoryName}',
              style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
            ),
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
                child: Column(
                  children: [
                    ...group.lines.map(
                      (line) => Container(
                        margin: const EdgeInsets.only(bottom: 8),
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: ErpColors.pageBackground,
                          borderRadius: BorderRadius.circular(10),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(line.installmentName, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
                            const SizedBox(height: 6),
                            Row(
                              children: [
                                Expanded(child: _MoneyChip(label: 'Prévu', value: formatMoney(line.amountExpected, group.currency))),
                                const SizedBox(width: 6),
                                Expanded(child: _MoneyChip(label: 'Payé', value: formatMoney(line.amountPaid, group.currency), color: ErpColors.success)),
                                const SizedBox(width: 6),
                                Expanded(child: _MoneyChip(label: 'Reste', value: formatMoney(line.remaining, group.currency), color: ErpColors.warning)),
                              ],
                            ),
                            if (line.dueDate != null)
                              Padding(
                                padding: const EdgeInsets.only(top: 4),
                                child: Text(
                                  'Échéance : ${_formatDate(line.dueDate)}',
                                  style: const TextStyle(fontSize: 11, color: ErpColors.textSecondary),
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: ErpColors.primary.withValues(alpha: 0.06),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Row(
                        children: [
                          Expanded(child: _MoneyChip(label: 'Total prévu', value: formatMoney(expected, group.currency), bold: true)),
                          Expanded(child: _MoneyChip(label: 'Total payé', value: formatMoney(paid, group.currency), bold: true, color: ErpColors.success)),
                          Expanded(child: _MoneyChip(label: 'Total reste', value: formatMoney(remaining, group.currency), bold: true, color: ErpColors.warning)),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
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
          Expanded(child: _TotalCell(label: 'Total prévu', value: formatMoney(expected, currency))),
          Expanded(child: _TotalCell(label: 'Total payé', value: formatMoney(paid, currency), color: ErpColors.success)),
          Expanded(child: _TotalCell(label: 'Total reste', value: formatMoney(remaining, currency), color: ErpColors.warning)),
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

class _MoneyChip extends StatelessWidget {
  const _MoneyChip({
    required this.label,
    required this.value,
    this.color,
    this.bold = false,
  });

  final String label;
  final String value;
  final Color? color;
  final bool bold;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 10, color: ErpColors.textSecondary)),
        Text(
          value,
          style: TextStyle(
            fontSize: bold ? 12 : 11,
            fontWeight: bold ? FontWeight.w800 : FontWeight.w600,
            color: color ?? ErpColors.navy,
          ),
        ),
      ],
    );
  }
}
