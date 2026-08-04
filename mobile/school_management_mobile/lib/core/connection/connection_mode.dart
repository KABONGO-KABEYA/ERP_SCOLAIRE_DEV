/// Mode de connexion détecté automatiquement (jamais choisi manuellement).
enum ConnectionMode {
  /// Probe en cours au démarrage / rafraîchissement.
  detecting,

  /// API locale joignable — lecture + écriture.
  local,

  /// API cloud joignable, locale non — lecture seule (+ notes enseignant).
  cloud,

  /// Aucune API joignable — consultation des données en cache local.
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
        ConnectionMode.cloud => 'Mode Distant',
        ConnectionMode.offline => 'Mode Cache',
      };

  String get subtitle => switch (this) {
        ConnectionMode.detecting => 'Recherche du serveur…',
        ConnectionMode.local => 'Même Wi‑Fi que le serveur — lecture + écriture.',
        ConnectionMode.cloud =>
          'Autre réseau — serveur distant (lecture seule, notes autorisées).',
        ConnectionMode.offline =>
          'Pas de connexion — données en cache uniquement.',
      };
}

class ConnectionSnapshot {
  const ConnectionSnapshot({
    required this.mode,
    this.baseUrl,
    this.message,
    this.hasInternet,
    this.requiresReauthentication = false,
  });

  final ConnectionMode mode;
  final String? baseUrl;
  final String? message;
  final bool? hasInternet;

  /// §4.10 — session invalidée (changement `serverInstanceId`).
  final bool requiresReauthentication;

  static const detecting = ConnectionSnapshot(mode: ConnectionMode.detecting);

  String get displayLabel {
    if (mode == ConnectionMode.offline && hasInternet == true) {
      return 'Mode Cache (serveurs injoignables)';
    }
    return mode.label;
  }

  String get displaySubtitle => message ?? mode.subtitle;

  ConnectionSnapshot copyWith({
    ConnectionMode? mode,
    String? baseUrl,
    String? message,
    bool? hasInternet,
    bool? requiresReauthentication,
  }) =>
      ConnectionSnapshot(
        mode: mode ?? this.mode,
        baseUrl: baseUrl ?? this.baseUrl,
        message: message ?? this.message,
        hasInternet: hasInternet ?? this.hasInternet,
        requiresReauthentication:
            requiresReauthentication ?? this.requiresReauthentication,
      );
}
