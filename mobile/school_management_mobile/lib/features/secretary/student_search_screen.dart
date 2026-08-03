import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../core/widgets/erp_widgets.dart';
import '../enrollment/models/enrollment_models.dart';

class SecretaryStudentSearchScreen extends ConsumerStatefulWidget {
  const SecretaryStudentSearchScreen({super.key});

  @override
  ConsumerState<SecretaryStudentSearchScreen> createState() => _SecretaryStudentSearchScreenState();
}

class _SecretaryStudentSearchScreenState extends ConsumerState<SecretaryStudentSearchScreen> {
  final _controller = TextEditingController();
  Timer? _debounce;
  List<EnrollmentStudentSearchResult> _results = [];
  bool _loading = false;
  String? _error;
  String _lastQuery = '';

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  void _onQueryChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 350), () => _search(value.trim()));
  }

  Future<void> _search(String query) async {
    if (query.length < 2) {
      setState(() {
        _results = [];
        _error = null;
        _lastQuery = query;
      });
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
      _lastQuery = query;
    });

    try {
      final items = await ref.read(secretaryStudentRepositoryProvider).quickSearch(query);
      if (!mounted) return;
      setState(() => _results = items);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _results = [];
      });
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Rechercher un élève'),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: ErpSearchBar(
              controller: _controller,
              autofocus: true,
              hintText: 'Nom, matricule, téléphone…',
              onChanged: (v) {
                setState(() {});
                _onQueryChanged(v);
              },
              onSubmitted: (v) => _search(v.trim()),
              onClear: () {
                setState(() {});
                _onQueryChanged('');
              },
            ),
          ),
          if (_loading) const LinearProgressIndicator(minHeight: 2),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(_error!, style: const TextStyle(color: ErpColors.danger)),
            ),
          Expanded(
            child: _results.isEmpty && !_loading && _lastQuery.length >= 2
                ? const Center(
                    child: Text(
                      'Aucun élève trouvé.',
                      style: TextStyle(color: ErpColors.textSecondary),
                    ),
                  )
                : ListView.separated(
                    padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                    itemCount: _results.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 8),
                    itemBuilder: (context, index) {
                      final s = _results[index];
                      return ErpCard(
                        onTap: () => context.push('/secretary/students/${s.id}'),
                        padding: const EdgeInsets.all(14),
                        child: Row(
                          children: [
                            CircleAvatar(
                              backgroundColor: ErpColors.primary.withValues(alpha: 0.1),
                              child: Text(
                                s.lastName.isNotEmpty ? s.lastName[0].toUpperCase() : '?',
                                style: const TextStyle(
                                  color: ErpColors.primary,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    s.fullName,
                                    style: const TextStyle(fontWeight: FontWeight.w600),
                                  ),
                                  const SizedBox(height: 2),
                                  Text(
                                    s.registrationNumber,
                                    style: const TextStyle(
                                      fontSize: 12,
                                      color: ErpColors.textPrimary,
                                    ),
                                  ),
                                  if (s.previousClassName != null &&
                                      s.previousClassName!.isNotEmpty)
                                    Text(
                                      s.previousClassName!,
                                      style: const TextStyle(
                                        fontSize: 12,
                                        color: ErpColors.textSecondary,
                                      ),
                                    ),
                                ],
                              ),
                            ),
                            const Icon(Icons.chevron_right, color: ErpColors.textSecondary),
                          ],
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
