import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/auth/auth_storage.dart';
import '../../core/providers/app_providers.dart';
import 'widgets/secretary_ui_widgets.dart';

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
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.w600,
                    height: 1.2,
                  ),
            ),
          const SizedBox(height: 10),
          Text(
            canEnroll
                ? 'Consultez les dossiers et enregistrez les élèves depuis votre téléphone.'
                : 'Mode Cloud : recherche et consultation OK. Les modifications (inscriptions, documents) nécessitent le réseau de l\'école.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(height: 1.45),
          ),
          const SizedBox(height: 28),
          SecretaryHomeCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const SecretaryFeatureIcon(icon: Icons.folder_shared_outlined),
                    const SizedBox(width: 18),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Dossier élève',
                            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                                  fontWeight: FontWeight.w600,
                                ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            'Rechercher un élève, consulter sa fiche et mettre à jour les documents (photo, etc.).',
                            style: Theme.of(context).textTheme.bodyMedium?.copyWith(height: 1.45),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 22),
                SecretaryFilledButton(
                  onPressed: () => context.push('/secretary/students'),
                  icon: Icons.search,
                  label: 'Rechercher un élève',
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),
          SecretaryHomeCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const SecretaryFeatureIcon(icon: Icons.person_add_alt_1),
                    const SizedBox(width: 18),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Inscription élève',
                            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                                  fontWeight: FontWeight.w600,
                                ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            'Nouvelle inscription ou réinscription avec adresse, responsables et documents.',
                            style: Theme.of(context).textTheme.bodyMedium?.copyWith(height: 1.45),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 22),
                SecretaryFilledButton(
                  onPressed: canEnroll
                      ? () => context.push('/secretary/enrollment?mode=new')
                      : null,
                  icon: Icons.add,
                  label: 'Nouvelle inscription',
                ),
                const SizedBox(height: 14),
                SecretaryOutlinedButton(
                  onPressed: canEnroll
                      ? () => context.push('/secretary/enrollment?mode=re')
                      : null,
                  icon: Icons.search,
                  label: 'Réinscription',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
