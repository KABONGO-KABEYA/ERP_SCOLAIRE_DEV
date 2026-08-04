import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/school_binding/activation_session.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';

void main() {
  group('Étape 2 — ActivationSession', () {
    test('fromJson/toJson roundtrip', () {
      final session = ActivationSession(
        activationSessionId: '11111111-1111-1111-1111-111111111111',
        activationTokenId: '22222222-2222-2222-2222-222222222222',
        deviceId: '33333333-3333-3333-3333-333333333333',
        schoolId: '44444444-4444-4444-4444-444444444444',
        status: ActivationSessionStatus.pending,
        createdAt: DateTime.utc(2026, 8, 4, 10, 0),
        expiresAt: DateTime.utc(2026, 8, 4, 10, 15),
        clientHints: {'platform': 'android', 'appVersion': '1.0.1'},
      );

      final restored = ActivationSession.fromJson(session.toJson());
      expect(restored.activationSessionId, session.activationSessionId);
      expect(restored.status, ActivationSessionStatus.pending);
      expect(restored.clientHints?['platform'], 'android');
    });
  });

  group('Étape 2 — SchoolBinding', () {
    test('fromJson/toJson roundtrip with nullable licenseId', () {
      final binding = SchoolBinding(
        schoolId: '44444444-4444-4444-4444-444444444444',
        schoolName: 'École Test',
        cloudBaseUrl: 'https://cloud.example.com',
        serverInstanceId: '55555555-5555-5555-5555-555555555555',
        licenseId: null,
        activationDate: DateTime.utc(2026, 8, 4, 11, 0),
        activationTokenId: '66666666-6666-6666-6666-666666666666',
        activationSessionId: '11111111-1111-1111-1111-111111111111',
        deviceId: '33333333-3333-3333-3333-333333333333',
        protocolVersion: 2,
        suggestedUserName: 'parent@ecole.cd',
        extensions: {'campusId': 'main'},
      );

      final restored = SchoolBinding.fromJson(binding.toJson());
      expect(restored.schoolName, 'École Test');
      expect(restored.licenseId, isNull);
      expect(restored.protocolVersion, 2);
      expect(restored.extensions?['campusId'], 'main');
    });
  });
}
