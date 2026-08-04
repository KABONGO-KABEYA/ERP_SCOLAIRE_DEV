import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'activation_session.dart';

/// Session éphémère entre start / complete (architecture v2 §4.4).
/// Étape 2 : stockage prêt ; aucun appel depuis l'UI ou la discovery.
class ActivationSessionStore {
  ActivationSessionStore({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  static const secureStorageKey = 'activation_session';

  final FlutterSecureStorage _storage;
  ActivationSession? _memory;

  ActivationSession? get currentInMemory => _memory;

  void setInMemory(ActivationSession? session) {
    _memory = session;
  }

  Future<void> persist(ActivationSession session) async {
    _memory = session;
    await _storage.write(
      key: secureStorageKey,
      value: jsonEncode(session.toJson()),
    );
  }

  Future<ActivationSession?> loadPersisted() async {
    final raw = await _storage.read(key: secureStorageKey);
    if (raw == null || raw.isEmpty) {
      return null;
    }
    final decoded = jsonDecode(raw);
    if (decoded is! Map<String, dynamic>) {
      return null;
    }
    final session = ActivationSession.fromJson(decoded);
    if (session.isExpired) {
      await clear();
      return null;
    }
    _memory = session;
    return session;
  }

  Future<void> clear() async {
    _memory = null;
    await _storage.delete(key: secureStorageKey);
  }
}
