/// Configuration des URL API (sans slash final).
///
/// Local (établissement) prioritaire ; Cloud en secours lecture seule.
///
/// Sous PowerShell, toujours guillemeter le dart-define :
/// `--dart-define=LOCAL_API_BASE_URL="http://10.115.85.242:5041"`
abstract final class ApiConfig {
  /// API locale (réseau établissement). Émulateur Android → 10.0.2.2.
  static const String localBaseUrl = String.fromEnvironment(
    'LOCAL_API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5041',
  );

  /// API cloud (copie synchronisée). Vide = cloud désactivé côté mobile.
  static const String cloudBaseUrl = String.fromEnvironment(
    'CLOUD_API_BASE_URL',
    defaultValue: '',
  );

  /// Rétrocompatibilité : `API_BASE_URL` force l’URL locale si fournie.
  static const String _legacyBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: '',
  );

  static const String _fallbackLocal = 'http://10.0.2.2:5041';

  static String get effectiveLocalBaseUrl {
    final legacy = _legacyBaseUrl.trim();
    if (isValidBaseUrl(legacy)) return normalize(legacy);
    final local = localBaseUrl.trim();
    if (isValidBaseUrl(local)) return normalize(local);
    return _fallbackLocal;
  }

  static bool get hasCloudUrl => isValidBaseUrl(cloudBaseUrl.trim());

  static String? get effectiveCloudBaseUrl {
    final cloud = cloudBaseUrl.trim();
    if (!isValidBaseUrl(cloud)) return null;
    return normalize(cloud);
  }

  static String normalize(String url) {
    var cleaned = url.trim();
    while (cleaned.endsWith('/')) {
      cleaned = cleaned.substring(0, cleaned.length - 1);
    }
    return cleaned;
  }

  /// Dio exige une URL absolue avec un host non vide (hors web).
  static bool isValidBaseUrl(String url) {
    if (url.isEmpty) return false;
    final uri = Uri.tryParse(url);
    if (uri == null) return false;
    if (uri.scheme != 'http' && uri.scheme != 'https') return false;
    return uri.host.isNotEmpty;
  }
}

/// Ancien export — préférer [ApiConfig.effectiveLocalBaseUrl].
@Deprecated('Utiliser ApiConfig.effectiveLocalBaseUrl')
const String apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://10.0.2.2:5041',
);
