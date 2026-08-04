import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/features/parent/notifications/parent_push_school_guard.dart';

void main() {
  group('ParentPushSchoolGuard', () {
    tearDown(ParentPushSchoolGuard.clearHubSchool);

    test('legacy (STRICT off) accepts notifications', () async {
      expect(
        await ParentPushSchoolGuard.acceptsNotification({
          'schoolId': '11111111-1111-1111-1111-111111111111',
        }),
        isTrue,
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
