/// Constantes partagées avec le module .NET LocalServerDiscovery.
abstract final class DiscoveryConstants {
  static const String serviceType = '_school-management._tcp';
  static const String serviceTypeLocal = '_school-management._tcp.local';
  static const String hostName = 'school-server.local';
  static const int apiPort = 5096;
  static const String healthPath = '/api/health';
  static const String defaultRemoteBaseUrl = 'http://169.58.93.203:1804';
  static const String lastKnownPrefsKey = 'local_server_discovery.last_base_url';

  /// Budget global d'une découverte complète (lastKnown → mDNS → scan → remote).
  /// Au-delà : abandon local et tentative cloud / offline.
  static const Duration discoveryOverallTimeout = Duration(seconds: 8);

  /// Timeout exposé à l'UI (`ConnectionModeNotifier`) — légèrement > overall.
  static const Duration discoveryUiTimeout = Duration(seconds: 10);

  static const Duration mdnsTimeout = Duration(seconds: 2);
  static const Duration mdnsStartTimeout = Duration(seconds: 1);
  static const Duration lastKnownTimeout = Duration(seconds: 1);

  /// Durée max du scan subnet (indépendamment du nombre d'adresses).
  static const Duration scanOverallTimeout = Duration(seconds: 3);
  static const Duration scanProbeTimeout = Duration(milliseconds: 400);
  static const int scanMaxParallelism = 12;

  /// Plafond dur : jamais 254×N préfixes (évite ~1016 probes au démarrage).
  static const int scanMaxAddresses = 48;

  /// Nombre max de préfixes /24 scannés (Wi‑Fi école typiquement 1).
  static const int scanMaxPrefixes = 1;

  static const Duration backgroundRecheckInterval = Duration(seconds: 60);

  /// `127.0.0.1` / `localhost` = le téléphone lui-même sur Android (sauf tunnel
  /// `adb reverse` explicitement activé pour le debug USB).
  static bool isLoopbackHost(String host) {
    final h = host.trim().toLowerCase();
    return h == '127.0.0.1' || h == 'localhost' || h == '::1';
  }

  /// Préfixes / plages typiques d'adaptateurs virtuels (VBox, WSL, Hyper-V, VPN lab).
  /// Ignorés pour mDNS et scan afin d'éviter des probes inutiles qui figent l'UI.
  static bool isLikelyVirtualHost(String host) {
    if (isLoopbackHost(host)) return true;
    final parts = host.split('.');
    if (parts.length != 4) return false;
    final a = int.tryParse(parts[0]) ?? -1;
    final b = int.tryParse(parts[1]) ?? -1;
    // VirtualBox host-only / NAT typiques
    if (a == 192 && b == 168) {
      final c = int.tryParse(parts[2]) ?? -1;
      if (c >= 56 && c <= 59) return true;
    }
    // WSL2 / Hyper-V (souvent 172.27–29.x)
    if (a == 172 && b >= 27 && b <= 29) return true;
    return false;
  }

  /// Préfixe /24 IPv4 (`a.b.c`) ou null si host non IPv4.
  static String? ipv4Prefix(String host) {
    final parts = host.split('.');
    if (parts.length != 4) return null;
    for (final p in parts) {
      final n = int.tryParse(p);
      if (n == null || n < 0 || n > 255) return null;
    }
    return '${parts[0]}.${parts[1]}.${parts[2]}';
  }

  static bool isPrivateIpv4(String host) {
    if (isLoopbackHost(host)) return false;
    final parts = host.split('.');
    if (parts.length != 4) return false;
    final a = int.tryParse(parts[0]) ?? -1;
    final b = int.tryParse(parts[1]) ?? -1;
    return a == 10 ||
        (a == 172 && b >= 16 && b <= 31) ||
        (a == 192 && b == 168);
  }
}
