import 'package:dio/dio.dart';
import 'package:package_info_plus/package_info_plus.dart';

import '../device/device_identity.dart';
import '../../features/parent/offline/parent_offline_cache.dart';
import 'activation_session_store.dart';
import 'bootstrap_api_client.dart';
import 'establishment_token_parser.dart';
import 'school_binding.dart';
import 'school_binding_repository.dart';

/// Exception métier du flux ParentActivation (invitation).
class ParentActivationException implements Exception {
  ParentActivationException(this.message);
  final String message;
  @override
  String toString() => message;
}

/// Flux invitation parent via Bootstrap `/activation/*` — **séparé** du QR établissement.
class SchoolActivationService {
  SchoolActivationService({
    BootstrapApiClient? bootstrap,
    SchoolBindingRepository? bindingRepository,
    ActivationSessionStore? sessionStore,
    Future<String> Function()? deviceIdProvider,
    Future<Map<String, dynamic>> Function()? clientHintsProvider,
  })  : _bootstrap = bootstrap ?? BootstrapApiClient(),
        _bindingRepository = bindingRepository ?? SchoolBindingRepository(),
        _sessionStore = sessionStore ?? ActivationSessionStore(),
        _deviceIdProvider = deviceIdProvider ?? (() => DeviceIdentity.deviceId),
        _clientHintsProvider = clientHintsProvider;

  final BootstrapApiClient _bootstrap;
  final SchoolBindingRepository _bindingRepository;
  final ActivationSessionStore _sessionStore;
  final Future<String> Function() _deviceIdProvider;
  final Future<Map<String, dynamic>> Function()? _clientHintsProvider;

  static String? extractTokenFromDeepLink(Uri uri) {
    if (uri.scheme != 'erp-scolaire') return null;
    if (uri.host != 'activate' && uri.path != '/activate') return null;
    final token = uri.queryParameters['token'];
    if (token == null || token.isEmpty) return null;
    return token;
  }

  Future<SchoolBinding> activateWithToken(String activationToken) async {
    final token = activationToken.trim();
    if (token.isEmpty) {
      throw ParentActivationException('Token d\'invitation parent manquant.');
    }

    // Phase 7 — refus local du QR établissement (ne jamais appeler /activation).
    if (EstablishmentTokenParser.isSchoolEstablishmentToken(token)) {
      throw ParentActivationException(
        'QR établissement non accepté ici. '
        'Utilisez « Rejoindre un établissement » pour lier le téléphone.',
      );
    }

    final deviceId = await _deviceIdProvider();
    final clientHints = await _resolveClientHints();

    try {
      final session = await _bootstrap.start(
        BootstrapStartRequest(
          token: token,
          deviceId: deviceId,
          clientHints: clientHints,
        ),
      );
      await _sessionStore.persist(session);

      final binding = await _bootstrap.complete(
        BootstrapCompleteRequest(
          activationSessionId: session.activationSessionId,
          deviceId: deviceId,
        ),
      );

      await _bindingRepository.addSchool(binding, setAsActive: true);
      await _sessionStore.clear();
      await _safeEnsurePartition();
      return binding;
    } on DioException catch (e) {
      await _sessionStore.clear();
      final body = e.response?.data;
      String? detail;
      if (body is Map && body['error'] != null) {
        detail = body['error'].toString();
      }
      throw ParentActivationException(
        detail ?? e.message ?? e.toString(),
      );
    } catch (e) {
      await _sessionStore.clear();
      rethrow;
    }
  }

  Future<Map<String, dynamic>> _resolveClientHints() async {
    if (_clientHintsProvider != null) {
      return _clientHintsProvider!();
    }
    try {
      final package = await PackageInfo.fromPlatform();
      return {
        'platform': 'flutter',
        'appVersion': package.version,
        'buildNumber': package.buildNumber,
      };
    } catch (_) {
      return {'platform': 'flutter'};
    }
  }

  static Future<void> _safeEnsurePartition() async {
    try {
      await ParentOfflineCache.ensureActivePartition();
    } catch (_) {}
  }
}
