import 'dart:async';
import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/api/api_client.dart';
import 'package:school_management_mobile/core/api/dio_factory.dart';
import 'package:school_management_mobile/core/auth/auth_storage.dart';
import 'package:school_management_mobile/core/cache/cache_partition_policy.dart';
import 'package:school_management_mobile/core/connection/connection_context.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_repository.dart';

class _Bindings extends SchoolBindingRepository {
  @override
  Future<SchoolBinding?> load() async => SchoolBinding(
      schoolId: 'b',
      schoolName: 'B',
      cloudBaseUrl: 'https://cloud.example',
      serverInstanceId: 'instance-b',
      activationDate: DateTime.utc(2026),
      activationTokenId: 't',
      activationSessionId: 's',
      deviceId: 'd',
      protocolVersion: 2);
}

class _Adapter implements HttpClientAdapter {
  _Adapter(this.respond);
  final Future<ResponseBody> Function(RequestOptions) respond;
  int calls = 0;
  @override
  Future<ResponseBody> fetch(RequestOptions options,
      Stream<Uint8List>? requestStream, Future<void>? cancelFuture) {
    calls++;
    return respond(options);
  }

  @override
  void close({bool force = false}) {}
}

ResponseBody ok() =>
    ResponseBody.fromString('{"success":true,"data":{"id":"b"}}', 200,
        headers: {
          Headers.contentTypeHeader: [Headers.jsonContentType]
        });
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late SchoolBindingRepository previous;
  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
    previous = CachePartitionPolicy.bindingRepository;
    CachePartitionPolicy.bindingRepository = _Bindings();
  });
  tearDown(() {
    CachePartitionPolicy.bindingRepository = previous;
  });
  ApiClient client(_Adapter adapter, {String school = 'b', bool ready = true}) {
    final dio = createApiDio('https://cloud.example');
    dio.httpClientAdapter = adapter;
    return ApiClient(
        baseUrl: 'https://cloud.example',
        schoolId: school,
        connectionReady: ready,
        dio: dio);
  }

  test('a client created for another school never sends a request', () async {
    final adapter = _Adapter((_) async => ok());
    await expectLater(
        client(adapter, school: 'a').getObject('/students', (j) => j),
        throwsA(isA<DioException>()));
    expect(adapter.calls, 0);
  });
  test('detecting or offline state blocks outgoing business requests',
      () async {
    final adapter = _Adapter((_) async => ok());
    await expectLater(client(adapter, ready: false).post('/enrollments', {}),
        throwsA(isA<DioException>()));
    expect(adapter.calls, 0);
    await ConnectionActivity.instance
        .waitForIdle()
        .timeout(const Duration(seconds: 1));
  });
  test('late response after switching school is discarded', () async {
    final pending = Completer<ResponseBody>();
    final started = Completer<void>();
    final adapter = _Adapter((_) {
      started.complete();
      return pending.future;
    });
    final request = client(adapter).getObject('/students', (j) => j);
    final expected = expectLater(request, throwsA(isA<DioException>()));
    await started.future;
    ConnectionContext.invalidate();
    pending.complete(ok());
    await expected;
    expect(adapter.calls, 1);
  });
  test('timed-out write is not replayed on another server', () async {
    final adapter = _Adapter((options) async {
      throw DioException(
          requestOptions: options,
          type: DioExceptionType.receiveTimeout,
          message: 'Simulated lost response');
    });
    await expectLater(
        client(adapter).post('/enrollments', {}), throwsA(isA<DioException>()));
    expect(adapter.calls, 1);
    await ConnectionActivity.instance
        .waitForIdle()
        .timeout(const Duration(seconds: 1));
  });
  test('write lock remains held until the response arrives', () async {
    final pending = Completer<ResponseBody>();
    final started = Completer<void>();
    final adapter = _Adapter((_) {
      started.complete();
      return pending.future;
    });
    final request = client(adapter).post('/enrollments', {});
    await started.future;
    var idle = false;
    final waiting =
        ConnectionActivity.instance.waitForIdle().then((_) => idle = true);
    await Future<void>.delayed(Duration.zero);
    expect(idle, isFalse);
    pending.complete(ok());
    await request;
    await waiting;
    expect(idle, isTrue);
  });
  test('session for another school cannot overwrite the current session',
      () async {
    await expectLater(
        AuthStorage.saveSession(
            accessToken: 'a',
            refreshToken: 'r',
            userName: 'A',
            roles: const [],
            permissions: const [],
            schoolId: 'a'),
        throwsStateError);
    expect(await AuthStorage.accessToken, isNull);
  });
}
