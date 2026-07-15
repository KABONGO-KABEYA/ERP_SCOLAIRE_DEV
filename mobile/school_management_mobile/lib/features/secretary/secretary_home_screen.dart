import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/auth/auth_storage.dart';
import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import '../../router/app_router.dart';

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
    return Scaffold(
      appBar: AppBar(
        title: const Text('Secrétariat'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            onPressed: () => logout(ref, context),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(ErpSpacing.page),
        children: [
          if (_userName != null)
            Text('Bonjour, $_userName', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          Text(
            'Enregistrez les élèves depuis votre téléphone.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 24),
          ErpCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: ErpColors.primary.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: const Icon(Icons.person_add_alt_1, color: ErpColors.primary, size: 32),
                    ),
                    const SizedBox(width: 16),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Inscription élève', style: Theme.of(context).textTheme.titleLarge),
                          const SizedBox(height: 4),
                          Text(
                            'Nouvelle inscription ou réinscription avec adresse, responsables et documents.',
                            style: Theme.of(context).textTheme.bodyMedium,
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 20),
                FilledButton.icon(
                  onPressed: () => context.push('/secretary/enrollment?mode=new'),
                  icon: const Icon(Icons.add),
                  label: const Text('Nouvelle inscription'),
                ),
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  onPressed: () => context.push('/secretary/enrollment?mode=re'),
                  icon: const Icon(Icons.search),
                  label: const Text('Réinscription'),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
