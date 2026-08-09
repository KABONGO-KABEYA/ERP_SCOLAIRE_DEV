import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/cache/cache_partition_policy.dart';
import '../../../core/providers/app_providers.dart';
import '../../../core/school_binding/school_binding.dart';
import '../../../core/school_binding/school_binding_repository.dart';
import '../../../core/theme/erp_theme.dart';

/// Gestion multi-établissements : liste, switch, ajout QR, suppression.
class RegisteredSchoolsScreen extends ConsumerStatefulWidget {
  const RegisteredSchoolsScreen({super.key});

  @override
  ConsumerState<RegisteredSchoolsScreen> createState() =>
      _RegisteredSchoolsScreenState();
}

class _RegisteredSchoolsScreenState
    extends ConsumerState<RegisteredSchoolsScreen> {
  final _repo = SchoolBindingRepository();
  bool _loading = true;
  String? _error;
  List<SchoolBinding> _schools = const [];
  String? _activeId;

  @override
  void initState() {
    super.initState();
    _reload();
  }

  Future<void> _reload() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final schools = await _repo.loadAll();
      final active = await _repo.activeSchoolId();
      if (!mounted) return;
      setState(() {
        _schools = schools;
        _activeId = active;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _loading = false;
      });
    }
  }

  Future<void> _switchTo(SchoolBinding school) async {
    if (school.schoolId == _activeId) return;
    setState(() => _loading = true);
    try {
      await _repo.setActive(school.schoolId);
      await ref.read(authStateProvider.notifier).setLoggedIn(false);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Établissement actif : ${school.schoolName}'),
        ),
      );
      context.go('/login');
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _loading = false;
      });
    }
  }

  Future<void> _remove(SchoolBinding school) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Retirer l\'établissement ?'),
        content: Text(
          '« ${school.schoolName} » sera retiré. Ses données locales '
          '(cache, session, notifications) seront effacées. '
          'Les autres établissements restent inchangés.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Annuler'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Retirer'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;

    setState(() => _loading = true);
    try {
      final outcome = await _repo.removeSchool(school.schoolId);
      if (!mounted) return;

      if (outcome == RemoveSchoolOutcome.registryEmpty) {
        await ref.read(authStateProvider.notifier).setLoggedIn(false);
        if (!mounted) return;
        context.go('/parent/activate?reason=registry_empty');
        return;
      }

      if (outcome == RemoveSchoolOutcome.switchedToOther) {
        await ref.read(authStateProvider.notifier).setLoggedIn(false);
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Établissement retiré. Reconnectez-vous sur le nouvel actif.',
            ),
          ),
        );
        context.go('/login');
        return;
      }

      await _reload();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Mes établissements'),
        backgroundColor: ErpColors.primary,
        foregroundColor: Colors.white,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _reload,
              child: ListView(
                padding: const EdgeInsets.all(ErpSpacing.page),
                children: [
                  const Text(
                    'Un seul établissement est actif à la fois. '
                    'Changez d\'école sans rescanner. Ajoutez-en une via QR.',
                    style: TextStyle(color: ErpColors.textSecondary),
                  ),
                  if (_error != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      _error!,
                      style: const TextStyle(color: ErpColors.danger),
                    ),
                  ],
                  const SizedBox(height: 16),
                  for (final school in _schools) ...[
                    _SchoolTile(
                      school: school,
                      isActive: CachePartitionPolicy.normalizeSchoolId(
                            school.schoolId,
                          ) ==
                          (_activeId == null
                              ? null
                              : CachePartitionPolicy.normalizeSchoolId(
                                  _activeId!,
                                )),
                      onActivate: () => _switchTo(school),
                      onRemove: () => _remove(school),
                    ),
                    const SizedBox(height: 10),
                  ],
                  const SizedBox(height: 8),
                  FilledButton.icon(
                    onPressed: () => context.push('/parent/activate'),
                    icon: const Icon(Icons.qr_code_scanner),
                    label: const Text('Ajouter un établissement (QR)'),
                  ),
                ],
              ),
            ),
    );
  }
}

class _SchoolTile extends StatelessWidget {
  const _SchoolTile({
    required this.school,
    required this.isActive,
    required this.onActivate,
    required this.onRemove,
  });

  final SchoolBinding school;
  final bool isActive;
  final VoidCallback onActivate;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: ErpColors.card,
      borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
          border: Border.all(
            color: isActive ? ErpColors.primary : ErpColors.border,
            width: isActive ? 1.5 : 1,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  Icons.school_outlined,
                  color: isActive ? ErpColors.primary : ErpColors.textSecondary,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    school.schoolName,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      color: ErpColors.navy,
                    ),
                  ),
                ),
                if (isActive)
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: ErpColors.primary.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Text(
                      'Actif',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: ErpColors.primary,
                      ),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              school.cloudBaseUrl,
              style: const TextStyle(
                fontSize: 12,
                color: ErpColors.textSecondary,
              ),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                if (!isActive)
                  TextButton(
                    onPressed: onActivate,
                    child: const Text('Activer'),
                  ),
                const Spacer(),
                TextButton(
                  onPressed: onRemove,
                  style: TextButton.styleFrom(foregroundColor: ErpColors.danger),
                  child: const Text('Retirer'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
