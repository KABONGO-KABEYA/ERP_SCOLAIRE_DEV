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
///
/// **Loopback (`127.0.0.1` / `localhost`) :**
/// Sur Android, `127.0.0.1` désigne **le téléphone lui-même**, pas le PC.
/// Ne jamais l'utiliser comme URL locale/cloud pour un APK « normal ».
/// Uniquement pour debug USB avec `adb reverse`, et seulement si
/// `--dart-define=ALLOW_USB_LOCAL_LOOPBACK=true` (voir `run-on-phone.ps1 -UsbLocalTunnel`).
abstract final class ApiConfig {
  /// API locale principale (réseau établissement). Émulateur Android → 10.0.2.2.
  ///
  /// Ne pas passer `http://127.0.0.1:…` pour un build téléphone hors tunnel USB :
  /// cela pointe vers le device, pas vers le serveur école.
  static const String localBaseUrl = String.fromEnvironment(
    'LOCAL_API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5096',
  );

  /// Liste optionnelle d'URL locales séparées par des virgules
  /// (toutes les IP LAN du PC : Ethernet, Wi‑Fi, hotspot).
  static const String localCandidatesRaw = String.fromEnvironment(
    'LOCAL_API_CANDIDATES',
    defaultValue: '',
  );

  /// API distante publique (hors Wi‑Fi école). Pas de tunnel USB.
  /// Éviter `127.0.0.1` sauf `-UsbCloudTunnel` + `ALLOW_USB_LOCAL_LOOPBACK`.
  static const String cloudBaseUrl = String.fromEnvironment(
    'CLOUD_API_BASE_URL',
    defaultValue: 'http://169.58.93.203:1804',
  );

  /// Autorise explicitement `127.0.0.1` / `localhost` (tunnel `adb reverse` uniquement).
  static const bool allowUsbLoopback = bool.fromEnvironment(
    'ALLOW_USB_LOCAL_LOOPBACK',
    defaultValue: false,
  );

  /// Rétrocompatibilité : `API_BASE_URL` force l’URL locale si fournie.
  static const String _legacyBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: '',
  );

  static const String _fallbackLocal = 'http://10.0.2.2:5096';
  static const String _fallbackCloud = 'http://169.58.93.203:1804';

  static bool isLoopbackUrl(String url) {
    try {
      final host = Uri.parse(normalize(url)).host.toLowerCase();
      return host == '127.0.0.1' || host == 'localhost' || host == '::1';
    } catch (_) {
      return false;
    }
  }

  static String get effectiveLocalBaseUrl {
    final legacy = _legacyBaseUrl.trim();
    if (isValidBaseUrl(legacy) && _acceptLocalUrl(legacy)) {
      return normalize(legacy);
    }
    final local = localBaseUrl.trim();
    if (isValidBaseUrl(local) && _acceptLocalUrl(local)) {
      return normalize(local);
    }
    return _fallbackLocal;
  }

  /// Toutes les URL locales à sonder (ordre : candidates → primary → legacy).
  /// Loopback exclu sauf [allowUsbLoopback].
  static List<String> get localBaseUrlCandidates {
    final seen = <String>{};
    final out = <String>[];

    void add(String raw) {
      final url = normalize(raw.trim());
      if (!isValidBaseUrl(url)) return;
      if (!_acceptLocalUrl(url)) return;
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

  static bool _acceptLocalUrl(String url) {
    if (!isLoopbackUrl(url)) return true;
    // 127.0.0.1 sur Android = le device. Ignoré hors tunnel USB explicite.
    return allowUsbLoopback;
  }

  static bool get hasCloudUrl => effectiveCloudBaseUrl != null;

  static String? get effectiveCloudBaseUrl {
    final cloud = cloudBaseUrl.trim();
    if (!isValidBaseUrl(cloud)) return null;
    if (isLoopbackUrl(cloud) && !allowUsbLoopback) {
      // Ne jamais traiter 127.0.0.1 comme serveur distant sur un téléphone.
      return normalize(_fallbackCloud);
    }
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
