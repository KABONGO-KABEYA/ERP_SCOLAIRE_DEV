import '../../../core/cache/cache_partition_policy.dart';
import '../../../core/config/binding_migration_config.dart';
import '../../../core/local_server_discovery/school_discovery_policy.dart';
import 'parent_push_preferences.dart';

/// Garde-fou : aucune notification d'une autre école en mode strict.
abstract final class ParentPushSchoolGuard {
  static String? _hubSchoolId;

  static void bindHubSchool(String? schoolId) {
    _hubSchoolId = schoolId?.trim();
  }

  static void clearHubSchool() {
    _hubSchoolId = null;
  }

  static Future<bool> acceptsNotification(Map<String, dynamic> payload) async {
    if (!BindingMigrationPolicy.isStrictSchoolDiscoveryEnabled) {
      return true;
    }

    final expected = await CachePartitionPolicy.activeSchoolId() ??
        await ParentPushPreferences.readPersistedSchoolId();
    if (expected == null || expected.isEmpty) {
      return false;
    }

    final fromPayload =
        payload['schoolId'] ?? payload['SchoolId'] ?? payload['school_id'];
    if (fromPayload != null) {
      return SchoolDiscoveryPolicy.schoolIdsMatch(
        fromPayload.toString(),
        expected,
      );
    }

    if (_hubSchoolId == null || _hubSchoolId!.isEmpty) {
      return false;
    }

    return SchoolDiscoveryPolicy.schoolIdsMatch(_hubSchoolId, expected);
  }
}
