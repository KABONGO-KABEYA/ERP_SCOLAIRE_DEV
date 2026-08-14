import 'dart:async';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import 'package:permission_handler/permission_handler.dart';

import '../theme/erp_theme.dart';

/// Scanner QR avec permission caméra, contournements appareil (TECNO / Android 14+)
/// et repli « photo galerie ».
class ErpQrScanner extends StatefulWidget {
  const ErpQrScanner({
    super.key,
    required this.onDetect,
    this.height = 220,
  });

  final void Function(BarcodeCapture capture) onDetect;
  final double height;

  @override
  State<ErpQrScanner> createState() => _ErpQrScannerState();
}

class _ErpQrScannerState extends State<ErpQrScanner> with WidgetsBindingObserver {
  static const _maxAutoRetries = 3;

  MobileScannerController? _controller;
  bool _checkingPermission = true;
  bool _permissionGranted = false;
  bool _permanentlyDenied = false;
  bool _analyzingGallery = false;
  bool _retryScheduled = false;
  int _scannerGeneration = 0;
  int _autoRetryCount = 0;
  MobileScannerException? _lastError;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    unawaited(_prepareScanner());
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _controller?.removeListener(_onControllerUpdate);
    unawaited(_controller?.dispose());
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    final controller = _controller;
    if (controller == null || !controller.value.hasCameraPermission) return;

    switch (state) {
      case AppLifecycleState.resumed:
        unawaited(_restartScanner());
      case AppLifecycleState.inactive:
      case AppLifecycleState.paused:
        unawaited(controller.stop());
      case AppLifecycleState.detached:
      case AppLifecycleState.hidden:
        break;
    }
  }

  MobileScannerController _createController() => MobileScannerController(
        detectionSpeed: DetectionSpeed.noDuplicates,
        facing: CameraFacing.back,
        // Contournement connu sur certains appareils (TECNO, Samsung A23…).
        cameraResolution: const Size(640, 480),
        useNewCameraSelector: true,
      );

  Future<void> _prepareScanner() async {
    setState(() {
      _checkingPermission = true;
      _permissionGranted = false;
      _permanentlyDenied = false;
      _lastError = null;
      _autoRetryCount = 0;
    });

    var status = await Permission.camera.status;
    if (!status.isGranted) {
      status = await Permission.camera.request();
    }

    if (!mounted) return;

    if (!status.isGranted) {
      setState(() {
        _checkingPermission = false;
        _permissionGranted = false;
        _permanentlyDenied = status.isPermanentlyDenied;
      });
      return;
    }

    await _restartScanner(showLoader: true);
  }

  Future<void> _restartScanner({bool showLoader = false}) async {
    _controller?.removeListener(_onControllerUpdate);
    await _controller?.dispose();
    _controller = null;

    if (!mounted) return;

    setState(() {
      if (showLoader) _checkingPermission = true;
      _permissionGranted = true;
      _lastError = null;
    });

    // Laisse CameraX se libérer (erreur « Available cameras: 0 » transitoire).
    await Future<void>.delayed(const Duration(milliseconds: 350));

    if (!mounted) return;

    final controller = _createController();
    controller.addListener(_onControllerUpdate);
    _controller = controller;

    setState(() {
      _checkingPermission = false;
      _scannerGeneration++;
    });
  }

  void _onControllerUpdate() {
    final controller = _controller;
    if (controller == null || !mounted) return;

    final error = controller.value.error;
    if (error == null) {
      if (_autoRetryCount > 0 || _lastError != null) {
        setState(() {
          _autoRetryCount = 0;
          _lastError = null;
        });
      }
      return;
    }

    if (_lastError?.errorCode == error.errorCode &&
        _lastError?.errorDetails?.code == error.errorDetails?.code) {
      return;
    }

    setState(() => _lastError = error);

    if (_retryScheduled || _autoRetryCount >= _maxAutoRetries) return;

    _retryScheduled = true;
    _autoRetryCount++;
    final attempt = _autoRetryCount;
    unawaited(
      Future<void>.delayed(Duration(milliseconds: 500 * attempt), () async {
        _retryScheduled = false;
        if (!mounted || _controller == null) return;
        if (_autoRetryCount != attempt) return;
        await _restartScanner();
      }),
    );
  }

  Future<void> _scanFromGallery() async {
    setState(() => _analyzingGallery = true);
    try {
      final picked = await ImagePicker().pickImage(source: ImageSource.gallery);
      if (picked == null || !mounted) return;

      final analyzer = MobileScannerController();
      try {
        final capture = await analyzer.analyzeImage(picked.path);
        if (!mounted) return;
        if (capture != null && capture.barcodes.isNotEmpty) {
          widget.onDetect(capture);
        } else {
          _showSnack('Aucun QR code détecté sur cette image.');
        }
      } finally {
        await analyzer.dispose();
      }
    } catch (_) {
      if (mounted) {
        _showSnack('Impossible d\'analyser l\'image sélectionnée.');
      }
    } finally {
      if (mounted) setState(() => _analyzingGallery = false);
    }
  }

  void _showSnack(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
  }

  String _scannerErrorMessage(MobileScannerException error) {
    final nativeCode = error.errorDetails?.code;
    final nativeMessage = error.errorDetails?.message?.trim();

    switch (error.errorCode) {
      case MobileScannerErrorCode.permissionDenied:
        return 'Accès à la caméra refusé. Autorisez la caméra dans les paramètres.';
      case MobileScannerErrorCode.unsupported:
        return 'Le scan QR n\'est pas pris en charge sur cet appareil.';
      case MobileScannerErrorCode.genericError:
        if (nativeCode == 'MOBILE_SCANNER_CAMERA_ERROR') {
          return 'Impossible d\'ouvrir la caméra. Fermez les autres apps '
              'qui l\'utilisent, puis réessayez.';
        }
        if (nativeCode == 'MOBILE_SCANNER_NO_CAMERA_ERROR') {
          return 'Aucune caméra disponible sur cet appareil.';
        }
        if (nativeCode == 'MOBILE_SCANNER_CAMERA_PERMISSION_REQUEST_PENDING') {
          return 'Autorisation caméra en cours…';
        }
        if (nativeMessage != null && nativeMessage.isNotEmpty) {
          return 'Scanner indisponible : $nativeMessage';
        }
        return 'Impossible d\'afficher le scanner QR. Utilisez « Photo du QR » '
            'ou collez le token ci-dessous.';
      case MobileScannerErrorCode.controllerAlreadyInitialized:
      case MobileScannerErrorCode.controllerUninitialized:
      case MobileScannerErrorCode.controllerDisposed:
        return 'Initialisation du scanner… Réessayez dans un instant.';
    }
  }

  Widget _frame({required Widget child}) {
    return SizedBox(
      height: widget.height,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(12),
        child: ColoredBox(
          color: const Color(0xFF0F172A),
          child: child,
        ),
      ),
    );
  }

  Widget _permissionPrompt() {
    return _frame(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.qr_code_scanner_rounded, color: Colors.white70, size: 40),
            const SizedBox(height: 12),
            const Text(
              'Autorisez l\'accès à la caméra pour scanner le QR code.',
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.white, fontSize: 13, height: 1.35),
            ),
            const SizedBox(height: 14),
            if (_permanentlyDenied)
              FilledButton.icon(
                onPressed: openAppSettings,
                icon: const Icon(Icons.settings_rounded, size: 18),
                label: const Text('Ouvrir les paramètres'),
                style: FilledButton.styleFrom(backgroundColor: ErpColors.primary),
              )
            else
              FilledButton.icon(
                onPressed: _prepareScanner,
                icon: const Icon(Icons.camera_alt_rounded, size: 18),
                label: const Text('Autoriser la caméra'),
                style: FilledButton.styleFrom(backgroundColor: ErpColors.primary),
              ),
          ],
        ),
      ),
    );
  }

  Widget _scannerError(MobileScannerException error) {
    final needsSettings = error.errorCode == MobileScannerErrorCode.permissionDenied;
    return _frame(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.error_outline_rounded, color: Colors.white70, size: 32),
            const SizedBox(height: 8),
            Text(
              _scannerErrorMessage(error),
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white, fontSize: 12, height: 1.35),
            ),
            const SizedBox(height: 10),
            Wrap(
              alignment: WrapAlignment.center,
              spacing: 8,
              runSpacing: 4,
              children: [
                if (needsSettings)
                  FilledButton.icon(
                    onPressed: openAppSettings,
                    icon: const Icon(Icons.settings_rounded, size: 16),
                    label: const Text('Paramètres'),
                    style: FilledButton.styleFrom(
                      backgroundColor: ErpColors.primary,
                      visualDensity: VisualDensity.compact,
                    ),
                  ),
                OutlinedButton.icon(
                  onPressed: _analyzingGallery ? null : _scanFromGallery,
                  icon: _analyzingGallery
                      ? const SizedBox(
                          width: 14,
                          height: 14,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.photo_library_outlined, size: 16),
                  label: const Text('Photo du QR'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Colors.white,
                    side: const BorderSide(color: Colors.white54),
                    visualDensity: VisualDensity.compact,
                  ),
                ),
                TextButton(
                  onPressed: () {
                    setState(() {
                      _autoRetryCount = 0;
                      _lastError = null;
                    });
                    unawaited(_restartScanner());
                  },
                  child: const Text('Réessayer', style: TextStyle(color: Colors.white)),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (_checkingPermission) {
      return _frame(
        child: const Center(
          child: CircularProgressIndicator(color: ErpColors.primary),
        ),
      );
    }

    if (!_permissionGranted || _controller == null) {
      return _permissionPrompt();
    }

    final showError = _lastError != null && _autoRetryCount >= _maxAutoRetries;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        SizedBox(
          height: widget.height,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: showError
                ? _scannerError(_lastError!)
                : MobileScanner(
                    key: ValueKey(_scannerGeneration),
                    controller: _controller,
                    onDetect: widget.onDetect,
                    errorBuilder: (context, error, child) {
                      if (_autoRetryCount < _maxAutoRetries) {
                        return _frame(
                          child: const Center(
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                CircularProgressIndicator(color: ErpColors.primary),
                                SizedBox(height: 10),
                                Text(
                                  'Activation caméra…',
                                  style: TextStyle(color: Colors.white70, fontSize: 12),
                                ),
                              ],
                            ),
                          ),
                        );
                      }
                      return _scannerError(error);
                    },
                  ),
          ),
        ),
        const SizedBox(height: 8),
        Align(
          alignment: Alignment.centerRight,
          child: TextButton.icon(
            onPressed: _analyzingGallery ? null : _scanFromGallery,
            icon: const Icon(Icons.photo_library_outlined, size: 18),
            label: const Text('Importer une photo du QR'),
            style: TextButton.styleFrom(foregroundColor: ErpColors.primary),
          ),
        ),
      ],
    );
  }
}
