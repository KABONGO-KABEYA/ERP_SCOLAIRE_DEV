import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/api/api_error_message.dart';
import '../../core/theme/erp_theme.dart';
import 'parent_providers.dart';

class ParentChangePasswordScreen extends ConsumerStatefulWidget {
  const ParentChangePasswordScreen({super.key});

  @override
  ConsumerState<ParentChangePasswordScreen> createState() =>
      _ParentChangePasswordScreenState();
}

class _ParentChangePasswordScreenState
    extends ConsumerState<ParentChangePasswordScreen> {
  final _current = TextEditingController();
  final _next = TextEditingController();
  final _confirm = TextEditingController();
  bool _loading = false;
  String? _error;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_next.text != _confirm.text) {
      setState(() => _error = 'Les mots de passe ne correspondent pas.');
      return;
    }
    if (_next.text.length < 8) {
      setState(() => _error = 'Le nouveau mot de passe doit contenir au moins 8 caractères.');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      await ref.read(parentAccountRepositoryProvider).changePassword(
            currentPassword: _current.text,
            newPassword: _next.text,
          );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Mot de passe mis à jour.')),
      );
      context.pop();
    } catch (e) {
      setState(() => _error = resolveApiErrorMessage(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Changer le mot de passe')),
      body: ListView(
        padding: const EdgeInsets.all(ErpSpacing.page),
        children: [
          TextField(
            controller: _current,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Mot de passe actuel'),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _next,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Nouveau mot de passe'),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _confirm,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Confirmer'),
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: const TextStyle(color: ErpColors.danger)),
          ],
          const SizedBox(height: 20),
          FilledButton(
            onPressed: _loading ? null : _submit,
            child: _loading
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Enregistrer'),
          ),
        ],
      ),
    );
  }
}
