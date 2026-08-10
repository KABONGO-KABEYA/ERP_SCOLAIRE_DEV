import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../../core/school_binding/establishment_token_parser.dart';
import '../../../core/school_binding/school_already_registered_exception.dart';
import '../../../core/school_binding/school_binding_activation_gate.dart';
import '../../../core/school_binding/school_establishment_service.dart';
import '../../../core/theme/erp_theme.dart';

/// Gate obligatoire : Rejoindre un établissement (QR établissement).
/// Distinct de [ParentActivationScreen] (invitation parent).
class EstablishmentGateScreen extends StatefulWidget {
  const EstablishmentGateScreen({
    super.key,
    this.initialToken,
    this.setAsActive = true,
  });

  final String? initialToken;

  /// Si false (ajout multi-école), l'actif existant peut être conservé
  /// sauf registre vide (le repository active alors automatiquement).
  final bool setAsActive;

  @override
  State<EstablishmentGateScreen> createState() =>
      _EstablishmentGateScreenState();
}

class _EstablishmentGateScreenState extends State<EstablishmentGateScreen> {
  final _tokenController = TextEditingController();
  final _service = SchoolEstablishmentService();
  bool _loading = false;
  bool _scanLocked = false;
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

  Future<void> _runEstablish(String raw) async {
    if (!SchoolBindingActivationGate.isActivationFlowEnabled) {
      setState(() => _error = 'Liaison établissement non disponible.');
      return;
    }

    final extracted = EstablishmentTokenParser.extractTokenFromScan(raw) ??
        raw.trim();
    if (extracted.isEmpty) {
      setState(() => _error = 'QR établissement invalide.');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
      _successSchool = null;
    });

    try {
      final binding = await _service.establishWithToken(
        extracted,
        setAsActive: widget.setAsActive,
      );
      if (!mounted) return;
      setState(() {
        _successSchool = binding.schoolName;
        _loading = false;
        _scanLocked = true;
      });
    } on SchoolAlreadyRegisteredException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _loading = false;
        _scanLocked = false;
      });
    } on SchoolEstablishmentException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _loading = false;
        _scanLocked = false;
      });
    } on DioException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message ?? e.toString();
        _loading = false;
        _scanLocked = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.toString();
        _loading = false;
        _scanLocked = false;
      });
    }
  }

  void _onDetect(BarcodeCapture capture) {
    if (_loading || _scanLocked) return;
    for (final barcode in capture.barcodes) {
      final raw = barcode.rawValue;
      if (raw == null || raw.isEmpty) continue;
      _scanLocked = true;
      _runEstablish(raw);
      return;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Rejoindre un établissement'),
        backgroundColor: ErpColors.primary,
        foregroundColor: Colors.white,
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          const Text(
            'Scannez le QR établissement fourni par l\'école '
            '(afficheur Desktop / Setup). Obligatoire pour tous les utilisateurs '
            '(parents, enseignants, direction, secrétariat…). '
            'Connexion Internet requise (Bootstrap).',
          ),
          const SizedBox(height: 8),
          Text(
            'Ce parcours est distinct de l\'invitation parent.',
            style: TextStyle(
              color: Theme.of(context).colorScheme.secondary,
              fontStyle: FontStyle.italic,
            ),
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
              labelText: 'Token / deep link établissement',
              border: OutlineInputBorder(),
            ),
            maxLines: 3,
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed:
                _loading ? null : () => _runEstablish(_tokenController.text),
            child: _loading
                ? const SizedBox(
                    height: 22,
                    width: 22,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Rejoindre'),
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(
              _error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ],
          if (_successSchool != null) ...[
            const SizedBox(height: 12),
            Text(
              'Établissement enregistré : $_successSchool. '
              'Vous pouvez vous connecter.',
              style: const TextStyle(
                color: Colors.green,
                fontWeight: FontWeight.w600,
              ),
            ),
            TextButton(
              onPressed: () {
                if (context.canPop()) {
                  context.pop();
                } else {
                  context.go('/login');
                }
              },
              child: const Text('Continuer vers la connexion'),
            ),
          ],
        ],
      ),
    );
  }
}
