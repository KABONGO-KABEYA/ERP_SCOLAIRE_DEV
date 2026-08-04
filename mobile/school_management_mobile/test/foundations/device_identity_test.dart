import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/device/device_identity.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
    DeviceIdentity.resetCachedForTests();
  });

  group('Foundations — DeviceId', () {
    test('stable across two startups when secure storage holds value', () async {
      const persisted = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
      FlutterSecureStorage.setMockInitialValues({
        'device_id_v1': persisted,
      });
      DeviceIdentity.resetCachedForTests();

      await DeviceIdentity.ensureInitialized();
      final first = await DeviceIdentity.deviceId;

      DeviceIdentity.resetCachedForTests();
      await DeviceIdentity.ensureInitialized();
      final second = await DeviceIdentity.deviceId;

      expect(first, persisted);
      expect(second, persisted);
    });

    test('stable after simulated app update (storage unchanged, cache cleared)', () async {
      await DeviceIdentity.ensureInitialized();
      final beforeUpdate = await DeviceIdentity.deviceId;

      DeviceIdentity.resetCachedForTests();
      await DeviceIdentity.ensureInitialized();
      final afterUpdate = await DeviceIdentity.deviceId;

      expect(afterUpdate, beforeUpdate);
    });

    test('creates uuid v4 when secure storage is empty', () async {
      await DeviceIdentity.ensureInitialized();
      final id = await DeviceIdentity.deviceId;

      expect(id, isNotEmpty);
      final uuidV4 = RegExp(
        r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
      );
      expect(uuidV4.hasMatch(id), isTrue);
    });

    test('second call in same session reuses cached id', () async {
      final a = await DeviceIdentity.deviceId;
      final b = await DeviceIdentity.deviceId;
      expect(b, a);
    });
  });
}
