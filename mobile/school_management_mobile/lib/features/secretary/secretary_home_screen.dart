import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/auth/auth_storage.dart';
import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';

class SecretaryHomeScreen extends ConsumerStatefulWidget {
  const SecretaryHomeScreen({super.key});

  @override
  ConsumerState<SecretaryHomeScreen> createState() => _SecretaryHomeScreenState();
}

class _SecretaryHomeScreenState extends ConsumerState<SecretaryHomeScreen> {
  String? _userName;

  @override
  void initState() {
    super.initState();
    AuthStorage.userName.then((name) {
      if (mounted) setState(() => _userName = name);
    });
  }

  @override
  Widget build(BuildContext context) {
    final canEnroll = ref.watch(writePolicyProvider).canEnrollStudents;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Secrétariat'),
        actions: [
          IconButton(
            icon: const Icon(Icons.person_outline),
            tooltip: 'Mon compte',
            onPressed: () => context.push('/secretary/account'),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 28),
        children: [
          if (_userName != null)
            Text(
              'Bonjour, $_userName',
              style: Theme.of(context).textTheme.titleLarge,
            ),
          const SizedBox(height: 8),
          Text(
            canEnroll
                ? 'Actions rapides'
                : 'Mode Distant : consultation OK. Inscriptions sur le Wi‑Fi école.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 24),
          _ActionCard(
            icon: Icons.folder_shared_outlined,
            title: 'Dossier élève',
            onTap: () => context.push('/secretary/students'),
          ),
          const SizedBox(height: 12),
          _ActionCard(
            icon: Icons.person_add_alt_1,
            title: 'Nouvelle inscription',
            enabled: canEnroll,
            onTap: () => context.push('/secretary/enrollment?mode=new'),
          ),
          const SizedBox(height: 12),
          _ActionCard(
            icon: Icons.replay_outlined,
            title: 'Réinscription',
            enabled: canEnroll,
            onTap: () => context.push('/secretary/enrollment?mode=re'),
          ),
        ],
      ),
    );
  }
}

class _ActionCard extends StatelessWidget {
  const _ActionCard({
    required this.icon,
    required this.title,
    required this.onTap,
    this.enabled = true,
  });

  final IconData icon;
  final String title;
  final VoidCallback onTap;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      onTap: enabled ? onTap : null,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 18),
      child: Opacity(
        opacity: enabled ? 1 : 0.45,
        child: Row(
          children: [
            Container(
              width: 48,
              height: 48,
              decoration: BoxDecoration(
                color: ErpColors.primary.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(icon, color: ErpColors.primary, size: 26),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Text(
                title,
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                  color: ErpColors.navy,
                ),
              ),
            ),
            const Icon(Icons.chevron_right, color: ErpColors.textSecondary),
          ],
        ),
      ),
    );
  }
}
