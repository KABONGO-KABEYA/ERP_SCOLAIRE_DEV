import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Identifiant stable de l'installation application (DeviceId — architecture v2).
class DeviceIdentity {
  DeviceIdentity._();

  static const _storageKey = 'device_id_v1';
  static const _storage = FlutterSecureStorage();
  static String? _cached;

  static Future<void> ensureInitialized() async {
    if (_cached != null && _cached!.isNotEmpty) return;
    final existing = await _storage.read(key: _storageKey);
    if (existing != null && existing.isNotEmpty) {
      _cached = existing;
      return;
    }
    final id = _newUuidV4();
    await _storage.write(key: _storageKey, value: id);
    _cached = id;
  }

  static Future<String> get deviceId async {
    await ensureInitialized();
    return _cached!;
  }

  static String? get cachedDeviceId => _cached;

  /// Réinitialise le cache en mémoire (tests de non-régression uniquement).
  @visibleForTesting
  static void resetCachedForTests() {
    _cached = null;
  }

  static String _newUuidV4() {
    final random = Random.secure();
    final bytes = List<int>.generate(16, (_) => random.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    String hex(int b) => b.toRadixString(16).padLeft(2, '0');
    final s = bytes.map(hex).join();
    return '${s.substring(0, 8)}-${s.substring(8, 12)}-${s.substring(12, 16)}-'
        '${s.substring(16, 20)}-${s.substring(20, 32)}';
  }
}
