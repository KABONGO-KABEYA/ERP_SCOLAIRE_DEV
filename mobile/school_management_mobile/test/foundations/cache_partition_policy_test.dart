import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/cache/cache_partition_policy.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
  });

  group('CachePartitionPolicy — étape 5', () {
    test('legacy scopeKey returns base key without school', () async {
      final key = await CachePartitionPolicy.scopeKey('access_token');
      expect(key, 'access_token');
    });

    test('hiveBoxName suffixes school id', () {
      expect(
        CachePartitionPolicy.hiveBoxName(
          'parent_offline_v1',
          '33333333-3333-3333-3333-333333333333',
        ),
        'parent_offline_v1_33333333333333333333333333333333',
      );
    });

    test('prefsPrefixForSchool is stable', () {
      expect(
        CachePartitionPolicy.prefsPrefixForSchool(
          '33333333-3333-3333-3333-333333333333',
        ),
        'school.33333333333333333333333333333333.',
      );
    });

    test('scopeKey partitions when schoolId explicite', () async {
      expect(
        await CachePartitionPolicy.scopeKey(
          'access_token',
          schoolId: '33333333-3333-3333-3333-333333333333',
        ),
        'school.33333333333333333333333333333333.access_token',
      );
    });
  });
}
