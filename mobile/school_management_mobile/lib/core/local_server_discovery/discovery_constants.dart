/// Constantes partagées avec le module .NET LocalServerDiscovery.
abstract final class DiscoveryConstants {
  static const String serviceType = '_school-management._tcp';
  static const String serviceTypeLocal = '_school-management._tcp.local';
  static const String hostName = 'school-server.local';
  static const int apiPort = 5096;
  static const String healthPath = '/api/health';
  static const String defaultRemoteBaseUrl = 'http://169.58.93.203:1804';
  static const String lastKnownPrefsKey = 'local_server_discovery.last_base_url';

  static const Duration mdnsTimeout = Duration(seconds: 2);
  static const Duration lastKnownTimeout = Duration(seconds: 2);
  static const Duration scanProbeTimeout = Duration(milliseconds: 500);
  static const Duration backgroundRecheckInterval = Duration(seconds: 30);
  static const int scanMaxParallelism = 32;
}
