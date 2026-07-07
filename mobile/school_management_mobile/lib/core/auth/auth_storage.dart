import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class AuthStorage {
  AuthStorage._();

  static const _storage = FlutterSecureStorage();
  static const _accessTokenKey = 'access_token';
  static const _refreshTokenKey = 'refresh_token';
  static const _userNameKey = 'user_name';
  static const _rolesKey = 'user_roles';

  static Future<void> saveSession({
    required String accessToken,
    required String refreshToken,
    required String userName,
    required List<String> roles,
  }) async {
    await _storage.write(key: _accessTokenKey, value: accessToken);
    await _storage.write(key: _refreshTokenKey, value: refreshToken);
    await _storage.write(key: _userNameKey, value: userName);
    await _storage.write(key: _rolesKey, value: roles.join(','));
  }

  static Future<String?> get accessToken => _storage.read(key: _accessTokenKey);

  static Future<String?> get refreshToken => _storage.read(key: _refreshTokenKey);

  static Future<String?> get userName => _storage.read(key: _userNameKey);

  static Future<List<String>> get roles async {
    final raw = await _storage.read(key: _rolesKey);
    if (raw == null || raw.isEmpty) return [];
    return raw.split(',').where((r) => r.isNotEmpty).toList();
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

  static Future<String> get homeRoute async {
    if (await isDirection) return '/direction/dashboard';
    if (await isTeacher) return '/teacher/assignments';
    return '/children';
  }

  static Future<void> clear() => _storage.deleteAll();
}
