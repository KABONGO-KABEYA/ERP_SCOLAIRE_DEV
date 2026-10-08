import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../core/api/api_error_message.dart';
import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../core/widgets/erp_qr_scanner.dart';
import 'controller_models.dart';

class ControllerHomeScreen extends ConsumerStatefulWidget {
  const ControllerHomeScreen({super.key});

  @override
  ConsumerState<ControllerHomeScreen> createState() =>
      _ControllerHomeScreenState();
}

class _ControllerHomeScreenState extends ConsumerState<ControllerHomeScreen> {
  ControllerSetup? _setup;
  List<ControllerInstallment> _installments = const [];
  List<ControllerStudentSearchItem> _students = const [];
  ControllerCheckResult? _result;
  String? _yearId;
  String? _feeTypeId;
  String? _installmentId;
  int _mode = 1;
  bool _loading = true;
  bool _checking = false;
  bool _searching = false;
  bool _scanLocked = false;
  bool _configurationExpanded = true;
  bool _controlExpanded = true;
  int _controlTab = 0;
  final _scrollController = ScrollController();
  String? _error;
  final _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    unawaited(_loadSetup());
  }

  @override
  void dispose() {
    _scrollController.dispose();
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _loadSetup({String? yearId}) async {
    setState(() {
      _configurationExpanded = true;
      _controlExpanded = true;
      _loading = true;
      _error = null;
      _result = null;
    });
    try {
      final setup = await ref
          .read(controllerRepositoryProvider)
          .getSetup(academicYearId: yearId);
      if (!mounted) return;
      final selectedYear = yearId ??
          setup.currentAcademicYearId ??
          (setup.academicYears.isEmpty ? null : setup.academicYears.first.id);
      setState(() {
        _setup = setup;
        _yearId = selectedYear;
        _feeTypeId = null;
        _installmentId = null;
        _installments = const [];
        _students = const [];
      });
    } catch (e) {
      if (mounted) setState(() => _error = resolveDashboardErrorMessage(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _selectFeeType(String? value) async {
    setState(() {
      _feeTypeId = value;
      _installmentId = null;
      _installments = const [];
      _students = const [];
      _result = null;
      _error = null;
    });
    if (value == null || _yearId == null) return;
    try {
      final items =
          await ref.read(controllerRepositoryProvider).getInstallments(
                academicYearId: _yearId!,
                feeTypeId: value,
              );
      if (mounted && _feeTypeId == value) {
        setState(() => _installments = items);
      }
    } catch (e) {
      if (mounted) setState(() => _error = resolveDashboardErrorMessage(e));
    }
  }

  bool get _selectionReady =>
      _yearId != null &&
      _feeTypeId != null &&
      (_mode == 1 || _installmentId != null);

  Map<String, Object?> get _selection => {
        'academicYearId': _yearId!,
        'feeTypeId': _feeTypeId!,
        'mode': _mode,
        'feeInstallmentId': _installmentId,
      };

  void _onDetect(BarcodeCapture capture) {
    if (_scanLocked || _checking || !_selectionReady || !_controlExpanded) {
      return;
    }
    final value = capture.barcodes
        .map((b) => b.rawValue?.trim())
        .whereType<String>()
        .firstWhere((v) => v.isNotEmpty, orElse: () => '');
    if (value.isEmpty) return;
    _scanLocked = true;
    unawaited(_checkQr(value));
  }

  Future<void> _checkQr(String qrPayload) async {
    setState(() {
      _checking = true;
      _error = null;
      _result = null;
    });
    try {
      final s = _selection;
      final result = await ref.read(controllerRepositoryProvider).checkQr(
            qrPayload: qrPayload,
            academicYearId: s['academicYearId']! as String,
            feeTypeId: s['feeTypeId']! as String,
            mode: s['mode']! as int,
            feeInstallmentId: s['feeInstallmentId'] as String?,
          );
      if (mounted) _showResult(result);
    } catch (e) {
      if (mounted) setState(() => _error = resolveDashboardErrorMessage(e));
    } finally {
      if (mounted) {
        setState(() {
          _checking = false;
          _scanLocked = false;
        });
      }
    }
  }

  Future<void> _search() async {
    final query = _searchController.text.trim();
    if (!_selectionReady || query.length < 2) return;
    setState(() {
      _searching = true;
      _students = const [];
      _result = null;
      _error = null;
    });
    try {
      final data = await ref.read(controllerRepositoryProvider).searchStudents(
            academicYearId: _yearId!,
            feeTypeId: _feeTypeId!,
            search: query,
          );
      if (mounted) setState(() => _students = data.items);
    } catch (e) {
      if (mounted) setState(() => _error = resolveDashboardErrorMessage(e));
    } finally {
      if (mounted) setState(() => _searching = false);
    }
  }

  Future<void> _checkStudent(String studentId) async {
    setState(() {
      _checking = true;
      _error = null;
      _result = null;
    });
    try {
      final s = _selection;
      final result = await ref.read(controllerRepositoryProvider).checkStudent(
            studentId: studentId,
            academicYearId: s['academicYearId']! as String,
            feeTypeId: s['feeTypeId']! as String,
            mode: s['mode']! as int,
            feeInstallmentId: s['feeInstallmentId'] as String?,
          );
      if (mounted) _showResult(result);
    } catch (e) {
      if (mounted) setState(() => _error = resolveDashboardErrorMessage(e));
    } finally {
      if (mounted) setState(() => _checking = false);
    }
  }

  void _showResult(ControllerCheckResult result) {
    FocusScope.of(context).unfocus();
    setState(() {
      _result = result;
      _configurationExpanded = false;
      _controlExpanded = false;
    });
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _scrollController.hasClients) {
        _scrollController.animateTo(0,
            duration: const Duration(milliseconds: 250), curve: Curves.easeOut);
      }
    });
  }

  Widget _panelHeader(
          String title, IconData icon, bool expanded, VoidCallback? onToggle) =>
      Row(
        children: [
          Icon(icon, color: ErpColors.primary),
          const SizedBox(width: 10),
          Expanded(child: Text(title, style: ErpTextStyles.title)),
          IconButton(
            tooltip: expanded ? 'Replier' : 'Déplier',
            onPressed: onToggle,
            icon: Icon(expanded ? Icons.expand_less : Icons.expand_more),
          ),
        ],
      );

  Future<void> _logout() async {
    final connection = ref.read(connectionModeProvider);
    await ref.read(authRepositoryProvider).logout(baseUrl: connection.baseUrl);
    await ref.read(authStateProvider.notifier).setLoggedIn(false);
    if (mounted) context.go('/login');
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Contrôle des frais'),
        actions: [
          IconButton(
            tooltip: 'Se déconnecter',
            onPressed: _logout,
            icon: const Icon(Icons.logout_rounded),
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: () => _loadSetup(yearId: _yearId),
              child: ListView(
                controller: _scrollController,
                padding: const EdgeInsets.all(ErpSpacing.page),
                children: [
                  _configurationCard(),
                  if (_error != null) ...[
                    const SizedBox(height: 12),
                    _message(_error!, ErpColors.danger, Icons.error_outline),
                  ],
                  if (_selectionReady) ...[
                    const SizedBox(height: 16),
                    _controlArea(),
                  ],
                  if (_checking) ...[
                    const SizedBox(height: 16),
                    const Center(child: CircularProgressIndicator()),
                  ],
                  if (_result != null) ...[
                    const SizedBox(height: 16),
                    _resultCard(_result!),
                  ],
                  const SizedBox(height: 32),
                ],
              ),
            ),
    );
  }

  Widget _configurationCard() {
    final setup = _setup;
    return ErpCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _panelHeader(
            'Paramètres du contrôle',
            Icons.tune_rounded,
            _configurationExpanded,
            _selectionReady
                ? () => setState(
                    () => _configurationExpanded = !_configurationExpanded)
                : null,
          ),
          if (!_configurationExpanded)
            Text(
              '${_result?.feeTypeName ?? (_setup?.feeTypes.where((f) => f.id == _feeTypeId).map((f) => f.name).firstOrNull ?? '')} • ${_mode == 1 ? 'Total annuel' : 'Par tranche'}',
              style: ErpTextStyles.label,
            ),
          if (_configurationExpanded) ...[
            const SizedBox(height: 16),
            DropdownButtonFormField<String>(
              key: ValueKey('year-$_yearId'),
              initialValue: _yearId,
              decoration: const InputDecoration(labelText: 'Année scolaire'),
              items: (setup?.academicYears ?? const [])
                  .map((y) => DropdownMenuItem(
                        value: y.id,
                        child: Text(
                            '${y.label}${y.isCurrent ? '  • En cours' : ''}'),
                      ))
                  .toList(),
              onChanged: (value) {
                if (value != null && value != _yearId) {
                  unawaited(_loadSetup(yearId: value));
                }
              },
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              key: ValueKey('fee-$_yearId-$_feeTypeId'),
              initialValue: _feeTypeId,
              decoration: const InputDecoration(labelText: 'Type de frais'),
              items: (setup?.feeTypes ?? const [])
                  .map((f) => DropdownMenuItem(
                        value: f.id,
                        child: Text('${f.name} (${f.currency})'),
                      ))
                  .toList(),
              onChanged: _selectFeeType,
            ),
            if ((setup?.feeTypes ?? const []).isEmpty && _yearId != null) ...[
              const SizedBox(height: 8),
              const Text(
                'Aucun type de frais n’est configuré pour cette année.',
                style: TextStyle(color: ErpColors.warning),
              ),
            ],
            const SizedBox(height: 16),
            const Text('Mode de contrôle', style: ErpTextStyles.label),
            const SizedBox(height: 8),
            SegmentedButton<int>(
              segments: const [
                ButtonSegment(
                    value: 1,
                    icon: Icon(Icons.summarize_outlined),
                    label: Text('Total annuel')),
                ButtonSegment(
                    value: 2,
                    icon: Icon(Icons.payments_outlined),
                    label: Text('Par tranche')),
              ],
              selected: {_mode},
              onSelectionChanged: (values) => setState(() {
                _mode = values.first;
                _result = null;
                if (_mode == 1) _installmentId = null;
              }),
            ),
            if (_mode == 2) ...[
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                key: ValueKey('installment-$_feeTypeId-$_installmentId'),
                initialValue: _installmentId,
                decoration:
                    const InputDecoration(labelText: 'Tranche de paiement'),
                items: _installments
                    .map((i) =>
                        DropdownMenuItem(value: i.id, child: Text(i.name)))
                    .toList(),
                onChanged: _feeTypeId == null
                    ? null
                    : (value) => setState(() {
                          _installmentId = value;
                          _result = null;
                        }),
              ),
            ],
          ],
        ],
      ),
    );
  }

  Widget _controlArea() => DefaultTabController(
        length: 2,
        initialIndex: _controlTab,
        child: ErpCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _panelHeader(
                'Scanner ou rechercher',
                Icons.person_search_outlined,
                _controlExpanded,
                () {
                  FocusScope.of(context).unfocus();
                  setState(() => _controlExpanded = !_controlExpanded);
                },
              ),
              if (_controlExpanded) ...[
                TabBar(
                  onTap: (index) => _controlTab = index,
                  tabs: const [
                    Tab(
                        icon: Icon(Icons.qr_code_scanner_rounded),
                        text: 'Scanner'),
                    Tab(icon: Icon(Icons.search_rounded), text: 'Rechercher'),
                  ],
                ),
                const SizedBox(height: 14),
                SizedBox(
                  height: 390,
                  child: TabBarView(
                    children: [
                      Column(
                        children: [
                          const Text(
                            'Placez le QR de la carte de l’élève dans le cadre.',
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: 12),
                          ErpQrScanner(onDetect: _onDetect, height: 285),
                        ],
                      ),
                      Column(
                        children: [
                          TextField(
                            controller: _searchController,
                            textInputAction: TextInputAction.search,
                            onSubmitted: (_) => _search(),
                            decoration: InputDecoration(
                              labelText: 'Matricule ou nom de l’élève',
                              suffixIcon: IconButton(
                                onPressed: _searching ? null : _search,
                                icon: _searching
                                    ? const SizedBox.square(
                                        dimension: 18,
                                        child: CircularProgressIndicator(
                                            strokeWidth: 2),
                                      )
                                    : const Icon(Icons.search),
                              ),
                            ),
                          ),
                          const SizedBox(height: 10),
                          Expanded(
                            child: _students.isEmpty
                                ? const Center(
                                    child: Text(
                                        'Saisissez au moins deux caractères.'),
                                  )
                                : ListView.separated(
                                    itemCount: _students.length,
                                    separatorBuilder: (_, __) =>
                                        const Divider(height: 1),
                                    itemBuilder: (_, index) {
                                      final student = _students[index];
                                      return ListTile(
                                        leading: const CircleAvatar(
                                            child: Icon(Icons.person_outline)),
                                        title: Text(student.fullName),
                                        subtitle: Text(
                                            '${student.registrationNumber} • ${student.className}'),
                                        trailing:
                                            const Icon(Icons.chevron_right),
                                        onTap: _checking
                                            ? null
                                            : () => _checkStudent(
                                                student.studentId),
                                      );
                                    },
                                  ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ],
            ],
          ),
        ),
      );

  Widget _resultCard(ControllerCheckResult result) {
    final color = switch (result.status) {
      1 => ErpColors.success,
      2 => ErpColors.warning,
      3 || 4 => ErpColors.danger,
      5 => ErpColors.primary,
      _ => ErpColors.textSecondary,
    };
    final money = NumberFormat('#,##0.00', 'fr_FR');
    return ErpCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
            decoration: BoxDecoration(
              color: color.withValues(alpha: .12),
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: color.withValues(alpha: .45)),
            ),
            child: Row(
              children: [
                Icon(result.status == 1 ? Icons.check_circle : Icons.info,
                    color: color, size: 32),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    result.statusLabel,
                    style: TextStyle(
                        color: color,
                        fontSize: 20,
                        fontWeight: FontWeight.w700),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          Text(result.fullName, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 4),
          Text('${result.registrationNumber} • ${result.className}'),
          if (result.cardNumber != null) Text('Carte : ${result.cardNumber}'),
          const Divider(height: 28),
          _line('Année scolaire', result.academicYearLabel),
          _line('Type de frais', result.feeTypeName),
          _line(
              'Contrôle',
              result.mode == 1
                  ? 'Total annuel'
                  : result.installmentName ?? 'Tranche'),
          if (result.dueDate != null) _line('Échéance', result.dueDate!),
          const Divider(height: 28),
          _moneyLine('Montant prévu', money.format(result.amountExpected),
              result.currency),
          _moneyLine(
              'Montant payé', money.format(result.amountPaid), result.currency,
              color: ErpColors.success),
          _moneyLine(
            result.balance < 0 ? 'Crédit' : 'Reste à payer',
            money.format(result.balance.abs()),
            result.currency,
            color: color,
          ),
          const SizedBox(height: 16),
          FilledButton.icon(
            onPressed: () => setState(() {
              _result = null;
              _students = const [];
              _searchController.clear();
              _controlExpanded = true;
            }),
            icon: const Icon(Icons.qr_code_scanner),
            label: const Text('Contrôler un autre élève'),
          ),
        ],
      ),
    );
  }

  Widget _line(String label, String value) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SizedBox(
                width: 120, child: Text(label, style: ErpTextStyles.label)),
            Expanded(
                child: Text(value,
                    style: const TextStyle(fontWeight: FontWeight.w600))),
          ],
        ),
      );

  Widget _moneyLine(String label, String amount, String currency,
          {Color? color}) =>
      Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(
          children: [
            Expanded(child: Text(label)),
            Text(
              '$amount $currency',
              style: TextStyle(
                  fontSize: 17, fontWeight: FontWeight.w700, color: color),
            ),
          ],
        ),
      );

  Widget _message(String text, Color color, IconData icon) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: color.withValues(alpha: .1),
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: color.withValues(alpha: .35)),
        ),
        child: Row(
          children: [
            Icon(icon, color: color),
            const SizedBox(width: 10),
            Expanded(child: Text(text, style: TextStyle(color: color))),
          ],
        ),
      );
}
