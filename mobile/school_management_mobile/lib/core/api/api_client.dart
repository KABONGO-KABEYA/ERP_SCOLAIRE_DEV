import 'dart:async';

import 'package:dio/dio.dart';

import '../auth/auth_storage.dart';
import '../models/api_response.dart';
import 'dio_factory.dart';

class ApiClient {
  ApiClient({
    required String baseUrl,
    FutureOr<void> Function()? onSessionExpired,
  }) : _onSessionExpired = onSessionExpired {
    _dio = createApiDio(baseUrl);

    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await AuthStorage.accessToken;
        if (token != null && token.isNotEmpty) {
          if (!await AuthStorage.sessionMatchesActiveSchool) {
            await AuthStorage.clear();
            await _onSessionExpired?.call();
            return handler.reject(
              DioException(
                requestOptions: options,
                type: DioExceptionType.cancel,
                message:
                    'Session hors contexte établissement (ActiveSchoolId ≠ JWT.SchoolId).',
              ),
            );
          }
          options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
      onError: (error, handler) async {
        final status = error.response?.statusCode;
        final path = error.requestOptions.path;
        final alreadyRetried = error.requestOptions.extra['authRetried'] == true;

        if (status != 401 || alreadyRetried || _isAuthEndpoint(path)) {
          return handler.next(error);
        }

        final refreshed = await _tryRefreshToken();
        if (!refreshed) {
          await AuthStorage.clear();
          await _onSessionExpired?.call();
          return handler.next(error);
        }

        try {
          final request = error.requestOptions;
          request.extra['authRetried'] = true;
          final token = await AuthStorage.accessToken;
          if (token != null && token.isNotEmpty) {
            request.headers['Authorization'] = 'Bearer $token';
          }
          final response = await _dio.fetch<dynamic>(request);
          return handler.resolve(response);
        } catch (e) {
          if (e is DioException) {
            return handler.next(e);
          }
          return handler.next(error);
        }
      },
    ));
  }

  final FutureOr<void> Function()? _onSessionExpired;
  late final Dio _dio;
  Future<bool>? _refreshInFlight;

  String get baseUrl => _dio.options.baseUrl;

  static bool _isAuthEndpoint(String path) {
    final p = path.toLowerCase();
    return p.contains('/auth/login') ||
        p.contains('/auth/refresh') ||
        p.contains('/auth/logout');
  }

  Future<bool> _tryRefreshToken() {
    return _refreshInFlight ??= _doRefresh().whenComplete(() {
      _refreshInFlight = null;
    });
  }

  Future<bool> _doRefresh() async {
    final refresh = await AuthStorage.refreshToken;
    if (refresh == null || refresh.isEmpty) return false;

    try {
      // Dio dédié sans interceptor auth pour éviter une boucle.
      final refreshDio = createApiDio(baseUrl);
      final response = await refreshDio.post<Map<String, dynamic>>(
        '/api/v1/auth/refresh',
        data: {'refreshToken': refresh},
      );
      final body = response.data;
      if (body == null) return false;

      final api = ApiResponse.fromJson(body, (data) => data);
      if (!api.success || api.data is! Map) return false;

      final data = Map<String, dynamic>.from(api.data as Map);
      final accessToken = data['accessToken'] as String?;
      final refreshToken = data['refreshToken'] as String?;
      final user = data['user'] as Map<String, dynamic>?;
      if (accessToken == null ||
          accessToken.isEmpty ||
          refreshToken == null ||
          refreshToken.isEmpty ||
          user == null) {
        return false;
      }

      final schoolId = user['schoolId']?.toString() ?? '';
      if (schoolId.isEmpty) return false;

      await AuthStorage.saveSession(
        accessToken: accessToken,
        refreshToken: refreshToken,
        userName: user['fullName'] as String? ??
            user['userName'] as String? ??
            'Utilisateur',
        roles: (user['roles'] as List<dynamic>?)
                ?.map((e) => e.toString())
                .toList() ??
            const [],
        permissions: (user['permissions'] as List<dynamic>?)
                ?.map((e) => e.toString())
                .toList() ??
            const [],
        schoolId: schoolId,
      );
      if (!await AuthStorage.sessionMatchesActiveSchool) {
        await AuthStorage.clear();
        return false;
      }
      return true;
    } catch (_) {
      return false;
    }
  }

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

  Future<T> postObject<T>(
    String path,
    Object? data,
    T Function(Map<String, dynamic> json) fromJson,
  ) async {
    final response = await _dio.post<Map<String, dynamic>>(path, data: data);
    final body = response.data;
    if (body == null) {
      throw DioException(requestOptions: response.requestOptions, message: 'Réponse vide');
    }

    final api = ApiResponse.fromJson(body, (d) => d);
    if (!api.success || api.data == null) {
      throw DioException(
        requestOptions: response.requestOptions,
        message: api.message ?? 'Erreur API',
      );
    }

    return fromJson(Map<String, dynamic>.from(api.data as Map));
  }

  Future<T> uploadMultipart<T>(
    String path,
    FormData formData,
    T Function(Map<String, dynamic> json) fromJson,
  ) async {
    final response = await _dio.post<Map<String, dynamic>>(
      path,
      data: formData,
      options: Options(contentType: 'multipart/form-data'),
    );
    final body = response.data;
    if (body == null) {
      throw DioException(requestOptions: response.requestOptions, message: 'Réponse vide');
    }

    final api = ApiResponse.fromJson(body, (d) => d);
    if (!api.success || api.data == null) {
      throw DioException(
        requestOptions: response.requestOptions,
        message: api.message ?? 'Erreur upload',
      );
    }

    return fromJson(Map<String, dynamic>.from(api.data as Map));
  }

  Future<void> delete(String path) async {
    final response = await _dio.delete<Map<String, dynamic>>(path);
    final body = response.data;
    if (body == null) {
      throw DioException(requestOptions: response.requestOptions, message: 'Réponse vide');
    }

    final api = ApiResponse.fromJson(body, (d) => d);
    if (!api.success) {
      throw DioException(
        requestOptions: response.requestOptions,
        message: api.message ?? 'Erreur suppression',
      );
    }
  }

  Future<List<int>> getBytes(String path) async {
    final response = await _dio.get<List<int>>(
      path,
      options: Options(responseType: ResponseType.bytes),
    );
    final data = response.data;
    if (data == null || data.isEmpty) {
      throw DioException(
        requestOptions: response.requestOptions,
        message: 'Fichier vide',
      );
    }
    return data;
  }
}
