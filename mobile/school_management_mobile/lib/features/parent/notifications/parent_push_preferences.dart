import 'package:shared_preferences/shared_preferences.dart';

import '../../../core/cache/cache_partition_policy.dart';

/// Préférences push/cursor/seen — scopées par école en mode strict.
abstract final class ParentPushPreferences {
  static const seenIdsBase = 'parent_push_seen_ids';
  static const seededBase = 'parent_push_seeded';
  static const changesCursorBase = 'parent_push_changes_after_id';
  static const pollEnabledBase = 'parent_push_fg_poll_enabled';

  static const fgBaseUrlBase = 'parent_push_fg_base_url_prefs';
  static const fgTokenBase = 'parent_push_fg_access_token_prefs';
  static const activeSchoolIdKey = 'parent_push_active_school_id';

  static String scopeKeyForSchool(String baseKey, String? schoolId) {
    if (schoolId == null || schoolId.isEmpty) {
      return baseKey;
    }
    return '${CachePartitionPolicy.prefsPrefixForSchool(schoolId)}$baseKey';
  }

  static Future<String?> activeSchoolId() async {
    if (!await CachePartitionPolicy.isPartitioningEnabled) {
      return null;
    }
    return CachePartitionPolicy.activeSchoolId();
  }

  static Future<String> resolveKey(String baseKey) async {
    final schoolId = await activeSchoolId();
    return scopeKeyForSchool(baseKey, schoolId);
  }

  static Future<void> persistActiveSchoolContext() async {
    final schoolId = await CachePartitionPolicy.activeSchoolId();
    final prefs = await SharedPreferences.getInstance();
    if (schoolId == null || schoolId.isEmpty) {
      await prefs.remove(activeSchoolIdKey);
    } else {
      await prefs.setString(activeSchoolIdKey, schoolId);
    }
  }

  static Future<void> clearActiveSchoolContext() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(activeSchoolIdKey);
  }

  /// Contexte école pour l'isolate FG (sans accès secure storage).
  static Future<String?> readPersistedSchoolId() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.reload();
    final id = prefs.getString(activeSchoolIdKey);
    if (id == null || id.isEmpty) return null;
    return id;
  }

  static Future<String> fgScopedKey(String baseKey) async {
    final schoolId = await readPersistedSchoolId();
    if (schoolId != null && schoolId.isNotEmpty) {
      return scopeKeyForSchool(baseKey, schoolId);
    }
    return baseKey;
  }

  static Future<List<String>> getSeenIds() async {
    final prefs = await SharedPreferences.getInstance();
    final key = await resolveKey(seenIdsBase);
    return prefs.getStringList(key) ?? const [];
  }

  static Future<void> setSeenIds(List<String> ids) async {
    final prefs = await SharedPreferences.getInstance();
    final key = await resolveKey(seenIdsBase);
    await prefs.setStringList(key, ids);
  }

  static Future<bool?> getSeeded() async {
    final prefs = await SharedPreferences.getInstance();
    final key = await resolveKey(seededBase);
    return prefs.getBool(key);
  }

  static Future<void> setSeeded(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    final key = await resolveKey(seededBase);
    await prefs.setBool(key, value);
  }

  static Future<String?> getChangesCursor() async {
    final prefs = await SharedPreferences.getInstance();
    final key = await resolveKey(changesCursorBase);
    return prefs.getString(key);
  }

  static Future<void> setChangesCursor(String id) async {
    final prefs = await SharedPreferences.getInstance();
    final key = await resolveKey(changesCursorBase);
    await prefs.setString(key, id);
  }

  static Future<bool?> getPollEnabled() async {
    final prefs = await SharedPreferences.getInstance();
    final key = await resolveKey(pollEnabledBase);
    return prefs.getBool(key);
  }

  static Future<void> setPollEnabled(bool enabled) async {
    final prefs = await SharedPreferences.getInstance();
    final key = await resolveKey(pollEnabledBase);
    await prefs.setBool(key, enabled);
  }

  static Future<void> purgeSchoolPushState(String schoolId) async {
    if (schoolId.isEmpty) return;
    final prefs = await SharedPreferences.getInstance();
    for (final base in [
      seenIdsBase,
      seededBase,
      changesCursorBase,
      pollEnabledBase,
      fgBaseUrlBase,
      fgTokenBase,
    ]) {
      await prefs.remove(scopeKeyForSchool(base, schoolId));
    }
  }
}
