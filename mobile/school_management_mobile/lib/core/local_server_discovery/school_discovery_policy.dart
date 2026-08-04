import '../config/api_config.dart';
import '../school_binding/school_binding.dart';
import 'discovery_models.dart';

/// Règles discovery post-binding (architecture v2 §4.8–§4.10).
abstract final class SchoolDiscoveryPolicy {
  static String normalizeSchoolId(String id) => id.trim().toLowerCase();

  static bool schoolIdsMatch(String? healthSchoolId, String bindingSchoolId) {
    if (bindingSchoolId.isEmpty) return false;
    final fromHealth = healthSchoolId?.trim();
    if (fromHealth == null || fromHealth.isEmpty) return false;
    return normalizeSchoolId(fromHealth) ==
        normalizeSchoolId(bindingSchoolId);
  }

  /// Cloud de référence : [SchoolBinding.cloudBaseUrl] en mode filtré.
  static String? cloudBaseUrlForBinding(SchoolBinding binding) {
    final url = binding.cloudBaseUrl.trim();
    if (!ApiConfig.isValidBaseUrl(url)) return null;
    return ApiConfig.normalize(url);
  }

  /// Candidat local ou distant accepté si `identity.schoolId` == binding (mode filtré).
  static bool acceptsHealthForBinding(HealthInfo health, SchoolBinding binding) {
    final identity = health.identity;
    if (identity == null) {
      return false;
    }
    return schoolIdsMatch(identity.schoolId, binding.schoolId);
  }

  static String? normalizeInstanceId(String? raw) {
    final v = raw?.trim();
    if (v == null || v.isEmpty) return null;
    return v.toLowerCase();
  }

  /// Détection réinstallation / nouvelle instance (§4.10) — sans actions de récupération.
  static ServerInstanceChange detectInstanceChange(
    SchoolBinding binding,
    HealthInfo health,
  ) {
    final observed = normalizeInstanceId(health.identity?.serverInstanceId);
    final stored = normalizeInstanceId(binding.serverInstanceId);
    if (observed == null || stored == null) {
      return const ServerInstanceChange.none();
    }
    if (observed == stored) {
      return const ServerInstanceChange.none();
    }
    return ServerInstanceChange(
      detected: true,
      previousInstanceId: binding.serverInstanceId,
      observedInstanceId: health.identity!.serverInstanceId!,
    );
  }
}

final class ServerInstanceChange {
  const ServerInstanceChange._({
    required this.detected,
    this.previousInstanceId,
    this.observedInstanceId,
  });

  const ServerInstanceChange.none()
      : detected = false,
        previousInstanceId = null,
        observedInstanceId = null;

  const ServerInstanceChange({
    required this.detected,
    required this.previousInstanceId,
    required this.observedInstanceId,
  });

  final bool detected;
  final String? previousInstanceId;
  final String? observedInstanceId;
}
