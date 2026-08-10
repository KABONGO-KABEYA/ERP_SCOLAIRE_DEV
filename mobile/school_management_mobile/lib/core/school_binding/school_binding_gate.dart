import '../config/binding_migration_config.dart';
import 'school_binding_activation_gate.dart';
import 'school_binding_repository.dart';

/// Gates connexion / discovery (architecture v2 + multi-établissements).
abstract final class SchoolBindingGate {
  static SchoolBindingRepository bindingRepository = SchoolBindingRepository();

  /// Après fin migration : parent sans binding ne peut pas ouvrir de session.
  static Future<bool> shouldBlockParentSessionWithoutBinding() async {
    if (!BindingMigrationPolicy.isPostMigrationPhase) {
      return false;
    }
    return !(await bindingRepository.hasBinding());
  }

  /// Tout rôle : pas de login métier sans au moins un établissement enregistré.
  static Future<bool> shouldBlockSessionWithoutEstablishment() async {
    return !(await bindingRepository.hasAnyRegisteredSchool());
  }

  /// Premier lancement / registre vide → QR établissement obligatoire.
  static Future<bool> shouldRequireEstablishmentQr() async {
    return !(await bindingRepository.hasAnyRegisteredSchool());
  }

  /// Alias historique — même sémantique que [shouldRequireEstablishmentQr].
  static Future<bool> shouldRequireActivationQr() =>
      shouldRequireEstablishmentQr();

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

  /// Entrée establish prioritaire (post-migration, pas d'école enregistrée).
  static Future<bool> shouldPreferEstablishmentEntry() async {
    if (await bindingRepository.hasAnyRegisteredSchool()) {
      return false;
    }
    return BindingMigrationPolicy.isPostMigrationPhase;
  }

  /// Alias historique.
  static Future<bool> shouldPreferActivationEntryForParent() =>
      shouldPreferEstablishmentEntry();

  static Future<bool> shouldUseBootstrapActivationFlow() async =>
      SchoolBindingActivationGate.isActivationFlowEnabled;
}
