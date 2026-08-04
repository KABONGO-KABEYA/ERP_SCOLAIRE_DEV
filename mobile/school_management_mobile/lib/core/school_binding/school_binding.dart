import '../connection/connection_protocol_constants.dart';

/// Contexte école persisté après `activation/complete` (architecture v2 §4.6).
class SchoolBinding {
  const SchoolBinding({
    required this.schoolId,
    required this.schoolName,
    required this.cloudBaseUrl,
    required this.serverInstanceId,
    required this.activationDate,
    required this.activationTokenId,
    required this.activationSessionId,
    required this.deviceId,
    required this.protocolVersion,
    this.licenseId,
    this.suggestedUserName,
    this.expiresAt,
    this.extensions,
  });

  final String schoolId;
  final String schoolName;
  final String cloudBaseUrl;
  final String serverInstanceId;
  final String? licenseId;
  final DateTime activationDate;
  final String activationTokenId;
  final String activationSessionId;
  final String deviceId;
  final int protocolVersion;
  final String? suggestedUserName;
  final DateTime? expiresAt;
  final Map<String, dynamic>? extensions;

  factory SchoolBinding.fromJson(Map<String, dynamic> json) {
    Map<String, dynamic>? ext;
    final extRaw = json['extensions'];
    if (extRaw is Map) {
      ext = Map<String, dynamic>.from(extRaw);
    }

    return SchoolBinding(
      schoolId: json['schoolId']?.toString() ?? '',
      schoolName: json['schoolName']?.toString() ?? '',
      cloudBaseUrl: json['cloudBaseUrl']?.toString() ?? '',
      serverInstanceId: json['serverInstanceId']?.toString() ?? '',
      licenseId: json['licenseId']?.toString(),
      activationDate:
          _parseUtc(json['activationDate']) ?? DateTime.now().toUtc(),
      activationTokenId: json['activationTokenId']?.toString() ?? '',
      activationSessionId: json['activationSessionId']?.toString() ?? '',
      deviceId: json['deviceId']?.toString() ?? '',
      protocolVersion: json['protocolVersion'] is int
          ? json['protocolVersion'] as int
          : int.tryParse(json['protocolVersion']?.toString() ?? '') ??
              ConnectionProtocolConstants.protocolVersion,
      suggestedUserName: json['suggestedUserName']?.toString(),
      expiresAt: _parseUtc(json['expiresAt']),
      extensions: ext,
    );
  }

  Map<String, dynamic> toJson() => {
        'schoolId': schoolId,
        'schoolName': schoolName,
        'cloudBaseUrl': cloudBaseUrl,
        'serverInstanceId': serverInstanceId,
        'licenseId': licenseId,
        'activationDate': activationDate.toUtc().toIso8601String(),
        'activationTokenId': activationTokenId,
        'activationSessionId': activationSessionId,
        'deviceId': deviceId,
        'protocolVersion': protocolVersion,
        if (suggestedUserName != null) 'suggestedUserName': suggestedUserName,
        if (expiresAt != null)
          'expiresAt': expiresAt!.toUtc().toIso8601String(),
        if (extensions != null) 'extensions': extensions,
      };

  static DateTime? _parseUtc(Object? raw) {
    if (raw == null) return null;
    return DateTime.tryParse(raw.toString())?.toUtc();
  }
}
