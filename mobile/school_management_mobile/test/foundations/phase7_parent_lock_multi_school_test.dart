import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/cache/cache_partition_policy.dart';
import 'package:school_management_mobile/core/school_binding/activation_session.dart';
import 'package:school_management_mobile/core/school_binding/bootstrap_api_client.dart';
import 'package:school_management_mobile/core/school_binding/establishment_session.dart';
import 'package:school_management_mobile/core/school_binding/school_activation_service.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_gate.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_repository.dart';
import 'package:school_management_mobile/core/school_binding/school_establishment_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

String _b64UrlJson(Map<String, dynamic> map) {
  final raw = utf8.encode(jsonEncode(map));
  return base64Url.encode(raw).replaceAll('=', '');
}

String buildTestJwt({
  required String tokenType,
  required String schoolId,
}) {
  final header = _b64UrlJson({'alg': 'none', 'typ': 'JWT'});
  final payload = _b64UrlJson({
    'token_type': tokenType,
    'typ': tokenType,
    'school_id': schoolId,
    'jti': 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'ver': 1,
  });
  return '$header.$payload.sig';
}

SchoolBinding _binding({
  required String schoolId,
  required String schoolName,
  required String cloudBaseUrl,
  required String activationTokenId,
  required String activationSessionId,
  String deviceId = 'device-phase7',
  String serverInstanceId = '11111111-1111-1111-1111-111111111111',
}) {
  return SchoolBinding(
    schoolId: schoolId,
    schoolName: schoolName,
    cloudBaseUrl: cloudBaseUrl,
    serverInstanceId: serverInstanceId,
    activationDate: DateTime.utc(2026, 8, 10, 14),
    activationTokenId: activationTokenId,
    activationSessionId: activationSessionId,
    deviceId: deviceId,
    protocolVersion: 2,
    extensions: {'bindingKind': 'school_establishment'},
  );
}

class _FakeEstablishBootstrap extends BootstrapApiClient {
  _FakeEstablishBootstrap()
      : super(dio: Dio(BaseOptions(baseUrl: 'http://bootstrap.test')));

  final List<String> paths = [];
  int completeCount = 0;

  @override
  Future<EstablishmentSession> startEstablishment(
    EstablishmentStartRequest request,
  ) async {
    paths.add('/establishment/start');
    return EstablishmentSession(
      establishmentSessionId: 'sess-${request.deviceId}-$completeCount',
      schoolId: 'pending',
      deviceId: request.deviceId,
      status: 'pending',
      expiresAt: DateTime.utc(2099, 1, 1),
    );
  }

  @override
  Future<SchoolBinding> completeEstablishment(
    EstablishmentCompleteRequest request,
  ) async {
    paths.add('/establishment/complete');
    completeCount++;
    if (completeCount == 1) {
      return _binding(
        schoolId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        schoolName: 'École A',
        cloudBaseUrl: 'https://cloud-a.example',
        activationTokenId: 'cred-A',
        activationSessionId: 'sess-A',
        deviceId: request.deviceId,
        serverInstanceId: 'aaaaaaaa-1111-1111-1111-111111111111',
      );
    }
    return _binding(
      schoolId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      schoolName: 'École B',
      cloudBaseUrl: 'https://cloud-b.example',
      activationTokenId: 'cred-B',
      activationSessionId: 'sess-B',
      deviceId: request.deviceId,
      serverInstanceId: 'bbbbbbbb-2222-2222-2222-222222222222',
    );
  }

  @override
  Future<ActivationSession> start(BootstrapStartRequest request) async {
    paths.add('/activation/start');
    throw StateError('ParentActivation ne doit pas être appelé dans ce scénario');
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const schoolA = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const schoolB = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

  setUp(() {
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

  group('Phase 7 — ParentActivation verrouillé (token_type)', () {
    test('SchoolActivationService refuse school_establishment localement',
        () async {
      final fake = _FakeEstablishBootstrap();
      final service = SchoolActivationService(
        bootstrap: fake,
        bindingRepository: SchoolBindingRepository(),
        deviceIdProvider: () async => 'd1',
        clientHintsProvider: () async => {},
      );

      expect(
        () => service.activateWithToken(
          buildTestJwt(tokenType: 'school_establishment', schoolId: schoolA),
        ),
        throwsA(
          isA<ParentActivationException>().having(
            (e) => e.message,
            'message',
            contains('QR établissement non accepté'),
          ),
        ),
      );
      expect(fake.paths, isEmpty);
    });
  });

  group('Phase 7 — Ajouter école A + B puis bascule A/B/A', () {
    test(
        'establish A puis B ; bindings intacts ; ActiveSchoolId A→B→A sans purge',
        () async {
      final fake = _FakeEstablishBootstrap();
      final repo = SchoolBindingRepository();
      SchoolBindingGate.bindingRepository = repo;
      CachePartitionPolicy.bindingRepository = repo;

      final establish = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: repo,
        deviceIdProvider: () async => 'device-phase7',
        clientHintsProvider: () async => {'platform': 'test'},
      );

      // Ajouter École A
      final bindingA = await establish.establishWithToken(
        buildTestJwt(tokenType: 'school_establishment', schoolId: schoolA),
      );
      expect(bindingA.schoolId, schoolA);
      expect(bindingA.schoolName, 'École A');
      expect(bindingA.cloudBaseUrl, 'https://cloud-a.example');
      expect(bindingA.activationTokenId, 'cred-A');
      expect(await repo.activeSchoolId(), schoolA);
      expect(await repo.loadAll(), hasLength(1));

      // Ajouter École B (sans remplacer A)
      final bindingB = await establish.establishWithToken(
        buildTestJwt(tokenType: 'school_establishment', schoolId: schoolB),
      );
      expect(bindingB.schoolId, schoolB);
      expect(bindingB.cloudBaseUrl, 'https://cloud-b.example');
      expect(bindingB.activationTokenId, 'cred-B');

      var all = await repo.loadAll();
      expect(all, hasLength(2));
      expect(all.map((e) => e.schoolId), containsAll([schoolA, schoolB]));

      // Snapshot bindings avant bascules
      SchoolBinding snap(String id) =>
          all.firstWhere((e) => e.schoolId.toLowerCase() == id.toLowerCase());

      final aBefore = snap(schoolA);
      final bBefore = snap(schoolB);
      expect(aBefore.cloudBaseUrl, 'https://cloud-a.example');
      expect(aBefore.activationTokenId, 'cred-A');
      expect(aBefore.activationSessionId, 'sess-A');
      expect(aBefore.serverInstanceId, 'aaaaaaaa-1111-1111-1111-111111111111');
      expect(bBefore.cloudBaseUrl, 'https://cloud-b.example');
      expect(bBefore.activationTokenId, 'cred-B');

      // Bascule B (A toujours présente, champs inchangés)
      await repo.setActive(schoolB);
      expect(await repo.activeSchoolId(), schoolB);
      expect((await repo.load())?.schoolName, 'École B');
      all = await repo.loadAll();
      expect(all, hasLength(2));
      expect(snap(schoolA).toJson(), aBefore.toJson());
      expect(snap(schoolB).toJson(), bBefore.toJson());

      // Bascule A (B toujours présente)
      await repo.setActive(schoolA);
      expect(await repo.activeSchoolId(), schoolA);
      expect((await repo.load())?.schoolName, 'École A');
      all = await repo.loadAll();
      expect(all, hasLength(2));
      expect(snap(schoolA).toJson(), aBefore.toJson());
      expect(snap(schoolB).toJson(), bBefore.toJson());

      // Retour B puis A — aucune perte
      await repo.setActive(schoolB);
      await repo.setActive(schoolA);
      all = await repo.loadAll();
      expect(all, hasLength(2));
      expect(snap(schoolA).cloudBaseUrl, 'https://cloud-a.example');
      expect(snap(schoolB).cloudBaseUrl, 'https://cloud-b.example');
      expect(snap(schoolA).activationTokenId, 'cred-A');
      expect(snap(schoolB).activationTokenId, 'cred-B');
      expect(await SchoolBindingGate.shouldRequireEstablishmentQr(), isFalse);

      // ParentActivation n'a pas été utilisé
      expect(fake.paths.every((p) => p.startsWith('/establishment')), isTrue);
    });

    test('ajout B sans setAsActive conserve A actif et les deux bindings',
        () async {
      final fake = _FakeEstablishBootstrap();
      final repo = SchoolBindingRepository();
      final establish = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: repo,
        deviceIdProvider: () async => 'device-phase7',
        clientHintsProvider: () async => {},
      );

      await establish.establishWithToken(
        buildTestJwt(tokenType: 'school_establishment', schoolId: schoolA),
      );
      await establish.establishWithToken(
        buildTestJwt(tokenType: 'school_establishment', schoolId: schoolB),
        setAsActive: false,
      );

      expect(await repo.activeSchoolId(), schoolA);
      expect(await repo.loadAll(), hasLength(2));
      expect((await repo.load())?.schoolName, 'École A');
    });
  });
}
