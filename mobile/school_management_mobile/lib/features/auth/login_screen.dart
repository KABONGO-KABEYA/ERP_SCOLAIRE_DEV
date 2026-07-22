import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_error_message.dart';
import '../../core/auth/auth_storage.dart';
import '../../core/connection/connection_mode.dart';
import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import 'auth_repository.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _userController = TextEditingController(text: 'admin');
  final _passwordController = TextEditingController();
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _userController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final connection = ref.read(connectionModeProvider);
    if (!connection.mode.isOnline || connection.baseUrl == null) {
      setState(() => _error = connection.message ?? 'Aucun serveur accessible.');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      await ref.read(authRepositoryProvider).login(
            _userController.text.trim(),
            _passwordController.text,
            baseUrl: connection.baseUrl,
          );
      await ref.read(authStateProvider.notifier).setLoggedIn(true);
      if (mounted) context.go(await AuthStorage.homeRoute);
    } catch (e) {
      setState(() => _error = resolveApiErrorMessage(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: LayoutBuilder(
        builder: (context, constraints) {
          if (constraints.maxWidth < 720) {
            return _buildForm(context, showBrandHeader: true);
          }

          return Row(
            children: [
              Expanded(child: _buildBrandPanel()),
              SizedBox(width: 420, child: _buildForm(context)),
            ],
          );
        },
      ),
    );
  }

  Widget _buildBrandPanel() {
    return Container(
      color: ErpColors.sidebar,
      padding: const EdgeInsets.all(48),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            width: 72,
            height: 72,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(18),
            ),
            padding: const EdgeInsets.all(8),
            child: Image.asset('assets/icon/app_icon.png', fit: BoxFit.contain),
          ),
          const SizedBox(height: 24),
          const Text(
            'ERP Scolaire RDC',
            style: TextStyle(fontSize: 32, fontWeight: FontWeight.w600, color: Colors.white),
          ),
          const SizedBox(height: 8),
          Text(
            'Espace Parent • Enseignant • Direction',
            style: TextStyle(fontSize: 16, color: Colors.white.withValues(alpha: 0.7)),
          ),
        ],
      ),
    );
  }

  Widget _buildForm(BuildContext context, {bool showBrandHeader = false}) {
    final connection = ref.watch(connectionModeProvider);
    final offline = connection.mode == ConnectionMode.offline;
    final detecting = connection.mode == ConnectionMode.detecting;

    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(ErpSpacing.page),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 420),
          child: ErpCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (showBrandHeader) ...[
                  Image.asset('assets/icon/app_icon.png', height: 72, fit: BoxFit.contain),
                  const SizedBox(height: 16),
                  Text('ERP Scolaire RDC', textAlign: TextAlign.center, style: Theme.of(context).textTheme.headlineMedium),
                  const SizedBox(height: 24),
                ] else ...[
                  Text('Connexion', style: Theme.of(context).textTheme.headlineMedium),
                  const SizedBox(height: 8),
                  Text('Accédez à votre espace mobile', style: Theme.of(context).textTheme.bodyMedium),
                  const SizedBox(height: 32),
                ],
                if (detecting) ...[
                  const LinearProgressIndicator(),
                  const SizedBox(height: 12),
                  Text(
                    'Recherche du serveur…',
                    style: Theme.of(context).textTheme.bodyMedium,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 16),
                ],
                if (offline) ...[
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: ErpColors.danger.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(10),
                      border: Border.all(color: ErpColors.danger.withValues(alpha: 0.3)),
                    ),
                    child: Text(
                      connection.message ??
                          'Aucun serveur n\'est accessible. Vérifiez le Wi‑Fi de l\'école ou Internet.',
                      style: const TextStyle(color: ErpColors.danger),
                    ),
                  ),
                  const SizedBox(height: 12),
                  OutlinedButton.icon(
                    onPressed: () =>
                        ref.read(connectionModeProvider.notifier).refresh(),
                    icon: const Icon(Icons.refresh),
                    label: const Text('Réessayer'),
                  ),
                  const SizedBox(height: 16),
                ],
                TextField(
                  controller: _userController,
                  enabled: !offline && !detecting,
                  decoration: const InputDecoration(
                    labelText: 'Identifiant',
                    prefixIcon: Icon(Icons.person_outline),
                  ),
                  textInputAction: TextInputAction.next,
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: _passwordController,
                  enabled: !offline && !detecting,
                  decoration: const InputDecoration(
                    labelText: 'Mot de passe',
                    prefixIcon: Icon(Icons.lock_outline),
                  ),
                  obscureText: true,
                  onSubmitted: (_) => _submit(),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 12),
                  Text(_error!, style: const TextStyle(color: ErpColors.danger)),
                ],
                const SizedBox(height: 24),
                FilledButton(
                  onPressed: (_loading || offline || detecting) ? null : _submit,
                  child: _loading
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                        )
                      : const Text('Se connecter'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
