import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'establishment_session.dart';

/// Session établissement éphémère (jamais le secret JWT).
class EstablishmentSessionStore {
  EstablishmentSessionStore({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  static const secureStorageKey = 'establishment_session';

  final FlutterSecureStorage _storage;
  EstablishmentSession? _memory;

  EstablishmentSession? get currentInMemory => _memory;

  Future<void> persist(EstablishmentSession session) async {
    _memory = session;
    await _storage.write(
      key: secureStorageKey,
      value: jsonEncode(session.toJson()),
    );
  }

  Future<EstablishmentSession?> loadPersisted() async {
    final raw = await _storage.read(key: secureStorageKey);
    if (raw == null || raw.isEmpty) return null;
    final decoded = jsonDecode(raw);
    if (decoded is! Map<String, dynamic>) return null;
    final session = EstablishmentSession.fromJson(decoded);
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
