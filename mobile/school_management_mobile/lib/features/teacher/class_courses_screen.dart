import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/erp_theme.dart';
import 'assignments_screen.dart';

/// Écran 2 — Cours de la classe (uniquement ceux de l'enseignant).
class TeacherClassCoursesScreen extends ConsumerWidget {
  const TeacherClassCoursesScreen({
    super.key,
    required this.classRoomId,
    required this.className,
    required this.academicYearId,
  });

  final String classRoomId;
  final String className;
  final String academicYearId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(teacherAssignmentsProvider);

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(title: Text(className.isEmpty ? 'Cours' : className)),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('$e')),
        data: (assignments) {
          final courses = assignments
              .where((a) => a.classRoomId == classRoomId)
              .toList()
            ..sort((a, b) => a.courseName.compareTo(b.courseName));

          if (courses.isEmpty) {
            return const Center(child: Text('Aucun cours affecté dans cette classe.'));
          }

          return ListView.separated(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
            itemCount: courses.length,
            separatorBuilder: (_, __) => const SizedBox(height: 10),
            itemBuilder: (context, i) {
              final c = courses[i];
              return ErpCard(
                onTap: () => context.push(
                  '/teacher/classes/$classRoomId/courses/${c.courseId}/evaluations'
                  '?className=${Uri.encodeComponent(className)}'
                  '&courseName=${Uri.encodeComponent(c.courseName)}'
                  '&yearId=${Uri.encodeComponent(academicYearId.isNotEmpty ? academicYearId : c.academicYearId)}'
                  '&maxScore=${c.maxScore}',
                ),
                padding: const EdgeInsets.fromLTRB(16, 16, 12, 16),
                child: Row(
                  children: [
                    Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color: ErpColors.primary.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: const Icon(Icons.menu_book_outlined, color: ErpColors.primary),
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            c.courseName,
                            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            'Maximum : ${c.maxScore}',
                            style: const TextStyle(color: ErpColors.textSecondary, fontSize: 12.5),
                          ),
                        ],
                      ),
                    ),
                    const Icon(Icons.chevron_right, color: ErpColors.textSecondary),
                  ],
                ),
              );
            },
          );
        },
      ),
    );
  }
}
