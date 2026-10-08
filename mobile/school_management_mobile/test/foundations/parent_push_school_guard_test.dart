import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/features/parent/notifications/parent_push_school_guard.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
    SharedPreferences.setMockInitialValues({});
  });
  group('ParentPushSchoolGuard', () {
    tearDown(ParentPushSchoolGuard.clearHubSchool);

    test('no active school rejects notifications', () async {
      expect(
        await ParentPushSchoolGuard.acceptsNotification({
          'schoolId': '11111111-1111-1111-1111-111111111111',
        }),
        isFalse,
      );
    });

    test('bind and clear hub school context', () {
      ParentPushSchoolGuard.bindHubSchool(
        '11111111-1111-1111-1111-111111111111',
      );
      ParentPushSchoolGuard.clearHubSchool();
    });
  });
}
