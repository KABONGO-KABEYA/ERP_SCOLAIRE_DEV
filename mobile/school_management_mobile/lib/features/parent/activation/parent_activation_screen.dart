import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../../core/school_binding/school_activation_service.dart';
import '../../../core/school_binding/school_binding_activation_gate.dart';
import '../../../core/theme/erp_theme.dart';

/// Activation parent QR / lien — Bootstrap uniquement (étape 3).
class ParentActivationScreen extends StatefulWidget {
  const ParentActivationScreen({super.key, this.initialToken});

  final String? initialToken;

  @override
  State<ParentActivationScreen> createState() => _ParentActivationScreenState();
}

class _ParentActivationScreenState extends State<ParentActivationScreen> {
  final _tokenController = TextEditingController();
  final _activation = SchoolActivationService();
  bool _loading = false;
  String? _error;
  String? _successSchool;

  @override
  void initState() {
    super.initState();
    if (widget.initialToken != null && widget.initialToken!.isNotEmpty) {
      _tokenController.text = widget.initialToken!;
    }
  }

  @override
  void dispose() {
    _tokenController.dispose();
    super.dispose();
  }

  Future<void> _runActivation(String token) async {
    if (!SchoolBindingActivationGate.isActivationFlowEnabled) {
      setState(() => _error = 'Activation non disponible.');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
      _successSchool = null;
    });

    try {
      final binding = await _activation.activateWithToken(token.trim());
      if (!mounted) return;
      setState(() {
        _successSchool = binding.schoolName;
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

  void _onDetect(BarcodeCapture capture) {
    for (final barcode in capture.barcodes) {
      final raw = barcode.rawValue;
      if (raw == null || raw.isEmpty) continue;
      final uri = Uri.tryParse(raw);
      if (uri != null) {
        final token = SchoolActivationService.extractTokenFromDeepLink(uri);
        if (token != null) {
          _runActivation(token);
          return;
        }
      }
      if (raw.contains('token=')) {
        final uri2 = Uri.tryParse(raw.startsWith('erp-scolaire') ? raw : 'erp-scolaire://activate?$raw');
        final token = uri2 != null ? SchoolActivationService.extractTokenFromDeepLink(uri2) : null;
        if (token != null) {
          _runActivation(token);
          return;
        }
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Activer l\'application'),
        backgroundColor: ErpColors.primary,
        foregroundColor: Colors.white,
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          const Text(
            'Scannez le QR code fourni par l\'école ou collez le token d\'activation. '
            'La connexion Internet est requise (Bootstrap).',
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 220,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(12),
              child: MobileScanner(onDetect: _onDetect),
            ),
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _tokenController,
            decoration: const InputDecoration(
              labelText: 'Token JWT',
              border: OutlineInputBorder(),
            ),
            maxLines: 3,
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: _loading ? null : () => _runActivation(_tokenController.text),
            child: _loading
                ? const SizedBox(
                    height: 22,
                    width: 22,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Activer'),
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ],
          if (_successSchool != null) ...[
            const SizedBox(height: 12),
            Text(
              'École activée : $_successSchool. Vous pouvez vous connecter.',
              style: const TextStyle(color: Colors.green, fontWeight: FontWeight.w600),
            ),
            TextButton(
              onPressed: () => context.go('/login'),
              child: const Text('Retour connexion'),
            ),
          ],
        ],
      ),
    );
  }
}
