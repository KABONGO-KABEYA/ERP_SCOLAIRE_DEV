import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import 'auth_repository.dart';

/// Écran explicite pour rôles non supportés sur mobile (plus de fallback parent).
class UnsupportedRoleScreen extends ConsumerWidget {
  const UnsupportedRoleScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(ErpSpacing.page),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: ErpCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Icon(
                    Icons.block_outlined,
                    size: 48,
                    color: ErpColors.danger,
                  ),
                  const SizedBox(height: 16),
                  Text(
                    'Rôle non pris en charge sur l\'application mobile',
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  const SizedBox(height: 12),
                  Text(
                    'Votre compte est authentifié, mais cet espace n\'est pas '
                    'disponible sur mobile pour votre rôle. '
                    'Utilisez l\'application bureau si nécessaire, ou reconnectez-vous '
                    'avec un compte Parent, Enseignant, Promoteur ou Secrétaire.',
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                  const SizedBox(height: 24),
                  FilledButton(
                    onPressed: () async {
                      final connection = ref.read(connectionModeProvider);
                      await ref.read(authRepositoryProvider).logout(
                            baseUrl: connection.baseUrl,
                          );
                      await ref
                          .read(authStateProvider.notifier)
                          .setLoggedIn(false);
                      if (context.mounted) context.go('/login');
                    },
                    child: const Text('Se déconnecter'),
                  ),
                  TextButton(
                    onPressed: () => context.push('/schools'),
                    child: const Text('Mes établissements'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
