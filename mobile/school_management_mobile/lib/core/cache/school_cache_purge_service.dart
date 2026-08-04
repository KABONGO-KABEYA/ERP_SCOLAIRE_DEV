import '../../features/parent/offline/parent_offline_cache.dart';
import 'school_scoped_preferences.dart';

/// Purge des données locales scopées à une école (offline + prefs partitionnées).
abstract final class SchoolCachePurgeService {
  static Future<void> purgeSchoolScope(String schoolId) async {
    if (schoolId.isEmpty) return;
    await ParentOfflineCache.purgeForSchool(schoolId);
    await SchoolScopedPreferences.removeAllForSchool(schoolId);
  }
}
