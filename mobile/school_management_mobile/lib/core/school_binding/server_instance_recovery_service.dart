import '../auth/auth_storage.dart';
import '../cache/cache_partition_policy.dart';
import '../cache/school_cache_purge_service.dart';
import '../local_server_discovery/discovery_models.dart';
import '../local_server_discovery/school_discovery_policy.dart';
import '../../features/auth/auth_repository.dart';
import '../../features/parent/notifications/parent_push_lifecycle.dart';
import '../../features/parent/offline/parent_offline_cache.dart';
import 'school_binding.dart';
import 'school_binding_gate.dart';
import 'school_binding_repository.dart';
import 'server_instance_binding_sync.dart';

/// Récupération §4.10 : purge offline, déconnexion, binding instance à jour.
final class ServerInstanceRecoveryOutcome {
  const ServerInstanceRecoveryOutcome({
    required this.requiresReauthentication,
    this.message,
  });

  final bool requiresReauthentication;
  final String? message;

  static const none = ServerInstanceRecoveryOutcome(requiresReauthentication: false);
}

abstract final class ServerInstanceRecoveryService {
  static AuthRepository authRepository = AuthRepository();

  static Future<ServerInstanceRecoveryOutcome> handleInstanceChange({
    required SchoolBinding binding,
    required ServerInstanceChange change,
    required HealthInfo health,
    String? apiBaseUrl,
  }) async {
    if (!change.detected) {
      return ServerInstanceRecoveryOutcome.none;
    }

    if (!await CachePartitionPolicy.isPartitioningEnabled) {
      await ServerInstanceBindingSync.syncFromHealth(
        binding: binding,
        health: health,
        repository: SchoolBindingGate.bindingRepository,
      );
      return ServerInstanceRecoveryOutcome.none;
    }

    await SchoolCachePurgeService.purgeSchoolScope(binding.schoolId);
    await ParentPushLifecycle.onInstanceRecovery(schoolId: binding.schoolId);

    if (apiBaseUrl != null && apiBaseUrl.isNotEmpty) {
      try {
        await authRepository.logout(baseUrl: apiBaseUrl);
      } catch (_) {
        await AuthStorage.clearSession();
      }
    } else {
      await AuthStorage.clearSession();
    }

    final observed = change.observedInstanceId?.trim();
    if (observed != null && observed.isNotEmpty) {
      final updated = ServerInstanceBindingSync.copyWithInstance(
        binding,
        observed,
      );
      await SchoolBindingGate.bindingRepository.save(updated);
    }

    await ParentOfflineCache.ensureActivePartition();

    return const ServerInstanceRecoveryOutcome(
      requiresReauthentication: true,
      message:
          'Installation serveur modifiée — données offline effacées. Reconnectez-vous.',
    );
  }
}
