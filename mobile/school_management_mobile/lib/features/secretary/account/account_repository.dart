import '../../../core/api/api_client.dart';
import '../../auth/models/auth_models.dart';

class AccountRepository {
  AccountRepository(this._api);

  final ApiClient _api;

  Future<AuthUser> getProfile() => _api.getObject(
        '/api/v1/auth/me',
        AuthUser.fromJson,
      );

  Future<String?> getSchoolName() async {
    try {
      return await _api.getObject(
        '/api/v1/schools/current',
        (json) => json['name'] as String?,
      );
    } catch (_) {
      return null;
    }
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    await _api.post(
      '/api/v1/auth/change-password',
      {
        'currentPassword': currentPassword,
        'newPassword': newPassword,
      },
    );
  }
}
