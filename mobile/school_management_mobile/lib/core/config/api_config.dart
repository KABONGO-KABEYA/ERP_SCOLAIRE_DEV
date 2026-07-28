/// Configuration des URL API (sans slash final).
///
/// Bascule automatique (sans USB) :
/// 1. Même Wi‑Fi que le PC serveur → API locale
/// 2. Autre réseau (4G / autre Wi‑Fi) → API distante (Cloud)
/// 3. Aucune connexion → Mode Cache (données déjà téléchargées)
///
/// Sous PowerShell, toujours guillemeter le dart-define :
/// `--dart-define=LOCAL_API_BASE_URL="http://192.168.137.33:5041"`
/// Plusieurs IP locales (Ethernet + Wi‑Fi/hotspot) :
/// `--dart-define=LOCAL_API_CANDIDATES="http://10.10.10.112:5041,http://192.168.137.33:5041"`
abstract final class ApiConfig {
  /// API locale principale (réseau établissement). Émulateur Android → 10.0.2.2.
  static const String localBaseUrl = String.fromEnvironment(
    'LOCAL_API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5041',
  );

  /// Liste optionnelle d'URL locales séparées par des virgules
  /// (toutes les IP LAN du PC : Ethernet, Wi‑Fi, hotspot).
  static const String localCandidatesRaw = String.fromEnvironment(
    'LOCAL_API_CANDIDATES',
    defaultValue: '',
  );

  /// API distante publique (hors Wi‑Fi école). Pas de tunnel USB.
  static const String cloudBaseUrl = String.fromEnvironment(
    'CLOUD_API_BASE_URL',
    defaultValue: 'http://161.97.105.22:1804',
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

  /// Toutes les URL locales à sonder (ordre : candidates → primary → legacy).
  static List<String> get localBaseUrlCandidates {
    final seen = <String>{};
    final out = <String>[];

    void add(String raw) {
      final url = normalize(raw.trim());
      if (!isValidBaseUrl(url)) return;
      final host = Uri.parse(url).host.toLowerCase();
      if (host == '127.0.0.1' || host == 'localhost') return;
      if (seen.add(url)) out.add(url);
    }

    for (final part in localCandidatesRaw.split(',')) {
      add(part);
    }
    add(localBaseUrl);
    add(_legacyBaseUrl);
    if (out.isEmpty) add(_fallbackLocal);
    return out;
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
