import '../config/binding_migration_config.dart';
import 'school_binding_activation_gate.dart';
import 'school_binding_repository.dart';

/// Gates connexion / discovery (architecture v2).
abstract final class SchoolBindingGate {
  static SchoolBindingRepository bindingRepository = SchoolBindingRepository();

  /// Après fin migration : parent sans binding ne peut pas ouvrir de session.
  static Future<bool> shouldBlockParentSessionWithoutBinding() async {
    if (!BindingMigrationPolicy.isPostMigrationPhase) {
      return false;
    }
    return !(await bindingRepository.hasBinding());
  }

  /// Pendant la fenêtre migration : login parent legacy autorisé sans binding.
  static bool get allowsLegacyParentLoginWithoutBinding =>
      BindingMigrationPolicy.effectiveAllowJwtBindingMigration;

  /// Discovery filtrée par `SchoolBinding` — active si `STRICT_SCHOOL_DISCOVERY` et binding présent.
  static Future<bool> shouldFilterDiscoveryByBinding() async {
    if (!BindingMigrationPolicy.isStrictSchoolDiscoveryEnabled) {
      return false;
    }
    return bindingRepository.hasBinding();
  }

  /// Entrée activation QR prioritaire (post-migration, pas de binding).
  static Future<bool> shouldPreferActivationEntryForParent() async {
    if (await bindingRepository.hasBinding()) {
      return false;
    }
    return BindingMigrationPolicy.isPostMigrationPhase;
  }

  static Future<bool> shouldUseBootstrapActivationFlow() async =>
      SchoolBindingActivationGate.isActivationFlowEnabled;
}
