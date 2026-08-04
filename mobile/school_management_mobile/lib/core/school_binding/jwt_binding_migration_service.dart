import 'package:dio/dio.dart';

import '../api/dio_factory.dart';
import '../config/api_config.dart';
import '../config/binding_migration_config.dart';
import '../connection/connection_mode.dart';
import '../device/device_identity.dart';
import '../local_server_discovery/discovery_constants.dart';
import '../local_server_discovery/discovery_models.dart';
import '../local_server_discovery/school_discovery_policy.dart';
import '../../features/auth/models/auth_models.dart';
import '../../features/parent/offline/parent_offline_cache.dart';
import 'jwt_binding_migration_constants.dart';
import 'school_binding.dart';
import 'school_binding_gate.dart';
import 'school_binding_repository.dart';

/// Migration assistée post-login : JWT + health → `SchoolBinding` (§4.11).
abstract final class JwtBindingMigrationService {
  static SchoolBindingRepository bindingRepository =
      SchoolBindingGate.bindingRepository;

  static SchoolBinding? buildBindingFromHealth({
    required AuthSession session,
    required HealthInfo health,
    required String apiBaseUrl,
    required ConnectionSnapshot connection,
  }) {
    final jwtSchoolId = session.user.schoolId.trim();
    if (jwtSchoolId.isEmpty) {
      return null;
    }

    final identity = health.identity;
    if (identity?.schoolId != null &&
        !SchoolDiscoveryPolicy.schoolIdsMatch(identity!.schoolId, jwtSchoolId)) {
      return null;
    }

    final schoolId = identity?.schoolId?.trim().isNotEmpty == true
        ? identity!.schoolId!.trim()
        : jwtSchoolId;

    final cloud = _resolveCloudBaseUrl(connection, apiBaseUrl);
    final instanceId = identity?.serverInstanceId?.trim() ?? '';
    final now = DateTime.now().toUtc();

    return SchoolBinding(
      schoolId: schoolId,
      schoolName: identity?.schoolName?.trim().isNotEmpty == true
          ? identity!.schoolName!.trim()
          : health.school,
      cloudBaseUrl: cloud,
      serverInstanceId: instanceId,
      licenseId: identity?.licenseId,
      activationDate: now,
      activationTokenId: JwtBindingMigrationConstants.activationTokenId,
      activationSessionId: JwtBindingMigrationConstants.activationSessionId,
      deviceId: '',
      protocolVersion: health.protocolVersion ?? 2,
      extensions: {
        JwtBindingMigrationConstants.extensionMigratedFromJwt: true,
        'migrationCompletedAt': now.toIso8601String(),
      },
    );
  }

  static String _resolveCloudBaseUrl(
    ConnectionSnapshot connection,
    String apiBaseUrl,
  ) {
    if (ApiConfig.hasCloudUrl) {
      return ApiConfig.normalize(ApiConfig.cloudBaseUrl);
    }
    if (connection.mode == DiscoveryMode.remote && connection.baseUrl != null) {
      return ApiConfig.normalize(connection.baseUrl!);
    }
    return ApiConfig.normalize(apiBaseUrl);
  }

  static Future<SchoolBinding?> tryMigrateAfterParentLogin({
    required AuthSession session,
    required String apiBaseUrl,
    required ConnectionSnapshot connection,
  }) async {
    if (!BindingMigrationPolicy.effectiveAllowJwtBindingMigration) {
      return null;
    }

    final isParent = session.user.roles
        .any((r) => r.toUpperCase().contains('PARENT'));
    if (!isParent) {
      return null;
    }

    if (await bindingRepository.hasBinding()) {
      return null;
    }

    final health = await _fetchHealth(apiBaseUrl);
    if (health == null) {
      return null;
    }

    var binding = buildBindingFromHealth(
      session: session,
      health: health,
      apiBaseUrl: apiBaseUrl,
      connection: connection,
    );
    if (binding == null) {
      return null;
    }

    final deviceId = await DeviceIdentity.deviceId;
    binding = SchoolBinding(
      schoolId: binding.schoolId,
      schoolName: binding.schoolName,
      cloudBaseUrl: binding.cloudBaseUrl,
      serverInstanceId: binding.serverInstanceId,
      licenseId: binding.licenseId,
      activationDate: binding.activationDate,
      activationTokenId: binding.activationTokenId,
      activationSessionId: binding.activationSessionId,
      deviceId: deviceId,
      protocolVersion: binding.protocolVersion,
      suggestedUserName: binding.suggestedUserName,
      expiresAt: binding.expiresAt,
      extensions: binding.extensions,
    );

    await bindingRepository.save(binding);
    await ParentOfflineCache.ensureActivePartition();
    return binding;
  }

  static Future<HealthInfo?> _fetchHealth(String baseUrl) async {
    try {
      final dio = createApiDio(ApiConfig.normalize(baseUrl));
      final response = await dio.get<Map<String, dynamic>>(
        DiscoveryConstants.healthPath,
        options: Options(
          receiveTimeout: const Duration(seconds: 8),
          sendTimeout: const Duration(seconds: 8),
        ),
      );
      final data = response.data;
      if (data == null) return null;
      return HealthInfo.fromJson(data);
    } catch (_) {
      return null;
    }
  }

  static Future<bool> isJwtMigratedBinding(SchoolBinding binding) async {
    final ext = binding.extensions;
    if (ext == null) return false;
    return ext[JwtBindingMigrationConstants.extensionMigratedFromJwt] == true;
  }
}
