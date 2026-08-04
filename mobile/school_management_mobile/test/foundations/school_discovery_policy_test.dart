import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/local_server_discovery/discovery_models.dart';
import 'package:school_management_mobile/core/local_server_discovery/school_discovery_policy.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';

void main() {
  final binding = SchoolBinding(
    schoolId: '33333333-3333-3333-3333-333333333333',
    schoolName: 'École A',
    cloudBaseUrl: 'https://cloud.example.com',
    serverInstanceId: '11111111-1111-1111-1111-111111111111',
    activationDate: DateTime.utc(2026, 1, 1),
    activationTokenId: 't',
    activationSessionId: 's',
    deviceId: 'd',
    protocolVersion: 2,
  );

  HealthInfo healthWith(String schoolId, {String? instanceId}) {
    return HealthInfo(
      status: 'ok',
      server: 'local',
      school: 'École A',
      version: '1.0',
      time: DateTime.utc(2026, 1, 1),
      identity: ServerHealthIdentity(
        schoolId: schoolId,
        serverInstanceId: instanceId,
      ),
    );
  }

  group('SchoolDiscoveryPolicy — étape 4', () {
    test('accepts matching schoolId', () {
      final health = healthWith('33333333-3333-3333-3333-333333333333');
      expect(
        SchoolDiscoveryPolicy.acceptsHealthForBinding(health, binding),
        isTrue,
      );
    });

    test('rejects different schoolId', () {
      final health = healthWith('44444444-4444-4444-4444-444444444444');
      expect(
        SchoolDiscoveryPolicy.acceptsHealthForBinding(health, binding),
        isFalse,
      );
    });

    test('rejects missing identity schoolId', () {
      final health = HealthInfo(
        status: 'ok',
        server: 'local',
        school: 'X',
        version: '1',
        time: DateTime.utc(2026, 1, 1),
      );
      expect(
        SchoolDiscoveryPolicy.acceptsHealthForBinding(health, binding),
        isFalse,
      );
    });

    test('cloudBaseUrlForBinding normalizes valid url', () {
      expect(
        SchoolDiscoveryPolicy.cloudBaseUrlForBinding(
          binding.copyWithCloud('https://cloud.example.com/'),
        ),
        'https://cloud.example.com',
      );
    });

    test('detectInstanceChange when ids differ', () {
      final health = healthWith(
        binding.schoolId,
        instanceId: '22222222-2222-2222-2222-222222222222',
      );
      final change =
          SchoolDiscoveryPolicy.detectInstanceChange(binding, health);
      expect(change.detected, isTrue);
      expect(change.previousInstanceId, binding.serverInstanceId);
    });
  });
}

extension on SchoolBinding {
  SchoolBinding copyWithCloud(String cloud) {
    return SchoolBinding(
      schoolId: schoolId,
      schoolName: schoolName,
      cloudBaseUrl: cloud,
      serverInstanceId: serverInstanceId,
      licenseId: licenseId,
      activationDate: activationDate,
      activationTokenId: activationTokenId,
      activationSessionId: activationSessionId,
      deviceId: deviceId,
      protocolVersion: protocolVersion,
    );
  }
}
