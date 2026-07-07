import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers/app_providers.dart';
import 'models/teacher_models.dart';

class TeacherClassScreen extends ConsumerStatefulWidget {
  const TeacherClassScreen({
    super.key,
    required this.classRoomId,
    required this.courseId,
    required this.academicYearId,
    required this.courseName,
    required this.className,
  });

  final String classRoomId;
  final String courseId;
  final String academicYearId;
  final String courseName;
  final String className;

  @override
  ConsumerState<TeacherClassScreen> createState() => _TeacherClassScreenState();
}

class _TeacherClassScreenState extends ConsumerState<TeacherClassScreen> {
  late Future<List<TeacherStudent>> _future;

  @override
  void initState() {
    super.initState();
    _future = ref.read(teacherRepositoryProvider).getClassStudents(widget.classRoomId);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('${widget.courseName} — ${widget.className}'),
        actions: [
          IconButton(
            icon: const Icon(Icons.edit_note),
            tooltip: 'Saisir les notes',
            onPressed: () => context.push(
              '/teacher/classes/${widget.classRoomId}/evaluations?courseId=${widget.courseId}&yearId=${widget.academicYearId}&course=${Uri.encodeComponent(widget.courseName)}&class=${Uri.encodeComponent(widget.className)}',
            ),
          ),
        ],
      ),
      body: FutureBuilder<List<TeacherStudent>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            return Center(child: Text('Erreur : ${snapshot.error}'));
          }

          final students = snapshot.data ?? [];
          if (students.isEmpty) {
            return const Center(child: Text('Aucun élève inscrit dans cette classe.'));
          }

          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: students.length,
            separatorBuilder: (_, __) => const SizedBox(height: 8),
            itemBuilder: (context, index) {
              final s = students[index];
              return Card(
                child: ListTile(
                  leading: CircleAvatar(child: Text('${index + 1}')),
                  title: Text(s.fullName),
                  subtitle: Text(s.registrationNumber),
                ),
              );
            },
          );
        },
      ),
    );
  }
}
