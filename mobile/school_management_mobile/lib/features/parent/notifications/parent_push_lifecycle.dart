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
    await onActiveSchoolSwitched(
      previousSchoolId: previousSchoolId,
      newSchoolId: newSchoolId,
    );
    // Compat v2.0.1 : purge push de l'ancienne école uniquement si
    // remplacement destructif (appelé depuis d'anciens chemins).
    // Le switch multi-écoles utilise [onActiveSchoolSwitched] seul.
    if (previousSchoolId != null &&
        previousSchoolId.isNotEmpty &&
        previousSchoolId != newSchoolId) {
      await ParentPushPreferences.purgeSchoolPushState(previousSchoolId);
    }
  }

  /// Changement d'établissement actif (multi) — reset transport, **pas** de purge data.
  static Future<void> onActiveSchoolSwitched({
    required String? previousSchoolId,
    required String? newSchoolId,
  }) async {
    await resetTransport();
    if (newSchoolId != null && newSchoolId.isNotEmpty) {
      await ParentPushPreferences.persistActiveSchoolContext();
    } else {
      await ParentPushPreferences.clearActiveSchoolContext();
    }
  }

  static Future<void> purgeSchoolPushData(String schoolId) async {
    await ParentPushPreferences.purgeSchoolPushState(schoolId);
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
    try {
      await _client?.stop();
    } catch (_) {}
    ParentPushSchoolGuard.clearHubSchool();
    try {
      await ParentPushForegroundService.stop();
    } catch (_) {}
    try {
      await ParentPushForegroundService.clearCredentials();
    } catch (_) {
      // Plugins absents (tests unitaires).
    }
  }
}
