/// Fenêtre migration JWT → SchoolBinding (architecture v2 §4.11).
/// Étape 2 : flags définis ; non appliqués au login / discovery.
abstract final class BindingMigrationConfig {
  /// `ALLOW_JWT_BINDING_MIGRATION` — compile-time / CI.
  static const bool allowJwtBindingMigration = bool.fromEnvironment(
    'ALLOW_JWT_BINDING_MIGRATION',
    defaultValue: true,
  );

  /// Discovery filtrée par binding — étape 4 ; défaut false (legacy).
  /// Activer en build : `--dart-define=STRICT_SCHOOL_DISCOVERY=true`.
  static const bool strictSchoolDiscovery = bool.fromEnvironment(
    'STRICT_SCHOOL_DISCOVERY',
    defaultValue: false,
  );

  /// Date ISO8601 UTC de fin de migration (prioritaire sur [jwtBindingMigrationDays]).
  static const String jwtBindingMigrationEndUtc = String.fromEnvironment(
    'JWT_BINDING_MIGRATION_END_UTC',
    defaultValue: '',
  );

  /// Durée en jours depuis [migrationEpochUtc] si pas de date explicite.
  static const int jwtBindingMigrationDays = int.fromEnvironment(
    'JWT_BINDING_MIGRATION_DAYS',
    defaultValue: 30,
  );

  /// Ancre pour calcul par durée (release build) — override en tests si besoin.
  static DateTime migrationEpochUtc = DateTime.utc(2026, 8, 4);
}

/// Politique migration — prête pour étapes 3+ ; inactif tant qu'aucun gate ne l'appelle.
abstract final class BindingMigrationPolicy {
  static bool get isStrictSchoolDiscoveryEnabled =>
      BindingMigrationConfig.strictSchoolDiscovery;

  static DateTime? get configuredMigrationEndUtc {
    final raw = BindingMigrationConfig.jwtBindingMigrationEndUtc.trim();
    if (raw.isNotEmpty) {
      return DateTime.tryParse(raw)?.toUtc();
    }
    if (BindingMigrationConfig.jwtBindingMigrationDays <= 0) {
      return null;
    }
    return BindingMigrationConfig.migrationEpochUtc.add(
      Duration(days: BindingMigrationConfig.jwtBindingMigrationDays),
    );
  }

  /// `false` après échéance même si le flag compile-time est true.
  static bool get effectiveAllowJwtBindingMigration {
    if (!BindingMigrationConfig.allowJwtBindingMigration) {
      return false;
    }
    final end = configuredMigrationEndUtc;
    if (end == null) {
      return BindingMigrationConfig.allowJwtBindingMigration;
    }
    return DateTime.now().toUtc().isBefore(end);
  }

  /// Fenêtre migration fermée (date dépassée ou flag compile-time false).
  static bool get isPostMigrationPhase => !effectiveAllowJwtBindingMigration;

  static int? get daysUntilMigrationEndUtc {
    final end = configuredMigrationEndUtc;
    if (end == null) return null;
    final now = DateTime.now().toUtc();
    if (!now.isBefore(end)) return 0;
    return end.difference(now).inDays;
  }

  static bool get isMigrationEndingSoon {
    final days = daysUntilMigrationEndUtc;
    return days != null && days > 0 && days <= 7;
  }
}
