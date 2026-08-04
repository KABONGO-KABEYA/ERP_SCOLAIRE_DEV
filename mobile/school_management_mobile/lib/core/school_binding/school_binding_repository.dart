import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../cache/cache_partition_policy.dart';
import '../cache/school_cache_purge_service.dart';
import '../../features/parent/notifications/parent_push_lifecycle.dart';
import 'school_binding.dart';

/// Seul point d'accès à la persistance `SchoolBinding` (architecture v2 §4.6).
class SchoolBindingRepository {
  SchoolBindingRepository({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  static const storageKey = 'school_binding';

  final FlutterSecureStorage _storage;

  Future<SchoolBinding?> load() async {
    final raw = await _storage.read(key: storageKey);
    if (raw == null || raw.isEmpty) {
      return null;
    }
    final decoded = jsonDecode(raw);
    if (decoded is! Map<String, dynamic>) {
      return null;
    }
    return SchoolBinding.fromJson(decoded);
  }

  Future<void> save(SchoolBinding binding) async {
    final existing = await load();
    if (existing != null &&
        existing.schoolId.isNotEmpty &&
        existing.schoolId != binding.schoolId &&
        await CachePartitionPolicy.isPartitioningEnabled) {
      await SchoolCachePurgeService.purgeSchoolScope(existing.schoolId);
      await ParentPushLifecycle.onSchoolBindingChanged(
        previousSchoolId: existing.schoolId,
        newSchoolId: binding.schoolId,
      );
    }

    final payload = jsonEncode(binding.toJson());
    await _storage.write(key: storageKey, value: payload);
  }

  Future<void> clear() async {
    await _storage.delete(key: storageKey);
  }

  Future<bool> hasBinding() async {
    final binding = await load();
    return binding != null && binding.schoolId.isNotEmpty;
  }
}
