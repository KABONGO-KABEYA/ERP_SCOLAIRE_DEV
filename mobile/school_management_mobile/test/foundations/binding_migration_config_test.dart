import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/config/binding_migration_config.dart';

void main() {
  group('Étape 2 — BindingMigrationPolicy', () {
    test('strictSchoolDiscovery defaults to false', () {
      expect(BindingMigrationConfig.strictSchoolDiscovery, isFalse);
      expect(BindingMigrationPolicy.isStrictSchoolDiscoveryEnabled, isFalse);
    });

    test('migration end computed from days when end utc empty', () {
      final end = BindingMigrationPolicy.configuredMigrationEndUtc;
      expect(end, isNotNull);
      expect(
        end!.difference(BindingMigrationConfig.migrationEpochUtc).inDays,
        BindingMigrationConfig.jwtBindingMigrationDays,
      );
    });
  });
}
