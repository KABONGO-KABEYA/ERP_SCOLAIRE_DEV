import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_storage.dart';
import '../../../core/providers/app_providers.dart';
import '../../../core/theme/erp_theme.dart';
import '../../../router/app_router.dart';
import '../../auth/models/auth_models.dart';
import 'account_helpers.dart';
import 'account_repository.dart';

final accountRepositoryProvider = Provider(
  (ref) => AccountRepository(ref.watch(apiClientProvider)),
);

class SecretaryAccountScreen extends ConsumerStatefulWidget {
  const SecretaryAccountScreen({super.key});

  @override
  ConsumerState<SecretaryAccountScreen> createState() => _SecretaryAccountScreenState();
}

class _SecretaryAccountScreenState extends ConsumerState<SecretaryAccountScreen> {
  AuthUser? _profile;
  String? _schoolName;
  String? _storedName;
  bool _loading = true;
  String? _error;

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
      final repo = ref.read(accountRepositoryProvider);
      final storedName = await AuthStorage.userName;
      final results = await Future.wait([
        repo.getProfile(),
        repo.getSchoolName(),
      ]);
      if (!mounted) return;
      setState(() {
        _profile = results[0] as AuthUser;
        _schoolName = results[1] as String?;
        _storedName = storedName;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = 'Impossible de charger le profil.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final connection = ref.watch(connectionModeProvider);
    final fullName = _profile?.fullName ?? _storedName ?? 'Utilisateur';
    final roleLabel = resolveRoleLabel(_profile?.roles ?? const []);
    final school = _schoolName ?? 'Établissement';
    final server = resolveServerLabel(connection.baseUrl, connection.displayLabel);

    return Scaffold(
      appBar: AppBar(title: const Text('Mon compte')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.all(ErpSpacing.page),
              children: [
                if (_error != null) ...[
                  Text(_error!, style: const TextStyle(color: ErpColors.danger)),
                  const SizedBox(height: 12),
                  OutlinedButton.icon(
                    onPressed: _load,
                    icon: const Icon(Icons.refresh),
                    label: const Text('Réessayer'),
                  ),
                  const SizedBox(height: 20),
                ],
                ErpCard(
                  child: Column(
                    children: [
                      CircleAvatar(
                        radius: 42,
                        backgroundColor: ErpColors.primary.withValues(alpha: 0.12),
                        child: Text(
                          _initials(fullName),
                          style: const TextStyle(
                            fontSize: 28,
                            fontWeight: FontWeight.w600,
                            color: ErpColors.primary,
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),
                      Text(
                        fullName,
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                      const SizedBox(height: 6),
                      Text(roleLabel, style: Theme.of(context).textTheme.bodyMedium),
                      const SizedBox(height: 4),
                      Text(school, style: Theme.of(context).textTheme.bodyMedium),
                      const SizedBox(height: 12),
                      Container(
                        width: double.infinity,
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                        decoration: BoxDecoration(
                          color: ErpColors.pageBackground,
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(color: ErpColors.border),
                        ),
                        child: Row(
                          children: [
                            const Icon(Icons.dns_outlined, size: 18, color: ErpColors.primary),
                            const SizedBox(width: 8),
                            Expanded(
                              child: Text(
                                server,
                                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                      color: ErpColors.textPrimary,
                                    ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 20),
                _AccountActionTile(
                  icon: Icons.apartment_outlined,
                  label: 'Mes établissements',
                  onTap: () => context.push('/schools'),
                ),
                const SizedBox(height: 10),
                _AccountActionTile(
                  icon: Icons.lock_outline,
                  label: 'Modifier le mot de passe',
                  onTap: () => context.push('/secretary/account/change-password'),
                ),
                const SizedBox(height: 10),
                _AccountActionTile(
                  icon: Icons.info_outline,
                  label: 'À propos de l\'application',
                  onTap: () => context.push('/secretary/account/about'),
                ),
                const SizedBox(height: 10),
                _AccountActionTile(
                  icon: Icons.logout,
                  label: 'Déconnexion',
                  isDestructive: true,
                  onTap: () => logout(ref, context),
                ),
              ],
            ),
    );
  }

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first[0].toUpperCase();
    return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
  }
}

class _AccountActionTile extends StatelessWidget {
  const _AccountActionTile({
    required this.icon,
    required this.label,
    required this.onTap,
    this.isDestructive = false,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool isDestructive;

  @override
  Widget build(BuildContext context) {
    final color = isDestructive ? ErpColors.danger : ErpColors.textPrimary;
    return Material(
      color: Theme.of(context).cardTheme.color ?? ErpColors.card,
      borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
      child: InkWell(
        borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
            border: Border.all(color: ErpColors.border),
          ),
          child: Row(
            children: [
              Icon(icon, color: color),
              const SizedBox(width: 14),
              Expanded(
                child: Text(
                  label,
                  style: TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w500,
                    color: color,
                  ),
                ),
              ),
              Icon(Icons.chevron_right, color: ErpColors.textSecondary.withValues(alpha: 0.8)),
            ],
          ),
        ),
      ),
    );
  }
}
