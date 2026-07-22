import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers/app_providers.dart';
import 'models/teacher_models.dart';

class TeacherGradeEntryScreen extends ConsumerStatefulWidget {
  const TeacherGradeEntryScreen({
    super.key,
    required this.evaluationId,
    required this.title,
    required this.maxScore,
    required this.classRoomId,
  });

  final String evaluationId;
  final String title;
  final int maxScore;
  final String classRoomId;

  @override
  ConsumerState<TeacherGradeEntryScreen> createState() => _TeacherGradeEntryScreenState();
}

class _GradeRow {
  _GradeRow({
    required this.studentId,
    required this.studentName,
    required this.controller,
    this.isAbsent = false,
  });

  final String studentId;
  final String studentName;
  final TextEditingController controller;
  bool isAbsent;
}

class _TeacherGradeEntryScreenState extends ConsumerState<TeacherGradeEntryScreen> {
  final List<_GradeRow> _rows = [];
  bool _loading = true;
  bool _saving = false;
  String? _error;

  @override
  void dispose() {
    for (final row in _rows) {
      row.controller.dispose();
    }
    super.dispose();
  }

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final repo = ref.read(teacherRepositoryProvider);
      final students = await repo.getClassStudents(widget.classRoomId);
      final entries = await repo.getGradeEntries(widget.evaluationId);
      final entryMap = {for (final e in entries) e.studentId: e};

      for (final row in _rows) {
        row.controller.dispose();
      }
      _rows.clear();

      for (final student in students) {
        final entry = entryMap[student.studentId];
        _rows.add(_GradeRow(
          studentId: student.studentId,
          studentName: student.fullName,
          controller: TextEditingController(
            text: entry != null && !entry.isAbsent ? entry.score.toStringAsFixed(1) : '',
          ),
          isAbsent: entry?.isAbsent ?? false,
        ));
      }
    } catch (e) {
      _error = e.toString();
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _save() async {
    final policy = ref.read(writePolicyProvider);
    if (!policy.canSubmitGrades) {
      setState(() => _error = 'Hors ligne : impossible d\'enregistrer les notes.');
      return;
    }

    setState(() => _saving = true);
    try {
      final grades = _rows.map((row) {
        final score = row.isAbsent ? 0.0 : (double.tryParse(row.controller.text.replaceAll(',', '.')) ?? 0.0);
        return {
          'studentId': row.studentId,
          'score': score,
          'isAbsent': row.isAbsent,
          'comment': null,
        };
      }).toList();

      await ref.read(teacherRepositoryProvider).submitGrades(
            evaluationId: widget.evaluationId,
            grades: grades,
          );

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Notes enregistrées')),
        );
        Navigator.of(context).pop();
      }
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final canSave = ref.watch(writePolicyProvider).canSubmitGrades;

    return Scaffold(
      appBar: AppBar(title: Text(widget.title)),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: (_saving || !canSave) ? null : _save,
        icon: _saving
            ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
            : const Icon(Icons.save),
        label: Text(canSave ? 'Enregistrer' : 'Lecture seule'),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Text('Note sur ${widget.maxScore}', style: Theme.of(context).textTheme.titleMedium),
                if (_error != null) ...[
                  const SizedBox(height: 8),
                  Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                ],
                const SizedBox(height: 16),
                ..._rows.map((row) => Card(
                      child: Padding(
                        padding: const EdgeInsets.all(12),
                        child: Row(
                          children: [
                            Expanded(child: Text(row.studentName)),
                            Checkbox(
                              value: row.isAbsent,
                              onChanged: (v) => setState(() => row.isAbsent = v ?? false),
                            ),
                            const Text('Abs.'),
                            const SizedBox(width: 8),
                            SizedBox(
                              width: 72,
                              child: TextField(
                                controller: row.controller,
                                enabled: !row.isAbsent,
                                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                                decoration: const InputDecoration(
                                  border: OutlineInputBorder(),
                                  isDense: true,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    )),
              ],
            ),
    );
  }
}
