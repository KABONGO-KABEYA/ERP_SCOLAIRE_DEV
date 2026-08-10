import 'package:dio/dio.dart';
import 'package:package_info_plus/package_info_plus.dart';

import '../device/device_identity.dart';
import '../../features/parent/offline/parent_offline_cache.dart';
import 'bootstrap_api_client.dart';
import 'establishment_error_mapper.dart';
import 'establishment_session_store.dart';
import 'establishment_token_parser.dart';
import 'school_already_registered_exception.dart';
import 'school_binding.dart';
import 'school_binding_repository.dart';

/// Exception métier du flux établissement (message UI prêt).
class SchoolEstablishmentException implements Exception {
  SchoolEstablishmentException(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Flux QR établissement → SchoolBinding (spec Phase 6).
///
/// N'utilise **pas** ParentActivationToken ni `/activation/*`.
class SchoolEstablishmentService {
  SchoolEstablishmentService({
    BootstrapApiClient? bootstrap,
    SchoolBindingRepository? bindingRepository,
    EstablishmentSessionStore? sessionStore,
    Future<String> Function()? deviceIdProvider,
    Future<Map<String, dynamic>> Function()? clientHintsProvider,
  })  : _bootstrap = bootstrap ?? BootstrapApiClient(),
        _bindingRepository = bindingRepository ?? SchoolBindingRepository(),
        _sessionStore = sessionStore ?? EstablishmentSessionStore(),
        _deviceIdProvider = deviceIdProvider ?? (() => DeviceIdentity.deviceId),
        _clientHintsProvider = clientHintsProvider;

  final BootstrapApiClient _bootstrap;
  final SchoolBindingRepository _bindingRepository;
  final EstablishmentSessionStore _sessionStore;
  final Future<String> Function() _deviceIdProvider;
  final Future<Map<String, dynamic>> Function()? _clientHintsProvider;

  /// Deep link `erp-scolaire://establish?token=` ou JWT brut.
  static String? extractTokenFromDeepLink(Uri uri) {
    if (uri.scheme != 'erp-scolaire') return null;
    final hostOrPath =
        uri.host.isNotEmpty ? uri.host : uri.path.replaceFirst('/', '');
    if (hostOrPath != 'establish' && uri.path != '/establish') return null;
    final token = uri.queryParameters['token'];
    if (token == null || token.isEmpty) return null;
    return token;
  }

  /// Scan / collage → binding enregistré localement.
  Future<SchoolBinding> establishWithToken(
    String establishmentToken, {
    bool setAsActive = true,
  }) async {
    final token = establishmentToken.trim();
    if (token.isEmpty) {
      throw SchoolEstablishmentException('QR établissement manquant.');
    }

    // Refus local des invitations parent (ne jamais appeler /establishment).
    if (EstablishmentTokenParser.isParentActivationToken(token)) {
      throw SchoolEstablishmentException(
        EstablishmentErrorMapper.mapServerMessage(
          'Token non valide pour l\'établissement (type incorrect).',
        ),
      );
    }

    final type = EstablishmentTokenParser.peekTokenType(token);
    if (type != null &&
        type != EstablishmentTokenParser.schoolEstablishmentType) {
      throw SchoolEstablishmentException(
        EstablishmentErrorMapper.mapServerMessage(
          'Token non valide pour l\'établissement (type incorrect).',
        ),
      );
    }

    final deviceId = await _deviceIdProvider();
    final clientHints = await _resolveClientHints();

    try {
      final session = await _bootstrap.startEstablishment(
        EstablishmentStartRequest(
          token: token,
          deviceId: deviceId,
          clientHints: clientHints,
        ),
      );
      await _sessionStore.persist(session);

      final binding = await _bootstrap.completeEstablishment(
        EstablishmentCompleteRequest(
          establishmentSessionId: session.establishmentSessionId,
          deviceId: deviceId,
        ),
      );

      // Ne jamais persister le JWT / secret — uniquement le SchoolBinding API.
      final saved = await _bindingRepository.addSchool(
        binding,
        setAsActive: setAsActive,
      );
      await _sessionStore.clear();
      await _safeEnsurePartition();
      return saved;
    } on SchoolAlreadyRegisteredException {
      await _sessionStore.clear();
      rethrow;
    } on DioException catch (e) {
      await _sessionStore.clear();
      throw SchoolEstablishmentException(EstablishmentErrorMapper.fromDio(e));
    } on SchoolEstablishmentException {
      await _sessionStore.clear();
      rethrow;
    } catch (e) {
      await _sessionStore.clear();
      throw SchoolEstablishmentException(e.toString());
    }
  }

  /// Variante testable : start puis complete avec DeviceId potentiellement différent.
  Future<SchoolBinding> establishWithDeviceIds({
    required String token,
    required String startDeviceId,
    required String completeDeviceId,
    bool setAsActive = true,
  }) async {
    if (EstablishmentTokenParser.isParentActivationToken(token)) {
      throw SchoolEstablishmentException(
        EstablishmentErrorMapper.mapServerMessage(
          'Token non valide pour l\'établissement (type incorrect).',
        ),
      );
    }

    try {
      final session = await _bootstrap.startEstablishment(
        EstablishmentStartRequest(token: token, deviceId: startDeviceId),
      );
      await _sessionStore.persist(session);

      final binding = await _bootstrap.completeEstablishment(
        EstablishmentCompleteRequest(
          establishmentSessionId: session.establishmentSessionId,
          deviceId: completeDeviceId,
        ),
      );

      final saved = await _bindingRepository.addSchool(
        binding,
        setAsActive: setAsActive,
      );
      await _sessionStore.clear();
      return saved;
    } on SchoolAlreadyRegisteredException {
      await _sessionStore.clear();
      rethrow;
    } on DioException catch (e) {
      await _sessionStore.clear();
      throw SchoolEstablishmentException(EstablishmentErrorMapper.fromDio(e));
    } catch (e) {
      await _sessionStore.clear();
      if (e is SchoolEstablishmentException) rethrow;
      throw SchoolEstablishmentException(e.toString());
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
    } catch (_) {
      // Hive / prefs non initialisés (tests unitaires).
    }
  }
}
