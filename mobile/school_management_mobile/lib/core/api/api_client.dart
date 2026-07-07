import 'package:dio/dio.dart';

import '../auth/auth_storage.dart';
import '../config/api_config.dart';
import '../models/api_response.dart';
import 'dio_factory.dart';

class ApiClient {
  ApiClient() {
    _dio = createApiDio(apiBaseUrl);

    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await AuthStorage.accessToken;
        if (token != null && token.isNotEmpty) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
    ));
  }

  late final Dio _dio;

  Future<List<T>> getList<T>(
    String path,
    T Function(Map<String, dynamic> json) fromJson,
  ) async {
    final response = await _dio.get<Map<String, dynamic>>(path);
    final body = response.data;
    if (body == null) {
      throw DioException(requestOptions: response.requestOptions, message: 'Réponse vide');
    }

    final api = ApiResponse.fromJson(body, (data) => data);
    if (!api.success || api.data == null) {
      throw DioException(
        requestOptions: response.requestOptions,
        message: api.message ?? 'Erreur API',
      );
    }

    final list = api.data as List<dynamic>;
    return list
        .map((e) => fromJson(Map<String, dynamic>.from(e as Map)))
        .toList();
  }

  Future<T> getObject<T>(
    String path,
    T Function(Map<String, dynamic> json) fromJson,
  ) async {
    final response = await _dio.get<Map<String, dynamic>>(path);
    final body = response.data;
    if (body == null) {
      throw DioException(requestOptions: response.requestOptions, message: 'Réponse vide');
    }

    final api = ApiResponse.fromJson(body, (data) => data);
    if (!api.success || api.data == null) {
      throw DioException(
        requestOptions: response.requestOptions,
        message: api.message ?? 'Erreur API',
      );
    }

    return fromJson(Map<String, dynamic>.from(api.data as Map));
  }

  Future<void> post(
    String path,
    Object? data,
  ) async {
    final response = await _dio.post<Map<String, dynamic>>(path, data: data);
    final body = response.data;
    if (body == null) {
      throw DioException(requestOptions: response.requestOptions, message: 'Réponse vide');
    }

    final api = ApiResponse.fromJson(body, (d) => d);
    if (!api.success) {
      throw DioException(
        requestOptions: response.requestOptions,
        message: api.message ?? 'Erreur API',
      );
    }
  }
}
