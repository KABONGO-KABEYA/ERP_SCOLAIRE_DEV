import '../config/binding_migration_config.dart';
import '../school_binding/school_binding_gate.dart';
import '../school_binding/school_binding_repository.dart';

/// Namespace cache par école (architecture v2 §4.9) — actif si `STRICT_SCHOOL_DISCOVERY`.
abstract final class CachePartitionPolicy {
  static SchoolBindingRepository bindingRepository =
      SchoolBindingGate.bindingRepository;

  static Future<bool> get isPartitioningEnabled async {
    if (!BindingMigrationPolicy.isStrictSchoolDiscoveryEnabled) {
      return false;
    }
    return bindingRepository.hasBinding();
  }

  static Future<String?> activeSchoolId() async {
    if (!await isPartitioningEnabled) return null;
    final binding = await bindingRepository.load();
    if (binding == null || binding.schoolId.isEmpty) return null;
    return binding.schoolId;
  }

  static String normalizeSchoolId(String schoolId) =>
      schoolId.trim().toLowerCase();

  static String storageSuffix(String schoolId) =>
      normalizeSchoolId(schoolId).replaceAll('-', '');

  static String prefsPrefixForSchool(String schoolId) =>
      'school.${storageSuffix(schoolId)}.';

  static Future<String> scopeKey(String baseKey, {String? schoolId}) async {
    final id = schoolId ?? await activeSchoolId();
    if (id == null) return baseKey;
    return '${prefsPrefixForSchool(id)}$baseKey';
  }

  static String hiveBoxName(String baseBoxName, String schoolId) =>
      '${baseBoxName}_${storageSuffix(schoolId)}';
}
