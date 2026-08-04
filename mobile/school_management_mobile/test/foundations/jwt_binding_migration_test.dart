import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/config/binding_migration_config.dart';
import 'package:school_management_mobile/core/local_server_discovery/discovery_models.dart';
import 'package:school_management_mobile/core/school_binding/jwt_binding_migration_constants.dart';
import 'package:school_management_mobile/core/school_binding/jwt_binding_migration_service.dart';
import 'package:school_management_mobile/features/auth/models/auth_models.dart';
import 'package:school_management_mobile/core/connection/connection_mode.dart';

void main() {
  group('JwtBindingMigrationService.buildBindingFromHealth', () {
    test('creates binding with migration markers', () {
      const session = AuthSession(
        accessToken: 'a',
        refreshToken: 'r',
        user: AuthUser(
          id: 'u1',
          schoolId: '11111111-1111-1111-1111-111111111111',
          userName: 'parent',
          email: 'p@test.com',
          fullName: 'Parent',
          roles: ['PARENT'],
          permissions: [],
        ),
      );

      final health = HealthInfo(
        status: 'ok',
        server: 'local',
        school: 'École Test',
        version: '1',
        time: DateTime.utc(2026, 8, 4),
        protocolVersion: 2,
        identity: const ServerHealthIdentity(
          schoolId: '11111111-1111-1111-1111-111111111111',
          schoolName: 'École Test',
          serverInstanceId: '22222222-2222-2222-2222-222222222222',
        ),
      );

      final binding = JwtBindingMigrationService.buildBindingFromHealth(
        session: session,
        health: health,
        apiBaseUrl: 'http://192.168.1.10:5096',
        connection: const ConnectionSnapshot(
          mode: ConnectionMode.local,
          baseUrl: 'http://192.168.1.10:5096',
        ),
      );

      expect(binding, isNotNull);
      expect(binding!.activationTokenId, JwtBindingMigrationConstants.activationTokenId);
      expect(binding.extensions?['migratedFromJwt'], isTrue);
    });

    test('rejects school mismatch', () {
      const session = AuthSession(
        accessToken: 'a',
        refreshToken: 'r',
        user: AuthUser(
          id: 'u1',
          schoolId: '11111111-1111-1111-1111-111111111111',
          userName: 'parent',
          email: 'p@test.com',
          fullName: 'Parent',
          roles: ['PARENT'],
          permissions: [],
        ),
      );

      final health = HealthInfo(
        status: 'ok',
        server: 'local',
        school: 'Autre',
        version: '1',
        time: DateTime.utc(2026, 8, 4),
        identity: const ServerHealthIdentity(
          schoolId: '33333333-3333-3333-3333-333333333333',
        ),
      );

      final binding = JwtBindingMigrationService.buildBindingFromHealth(
        session: session,
        health: health,
        apiBaseUrl: 'http://localhost:5096',
        connection: const ConnectionSnapshot(mode: ConnectionMode.local),
      );

      expect(binding, isNull);
    });
  });

  group('BindingMigrationPolicy', () {
    test('binding migration config defaults', () {
      expect(BindingMigrationConfig.allowJwtBindingMigration, isTrue);
      expect(BindingMigrationPolicy.effectiveAllowJwtBindingMigration, isTrue);
      expect(BindingMigrationPolicy.isPostMigrationPhase, isFalse);
    });
  });
}
