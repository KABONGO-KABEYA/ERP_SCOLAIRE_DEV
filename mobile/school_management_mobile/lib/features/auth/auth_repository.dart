import 'package:dio/dio.dart';

import '../../core/api/dio_factory.dart';
import '../../core/auth/auth_storage.dart';
import '../../core/auth/session_school_coherence.dart';
import '../../core/cache/cache_partition_policy.dart';
import '../../core/config/api_config.dart';
import '../../core/models/api_response.dart';
import '../parent/notifications/parent_push_foreground_service.dart';
import 'models/auth_models.dart';

class AuthRepository {
  /// [baseUrl] = URL active (local ou cloud) après détection automatique.
  Future<AuthSession> login(
    String userName,
    String password, {
    String? baseUrl,
  }) async {
    final url = ApiConfig.normalize(
      baseUrl ?? ApiConfig.effectiveLocalBaseUrl,
    );
    final dio = createApiDio(url);

    final response = await dio.post<Map<String, dynamic>>(
      '/api/v1/auth/login',
      data: {'userName': userName, 'password': password},
      options: Options(validateStatus: (status) => status != null && status < 500),
    );

    final body = response.data;
    if (body == null) {
      throw DioException(requestOptions: response.requestOptions, message: 'Réponse vide');
    }

    final api = ApiResponse.fromJson(body, (data) => data);
    if (!api.success || api.data == null) {
      throw DioException(
        requestOptions: response.requestOptions,
        response: response,
        message: api.message ?? 'Nom d\'utilisateur ou mot de passe incorrect.',
      );
    }

    final session = AuthSession.fromJson(Map<String, dynamic>.from(api.data as Map));

    final activeSchoolId = await CachePartitionPolicy.activeSchoolId();
    if (!SessionSchoolCoherence.matchesLoginUser(
      activeSchoolId: activeSchoolId,
      userSchoolId: session.user.schoolId,
      accessToken: session.accessToken,
    )) {
      throw DioException(
        requestOptions: response.requestOptions,
        message:
            'Le compte connecté n\'appartient pas à l\'établissement actif. '
            'Changez d\'établissement ou utilisez le bon compte.',
      );
    }

    await AuthStorage.saveSession(
      accessToken: session.accessToken,
      refreshToken: session.refreshToken,
      userName: session.user.fullName,
      roles: session.user.roles,
      permissions: session.user.permissions,
      schoolId: session.user.schoolId,
    );
    return session;
  }

  Future<void> logout({String? baseUrl}) async {
    final refresh = await AuthStorage.refreshToken;
    if (refresh != null) {
      try {
        final url = ApiConfig.normalize(
          baseUrl ?? ApiConfig.effectiveLocalBaseUrl,
        );
        final dio = createApiDio(url);
        final token = await AuthStorage.accessToken;
        await dio.post(
          '/api/v1/auth/logout',
          data: {'refreshToken': refresh},
          options: Options(headers: {
            if (token != null) 'Authorization': 'Bearer $token',
          }),
        );
      } catch (_) {}
    }
    await ParentPushForegroundService.stop();
    await ParentPushForegroundService.clearCredentials();
    await AuthStorage.clear();
  }
}
