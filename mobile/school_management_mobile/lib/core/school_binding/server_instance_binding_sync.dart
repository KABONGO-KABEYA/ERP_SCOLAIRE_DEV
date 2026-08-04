import '../local_server_discovery/discovery_models.dart';
import '../local_server_discovery/school_discovery_policy.dart';
import 'school_binding.dart';
import 'school_binding_repository.dart';

/// Met à jour `SchoolBinding.serverInstanceId` après health valide (§4.10 partiel).
abstract final class ServerInstanceBindingSync {
  static Future<(SchoolBinding?, ServerInstanceChange)> syncFromHealth({
    required SchoolBinding binding,
    required HealthInfo health,
    required SchoolBindingRepository repository,
  }) async {
    final observed = health.identity?.serverInstanceId?.trim();
    if (observed == null || observed.isEmpty) {
      return (null, const ServerInstanceChange.none());
    }

    final change = SchoolDiscoveryPolicy.detectInstanceChange(binding, health);
    final storedEmpty = binding.serverInstanceId.trim().isEmpty;
    if (!change.detected && !storedEmpty) {
      return (null, const ServerInstanceChange.none());
    }

    if (!change.detected && storedEmpty) {
      final updated = copyWithInstance(binding, observed);
      await repository.save(updated);
      return (updated, const ServerInstanceChange.none());
    }

    final updated = copyWithInstance(binding, observed);
    await repository.save(updated);
    return (updated, change);
  }

  static SchoolBinding copyWithInstance(SchoolBinding binding, String instanceId) {
    return SchoolBinding(
      schoolId: binding.schoolId,
      schoolName: binding.schoolName,
      cloudBaseUrl: binding.cloudBaseUrl,
      serverInstanceId: instanceId,
      licenseId: binding.licenseId,
      activationDate: binding.activationDate,
      activationTokenId: binding.activationTokenId,
      activationSessionId: binding.activationSessionId,
      deviceId: binding.deviceId,
      protocolVersion: binding.protocolVersion,
      suggestedUserName: binding.suggestedUserName,
      expiresAt: binding.expiresAt,
      extensions: binding.extensions,
    );
  }
}
