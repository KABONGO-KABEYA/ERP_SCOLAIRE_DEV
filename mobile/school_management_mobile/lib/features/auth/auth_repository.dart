import 'package:dio/dio.dart';

import '../../core/api/dio_factory.dart';
import '../../core/auth/auth_storage.dart';
import '../../core/config/api_config.dart';
import '../../core/models/api_response.dart';
import 'models/auth_models.dart';

class AuthRepository {
  AuthRepository() {
    _dio = createApiDio(apiBaseUrl);
  }

  late final Dio _dio;

  Future<AuthSession> login(String userName, String password) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/api/v1/auth/login',
      data: {'userName': userName, 'password': password},
    );

    final body = response.data;
    if (body == null) {
      throw DioException(requestOptions: response.requestOptions, message: 'Réponse vide');
    }

    final api = ApiResponse.fromJson(body, (data) => data);
    if (!api.success || api.data == null) {
      throw DioException(
        requestOptions: response.requestOptions,
        message: api.message ?? 'Identifiants invalides',
      );
    }

    final session = AuthSession.fromJson(Map<String, dynamic>.from(api.data as Map));
    await AuthStorage.saveSession(
      accessToken: session.accessToken,
      refreshToken: session.refreshToken,
      userName: session.user.fullName,
      roles: session.user.roles,
    );
    return session;
  }

  Future<void> logout() async {
    final refresh = await AuthStorage.refreshToken;
    if (refresh != null) {
      try {
        final token = await AuthStorage.accessToken;
        await _dio.post(
          '/api/v1/auth/logout',
          data: {'refreshToken': refresh},
          options: Options(headers: {
            if (token != null) 'Authorization': 'Bearer $token',
          }),
        );
      } catch (_) {}
    }
    await AuthStorage.clear();
  }
}
