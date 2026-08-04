/// Bootstrap API globale (activation — étapes ultérieures).
abstract final class BootstrapConfig {
  static const String defaultBaseUrl = String.fromEnvironment(
    'BOOTSTRAP_API_BASE_URL',
    defaultValue: 'https://bootstrap.erp-scolaire.com',
  );

  static String get baseUrl {
    final trimmed = defaultBaseUrl.trim();
    if (trimmed.isEmpty) return 'https://bootstrap.erp-scolaire.com';
    return trimmed.endsWith('/')
        ? trimmed.substring(0, trimmed.length - 1)
        : trimmed;
  }
}
