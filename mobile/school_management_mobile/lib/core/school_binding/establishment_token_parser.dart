import 'dart:convert';

/// Lecture locale non validante des claims JWT (sans secret, sans log du token).
abstract final class EstablishmentTokenParser {
  static const schoolEstablishmentType = 'school_establishment';
  static const parentActivationType = 'parent_activation';

  /// Extrait le JWT depuis deep link `erp-scolaire://establish?token=` ou JWT brut.
  static String? extractTokenFromScan(String raw) {
    final trimmed = raw.trim();
    if (trimmed.isEmpty) return null;

    final uri = Uri.tryParse(trimmed);
    if (uri != null && uri.scheme == 'erp-scolaire') {
      final hostOrPath = uri.host.isNotEmpty ? uri.host : uri.path.replaceFirst('/', '');
      if (hostOrPath == 'establish' || uri.path == '/establish') {
        final token = uri.queryParameters['token'];
        if (token != null && token.isNotEmpty) return token;
      }
      // Deep link parent → refusé pour le gate établissement.
      if (hostOrPath == 'activate' || uri.path == '/activate') {
        return null;
      }
    }

    if (trimmed.contains('token=')) {
      final asUri = Uri.tryParse(
        trimmed.startsWith('erp-scolaire')
            ? trimmed
            : 'erp-scolaire://establish?$trimmed',
      );
      if (asUri != null) {
        final hostOrPath =
            asUri.host.isNotEmpty ? asUri.host : asUri.path.replaceFirst('/', '');
        if (hostOrPath == 'establish' || asUri.path == '/establish') {
          final token = asUri.queryParameters['token'];
          if (token != null && token.isNotEmpty) return token;
        }
      }
    }

    // JWT compact (3 segments).
    final parts = trimmed.split('.');
    if (parts.length == 3 && parts.every((p) => p.isNotEmpty)) {
      return trimmed;
    }
    return null;
  }

  /// `token_type` du payload JWT, ou null si illisible.
  static String? peekTokenType(String jwt) {
    try {
      final parts = jwt.split('.');
      if (parts.length < 2) return null;
      final normalized = base64Url.normalize(parts[1]);
      final payload = utf8.decode(base64Url.decode(normalized));
      final map = jsonDecode(payload);
      if (map is! Map) return null;
      return map['token_type']?.toString() ?? map['typ']?.toString();
    } catch (_) {
      return null;
    }
  }

  static bool isParentActivationToken(String jwt) {
    final type = peekTokenType(jwt);
    return type == parentActivationType;
  }

  static bool isSchoolEstablishmentToken(String jwt) {
    final type = peekTokenType(jwt);
    return type == schoolEstablishmentType;
  }
}
