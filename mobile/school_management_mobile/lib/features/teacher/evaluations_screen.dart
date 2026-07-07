import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers/app_providers.dart';
import 'models/teacher_models.dart';

class TeacherEvaluationsScreen extends ConsumerStatefulWidget {
  const TeacherEvaluationsScreen({
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
  ConsumerState<TeacherEvaluationsScreen> createState() => _TeacherEvaluationsScreenState();
}

class _TeacherEvaluationsScreenState extends ConsumerState<TeacherEvaluationsScreen> {
  List<TeacherPeriod> _periods = [];
  TeacherPeriod? _selectedPeriod;
  List<TeacherEvaluation> _evaluations = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadPeriods();
  }

  Future<void> _loadPeriods() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final repo = ref.read(teacherRepositoryProvider);
      final periods = await repo.getPeriods(widget.academicYearId);
      setState(() {
        _periods = periods;
        _selectedPeriod = periods.isNotEmpty ? periods.first : null;
      });
      if (_selectedPeriod != null) await _loadEvaluations();
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _loadEvaluations() async {
    if (_selectedPeriod == null) return;
    setState(() => _loading = true);
    try {
      final repo = ref.read(teacherRepositoryProvider);
      final all = await repo.getEvaluations(
        classRoomId: widget.classRoomId,
        academicPeriodId: _selectedPeriod!.id,
      );
      setState(() {
        _evaluations = all.where((e) => e.courseId == widget.courseId).toList();
        _error = null;
      });
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _createEvaluation() async {
    if (_selectedPeriod == null) return;
    try {
      await ref.read(teacherRepositoryProvider).createEvaluation(
            academicYearId: widget.academicYearId,
            academicPeriodId: _selectedPeriod!.id,
            courseId: widget.courseId,
            classRoomId: widget.classRoomId,
            title: 'Interrogation',
          );
      await _loadEvaluations();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Évaluation créée')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Notes — ${widget.courseName}'),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _createEvaluation,
        icon: const Icon(Icons.add),
        label: const Text('Nouvelle éval.'),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : Column(
              children: [
                Padding(
                  padding: const EdgeInsets.all(16),
                  child: DropdownButtonFormField<TeacherPeriod>(
                    value: _selectedPeriod,
                    decoration: const InputDecoration(
                      labelText: 'Période',
                      border: OutlineInputBorder(),
                    ),
                    items: _periods
                        .map((p) => DropdownMenuItem(value: p, child: Text(p.name)))
                        .toList(),
                    onChanged: (p) async {
                      setState(() => _selectedPeriod = p);
                      await _loadEvaluations();
                    },
                  ),
                ),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                  ),
                Expanded(
                  child: _evaluations.isEmpty
                      ? const Center(child: Text('Aucune évaluation pour cette période.'))
                      : ListView.separated(
                          padding: const EdgeInsets.all(16),
                          itemCount: _evaluations.length,
                          separatorBuilder: (_, __) => const SizedBox(height: 8),
                          itemBuilder: (context, index) {
                            final e = _evaluations[index];
                            return Card(
                              child: ListTile(
                                title: Text(e.title),
                                subtitle: Text('${e.evaluationDate} • /${e.maxScore}'),
                                trailing: e.isOpen
                                    ? const Icon(Icons.edit_note)
                                    : const Icon(Icons.lock_outline),
                                onTap: e.isOpen
                                    ? () => context.push(
                                          '/teacher/evaluations/${e.id}/grades?title=${Uri.encodeComponent(e.title)}&max=${e.maxScore}&classRoomId=${widget.classRoomId}',
                                        )
                                    : null,
                              ),
                            );
                          },
                        ),
                ),
              ],
            ),
    );
  }
}
