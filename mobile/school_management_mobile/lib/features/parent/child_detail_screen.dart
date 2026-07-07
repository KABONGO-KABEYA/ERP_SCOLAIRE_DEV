import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers/app_providers.dart';
import 'models/parent_models.dart';

class ChildDetailScreen extends ConsumerStatefulWidget {
  const ChildDetailScreen({
    super.key,
    required this.studentId,
    required this.studentName,
  });

  final String studentId;
  final String studentName;

  @override
  ConsumerState<ChildDetailScreen> createState() => _ChildDetailScreenState();
}

class _ChildDetailScreenState extends ConsumerState<ChildDetailScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  late Future<List<ParentPayment>> _paymentsFuture;
  late Future<List<ParentBulletin>> _bulletinsFuture;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    final repo = ref.read(parentRepositoryProvider);
    _paymentsFuture = repo.getPayments(widget.studentId);
    _bulletinsFuture = repo.getBulletins(widget.studentId);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.studentName),
        bottom: TabBar(
          controller: _tabController,
          tabs: const [
            Tab(text: 'Paiements', icon: Icon(Icons.payments_outlined)),
            Tab(text: 'Bulletins', icon: Icon(Icons.assignment_outlined)),
          ],
        ),
      ),
      body: TabBarView(
        controller: _tabController,
        children: [
          _PaymentsTab(future: _paymentsFuture),
          _BulletinsTab(future: _bulletinsFuture),
        ],
      ),
    );
  }
}

class _PaymentsTab extends StatelessWidget {
  const _PaymentsTab({required this.future});

  final Future<List<ParentPayment>> future;

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<List<ParentPayment>>(
      future: future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return Center(child: Text('Erreur : ${snapshot.error}'));
        }

        final payments = snapshot.data ?? [];
        if (payments.isEmpty) {
          return const Center(child: Text('Aucun paiement enregistré.'));
        }

        return ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: payments.length,
          separatorBuilder: (_, __) => const SizedBox(height: 8),
          itemBuilder: (context, index) {
            final p = payments[index];
            return Card(
              child: ListTile(
                title: Text('${p.totalAmount.toStringAsFixed(0)} ${p.currencyLabel}'),
                subtitle: Text('${p.receiptNumber} • ${p.paymentDate.toLocal()}'),
                trailing: Chip(label: Text(p.statusLabel)),
              ),
            );
          },
        );
      },
    );
  }
}

class _BulletinsTab extends StatelessWidget {
  const _BulletinsTab({required this.future});

  final Future<List<ParentBulletin>> future;

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<List<ParentBulletin>>(
      future: future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return Center(child: Text('Erreur : ${snapshot.error}'));
        }

        final bulletins = snapshot.data ?? [];
        if (bulletins.isEmpty) {
          return const Center(child: Text('Aucun bulletin disponible.'));
        }

        return ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: bulletins.length,
          separatorBuilder: (_, __) => const SizedBox(height: 8),
          itemBuilder: (context, index) {
            final b = bulletins[index];
            return Card(
              child: ListTile(
                title: Text(b.periodName),
                subtitle: Text('Moyenne : ${b.average.toStringAsFixed(2)} / 20'),
                trailing: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text('Rang ${b.rank}/${b.classSize}'),
                    Text('${b.percentage.toStringAsFixed(1)} %'),
                  ],
                ),
              ),
            );
          },
        );
      },
    );
  }
}
