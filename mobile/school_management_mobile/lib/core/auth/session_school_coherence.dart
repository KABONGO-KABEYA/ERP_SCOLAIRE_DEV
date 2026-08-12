import 'dart:convert';

import '../cache/cache_partition_policy.dart';

/// Garantit ActiveSchoolId == SchoolId de session/JWT (architecture multi-école).
///
/// Ne modifie pas RegisteredSchoolsStore / ActiveSchoolId / SchoolBinding :
/// lit l'actif et compare à la session partitionnée / claim `school_id`.
abstract final class SessionSchoolCoherence {
  /// Extrait le claim `school_id` (ou `schoolId`) d'un JWT sans valider la signature.
  static String? peekSchoolIdFromJwt(String? jwt) {
    if (jwt == null || jwt.isEmpty) return null;
    try {
      final parts = jwt.split('.');
      if (parts.length < 2) return null;
      final normalized = base64Url.normalize(parts[1]);
      final payload =
          jsonDecode(utf8.decode(base64Url.decode(normalized)));
      if (payload is! Map) return null;
      final raw = payload['school_id'] ?? payload['schoolId'];
      if (raw == null) return null;
      final id = raw.toString().trim();
      if (id.isEmpty) return null;
      return CachePartitionPolicy.normalizeSchoolId(id);
    } catch (_) {
      return null;
    }
  }

  static bool matches({
    required String? activeSchoolId,
    required String? sessionSchoolId,
    String? jwtSchoolId,
  }) {
    if (activeSchoolId == null || activeSchoolId.isEmpty) return false;
    final active = CachePartitionPolicy.normalizeSchoolId(activeSchoolId);

    if (sessionSchoolId != null && sessionSchoolId.isNotEmpty) {
      if (CachePartitionPolicy.normalizeSchoolId(sessionSchoolId) != active) {
        return false;
      }
    }

    if (jwtSchoolId != null && jwtSchoolId.isNotEmpty) {
      if (CachePartitionPolicy.normalizeSchoolId(jwtSchoolId) != active) {
        return false;
      }
    }

    // Au moins une source session/JWT doit confirmer l'école active.
    final hasSession =
        sessionSchoolId != null && sessionSchoolId.trim().isNotEmpty;
    final hasJwt = jwtSchoolId != null && jwtSchoolId.trim().isNotEmpty;
    return hasSession || hasJwt;
  }

  static bool matchesLoginUser({
    required String? activeSchoolId,
    required String userSchoolId,
    String? accessToken,
  }) {
    final jwtId = peekSchoolIdFromJwt(accessToken);
    return matches(
      activeSchoolId: activeSchoolId,
      sessionSchoolId: userSchoolId,
      jwtSchoolId: jwtId,
    );
  }
}
