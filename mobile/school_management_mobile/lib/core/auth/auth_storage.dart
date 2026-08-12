import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../cache/cache_partition_policy.dart';
import 'mobile_role_routing.dart';
import 'session_school_coherence.dart';

class AuthStorage {
  AuthStorage._();

  static const _storage = FlutterSecureStorage();
  static const _accessTokenKey = 'access_token';
  static const _refreshTokenKey = 'refresh_token';
  static const _userNameKey = 'user_name';
  static const _rolesKey = 'user_roles';
  static const _permissionsKey = 'user_permissions';
  static const _schoolIdKey = 'user_school_id';

  static const _sessionKeys = [
    _accessTokenKey,
    _refreshTokenKey,
    _userNameKey,
    _rolesKey,
    _permissionsKey,
    _schoolIdKey,
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
    required String schoolId,
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
    await _storage.write(
      key: await _resolveKey(_schoolIdKey),
      value: CachePartitionPolicy.normalizeSchoolId(schoolId),
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

  static Future<String?> get sessionSchoolId async {
    final raw = await _storage.read(key: await _resolveKey(_schoolIdKey));
    if (raw == null || raw.isEmpty) {
      // Migration douce : claim JWT si ancienne session sans schoolId stocké.
      return SessionSchoolCoherence.peekSchoolIdFromJwt(await accessToken);
    }
    return CachePartitionPolicy.normalizeSchoolId(raw);
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

  static Future<MobileSpace> get mobileSpace async => MobileRoleRouting.resolve(
        roles: await roles,
        permissions: await permissions,
      );

  static Future<bool> get isTeacher async =>
      (await mobileSpace) == MobileSpace.teacher;

  static Future<bool> get isParent async =>
      (await mobileSpace) == MobileSpace.parent;

  static Future<bool> get isPromoteur async =>
      (await mobileSpace) == MobileSpace.promoteur;

  static Future<bool> get isSecretary async =>
      (await mobileSpace) == MobileSpace.secretary;

  /// Permission métier inscription (espace secrétaire) — pas un proxy de rôle.
  static Future<bool> get canManageEnrollments async {
    final perms = await permissions;
    if (perms.contains('students.create')) return true;
    final userRoles = await roles;
    return userRoles.any((r) {
      final upper = r.trim().toUpperCase();
      return upper == 'SECRETAIRE' ||
          upper == 'SECRETARY' ||
          upper.startsWith('SECRET');
    });
  }

  static Future<String> get homeRoute async =>
      MobileRoleRouting.homeRouteFor(await mobileSpace);

  /// ActiveSchoolId doit correspondre au SchoolId session/JWT courant.
  static Future<bool> get sessionMatchesActiveSchool async {
    final active = await CachePartitionPolicy.activeSchoolId();
    final sessionId = await sessionSchoolId;
    final jwtId =
        SessionSchoolCoherence.peekSchoolIdFromJwt(await accessToken);
    return SessionSchoolCoherence.matches(
      activeSchoolId: active,
      sessionSchoolId: sessionId,
      jwtSchoolId: jwtId,
    );
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
