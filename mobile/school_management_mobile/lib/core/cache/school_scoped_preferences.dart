import 'package:shared_preferences/shared_preferences.dart';

import 'cache_partition_policy.dart';

/// Accès SharedPreferences avec clé scopée école (legacy = clé brute).
abstract final class SchoolScopedPreferences {
  static Future<String> resolveKey(String baseKey) =>
      CachePartitionPolicy.scopeKey(baseKey);

  static Future<String?> getString(String baseKey) async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(await resolveKey(baseKey));
  }

  static Future<bool> setString(String baseKey, String value) async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.setString(await resolveKey(baseKey), value);
  }

  static Future<bool?> getBool(String baseKey) async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getBool(await resolveKey(baseKey));
  }

  static Future<bool> setBool(String baseKey, bool value) async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.setBool(await resolveKey(baseKey), value);
  }

  static Future<bool> remove(String baseKey) async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.remove(await resolveKey(baseKey));
  }

  static Future<void> removeAllForSchool(String schoolId) async {
    final prefs = await SharedPreferences.getInstance();
    final prefix = CachePartitionPolicy.prefsPrefixForSchool(schoolId);
    for (final key in prefs.getKeys()) {
      if (key.startsWith(prefix)) {
        await prefs.remove(key);
      }
    }
  }
}
