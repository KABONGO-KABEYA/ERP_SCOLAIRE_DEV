import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/auth/auth_storage.dart';
import 'package:school_management_mobile/core/cache/cache_partition_policy.dart';
import 'package:school_management_mobile/core/school_binding/registered_schools_store.dart';
import 'package:school_management_mobile/core/school_binding/school_already_registered_exception.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_gate.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_repository.dart';
import 'package:school_management_mobile/features/parent/notifications/parent_push_preferences.dart';
import 'package:shared_preferences/shared_preferences.dart';

SchoolBinding _binding({
  required String schoolId,
  required String schoolName,
  String serverInstanceId = '11111111-1111-1111-1111-111111111111',
}) {
  return SchoolBinding(
    schoolId: schoolId,
    schoolName: schoolName,
    cloudBaseUrl: 'https://$schoolName.example.com',
    serverInstanceId: serverInstanceId,
    activationDate: DateTime.utc(2026, 8, 4),
    activationTokenId: 'token-$schoolId',
    activationSessionId: 'session-$schoolId',
    deviceId: 'device-global',
    protocolVersion: 2,
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const schoolA = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const schoolB = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

  setUp(() async {
    FlutterSecureStorage.setMockInitialValues({});
    SharedPreferences.setMockInitialValues({});
    SchoolBindingGate.bindingRepository = SchoolBindingRepository();
    CachePartitionPolicy.bindingRepository =
        SchoolBindingGate.bindingRepository;
  });

  tearDown(() {
    SchoolBindingGate.bindingRepository = SchoolBindingRepository();
    CachePartitionPolicy.bindingRepository =
        SchoolBindingGate.bindingRepository;
  });

  group('Multi-écoles — migration mono → registre', () {
    test('legacy school_binding migre vers registry + activeSchoolId', () async {
      final legacy = _binding(schoolId: schoolA, schoolName: 'EcoleA');
      FlutterSecureStorage.setMockInitialValues({
        RegisteredSchoolsStore.legacyBindingKey: jsonEncode(legacy.toJson()),
      });

      final repo = SchoolBindingRepository();
      SchoolBindingGate.bindingRepository = repo;
      CachePartitionPolicy.bindingRepository = repo;

      final loaded = await repo.load();
      expect(loaded?.schoolId, schoolA);
      expect(await repo.activeSchoolId(), schoolA.toLowerCase());
      expect(await repo.loadAll(), hasLength(1));

      final storage = const FlutterSecureStorage();
      expect(await storage.read(key: RegisteredSchoolsStore.legacyBindingKey), isNull);
      expect(
        await storage.read(key: RegisteredSchoolsStore.registryKey),
        isNotNull,
      );
      expect(
        await storage.read(key: RegisteredSchoolsStore.activeSchoolIdKey),
        schoolA.toLowerCase(),
      );
    });
  });

  group('Multi-écoles — ajout / switch / doublon', () {
    test('ajout A puis B ; A→B ; B→A sans perte', () async {
      final repo = SchoolBindingRepository();
      SchoolBindingGate.bindingRepository = repo;
      CachePartitionPolicy.bindingRepository = repo;

      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));
      expect(await repo.activeSchoolId(), schoolA.toLowerCase());

      await repo.addSchool(
        _binding(schoolId: schoolB, schoolName: 'B'),
        setAsActive: false,
      );
      expect(await repo.loadAll(), hasLength(2));
      expect(await repo.activeSchoolId(), schoolA.toLowerCase());
      expect((await repo.load())?.schoolName, 'A');

      await repo.setActive(schoolB);
      expect(await repo.activeSchoolId(), schoolB.toLowerCase());
      expect((await repo.load())?.schoolName, 'B');
      expect(await repo.loadAll(), hasLength(2));

      await repo.setActive(schoolA);
      expect(await repo.activeSchoolId(), schoolA.toLowerCase());
      expect(await repo.loadAll(), hasLength(2));
    });

    test('refus doublon SchoolAlreadyRegisteredException', () async {
      final repo = SchoolBindingRepository();
      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));
      expect(
        () => repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A-bis')),
        throwsA(isA<SchoolAlreadyRegisteredException>()),
      );
      expect(await repo.loadAll(), hasLength(1));
    });
  });

  group('Multi-écoles — suppression', () {
    test('suppression école non active conserve l\'actif', () async {
      final repo = SchoolBindingRepository();
      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));
      await repo.addSchool(
        _binding(schoolId: schoolB, schoolName: 'B'),
        setAsActive: false,
      );

      final outcome = await repo.removeSchool(schoolB);
      expect(outcome, RemoveSchoolOutcome.removedInactive);
      expect(await repo.activeSchoolId(), schoolA.toLowerCase());
      expect(await repo.loadAll(), hasLength(1));
    });

    test('suppression école active bascule vers l\'autre', () async {
      final repo = SchoolBindingRepository();
      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));
      await repo.addSchool(_binding(schoolId: schoolB, schoolName: 'B'));

      expect(await repo.activeSchoolId(), schoolB.toLowerCase());
      final outcome = await repo.removeSchool(schoolB);
      expect(outcome, RemoveSchoolOutcome.switchedToOther);
      expect(await repo.activeSchoolId(), schoolA.toLowerCase());
      expect(await repo.loadAll(), hasLength(1));
    });

    test('suppression de la dernière école → registryEmpty', () async {
      final repo = SchoolBindingRepository();
      SchoolBindingGate.bindingRepository = repo;
      CachePartitionPolicy.bindingRepository = repo;

      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));
      final outcome = await repo.removeSchool(schoolA);
      expect(outcome, RemoveSchoolOutcome.registryEmpty);
      expect(await repo.load(), isNull);
      expect(await repo.hasAnyRegisteredSchool(), isFalse);
      expect(await SchoolBindingGate.shouldRequireActivationQr(), isTrue);
    });
  });

  group('Multi-écoles — isolation + persistance', () {
    test('isolation clés cache / auth / push par schoolId', () async {
      final repo = SchoolBindingRepository();
      SchoolBindingGate.bindingRepository = repo;
      CachePartitionPolicy.bindingRepository = repo;

      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));
      await AuthStorage.saveSession(
        accessToken: 'token-A',
        refreshToken: 'refresh-A',
        userName: 'parentA',
        roles: const ['PARENT'],
        permissions: const [],
      );
      final prefs = await SharedPreferences.getInstance();
      await prefs.setStringList(
        ParentPushPreferences.scopeKeyForSchool(
          ParentPushPreferences.seenIdsBase,
          schoolA,
        ),
        ['n1'],
      );

      await repo.addSchool(_binding(schoolId: schoolB, schoolName: 'B'));
      await AuthStorage.saveSession(
        accessToken: 'token-B',
        refreshToken: 'refresh-B',
        userName: 'parentB',
        roles: const ['PARENT'],
        permissions: const [],
      );
      await prefs.setStringList(
        ParentPushPreferences.scopeKeyForSchool(
          ParentPushPreferences.seenIdsBase,
          schoolB,
        ),
        ['n2'],
      );

      expect(await AuthStorage.accessToken, 'token-B');

      final keyA = CachePartitionPolicy.prefsPrefixForSchool(schoolA);
      final keyB = CachePartitionPolicy.prefsPrefixForSchool(schoolB);
      expect(keyA != keyB, isTrue);
      expect(
        CachePartitionPolicy.hiveBoxName('parent_offline_v1', schoolA),
        isNot(
          CachePartitionPolicy.hiveBoxName('parent_offline_v1', schoolB),
        ),
      );

      await repo.setActive(schoolA);
      expect(await AuthStorage.accessToken, 'token-A');

      expect(
        prefs.getStringList(
          ParentPushPreferences.scopeKeyForSchool(
            ParentPushPreferences.seenIdsBase,
            schoolA,
          ),
        ),
        ['n1'],
      );
      expect(
        prefs.getStringList(
          ParentPushPreferences.scopeKeyForSchool(
            ParentPushPreferences.seenIdsBase,
            schoolB,
          ),
        ),
        ['n2'],
      );

      await repo.removeSchool(schoolB);
      expect(
        prefs.getStringList(
          ParentPushPreferences.scopeKeyForSchool(
            ParentPushPreferences.seenIdsBase,
            schoolB,
          ),
        ),
        isNull,
      );
      expect(
        prefs.getStringList(
          ParentPushPreferences.scopeKeyForSchool(
            ParentPushPreferences.seenIdsBase,
            schoolA,
          ),
        ),
        ['n1'],
      );
    });

    test('persistance registre après « redémarrage » (nouveau repo)', () async {
      final repo1 = SchoolBindingRepository();
      await repo1.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));
      await repo1.addSchool(
        _binding(schoolId: schoolB, schoolName: 'B'),
        setAsActive: false,
      );
      await repo1.setActive(schoolB);

      final repo2 = SchoolBindingRepository();
      expect(await repo2.loadAll(), hasLength(2));
      expect(await repo2.activeSchoolId(), schoolB.toLowerCase());
      expect((await repo2.load())?.schoolName, 'B');
    });

    test('switch ne purge pas le registre', () async {
      final repo = SchoolBindingRepository();
      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));
      await repo.addSchool(_binding(schoolId: schoolB, schoolName: 'B'));
      await repo.setActive(schoolA);
      await repo.setActive(schoolB);
      expect(await repo.loadAll(), hasLength(2));
    });
  });
}
