import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers/app_providers.dart';
import '../../router/app_router.dart';
import 'models/teacher_models.dart';

class TeacherAssignmentsScreen extends ConsumerStatefulWidget {
  const TeacherAssignmentsScreen({super.key});

  @override
  ConsumerState<TeacherAssignmentsScreen> createState() => _TeacherAssignmentsScreenState();
}

class _TeacherAssignmentsScreenState extends ConsumerState<TeacherAssignmentsScreen> {
  late Future<List<TeacherAssignment>> _future;
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
    _future = ref.read(teacherRepositoryProvider).getAssignments();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Mes cours'),
        actions: [
          IconButton(icon: const Icon(Icons.logout), onPressed: () => logout(ref, context)),
        ],
      ),
      body: FutureBuilder<List<TeacherAssignment>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text('Erreur : ${snapshot.error}'));
          }

          final assignments = snapshot.data ?? [];
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (_userName != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: Text('Bonjour, $_userName', style: Theme.of(context).textTheme.titleMedium),
                ),
              if (assignments.isEmpty)
                const Card(
                  child: Padding(
                    padding: EdgeInsets.all(24),
                    child: Text('Aucune affectation de cours pour ce compte.'),
                  ),
                ),
              ...assignments.map((a) => Card(
                    child: ListTile(
                      leading: const CircleAvatar(child: Icon(Icons.menu_book)),
                      title: Text(a.courseName),
                      subtitle: Text('${a.classRoomName} • ${a.academicYearLabel}'),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => context.push(
                        '/teacher/classes/${a.classRoomId}?courseId=${a.courseId}&yearId=${a.academicYearId}&course=${Uri.encodeComponent(a.courseName)}&class=${Uri.encodeComponent(a.classRoomName)}',
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
