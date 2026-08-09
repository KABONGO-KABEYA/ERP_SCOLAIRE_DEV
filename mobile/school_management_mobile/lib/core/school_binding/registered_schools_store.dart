import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../cache/cache_partition_policy.dart';
import 'school_binding.dart';

/// Persistance bas niveau : registre N écoles + pointeur actif.
///
/// Ne remplace pas [SchoolBindingRepository] — celui-ci reste le point d'accès
/// public (architecture v2 §4.6).
class RegisteredSchoolsStore {
  RegisteredSchoolsStore({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  static const registryKey = 'school_bindings_registry';
  static const activeSchoolIdKey = 'active_school_id';

  /// Clé legacy mono-école (v2.0.1).
  static const legacyBindingKey = 'school_binding';

  final FlutterSecureStorage _storage;
  bool _migrationChecked = false;

  Future<void> ensureMigrated() async {
    if (_migrationChecked) return;
    _migrationChecked = true;

    final registryRaw = await _storage.read(key: registryKey);
    if (registryRaw != null && registryRaw.isNotEmpty) {
      // Déjà en format multi — s'assurer qu'un actif existe si le registre est non vide.
      final map = _decodeRegistry(registryRaw);
      final active = await _storage.read(key: activeSchoolIdKey);
      if (map.isNotEmpty &&
          (active == null ||
              active.isEmpty ||
              !map.containsKey(CachePartitionPolicy.normalizeSchoolId(active)))) {
        await _storage.write(
          key: activeSchoolIdKey,
          value: map.keys.first,
        );
      }
      // Nettoyage optionnel du blob legacy s'il reste.
      return;
    }

    final legacyRaw = await _storage.read(key: legacyBindingKey);
    if (legacyRaw == null || legacyRaw.isEmpty) {
      return;
    }

    final decoded = jsonDecode(legacyRaw);
    if (decoded is! Map<String, dynamic>) {
      return;
    }

    final binding = SchoolBinding.fromJson(decoded);
    if (binding.schoolId.isEmpty) {
      return;
    }

    final id = CachePartitionPolicy.normalizeSchoolId(binding.schoolId);
    await _writeRegistry({id: binding});
    await _storage.write(key: activeSchoolIdKey, value: id);
    // Conserve legacy en lecture seule jusqu'à confirmation ; on peut le supprimer
    // pour éviter double source — après migration réussie on efface.
    await _storage.delete(key: legacyBindingKey);
  }

  Future<Map<String, SchoolBinding>> loadRegistry() async {
    await ensureMigrated();
    final raw = await _storage.read(key: registryKey);
    if (raw == null || raw.isEmpty) {
      return {};
    }
    return _decodeRegistry(raw);
  }

  Future<void> writeRegistry(Map<String, SchoolBinding> registry) async {
    await _writeRegistry(registry);
  }

  Future<String?> readActiveSchoolId() async {
    await ensureMigrated();
    final id = await _storage.read(key: activeSchoolIdKey);
    if (id == null || id.isEmpty) return null;
    return CachePartitionPolicy.normalizeSchoolId(id);
  }

  Future<void> writeActiveSchoolId(String? schoolId) async {
    if (schoolId == null || schoolId.isEmpty) {
      await _storage.delete(key: activeSchoolIdKey);
      return;
    }
    await _storage.write(
      key: activeSchoolIdKey,
      value: CachePartitionPolicy.normalizeSchoolId(schoolId),
    );
  }

  Future<void> clearAll() async {
    await _storage.delete(key: registryKey);
    await _storage.delete(key: activeSchoolIdKey);
    await _storage.delete(key: legacyBindingKey);
    _migrationChecked = false;
  }

  Future<void> _writeRegistry(Map<String, SchoolBinding> registry) async {
    final encoded = <String, dynamic>{};
    for (final entry in registry.entries) {
      encoded[entry.key] = entry.value.toJson();
    }
    await _storage.write(key: registryKey, value: jsonEncode(encoded));
  }

  Map<String, SchoolBinding> _decodeRegistry(String raw) {
    final decoded = jsonDecode(raw);
    if (decoded is! Map) {
      return {};
    }
    final result = <String, SchoolBinding>{};
    for (final entry in decoded.entries) {
      final value = entry.value;
      if (value is! Map) continue;
      final binding = SchoolBinding.fromJson(Map<String, dynamic>.from(value));
      if (binding.schoolId.isEmpty) continue;
      result[CachePartitionPolicy.normalizeSchoolId(binding.schoolId)] = binding;
    }
    return result;
  }
}
