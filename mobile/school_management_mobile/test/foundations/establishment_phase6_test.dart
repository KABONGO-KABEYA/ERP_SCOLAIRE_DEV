import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/config/bootstrap_config.dart';
import 'package:school_management_mobile/core/school_binding/bootstrap_api_client.dart';
import 'package:school_management_mobile/core/school_binding/establishment_error_mapper.dart';
import 'package:school_management_mobile/core/school_binding/establishment_session.dart';
import 'package:school_management_mobile/core/school_binding/establishment_session_store.dart';
import 'package:school_management_mobile/core/school_binding/establishment_token_parser.dart';
import 'package:school_management_mobile/core/school_binding/school_already_registered_exception.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_gate.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_repository.dart';
import 'package:school_management_mobile/core/school_binding/school_establishment_service.dart';
import 'package:shared_preferences/shared_preferences.dart';

String _b64UrlJson(Map<String, dynamic> map) {
  final raw = utf8.encode(jsonEncode(map));
  return base64Url.encode(raw).replaceAll('=', '');
}

/// JWT non signé — lecture locale `token_type` uniquement (pas un secret métier).
String buildTestJwt({
  required String tokenType,
  String schoolId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
}) {
  final header = _b64UrlJson({'alg': 'none', 'typ': 'JWT'});
  final payload = _b64UrlJson({
    'token_type': tokenType,
    'school_id': schoolId,
    'jti': 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'ver': 1,
  });
  return '$header.$payload.sig';
}

SchoolBinding _binding({
  required String schoolId,
  required String schoolName,
  String deviceId = 'device-1',
}) {
  return SchoolBinding(
    schoolId: schoolId,
    schoolName: schoolName,
    cloudBaseUrl: 'https://cloud.example/$schoolId',
    serverInstanceId: '11111111-1111-1111-1111-111111111111',
    activationDate: DateTime.utc(2026, 8, 10),
    activationTokenId: 'cred-$schoolId',
    activationSessionId: 'sess-$schoolId',
    deviceId: deviceId,
    protocolVersion: 2,
    extensions: {'bindingKind': 'school_establishment'},
  );
}

class FakeBootstrapApiClient extends BootstrapApiClient {
  FakeBootstrapApiClient()
      : super(dio: Dio(BaseOptions(baseUrl: 'http://bootstrap.test')));

  final List<String> calledPaths = [];
  String? lastStartDeviceId;
  String? lastCompleteDeviceId;
  String? lastToken;
  DioException? startError;
  DioException? completeError;
  SchoolBinding Function(EstablishmentStartRequest start)? bindingFactory;

  int startCount = 0;
  int completeCount = 0;

  @override
  Future<EstablishmentSession> startEstablishment(
    EstablishmentStartRequest request,
  ) async {
    calledPaths.add('/establishment/start');
    startCount++;
    lastStartDeviceId = request.deviceId;
    lastToken = request.token;
    if (startError != null) throw startError!;
    return EstablishmentSession(
      establishmentSessionId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
      schoolId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      deviceId: request.deviceId,
      status: 'pending',
      expiresAt: DateTime.utc(2026, 8, 10, 12, 15),
    );
  }

  @override
  Future<SchoolBinding> completeEstablishment(
    EstablishmentCompleteRequest request,
  ) async {
    calledPaths.add('/establishment/complete');
    completeCount++;
    lastCompleteDeviceId = request.deviceId;
    if (completeError != null) throw completeError!;
    if (lastStartDeviceId != null &&
        lastStartDeviceId!.toLowerCase() != request.deviceId.toLowerCase()) {
      throw DioException(
        requestOptions: RequestOptions(path: '/establishment/complete'),
        response: Response(
          requestOptions: RequestOptions(path: '/establishment/complete'),
          statusCode: 400,
          data: {'error': 'DeviceId incompatible.'},
        ),
        type: DioExceptionType.badResponse,
      );
    }
    final start = EstablishmentStartRequest(
      token: lastToken ?? '',
      deviceId: lastStartDeviceId ?? request.deviceId,
    );
    if (bindingFactory != null) return bindingFactory!(start);
    return _binding(
      schoolId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      schoolName: 'École A',
      deviceId: request.deviceId,
    );
  }
}

DioException _dioError({
  required int status,
  required String message,
}) {
  return DioException(
    requestOptions: RequestOptions(path: '/establishment/start'),
    response: Response(
      requestOptions: RequestOptions(path: '/establishment/start'),
      statusCode: status,
      data: {'error': message},
    ),
    type: DioExceptionType.badResponse,
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const schoolA = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  const schoolB = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
    SharedPreferences.setMockInitialValues({});
    SchoolBindingGate.bindingRepository = SchoolBindingRepository();
  });

  tearDown(() {
    SchoolBindingGate.bindingRepository = SchoolBindingRepository();
  });

  group('Phase 6 — Bootstrap URL', () {
    test('default / resolved URL is gopvetrs labo (never bootstrap.169)', () {
      expect(BootstrapConfig.baseUrl, BootstrapConfig.labBootstrapBaseUrl);
      expect(BootstrapConfig.baseUrl.contains('gopvetrs'), isTrue);
      expect(BootstrapConfig.baseUrl.contains('bootstrap.169'), isFalse);
    });
  });

  group('Phase 6 — Gate (A, F)', () {
    test('A — install neuve : shouldRequireEstablishmentQr == true', () async {
      expect(await SchoolBindingGate.shouldRequireEstablishmentQr(), isTrue);
      expect(await SchoolBindingGate.shouldRequireActivationQr(), isTrue);
      expect(
        await SchoolBindingGate.shouldBlockSessionWithoutEstablishment(),
        isTrue,
      );
    });

    test('F — login bloqué tant que pas de SchoolBinding', () async {
      expect(
        await SchoolBindingGate.shouldBlockSessionWithoutEstablishment(),
        isTrue,
      );

      final repo = SchoolBindingRepository();
      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));
      SchoolBindingGate.bindingRepository = repo;

      expect(await SchoolBindingGate.shouldRequireEstablishmentQr(), isFalse);
      expect(
        await SchoolBindingGate.shouldBlockSessionWithoutEstablishment(),
        isFalse,
      );
      expect(await repo.activeSchoolId(), schoolA);
    });
  });

  group('Phase 6 — Establish flow (B, C, D, E)', () {
    test('B/C/D/E — start+complete → binding + registry + ActiveSchoolId',
        () async {
      final fake = FakeBootstrapApiClient();
      final repo = SchoolBindingRepository();
      SchoolBindingGate.bindingRepository = repo;
      final service = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: repo,
        sessionStore: EstablishmentSessionStore(),
        deviceIdProvider: () async => 'device-ok',
        clientHintsProvider: () async => {'platform': 'test'},
      );

      final token = buildTestJwt(tokenType: 'school_establishment');
      final binding = await service.establishWithToken(token);

      expect(fake.calledPaths, [
        '/establishment/start',
        '/establishment/complete',
      ]);
      expect(fake.lastStartDeviceId, 'device-ok');
      expect(fake.lastCompleteDeviceId, 'device-ok');
      expect(binding.schoolId, schoolA);
      expect(binding.schoolName, 'École A');

      final all = await repo.loadAll();
      expect(all, hasLength(1));
      expect(all.single.schoolId, schoolA);
      expect(await repo.activeSchoolId(), schoolA);
      expect(await SchoolBindingGate.shouldRequireEstablishmentQr(), isFalse);
    });
  });

  group('Phase 6 — Erreurs (G, H, M, N, O)', () {
    test('G — QR invalide → message', () async {
      final fake = FakeBootstrapApiClient()
        ..startError = _dioError(
          status: 401,
          message: 'Token établissement invalide.',
        );
      final repo = SchoolBindingRepository();
      final service = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: repo,
        deviceIdProvider: () async => 'd1',
        clientHintsProvider: () async => {},
      );

      expect(
        () => service.establishWithToken(
          buildTestJwt(tokenType: 'school_establishment'),
        ),
        throwsA(
          isA<SchoolEstablishmentException>().having(
            (e) => e.message,
            'message',
            contains('invalide'),
          ),
        ),
      );
      expect(await repo.loadAll(), isEmpty);
    });

    test('H — QR révoqué → message approprié', () async {
      final fake = FakeBootstrapApiClient()
        ..startError = _dioError(
          status: 403,
          message:
              'QR établissement révoqué. Demandez un nouveau QR à l\'école.',
        );
      final service = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: SchoolBindingRepository(),
        deviceIdProvider: () async => 'd1',
        clientHintsProvider: () async => {},
      );

      expect(
        () => service.establishWithToken(
          buildTestJwt(tokenType: 'school_establishment'),
        ),
        throwsA(
          isA<SchoolEstablishmentException>().having(
            (e) => e.message,
            'message',
            contains('révoqué'),
          ),
        ),
      );
    });

    test('M — ParentActivationToken refusé par /establishment (local)',
        () async {
      final fake = FakeBootstrapApiClient();
      final repo = SchoolBindingRepository();
      final service = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: repo,
        deviceIdProvider: () async => 'd1',
        clientHintsProvider: () async => {},
      );

      final parentToken = buildTestJwt(tokenType: 'parent_activation');
      expect(
        () => service.establishWithToken(parentToken),
        throwsA(
          isA<SchoolEstablishmentException>().having(
            (e) => e.message,
            'message',
            contains('pas un QR établissement'),
          ),
        ),
      );
      expect(fake.startCount, 0);
      expect(await repo.loadAll(), isEmpty);
    });

    test('N — Bootstrap indisponible → aucune donnée locale corrompue',
        () async {
      final fake = FakeBootstrapApiClient()
        ..startError = DioException(
          requestOptions: RequestOptions(path: '/establishment/start'),
          type: DioExceptionType.connectionError,
          error: 'Connection refused',
        );
      final repo = SchoolBindingRepository();
      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));

      final service = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: repo,
        deviceIdProvider: () async => 'd1',
        clientHintsProvider: () async => {},
      );

      expect(
        () => service.establishWithToken(
          buildTestJwt(
            tokenType: 'school_establishment',
            schoolId: schoolB,
          ),
        ),
        throwsA(
          isA<SchoolEstablishmentException>().having(
            (e) => e.message,
            'message',
            contains('Bootstrap indisponible'),
          ),
        ),
      );

      final after = await repo.loadAll();
      expect(after, hasLength(1));
      expect(after.single.schoolId, schoolA);
      expect(await repo.activeSchoolId(), schoolA);
    });

    test('O — DeviceId différent start/complete → rejet propre', () async {
      final fake = FakeBootstrapApiClient();
      final repo = SchoolBindingRepository();
      final service = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: repo,
        clientHintsProvider: () async => {},
      );

      expect(
        () => service.establishWithDeviceIds(
          token: buildTestJwt(tokenType: 'school_establishment'),
          startDeviceId: 'device-start',
          completeDeviceId: 'device-other',
        ),
        throwsA(
          isA<SchoolEstablishmentException>().having(
            (e) => e.message,
            'message',
            contains('Appareil incompatible'),
          ),
        ),
      );
      expect(await repo.loadAll(), isEmpty);
      expect(fake.startCount, 1);
      expect(fake.completeCount, 1);
    });
  });

  group('Phase 6 — Multi-écoles (I, J, K, L)', () {
    test('I — QR école déjà enregistrée → aucun doublon', () async {
      final repo = SchoolBindingRepository();
      await repo.addSchool(_binding(schoolId: schoolA, schoolName: 'A'));

      final fake = FakeBootstrapApiClient();
      final service = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: repo,
        deviceIdProvider: () async => 'd1',
        clientHintsProvider: () async => {},
      );

      expect(
        () => service.establishWithToken(
          buildTestJwt(tokenType: 'school_establishment', schoolId: schoolA),
        ),
        throwsA(isA<SchoolAlreadyRegisteredException>()),
      );
      expect(await repo.loadAll(), hasLength(1));
    });

    test('J/K/L — deuxième école ajoutée ; switch ActiveSchoolId sans purge',
        () async {
      final fake = FakeBootstrapApiClient();
      fake.bindingFactory = (_) {
        if (fake.completeCount <= 1) {
          return _binding(schoolId: schoolA, schoolName: 'A');
        }
        return _binding(schoolId: schoolB, schoolName: 'B');
      };

      final repo = SchoolBindingRepository();
      SchoolBindingGate.bindingRepository = repo;
      final service = SchoolEstablishmentService(
        bootstrap: fake,
        bindingRepository: repo,
        deviceIdProvider: () async => 'd1',
        clientHintsProvider: () async => {},
      );

      await service.establishWithToken(
        buildTestJwt(tokenType: 'school_establishment', schoolId: schoolA),
      );
      expect(await repo.activeSchoolId(), schoolA);

      await service.establishWithToken(
        buildTestJwt(tokenType: 'school_establishment', schoolId: schoolB),
      );

      final all = await repo.loadAll();
      expect(all.map((e) => e.schoolId), containsAll([schoolA, schoolB]));
      expect(all, hasLength(2));

      await repo.setActive(schoolB);
      expect(await repo.activeSchoolId(), schoolB);
      expect(
        (await repo.loadAll()).map((e) => e.schoolId),
        containsAll([schoolA, schoolB]),
      );

      await repo.setActive(schoolA);
      expect(await repo.activeSchoolId(), schoolA);
      expect(
        (await repo.loadAll()).map((e) => e.schoolId),
        containsAll([schoolA, schoolB]),
      );
    });
  });

  group('Phase 6 — Token parser / errors / storage', () {
    test('extract establish deep link and reject activate deep link', () {
      final jwt = buildTestJwt(tokenType: 'school_establishment');
      expect(
        EstablishmentTokenParser.extractTokenFromScan(
          'erp-scolaire://establish?token=$jwt',
        ),
        jwt,
      );
      expect(
        EstablishmentTokenParser.extractTokenFromScan(
          'erp-scolaire://activate?token=$jwt',
        ),
        isNull,
      );
      expect(EstablishmentTokenParser.extractTokenFromScan(jwt), jwt);
    });

    test('error mapper covers revoked / version / unknown school / expired', () {
      expect(
        EstablishmentErrorMapper.mapServerMessage(
          'Version de credential invalide.',
        ),
        contains('Version'),
      );
      expect(
        EstablishmentErrorMapper.mapServerMessage(
          'École introuvable dans le registre Bootstrap.',
        ),
        contains('École inconnue'),
      );
      expect(
        EstablishmentErrorMapper.mapServerMessage(
          'Session d\'établissement expirée.',
        ),
        contains('expirée'),
      );
    });

    test('session JSON keys exclude token/secret', () async {
      final store = EstablishmentSessionStore();
      final session = EstablishmentSession(
        establishmentSessionId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
        schoolId: schoolA,
        deviceId: 'd1',
        status: 'pending',
        expiresAt: DateTime.utc(2099, 1, 1),
      );
      await store.persist(session);
      final json = session.toJson();
      expect(json.containsKey('token'), isFalse);
      expect(json.containsKey('secret'), isFalse);
      expect(json.containsKey('secretHash'), isFalse);
      expect(json['establishmentSessionId'], isNotEmpty);

      final loaded = await store.loadPersisted();
      expect(loaded?.establishmentSessionId, session.establishmentSessionId);
    });
  });
}
