/// Mode de connexion détecté automatiquement (jamais choisi manuellement).
enum ConnectionMode {
  /// Probe en cours au démarrage / rafraîchissement.
  detecting,

  /// API locale joignable — lecture + écriture.
  local,

  /// API cloud joignable, locale non — lecture seule (+ notes enseignant).
  cloud,

  /// Aucune API joignable (Wi‑Fi école hors portée et/ou Cloud indisponible).
  offline,
}

extension ConnectionModeX on ConnectionMode {
  bool get isOnline => this == ConnectionMode.local || this == ConnectionMode.cloud;

  bool get allowsWrites => this == ConnectionMode.local;

  /// Exception documentée : saisie de notes en Mode Cloud pour enseignants autorisés.
  bool get allowsGradeWrites =>
      this == ConnectionMode.local || this == ConnectionMode.cloud;

  String get label => switch (this) {
        ConnectionMode.detecting => 'Détection…',
        ConnectionMode.local => 'Mode Local',
        ConnectionMode.cloud => 'Mode Cloud',
        ConnectionMode.offline => 'Hors ligne',
      };

  String get subtitle => switch (this) {
        ConnectionMode.detecting => 'Recherche du serveur…',
        ConnectionMode.local => 'Connecté au serveur de l\'établissement.',
        ConnectionMode.cloud =>
          'Connecté au Cloud — lecture seule (notes enseignants autorisées).',
        ConnectionMode.offline => 'Aucun serveur disponible.',
      };
}

class ConnectionSnapshot {
  const ConnectionSnapshot({
    required this.mode,
    this.baseUrl,
    this.message,
    this.hasInternet,
  });

  final ConnectionMode mode;
  final String? baseUrl;
  final String? message;

  /// `true` si une connectivité Internet générale est détectée (4G/Wi‑Fi public).
  final bool? hasInternet;

  static const detecting = ConnectionSnapshot(mode: ConnectionMode.detecting);

  String get displayLabel {
    if (mode == ConnectionMode.offline && hasInternet == true) {
      return 'Serveur inaccessible';
    }
    return mode.label;
  }

  String get displaySubtitle => message ?? mode.subtitle;

  ConnectionSnapshot copyWith({
    ConnectionMode? mode,
    String? baseUrl,
    String? message,
    bool? hasInternet,
  }) =>
      ConnectionSnapshot(
        mode: mode ?? this.mode,
        baseUrl: baseUrl ?? this.baseUrl,
        message: message ?? this.message,
        hasInternet: hasInternet ?? this.hasInternet,
      );
}
