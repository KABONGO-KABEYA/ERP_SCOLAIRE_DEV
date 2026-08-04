import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/features/parent/notifications/parent_push_preferences.dart';

void main() {
  group('ParentPushPreferences.scopeKeyForSchool', () {
    test('legacy key when schoolId empty', () {
      expect(
        ParentPushPreferences.scopeKeyForSchool('parent_push_seen_ids', null),
        'parent_push_seen_ids',
      );
    });

    test('scoped key uses school prefix', () {
      final key = ParentPushPreferences.scopeKeyForSchool(
        'parent_push_seen_ids',
        'A1B2C3D4-E5F6-7890-ABCD-EF1234567890',
      );
      expect(key.startsWith('school.'), isTrue);
      expect(key.endsWith('parent_push_seen_ids'), isTrue);
      expect(key.contains('a1b2c3d4e5f67890abcdef1234567890'), isTrue);
    });
  });
}
