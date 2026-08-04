import 'parent_push_foreground_service.dart';
import 'parent_push_preferences.dart';
import 'parent_push_realtime_client.dart';
import 'parent_push_school_guard.dart';

/// Cycle de vie push / SignalR (changement école, recovery instance, reauth).
abstract final class ParentPushLifecycle {
  static ParentPushRealtimeClient? _client;

  static void registerClient(ParentPushRealtimeClient client) {
    _client = client;
  }

  static void unregisterClient(ParentPushRealtimeClient client) {
    if (_client == client) {
      _client = null;
    }
  }

  static Future<void> onCredentialsSynced() async {
    await ParentPushPreferences.persistActiveSchoolContext();
  }

  static Future<void> onSchoolBindingChanged({
    required String? previousSchoolId,
    required String newSchoolId,
  }) async {
    await resetTransport();
    if (previousSchoolId != null &&
        previousSchoolId.isNotEmpty &&
        previousSchoolId != newSchoolId) {
      await ParentPushPreferences.purgeSchoolPushState(previousSchoolId);
    }
    await ParentPushPreferences.persistActiveSchoolContext();
  }

  static Future<void> onInstanceRecovery({required String schoolId}) async {
    await resetTransport();
    await ParentPushPreferences.purgeSchoolPushState(schoolId);
    await ParentPushPreferences.clearActiveSchoolContext();
  }

  static Future<void> onReauthenticationRequired() async {
    await resetTransport();
  }

  static Future<void> resetTransport() async {
    await _client?.stop();
    ParentPushSchoolGuard.clearHubSchool();
    await ParentPushForegroundService.stop();
    await ParentPushForegroundService.clearCredentials();
  }
}
