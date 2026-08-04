import '../config/binding_migration_config.dart';

/// Politique déploiement progressif (STRICT + fin migration JWT).
abstract final class StrictDiscoveryRolloutPolicy {
  /// Phase post-migration : le build prod parent devrait activer
  /// `--dart-define=STRICT_SCHOOL_DISCOVERY=true`.
  static bool get shouldEnableStrictDiscoveryInProductionBuild =>
      BindingMigrationPolicy.isPostMigrationPhase;

  /// Fenêtre migration encore ouverte.
  static bool get isMigrationWindowOpen =>
      BindingMigrationPolicy.effectiveAllowJwtBindingMigration;

  static String get rolloutHint {
    if (isMigrationWindowOpen) {
      final days = BindingMigrationPolicy.daysUntilMigrationEndUtc;
      if (days != null && days > 0) {
        return 'Migration JWT : $days jour(s) restant(s) — préparez STRICT_SCHOOL_DISCOVERY.';
      }
      return 'Migration JWT active — scan QR recommandé pour activation officielle.';
    }
    if (!BindingMigrationPolicy.isStrictSchoolDiscoveryEnabled) {
      return 'Post-migration : activez STRICT_SCHOOL_DISCOVERY au prochain build.';
    }
    return 'Mode strict discovery actif.';
  }
}
