import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/auth/auth_storage.dart';
import 'package:school_management_mobile/core/cache/cache_partition_policy.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_gate.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_repository.dart';
import 'package:shared_preferences/shared_preferences.dart';

SchoolBinding _binding(String schoolId, String name) => SchoolBinding(
      schoolId: schoolId,
      schoolName: name,
      cloudBaseUrl: 'https://$name.example.com',
      serverInstanceId: '11111111-1111-1111-1111-111111111111',
      activationDate: DateTime.utc(2026, 8, 4),
      activationTokenId: 'token-$schoolId',
      activationSessionId: 'session-$schoolId',
      deviceId: 'device-global',
      protocolVersion: 2,
    );

String _fakeJwt(String schoolId) {
  String b64(Map<String, Object?> map) {
    final raw = base64Url.encode(utf8.encode(jsonEncode(map)));
    return raw.replaceAll('=', '');
  }

  return '${b64({'alg': 'none'})}.${b64({'school_id': schoolId})}.sig';
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const schoolA = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const schoolB = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

  setUp(() async {
    FlutterSecureStorage.setMockInitialValues({});
    SharedPreferences.setMockInitialValues({});
    final repo = SchoolBindingRepository();
    SchoolBindingGate.bindingRepository = repo;
    CachePartitionPolicy.bindingRepository = repo;
  });

  tearDown(() {
    SchoolBindingGate.bindingRepository = SchoolBindingRepository();
    CachePartitionPolicy.bindingRepository =
        SchoolBindingGate.bindingRepository;
  });

  test('AuthStorage — sessionMatchesActiveSchool A→B→A', () async {
    final repo = SchoolBindingGate.bindingRepository;

    await repo.addSchool(_binding(schoolA, 'A'));
    await AuthStorage.saveSession(
      accessToken: _fakeJwt(schoolA),
      refreshToken: 'r-a',
      userName: 'Parent A',
      roles: const ['PARENT'],
      permissions: const [],
      schoolId: schoolA,
    );
    expect(await AuthStorage.sessionMatchesActiveSchool, isTrue);
    expect(await AuthStorage.homeRoute, '/parent/home');

    await repo.addSchool(_binding(schoolB, 'B'));
    await repo.setActive(schoolB);
    // Partition B sans session → mismatch / pas de token cohérent.
    expect(await AuthStorage.isLoggedIn, isFalse);
    expect(await AuthStorage.sessionMatchesActiveSchool, isFalse);

    await AuthStorage.saveSession(
      accessToken: _fakeJwt(schoolB),
      refreshToken: 'r-b',
      userName: 'Parent B',
      roles: const ['PARENT'],
      permissions: const [],
      schoolId: schoolB,
    );
    expect(await AuthStorage.sessionMatchesActiveSchool, isTrue);

    await repo.setActive(schoolA);
    expect(await AuthStorage.accessToken, _fakeJwt(schoolA));
    expect(await AuthStorage.sessionMatchesActiveSchool, isTrue);
    expect(
      await AuthStorage.sessionSchoolId,
      CachePartitionPolicy.normalizeSchoolId(schoolA),
    );
  });
}
