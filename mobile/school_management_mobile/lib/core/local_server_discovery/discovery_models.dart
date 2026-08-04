enum DiscoverySource { unknown, mdns, lastKnown, subnetScan, remote }

enum DiscoveryMode { detecting, local, remote, offline }

/// Identité serveur (health v2 — protocolVersion 2).
class ServerHealthIdentity {
  const ServerHealthIdentity({
    this.serverInstanceId,
    this.schoolId,
    this.schoolName,
    this.licenseId,
    this.publicKeyFingerprint,
    this.keyVersion,
  });

  final String? serverInstanceId;
  final String? schoolId;
  final String? schoolName;
  final String? licenseId;
  final String? publicKeyFingerprint;
  final int? keyVersion;

  factory ServerHealthIdentity.fromJson(Map<String, dynamic>? json) {
    if (json == null) {
      return const ServerHealthIdentity();
    }
    return ServerHealthIdentity(
      serverInstanceId: json['serverInstanceId']?.toString(),
      schoolId: json['schoolId']?.toString(),
      schoolName: json['schoolName']?.toString(),
      licenseId: json['licenseId']?.toString(),
      publicKeyFingerprint: json['publicKeyFingerprint']?.toString(),
      keyVersion: json['keyVersion'] is int
          ? json['keyVersion'] as int
          : int.tryParse(json['keyVersion']?.toString() ?? ''),
    );
  }
}

class HealthInfo {
  const HealthInfo({
    required this.status,
    required this.server,
    required this.school,
    required this.version,
    required this.time,
    this.apiVersion,
    this.protocolVersion,
    this.identity,
    this.serverSignature,
  });

  final String status;
  final String server;
  final String school;
  final String version;
  final DateTime time;
  final String? apiVersion;
  final int? protocolVersion;
  final ServerHealthIdentity? identity;
  final String? serverSignature;

  factory HealthInfo.fromJson(Map<String, dynamic> json) {
    ServerHealthIdentity? identity;
    final identityRaw = json['identity'];
    if (identityRaw is Map) {
      identity = ServerHealthIdentity.fromJson(
        Map<String, dynamic>.from(identityRaw),
      );
    }

    final schoolName = identity?.schoolName ??
        json['schoolName']?.toString() ??
        json['school']?.toString() ??
        'École';

    return HealthInfo(
      status: (json['status'] ?? 'ok').toString(),
      server: (json['server'] ?? 'local').toString(),
      school: schoolName,
      version: (json['version'] ?? '1.0.0').toString(),
      time: DateTime.tryParse(json['time']?.toString() ?? '')?.toUtc() ??
          DateTime.now().toUtc(),
      apiVersion: json['apiVersion']?.toString(),
      protocolVersion: json['protocolVersion'] is int
          ? json['protocolVersion'] as int
          : int.tryParse(json['protocolVersion']?.toString() ?? ''),
      identity: identity,
      serverSignature: json['serverSignature']?.toString(),
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
    this.serverInstanceIdChanged = false,
    this.previousServerInstanceId,
    this.observedServerInstanceId,
  });

  final DiscoveryMode mode;
  final DiscoverySource source;
  final String? baseUrl;
  final HealthInfo? health;
  final String message;

  /// §4.10 — instance serveur différente du binding (actions recovery = étape 5).
  final bool serverInstanceIdChanged;
  final String? previousServerInstanceId;
  final String? observedServerInstanceId;

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
