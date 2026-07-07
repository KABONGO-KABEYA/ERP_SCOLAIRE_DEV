import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers/app_providers.dart';
import '../../router/app_router.dart';
import 'models/parent_models.dart';

class ChildrenScreen extends ConsumerStatefulWidget {
  const ChildrenScreen({super.key});

  @override
  ConsumerState<ChildrenScreen> createState() => _ChildrenScreenState();
}

class _ChildrenScreenState extends ConsumerState<ChildrenScreen> {
  late Future<List<ParentChild>> _future;
  String? _userName;

  @override
  void initState() {
    super.initState();
    _load();
    currentUserName().then((name) {
      if (mounted) setState(() => _userName = name);
    });
  }

  void _load() {
    _future = ref.read(parentRepositoryProvider).getChildren();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Mes enfants'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            onPressed: () => logout(ref, context),
          ),
        ],
      ),
      body: FutureBuilder<List<ParentChild>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text('Erreur : ${snapshot.error}'));
          }

          final children = snapshot.data ?? [];
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (_userName != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: Text('Bonjour, $_userName',
                      style: Theme.of(context).textTheme.titleMedium),
                ),
              if (children.isEmpty)
                const Card(
                  child: Padding(
                    padding: EdgeInsets.all(24),
                    child: Text('Aucun enfant associé à ce compte.'),
                  ),
                ),
              ...children.map((child) => Card(
                    child: ListTile(
                      leading: const CircleAvatar(child: Icon(Icons.person)),
                      title: Text(child.fullName),
                      subtitle: Text(
                        '${child.registrationNumber}${child.className != null ? ' • ${child.className}' : ''}',
                      ),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => context.push(
                        '/children/${child.studentId}?name=${Uri.encodeComponent(child.fullName)}',
                      ),
                    ),
                  )),
            ],
          );
        },
      ),
    );
  }
}
