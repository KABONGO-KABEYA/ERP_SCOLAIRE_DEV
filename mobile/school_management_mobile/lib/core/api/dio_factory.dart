import 'package:dio/dio.dart';

import 'configure_dio_stub.dart'
    if (dart.library.io) 'configure_dio_io.dart';
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
  configureDio(dio);
  return dio;
}
