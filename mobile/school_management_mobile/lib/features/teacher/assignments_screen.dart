import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../router/app_router.dart';
import 'models/teacher_models.dart';

final teacherAssignmentsProvider =
    FutureProvider.autoDispose<List<TeacherAssignment>>((ref) async {
  return ref.watch(teacherRepositoryProvider).getAssignments();
});

/// Écran 1 — Mes classes (uniquement celles de l'enseignant).
class TeacherAssignmentsScreen extends ConsumerWidget {
  const TeacherAssignmentsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(teacherAssignmentsProvider);

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Cotation — Mes classes'),
        actions: [
          IconButton(
            tooltip: 'Actualiser',
            onPressed: () => ref.invalidate(teacherAssignmentsProvider),
            icon: const Icon(Icons.refresh),
          ),
          IconButton(
            tooltip: 'Déconnexion',
            icon: const Icon(Icons.logout),
            onPressed: () => logout(ref, context),
          ),
        ],
      ),
      body: async.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => _ErrorPane(
          message: e.toString(),
          onRetry: () => ref.invalidate(teacherAssignmentsProvider),
        ),
        data: (assignments) {
          final classes = groupAssignmentsByClass(assignments);
          if (classes.isEmpty) {
            return const Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text(
                  'Aucune classe affectée.\nContactez l’administration.',
                  textAlign: TextAlign.center,
                ),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: () async => ref.invalidate(teacherAssignmentsProvider),
            child: ListView.separated(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
              itemCount: classes.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, i) {
                final c = classes[i];
                return _ClassCard(
                  group: c,
                  onTap: () => context.push(
                    '/teacher/classes/${c.classRoomId}/courses'
                    '?name=${Uri.encodeComponent(c.classRoomName)}'
                    '&yearId=${Uri.encodeComponent(c.academicYearId)}',
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}

class _ClassCard extends StatelessWidget {
  const _ClassCard({required this.group, required this.onTap});

  final TeacherClassCard group;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      onTap: onTap,
      padding: const EdgeInsets.fromLTRB(16, 14, 12, 14),
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: ErpColors.primary.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(Icons.class_outlined, color: ErpColors.primary),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  group.classRoomName,
                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
                ),
                const SizedBox(height: 6),
                Text(
                  '${group.studentCount} élève${group.studentCount > 1 ? 's' : ''}'
                  '  ·  ${group.courseCount} cours',
                  style: const TextStyle(color: ErpColors.textSecondary, fontSize: 13),
                ),
              ],
            ),
          ),
          const Icon(Icons.chevron_right, color: ErpColors.textSecondary),
        ],
      ),
    );
  }
}

class _ErrorPane extends StatelessWidget {
  const _ErrorPane({required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton(onPressed: onRetry, child: const Text('Réessayer')),
          ],
        ),
      ),
    );
  }
}
