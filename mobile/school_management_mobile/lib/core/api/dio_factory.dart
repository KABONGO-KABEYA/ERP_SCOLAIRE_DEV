import 'package:dio/dio.dart';

import 'configure_dio_stub.dart'
    if (dart.library.io) 'configure_dio_io.dart';

Dio createApiDio(String baseUrl) {
  final dio = Dio(BaseOptions(
    baseUrl: baseUrl,
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 30),
    headers: {'Accept': 'application/json'},
  ));
  configureDio(dio);
  return dio;
}
