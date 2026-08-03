import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import 'models/teacher_models.dart';

/// Écran 3 — Évaluations de la sous-période ouverte (sans choix manuel de période).
class TeacherEvaluationsScreen extends ConsumerStatefulWidget {
  const TeacherEvaluationsScreen({
    super.key,
    required this.classRoomId,
    required this.courseId,
    required this.academicYearId,
    required this.courseName,
    required this.className,
    required this.maxScore,
  });

  final String classRoomId;
  final String courseId;
  final String academicYearId;
  final String courseName;
  final String className;
  final int maxScore;

  @override
  ConsumerState<TeacherEvaluationsScreen> createState() => _TeacherEvaluationsScreenState();
}

class _TeacherEvaluationsScreenState extends ConsumerState<TeacherEvaluationsScreen> {
  TeacherPeriod? _openPeriod;
  List<TeacherEvaluation> _evaluations = [];
  bool _loading = true;
  bool _creating = false;
  String? _error;
  bool _noOpenPeriod = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _noOpenPeriod = false;
    });
    try {
      final repo = ref.read(teacherRepositoryProvider);
      final period = await repo.getOpenPeriod(
        classRoomId: widget.classRoomId,
        academicYearId: widget.academicYearId,
      );

      if (period == null) {
        setState(() {
          _openPeriod = null;
          _evaluations = [];
          _noOpenPeriod = true;
        });
        return;
      }

      final all = await repo.getEvaluations(
        classRoomId: widget.classRoomId,
        academicPeriodId: period.id,
      );

      setState(() {
        _openPeriod = period;
        _evaluations = all.where((e) => e.courseId == widget.courseId).toList()
          ..sort((a, b) => b.evaluationDate.compareTo(a.evaluationDate));
      });
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _showCreateDialog() async {
    if (_openPeriod == null || !_openPeriod!.isEditable) return;

    final repo = ref.read(teacherRepositoryProvider);
    List<EvaluationTypeOption> types;
    try {
      types = await repo.getEvaluationTypes();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
      return;
    }

    if (types.isEmpty) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Aucun type d’évaluation configuré.')),
        );
      }
      return;
    }

    if (!mounted) return;

    EvaluationTypeOption selectedType = types.first;
    final titleCtrl = TextEditingController();
    var date = DateTime.now();

    final confirmed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) {
        return Padding(
          padding: EdgeInsets.only(
            left: 20,
            right: 20,
            top: 16,
            bottom: MediaQuery.of(ctx).viewInsets.bottom + 20,
          ),
          child: StatefulBuilder(
            builder: (ctx, setModal) {
              return Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Nouvelle évaluation',
                    style: Theme.of(ctx).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${widget.courseName} · ${_openPeriod!.name} · /${widget.maxScore}',
                    style: TextStyle(color: Colors.grey.shade700, fontSize: 13),
                  ),
                  const SizedBox(height: 16),
                  InputDecorator(
                    decoration: const InputDecoration(
                      labelText: 'Type',
                      border: OutlineInputBorder(),
                      contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                    ),
                    child: DropdownButtonHideUnderline(
                      child: DropdownButton<EvaluationTypeOption>(
                        isExpanded: true,
                        value: selectedType,
                        items: types
                            .map((t) => DropdownMenuItem(value: t, child: Text(t.name)))
                            .toList(),
                        onChanged: (v) {
                          if (v != null) setModal(() => selectedType = v);
                        },
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: titleCtrl,
                    decoration: const InputDecoration(
                      labelText: 'Libellé',
                      border: OutlineInputBorder(),
                    ),
                    textCapitalization: TextCapitalization.sentences,
                  ),
                  const SizedBox(height: 12),
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Date'),
                    subtitle: Text(DateFormat('dd/MM/yyyy').format(date)),
                    trailing: const Icon(Icons.calendar_today),
                    onTap: () async {
                      final picked = await showDatePicker(
                        context: ctx,
                        initialDate: date,
                        firstDate: DateTime(date.year - 1),
                        lastDate: DateTime(date.year + 1),
                      );
                      if (picked != null) setModal(() => date = picked);
                    },
                  ),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: _creating
                        ? null
                        : () {
                            if (titleCtrl.text.trim().isEmpty) {
                              ScaffoldMessenger.of(ctx).showSnackBar(
                                const SnackBar(content: Text('Saisissez un libellé.')),
                              );
                              return;
                            }
                            Navigator.pop(ctx, true);
                          },
                    child: const Text('Créer'),
                  ),
                ],
              );
            },
          ),
        );
      },
    );

    final title = titleCtrl.text.trim();
    titleCtrl.dispose();
    if (confirmed != true || title.isEmpty) return;

    setState(() => _creating = true);
    try {
      await repo.createEvaluation(
        academicYearId: widget.academicYearId,
        academicPeriodId: _openPeriod!.id,
        courseId: widget.courseId,
        classRoomId: widget.classRoomId,
        evaluationTypeId: selectedType.id,
        title: title,
        maxScore: widget.maxScore,
        evaluationDate: DateFormat('yyyy-MM-dd').format(date),
      );
      await _load();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Évaluation créée')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
    } finally {
      if (mounted) setState(() => _creating = false);
    }
  }

  String _formatDate(String raw) {
    if (raw.isEmpty) return '—';
    final d = DateTime.tryParse(raw);
    if (d == null) return raw;
    return DateFormat('dd/MM/yyyy').format(d);
  }

  @override
  Widget build(BuildContext context) {
    final canCreate = _openPeriod != null && _openPeriod!.isEditable && !_noOpenPeriod;

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: Text(widget.courseName),
        actions: [
          IconButton(
            tooltip: 'Actualiser',
            onPressed: _loading ? null : _load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      floatingActionButton: canCreate
          ? FloatingActionButton.extended(
              onPressed: _creating ? null : _showCreateDialog,
              icon: const Icon(Icons.add),
              label: const Text('Nouvelle évaluation'),
            )
          : null,
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 88),
                children: [
                  if (_openPeriod != null)
                    _PeriodBanner(
                      name: _openPeriod!.name,
                      kindLabel: _openPeriod!.kindLabel,
                      editable: _openPeriod!.isEditable,
                    ),
                  if (_noOpenPeriod)
                    const _InfoCard(
                      icon: Icons.lock_clock,
                      title: 'Aucune sous-période ouverte',
                      message:
                          'La saisie des notes est désactivée. '
                          'Attendez l’ouverture d’une sous-période dans le calendrier pédagogique.',
                    ),
                  if (_error != null) ...[
                    const SizedBox(height: 8),
                    Text(_error!, style: const TextStyle(color: ErpColors.danger)),
                  ],
                  if (!_noOpenPeriod && _evaluations.isEmpty && _error == null) ...[
                    const SizedBox(height: 32),
                    const Center(child: Text('Aucune évaluation pour cette période.')),
                  ],
                  ..._evaluations.map((e) {
                    final readOnly = !e.isOpen || !(_openPeriod?.isEditable ?? false);
                    return Padding(
                      padding: const EdgeInsets.only(top: 10),
                      child: ErpCard(
                        onTap: () async {
                          await context.push(
                            '/teacher/evaluations/${e.id}/grades'
                            '?title=${Uri.encodeComponent(e.title)}'
                            '&max=${e.maxScore}'
                            '&classRoomId=${widget.classRoomId}'
                            '&open=${e.isOpen && (_openPeriod?.isEditable ?? false)}',
                          );
                          if (mounted) await _load();
                        },
                        padding: const EdgeInsets.fromLTRB(16, 14, 12, 14),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    e.title,
                                    style: const TextStyle(
                                      fontWeight: FontWeight.w700,
                                      fontSize: 15,
                                    ),
                                  ),
                                ),
                                if (readOnly)
                                  const Icon(Icons.lock_outline, size: 18, color: ErpColors.textSecondary),
                              ],
                            ),
                            const SizedBox(height: 6),
                            Text(
                              '${e.evaluationTypeName.isEmpty ? 'Évaluation' : e.evaluationTypeName}'
                              '  ·  ${_formatDate(e.evaluationDate)}'
                              '  ·  /${e.maxScore}',
                              style: const TextStyle(color: ErpColors.textSecondary, fontSize: 12.5),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              '${e.gradedCount}/${e.studentCount} élève${e.studentCount > 1 ? 's' : ''} coté${e.gradedCount > 1 ? 's' : ''}',
                              style: const TextStyle(
                                color: ErpColors.primary,
                                fontWeight: FontWeight.w600,
                                fontSize: 13,
                              ),
                            ),
                          ],
                        ),
                      ),
                    );
                  }),
                ],
              ),
            ),
    );
  }
}

class _PeriodBanner extends StatelessWidget {
  const _PeriodBanner({
    required this.name,
    required this.kindLabel,
    required this.editable,
  });

  final String name;
  final String kindLabel;
  final bool editable;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: editable
            ? ErpColors.primary.withValues(alpha: 0.08)
            : ErpColors.warning.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        children: [
          Icon(
            editable ? Icons.event_available : Icons.lock_outline,
            size: 20,
            color: editable ? ErpColors.primary : ErpColors.warning,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              '$name${kindLabel.isNotEmpty ? ' ($kindLabel)' : ''}',
              style: TextStyle(
                fontWeight: FontWeight.w600,
                color: editable ? ErpColors.primary : ErpColors.textPrimary,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({
    required this.icon,
    required this.title,
    required this.message,
  });

  final IconData icon;
  final String title;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          children: [
            Icon(icon, size: 40, color: ErpColors.warning),
            const SizedBox(height: 12),
            Text(title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
            const SizedBox(height: 8),
            Text(message, textAlign: TextAlign.center, style: const TextStyle(color: ErpColors.textSecondary)),
          ],
        ),
      ),
    );
  }
}
