/// Session éphémère entre `/establishment/start` et `/establishment/complete`.
class EstablishmentSession {
  const EstablishmentSession({
    required this.establishmentSessionId,
    required this.schoolId,
    required this.deviceId,
    required this.status,
    required this.expiresAt,
  });

  final String establishmentSessionId;
  final String schoolId;
  final String deviceId;
  final String status;
  final DateTime expiresAt;

  bool get isExpired => DateTime.now().toUtc().isAfter(expiresAt.toUtc());

  factory EstablishmentSession.fromJson(Map<String, dynamic> json) {
    return EstablishmentSession(
      establishmentSessionId:
          json['establishmentSessionId']?.toString() ?? '',
      schoolId: json['schoolId']?.toString() ?? '',
      deviceId: json['deviceId']?.toString() ?? '',
      status: json['status']?.toString() ?? 'pending',
      expiresAt: _parseUtc(json['expiresAt']) ?? DateTime.now().toUtc(),
    );
  }

  Map<String, dynamic> toJson() => {
        'establishmentSessionId': establishmentSessionId,
        'schoolId': schoolId,
        'deviceId': deviceId,
        'status': status,
        'expiresAt': expiresAt.toUtc().toIso8601String(),
      };

  static DateTime? _parseUtc(Object? raw) {
    if (raw == null) return null;
    return DateTime.tryParse(raw.toString())?.toUtc();
  }
}
