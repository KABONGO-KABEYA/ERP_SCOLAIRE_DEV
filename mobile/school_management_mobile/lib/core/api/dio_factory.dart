import '../connection/connection_context.dart';
import 'package:dio/dio.dart';

import 'configure_dio_stub.dart' if (dart.library.io) 'configure_dio_io.dart';
import '../config/api_config.dart';

Dio createApiDio(String baseUrl) {
  final normalized = ApiConfig.normalize(baseUrl);
  if (!ApiConfig.isValidBaseUrl(normalized)) {
    throw ArgumentError.value(
      baseUrl,
      'baseUrl',
      'URL API invalide (attendu http://host:port). '
          'Sous PowerShell, guillemettez le dart-define.',
    );
  }

  final dio = Dio(BaseOptions(
    baseUrl: normalized,
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 30),
    headers: {'Accept': 'application/json'},
  ));
  void release(RequestOptions request) {
    final done = request.extra.remove('connectionWriteRelease');
    if (done is void Function()) done();
  }

  dio.interceptors.add(InterceptorsWrapper(
    onRequest: (options, handler) {
      if (!const {'GET', 'HEAD', 'OPTIONS'}
          .contains(options.method.toUpperCase())) {
        options.extra['connectionWriteRelease'] =
            ConnectionActivity.instance.beginWrite();
      }
      handler.next(options);
    },
    onResponse: (response, handler) {
      release(response.requestOptions);
      handler.next(response);
    },
    onError: (error, handler) {
      release(error.requestOptions);
      handler.next(error);
    },
  ));
  configureDio(dio);
  return dio;
}
