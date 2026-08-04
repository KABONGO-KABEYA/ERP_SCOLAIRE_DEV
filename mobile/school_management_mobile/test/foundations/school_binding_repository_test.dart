import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_repository.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
  });

  group('Étape 2 — SchoolBindingRepository', () {
    test('save load clear cycle', () async {
      final repo = SchoolBindingRepository();
      expect(await repo.hasBinding(), isFalse);

      final binding = SchoolBinding(
        schoolId: '44444444-4444-4444-4444-444444444444',
        schoolName: 'École',
        cloudBaseUrl: 'https://cloud.example.com',
        serverInstanceId: '55555555-5555-5555-5555-555555555555',
        activationDate: DateTime.utc(2026, 8, 4),
        activationTokenId: 't1',
        activationSessionId: 's1',
        deviceId: 'd1',
        protocolVersion: 2,
      );

      await repo.save(binding);
      expect(await repo.hasBinding(), isTrue);
      final loaded = await repo.load();
      expect(loaded?.schoolId, binding.schoolId);

      await repo.clear();
      expect(await repo.load(), isNull);
    });
  });
}
