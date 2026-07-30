enum DiscoverySource { unknown, mdns, lastKnown, subnetScan, remote }

enum DiscoveryMode { detecting, local, remote, offline }

class HealthInfo {
  const HealthInfo({
    required this.status,
    required this.server,
    required this.school,
    required this.version,
    required this.time,
  });

  final String status;
  final String server;
  final String school;
  final String version;
  final DateTime time;

  factory HealthInfo.fromJson(Map<String, dynamic> json) {
    return HealthInfo(
      status: (json['status'] ?? 'ok').toString(),
      server: (json['server'] ?? 'local').toString(),
      school: (json['school'] ?? 'École').toString(),
      version: (json['version'] ?? '1.0.0').toString(),
      time: DateTime.tryParse(json['time']?.toString() ?? '')?.toUtc() ??
          DateTime.now().toUtc(),
    );
  }
}

class DiscoveryResult {
  const DiscoveryResult({
    required this.mode,
    required this.source,
    this.baseUrl,
    this.health,
    required this.message,
  });

  final DiscoveryMode mode;
  final DiscoverySource source;
  final String? baseUrl;
  final HealthInfo? health;
  final String message;

  bool get isLocal => mode == DiscoveryMode.local && baseUrl != null;
  bool get isRemote => mode == DiscoveryMode.remote && baseUrl != null;

  static const detecting = DiscoveryResult(
    mode: DiscoveryMode.detecting,
    source: DiscoverySource.unknown,
    message: 'Recherche du serveur…',
  );

  static DiscoveryResult offline(String message) => DiscoveryResult(
        mode: DiscoveryMode.offline,
        source: DiscoverySource.unknown,
        message: message,
      );
}
