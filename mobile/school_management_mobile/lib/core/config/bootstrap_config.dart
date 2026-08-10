/// Bootstrap API globale — liaison établissement (et activation parent séparée).
abstract final class BootstrapConfig {
  /// URL labo Coolify actuelle (Phase 6+). Ne jamais utiliser `bootstrap.169…`.
  static const String labBootstrapBaseUrl =
      'https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io';

  static const String _legacyForbiddenHost = 'bootstrap.169.58.93.203.sslip.io';

  static const String defaultBaseUrl = String.fromEnvironment(
    'BOOTSTRAP_API_BASE_URL',
    defaultValue: labBootstrapBaseUrl,
  );

  static String get baseUrl {
    final trimmed = defaultBaseUrl.trim();
    if (trimmed.isEmpty) return labBootstrapBaseUrl;
    final normalized = trimmed.endsWith('/')
        ? trimmed.substring(0, trimmed.length - 1)
        : trimmed;
    if (_isForbiddenLegacy(normalized)) {
      return labBootstrapBaseUrl;
    }
    return normalized;
  }

  static bool _isForbiddenLegacy(String url) {
    final lower = url.toLowerCase();
    return lower.contains(_legacyForbiddenHost) ||
        lower.contains('://bootstrap.169.58.93.203');
  }
}
