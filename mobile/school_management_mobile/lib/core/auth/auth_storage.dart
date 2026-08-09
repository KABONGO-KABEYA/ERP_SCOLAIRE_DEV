import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../cache/cache_partition_policy.dart';

class AuthStorage {
  AuthStorage._();

  static const _storage = FlutterSecureStorage();
  static const _accessTokenKey = 'access_token';
  static const _refreshTokenKey = 'refresh_token';
  static const _userNameKey = 'user_name';
  static const _rolesKey = 'user_roles';
  static const _permissionsKey = 'user_permissions';

  static const _sessionKeys = [
    _accessTokenKey,
    _refreshTokenKey,
    _userNameKey,
    _rolesKey,
    _permissionsKey,
  ];

  static Future<String> _resolveKey(String base) async {
    final scoped = await CachePartitionPolicy.scopeKey(base);
    if (scoped == base) return base;

    // Migration soft mono → partition : copie la clé legacy non scopée.
    final existing = await _storage.read(key: scoped);
    if (existing == null) {
      final legacy = await _storage.read(key: base);
      if (legacy != null && legacy.isNotEmpty) {
        await _storage.write(key: scoped, value: legacy);
        await _storage.delete(key: base);
      }
    }
    return scoped;
  }

  static Future<void> saveSession({
    required String accessToken,
    required String refreshToken,
    required String userName,
    required List<String> roles,
    required List<String> permissions,
  }) async {
    await _storage.write(
      key: await _resolveKey(_accessTokenKey),
      value: accessToken,
    );
    await _storage.write(
      key: await _resolveKey(_refreshTokenKey),
      value: refreshToken,
    );
    await _storage.write(
      key: await _resolveKey(_userNameKey),
      value: userName,
    );
    await _storage.write(
      key: await _resolveKey(_rolesKey),
      value: roles.join(','),
    );
    await _storage.write(
      key: await _resolveKey(_permissionsKey),
      value: permissions.join(','),
    );
  }

  static Future<String?> get accessToken async {
    return _storage.read(key: await _resolveKey(_accessTokenKey));
  }

  static Future<String?> get refreshToken async {
    return _storage.read(key: await _resolveKey(_refreshTokenKey));
  }

  static Future<String?> get userName async {
    return _storage.read(key: await _resolveKey(_userNameKey));
  }

  static Future<List<String>> get roles async {
    final raw = await _storage.read(key: await _resolveKey(_rolesKey));
    if (raw == null || raw.isEmpty) return [];
    return raw.split(',').where((r) => r.isNotEmpty).toList();
  }

  static Future<List<String>> get permissions async {
    final raw = await _storage.read(key: await _resolveKey(_permissionsKey));
    if (raw == null || raw.isEmpty) return [];
    return raw.split(',').where((p) => p.isNotEmpty).toList();
  }

  static Future<bool> get isLoggedIn async =>
      (await accessToken)?.isNotEmpty == true;

  static Future<bool> get isTeacher async {
    final userRoles = await roles;
    return userRoles.any((r) => r.toUpperCase().contains('ENSEIGNANT'));
  }

  static Future<bool> get isParent async {
    final userRoles = await roles;
    return userRoles.any((r) => r.toUpperCase().contains('PARENT'));
  }

  static Future<bool> get isDirection async {
    final userRoles = await roles;
    return userRoles.any((r) => r.toUpperCase().contains('DIRECTION'));
  }

  static Future<bool> get isPromoteur async {
    final userRoles = await roles;
    return userRoles.any((r) {
      final upper = r.toUpperCase();
      return upper.contains('PROMOTEUR') || upper.contains('PROPRIETAIRE');
    });
  }

  static Future<bool> get canManageEnrollments async {
    final perms = await permissions;
    if (perms.contains('admin.full') || perms.contains('students.create')) {
      return true;
    }
    final userRoles = await roles;
    return userRoles.any((r) {
      final upper = r.toUpperCase();
      return upper.contains('ADMIN') || upper.contains('SECRET');
    });
  }

  static Future<String> get homeRoute async {
    if (await isTeacher) return '/teacher/assignments';
    if (await canManageEnrollments) return '/secretary/home';
    if (await isPromoteur || await isDirection) return '/promoteur/dashboard';
    return '/parent/home';
  }

  /// Efface la session courante (clés scopées ou legacy) — ne touche pas `device_id` / bindings.
  static Future<void> clearSession() async {
    if (await CachePartitionPolicy.isPartitioningEnabled) {
      for (final base in _sessionKeys) {
        await _storage.delete(key: await _resolveKey(base));
      }
      return;
    }

    for (final base in _sessionKeys) {
      await _storage.delete(key: base);
    }
  }

  /// Efface les tokens/roles stockés pour une école précise (suppression d'établissement).
  static Future<void> clearSessionForSchool(String schoolId) async {
    if (schoolId.isEmpty) return;
    final prefix = CachePartitionPolicy.prefsPrefixForSchool(schoolId);
    for (final base in _sessionKeys) {
      await _storage.delete(key: '$prefix$base');
    }
  }

  /// Alias historique — préférer [clearSession].
  static Future<void> clear() => clearSession();
}
