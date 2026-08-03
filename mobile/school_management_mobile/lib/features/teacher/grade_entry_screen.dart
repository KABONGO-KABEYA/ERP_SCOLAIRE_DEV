import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../core/widgets/erp_widgets.dart';

/// Écran 4 — Saisie des notes (cartes compactes, pas de grille tableau).
class TeacherGradeEntryScreen extends ConsumerStatefulWidget {
  const TeacherGradeEntryScreen({
    super.key,
    required this.evaluationId,
    required this.title,
    required this.maxScore,
    required this.classRoomId,
    required this.isOpen,
  });

  final String evaluationId;
  final String title;
  final int maxScore;
  final String classRoomId;
  final bool isOpen;

  @override
  ConsumerState<TeacherGradeEntryScreen> createState() => _TeacherGradeEntryScreenState();
}

class _GradeCard {
  _GradeCard({
    required this.index,
    required this.studentId,
    required this.studentName,
    required this.controller,
    required this.focusNode,
    this.isAbsent = false,
  });

  final int index;
  final String studentId;
  final String studentName;
  final TextEditingController controller;
  final FocusNode focusNode;
  bool isAbsent;
}

class _TeacherGradeEntryScreenState extends ConsumerState<TeacherGradeEntryScreen> {
  final List<_GradeCard> _cards = [];
  final _searchController = TextEditingController();
  String _query = '';
  bool _loading = true;
  bool _saving = false;
  String? _error;

  bool get _readOnly => !widget.isOpen;

  List<_GradeCard> get _filteredCards {
    final q = _query.trim().toLowerCase();
    if (q.isEmpty) return _cards;
    return _cards
        .where((c) => c.studentName.toLowerCase().contains(q))
        .toList();
  }

  @override
  void dispose() {
    _searchController.dispose();
    for (final c in _cards) {
      c.controller.dispose();
      c.focusNode.dispose();
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

      for (final c in _cards) {
        c.controller.dispose();
        c.focusNode.dispose();
      }
      _cards.clear();

      var i = 0;
      for (final student in students) {
        final entry = entryMap[student.studentId];
        final card = _GradeCard(
          index: ++i,
          studentId: student.studentId,
          studentName: student.fullName,
          controller: TextEditingController(
            text: entry != null && !entry.isAbsent
                ? _formatScore(entry.score)
                : '',
          ),
          focusNode: FocusNode(),
          isAbsent: entry?.isAbsent ?? false,
        );
        _cards.add(card);
      }
    } catch (e) {
      _error = e.toString();
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  String _formatScore(double score) {
    if (score == score.roundToDouble()) return score.toInt().toString();
    return score.toStringAsFixed(1);
  }

  String? _validateScore(String text) {
    final trimmed = text.trim();
    if (trimmed.isEmpty) return null;
    final value = double.tryParse(trimmed.replaceAll(',', '.'));
    if (value == null) return 'Note invalide.';
    if (value < 0) return 'La note ne peut pas être négative.';
    if (value > widget.maxScore) {
      return 'La note ne peut pas dépasser ${widget.maxScore}.';
    }
    return null;
  }

  void _focusNext(int currentIndex) {
    final next = currentIndex; // 0-based index in list; card.index is 1-based
    if (next + 1 >= _cards.length) {
      _cards[next].focusNode.unfocus();
      return;
    }
    _cards[next + 1].focusNode.requestFocus();
  }

  Future<void> _save() async {
    if (_readOnly) return;

    final policy = ref.read(writePolicyProvider);
    if (!policy.canSubmitGrades) {
      setState(() => _error = 'Mode Cache : impossible d’enregistrer les notes.');
      return;
    }

    for (final card in _cards) {
      if (card.isAbsent) continue;
      final err = _validateScore(card.controller.text);
      if (err != null) {
        setState(() => _error = '${card.studentName} : $err');
        card.focusNode.requestFocus();
        return;
      }
    }

    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final grades = _cards.map((card) {
        final raw = card.controller.text.trim().replaceAll(',', '.');
        final score = card.isAbsent
            ? 0.0
            : (raw.isEmpty ? 0.0 : (double.tryParse(raw) ?? 0.0));
        return {
          'studentId': card.studentId,
          'score': score,
          'isAbsent': card.isAbsent,
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
        Navigator.of(context).pop(true);
      }
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final canSave = !_readOnly && ref.watch(writePolicyProvider).canSubmitGrades;

    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: Text(widget.title),
        actions: [
          if (_readOnly)
            const Padding(
              padding: EdgeInsets.only(right: 12),
              child: Center(
                child: Chip(
                  avatar: Icon(Icons.lock_outline, size: 16),
                  label: Text('Lecture seule'),
                  visualDensity: VisualDensity.compact,
                ),
              ),
            ),
        ],
      ),
      floatingActionButton: canSave
          ? FloatingActionButton.extended(
              onPressed: _saving ? null : _save,
              icon: _saving
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                    )
                  : const Icon(Icons.save),
              label: const Text('Enregistrer'),
            )
          : null,
      body: _loading
          ? const ErpLoadingState()
          : Column(
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
                  child: ErpCard(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                    child: Text(
                      'Note sur ${widget.maxScore}  ·  ${_cards.length} élève${_cards.length > 1 ? 's' : ''}',
                      style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        color: ErpColors.primary,
                      ),
                    ),
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                  child: ErpSearchBar(
                    controller: _searchController,
                    hintText: 'Filtrer un élève…',
                    onChanged: (v) => setState(() => _query = v),
                    onClear: () => setState(() => _query = ''),
                  ),
                ),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
                    child: Text(_error!, style: const TextStyle(color: ErpColors.danger)),
                  ),
                Expanded(
                  child: Builder(
                    builder: (context) {
                      final visible = _filteredCards;
                      if (visible.isEmpty) {
                        return const Padding(
                          padding: EdgeInsets.all(24),
                          child: ErpEmptyState(
                            title: 'Aucun élève',
                            description: 'Aucun résultat pour ce filtre.',
                            icon: Icons.search_off,
                          ),
                        );
                      }
                      return ListView.separated(
                        padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
                        itemCount: visible.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (context, index) {
                          final card = visible[index];
                          final fullIndex = _cards.indexOf(card);
                          return _StudentGradeCard(
                            card: card,
                            maxScore: widget.maxScore,
                            readOnly: _readOnly || !canSave,
                            onAbsentChanged: (v) {
                              setState(() {
                                card.isAbsent = v;
                                if (v) card.controller.clear();
                              });
                            },
                            onSubmitted: () {
                              final err = _validateScore(card.controller.text);
                              if (err != null) {
                                setState(() => _error = '${card.studentName} : $err');
                                return;
                              }
                              setState(() => _error = null);
                              _focusNext(fullIndex);
                            },
                            onChanged: () {},
                          );
                        },
                      );
                    },
                  ),
                ),
              ],
            ),
    );
  }
}

class _StudentGradeCard extends StatelessWidget {
  const _StudentGradeCard({
    required this.card,
    required this.maxScore,
    required this.readOnly,
    required this.onAbsentChanged,
    required this.onSubmitted,
    required this.onChanged,
  });

  final _GradeCard card;
  final int maxScore;
  final bool readOnly;
  final ValueChanged<bool> onAbsentChanged;
  final VoidCallback onSubmitted;
  final VoidCallback onChanged;

  @override
  Widget build(BuildContext context) {
    final initials = _initials(card.studentName);

    return ErpCard(
      padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          CircleAvatar(
            radius: 22,
            backgroundColor: ErpColors.primary.withValues(alpha: 0.12),
            child: Text(
              initials,
              style: const TextStyle(
                color: ErpColors.primary,
                fontWeight: FontWeight.w700,
                fontSize: 13,
              ),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${card.index}. ${card.studentName}',
                  style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(
                  ' / $maxScore',
                  style: const TextStyle(color: ErpColors.textSecondary, fontSize: 12),
                ),
              ],
            ),
          ),
          Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              SizedBox(
                width: 88,
                height: ErpSpacing.minTap,
                child: TextField(
                  controller: card.controller,
                  focusNode: card.focusNode,
                  enabled: !readOnly && !card.isAbsent,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  textInputAction: TextInputAction.next,
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 18),
                  inputFormatters: [
                    FilteringTextInputFormatter.allow(RegExp(r'[0-9.,]')),
                  ],
                  decoration: InputDecoration(
                    isDense: true,
                    contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 12),
                    border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                    hintText: '—',
                  ),
                  onChanged: (_) => onChanged(),
                  onSubmitted: (_) => onSubmitted(),
                  onEditingComplete: onSubmitted,
                ),
              ),
              if (!readOnly)
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Checkbox(
                      value: card.isAbsent,
                      visualDensity: VisualDensity.compact,
                      onChanged: (v) => onAbsentChanged(v ?? false),
                    ),
                    const Text('Abs.', style: TextStyle(fontSize: 12)),
                  ],
                ),
            ],
          ),
        ],
      ),
    );
  }

  static String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first.substring(0, 1) + parts.last.substring(0, 1)).toUpperCase();
  }
}
