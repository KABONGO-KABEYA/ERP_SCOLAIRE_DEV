import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/local_server_discovery/discovery_models.dart';

void main() {
  group('Foundations — Health v2 client parse', () {
    test('legacy health json without identity block still parses', () {
      final health = HealthInfo.fromJson({
        'status': 'ok',
        'server': 'local',
        'school': 'Mon École',
        'version': '1.0.0',
        'time': '2026-01-01T12:00:00Z',
      });

      expect(health.status, 'ok');
      expect(health.school, 'Mon École');
      expect(health.protocolVersion, isNull);
      expect(health.identity, isNull);
    });

    test('v2 health json with identity and protocolVersion', () {
      final health = HealthInfo.fromJson({
        'status': 'ok',
        'server': 'local',
        'school': 'Mon École',
        'version': '1.0.0',
        'time': '2026-01-01T12:00:00Z',
        'apiVersion': '1.0',
        'protocolVersion': 2,
        'serverSignature': null,
        'identity': {
          'serverInstanceId': '11111111-1111-1111-1111-111111111111',
          'schoolId': null,
          'schoolName': 'Mon École',
          'publicKeyFingerprint': 'sha256:abc',
          'keyVersion': 1,
        },
      });

      expect(health.protocolVersion, 2);
      expect(health.apiVersion, '1.0');
      expect(health.identity?.serverInstanceId,
          '11111111-1111-1111-1111-111111111111');
      expect(health.identity?.keyVersion, 1);
      expect(health.serverSignature, isNull);
    });
  });
}
