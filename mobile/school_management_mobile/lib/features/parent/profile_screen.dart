import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/auth/auth_storage.dart';
import '../../core/theme/erp_theme.dart';
import '../../router/app_router.dart';
import '../secretary/account/account_helpers.dart';
import 'parent_providers.dart';

class ParentProfileScreen extends ConsumerStatefulWidget {
  const ParentProfileScreen({super.key});

  @override
  ConsumerState<ParentProfileScreen> createState() => _ParentProfileScreenState();
}

class _ParentProfileScreenState extends ConsumerState<ParentProfileScreen> {
  bool _loading = true;
  String _name = 'Parent';
  String _email = '';
  String _phone = '';
  String _school = '';
  List<String> _roles = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final repo = ref.read(parentAccountRepositoryProvider);
      final stored = await AuthStorage.userName;
      final profile = await repo.getProfile();
      final school = await repo.getSchoolName();
      if (!mounted) return;
      setState(() {
        _name = profile.fullName.isNotEmpty ? profile.fullName : (stored ?? 'Parent');
        _email = profile.email;
        _phone = profile.userName;
        _school = school ?? 'Établissement';
        _roles = profile.roles;
        _loading = false;
      });
    } catch (_) {
      final stored = await AuthStorage.userName;
      if (!mounted) return;
      setState(() {
        _name = stored ?? 'Parent';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final subscription = ref.watch(parentSubscriptionProvider).valueOrNull;

    return Scaffold(
      appBar: AppBar(title: const Text('Profil')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : ListView(
              padding: const EdgeInsets.all(ErpSpacing.page),
              children: [
                ErpCard(
                  child: Column(
                    children: [
                      CircleAvatar(
                        radius: 42,
                        backgroundColor: ErpColors.primary.withValues(alpha: 0.12),
                        child: Text(
                          _initials(_name),
                          style: const TextStyle(
                            fontSize: 28,
                            fontWeight: FontWeight.w700,
                            color: ErpColors.primary,
                          ),
                        ),
                      ),
                      const SizedBox(height: 14),
                      Text(
                        _name,
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w700,
                          color: ErpColors.navy,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        resolveRoleLabel(_roles),
                        style: const TextStyle(color: ErpColors.textSecondary),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 14),
                ErpCard(
                  child: Column(
                    children: [
                      _InfoRow(icon: Icons.email_outlined, label: 'Email', value: _email.isEmpty ? '—' : _email),
                      const Divider(height: 24),
                      _InfoRow(icon: Icons.phone_outlined, label: 'Téléphone / identifiant', value: _phone.isEmpty ? '—' : _phone),
                      const Divider(height: 24),
                      _InfoRow(icon: Icons.school_outlined, label: 'École', value: _school),
                      const Divider(height: 24),
                      _InfoRow(
                        icon: Icons.home_outlined,
                        label: 'Adresse',
                        value: 'Non renseignée',
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 14),
                ErpCard(
                  child: ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: Icon(
                      Icons.workspace_premium,
                      color: subscription?.isActive == true
                          ? ErpColors.success
                          : ErpColors.warning,
                    ),
                    title: Text(
                      subscription?.isActive == true
                          ? 'Abonnement Premium'
                          : 'Abonnement Gratuit',
                    ),
                    subtitle: Text(
                      subscription?.isActive == true
                          ? 'Valable jusqu’au ${_formatDate(subscription?.expiryDate)}'
                          : '1,50 USD / année scolaire',
                    ),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => context.push('/parent/subscription'),
                  ),
                ),
                const SizedBox(height: 14),
                FilledButton.tonalIcon(
                  onPressed: () => context.push('/schools'),
                  icon: const Icon(Icons.apartment_outlined),
                  label: const Text('Mes établissements'),
                ),
                const SizedBox(height: 10),
                FilledButton.tonalIcon(
                  onPressed: () => context.push('/parent/change-password'),
                  icon: const Icon(Icons.lock_reset_outlined),
                  label: const Text('Changer le mot de passe'),
                ),
                const SizedBox(height: 10),
                OutlinedButton.icon(
                  onPressed: () => logout(ref, context),
                  icon: const Icon(Icons.logout),
                  label: const Text('Déconnexion'),
                ),
              ],
            ),
    );
  }

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+'));
    if (parts.isEmpty || parts.first.isEmpty) return '?';
    if (parts.length == 1) return parts.first[0].toUpperCase();
    return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
  }

  String _formatDate(DateTime? date) {
    if (date == null) return '—';
    return '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year}';
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, color: ErpColors.textSecondary, size: 20),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
              const SizedBox(height: 2),
              Text(value, style: const TextStyle(fontWeight: FontWeight.w600)),
            ],
          ),
        ),
      ],
    );
  }
}
