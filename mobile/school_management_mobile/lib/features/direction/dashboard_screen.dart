import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../router/app_router.dart';
import 'models/direction_models.dart';

class DirectionDashboardScreen extends ConsumerStatefulWidget {
  const DirectionDashboardScreen({super.key});

  @override
  ConsumerState<DirectionDashboardScreen> createState() => _DirectionDashboardScreenState();
}

class _DirectionDashboardScreenState extends ConsumerState<DirectionDashboardScreen> {
  DashboardStats? _dashboard;
  FinancialSummary? _financial;
  List<EnrollmentByClass> _enrollment = [];
  List<ClassAverageReport> _averages = [];
  bool _loading = true;
  String? _error;
  String? _userName;

  @override
  void initState() {
    super.initState();
    _load();
    currentUserName().then((name) {
      if (mounted) setState(() => _userName = name);
    });
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final repo = ref.read(directionRepositoryProvider);
      final results = await Future.wait([
        repo.getDashboard(),
        repo.getFinancialSummary(),
        repo.getEnrollmentByClass(),
        repo.getClassAverages(),
      ]);
      setState(() {
        _dashboard = results[0] as DashboardStats;
        _financial = results[1] as FinancialSummary;
        _enrollment = results[2] as List<EnrollmentByClass>;
        _averages = results[3] as List<ClassAverageReport>;
      });
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Tableau de bord Direction'),
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
          IconButton(icon: const Icon(Icons.logout), onPressed: () => logout(ref, context)),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.all(ErpSpacing.page),
                children: [
                  if (_userName != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 16),
                      child: Text('Bonjour, $_userName', style: Theme.of(context).textTheme.titleLarge),
                    ),
                  if (_error != null)
                    Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                  if (_dashboard != null) ...[
                    _KpiGrid(dashboard: _dashboard!),
                    const SizedBox(height: 16),
                  ],
                  if (_financial != null) ...[
                    ErpCard(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Finances', style: Theme.of(context).textTheme.titleMedium),
                          const SizedBox(height: 8),
                          Text('Encaissé : ${_financial!.totalCollected.toStringAsFixed(0)} CDF'),
                          Text('Paiements : ${_financial!.paymentCount}'),
                          Text('Débiteurs : ${_financial!.debtorCount} • À jour : ${_financial!.upToDateCount}'),
                        ],
                      ),
                    ),
                    const SizedBox(height: 16),
                  ],
                  Text('Effectifs par classe', style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  ..._enrollment.map((e) => ErpCard(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        child: ListTile(
                          title: Text('${e.className} (${e.classCode})'),
                          subtitle: Text(e.sectionName),
                          trailing: Text('${e.totalStudents} élèves'),
                        ),
                      )),
                  const SizedBox(height: 16),
                  Text('Moyennes par classe', style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  if (_averages.isEmpty)
                    const Text('Aucune moyenne calculée pour le moment.'),
                  ..._averages.map((a) => ErpCard(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        child: ListTile(
                          title: Text('${a.className} — ${a.periodName}'),
                          subtitle: Text('Moy. ${a.classAverage.toStringAsFixed(2)} / 20'),
                          trailing: Text('${a.passCount}✓ ${a.failCount}✗'),
                        ),
                      )),
                ],
              ),
            ),
    );
  }
}

class _KpiGrid extends StatelessWidget {
  const _KpiGrid({required this.dashboard});

  final DashboardStats dashboard;

  @override
  Widget build(BuildContext context) {
    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 8,
      crossAxisSpacing: 8,
      childAspectRatio: 1.8,
      children: [
        _KpiCard(label: 'Élèves', value: '${dashboard.totalStudents}'),
        _KpiCard(label: 'Inscriptions', value: '${dashboard.activeEnrollments}'),
        _KpiCard(label: 'Classes', value: '${dashboard.totalClassRooms}'),
        _KpiCard(label: 'Enseignants', value: '${dashboard.totalTeachers}'),
      ],
    );
  }
}

class _KpiCard extends StatelessWidget {
  const _KpiCard({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      padding: const EdgeInsets.all(16),
      child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(label, style: Theme.of(context).textTheme.bodySmall),
            Text(value, style: Theme.of(context).textTheme.headlineSmall),
          ],
      ),
    );
  }
}
