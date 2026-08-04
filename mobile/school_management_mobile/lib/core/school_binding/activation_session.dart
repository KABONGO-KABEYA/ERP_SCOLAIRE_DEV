/// Session d'activation transitoire (architecture v2 §4.4) — étape 2 : modèle uniquement.
enum ActivationSessionStatus {
  pending,
  completed,
  failed,
  revoked;

  static ActivationSessionStatus? fromJson(String? raw) {
    if (raw == null || raw.isEmpty) return null;
    for (final value in ActivationSessionStatus.values) {
      if (value.name == raw) return value;
    }
    return null;
  }

  String toJson() => name;
}

/// Copie client optionnelle entre `activation/start` et `activation/complete`.
class ActivationSession {
  const ActivationSession({
    required this.activationSessionId,
    required this.activationTokenId,
    required this.deviceId,
    required this.schoolId,
    required this.status,
    required this.createdAt,
    required this.expiresAt,
    this.clientHints,
  });

  final String activationSessionId;
  final String activationTokenId;
  final String deviceId;
  final String schoolId;
  final ActivationSessionStatus status;
  final DateTime createdAt;
  final DateTime expiresAt;
  final Map<String, dynamic>? clientHints;

  bool get isExpired => DateTime.now().toUtc().isAfter(expiresAt.toUtc());

  factory ActivationSession.fromJson(Map<String, dynamic> json) {
    final statusRaw = json['status']?.toString();
    final status = ActivationSessionStatus.fromJson(statusRaw) ??
        ActivationSessionStatus.pending;

    Map<String, dynamic>? hints;
    final hintsRaw = json['clientHints'];
    if (hintsRaw is Map) {
      hints = Map<String, dynamic>.from(hintsRaw);
    }

    return ActivationSession(
      activationSessionId: json['activationSessionId']?.toString() ?? '',
      activationTokenId: json['activationTokenId']?.toString() ?? '',
      deviceId: json['deviceId']?.toString() ?? '',
      schoolId: json['schoolId']?.toString() ?? '',
      status: status,
      createdAt: _parseUtc(json['createdAt']) ?? DateTime.now().toUtc(),
      expiresAt: _parseUtc(json['expiresAt']) ?? DateTime.now().toUtc(),
      clientHints: hints,
    );
  }

  Map<String, dynamic> toJson() => {
        'activationSessionId': activationSessionId,
        'activationTokenId': activationTokenId,
        'deviceId': deviceId,
        'schoolId': schoolId,
        'status': status.toJson(),
        'createdAt': createdAt.toUtc().toIso8601String(),
        'expiresAt': expiresAt.toUtc().toIso8601String(),
        if (clientHints != null) 'clientHints': clientHints,
      };

  static DateTime? _parseUtc(Object? raw) {
    if (raw == null) return null;
    return DateTime.tryParse(raw.toString())?.toUtc();
  }
}
