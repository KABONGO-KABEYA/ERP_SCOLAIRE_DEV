import 'package:flutter/foundation.dart';

/// Traces d'audit notifications (filtre logcat : PushAudit).
/// Ne change pas le comportement métier — diagnostic uniquement.
class ParentPushAudit {
  ParentPushAudit._();

  static String _ts() => DateTime.now().toIso8601String();

  static void log(String message) {
    debugPrint('[PushAudit] ${_ts()} $message');
  }

  static void fgLifecycle(String event, {Map<String, Object?>? data}) {
    final extra = data == null || data.isEmpty
        ? ''
        : ' ${data.entries.map((e) => '${e.key}=${e.value}').join(' ')}';
    log('FG.$event$extra');
  }

  static void poll(String phase, {Map<String, Object?>? data}) {
    final extra = data == null || data.isEmpty
        ? ''
        : ' ${data.entries.map((e) => '${e.key}=${e.value}').join(' ')}';
    log('Poll.$phase$extra');
  }

  static void http(
    String method,
    String url, {
    int? status,
    int? ms,
    Object? error,
  }) {
    log(
      'HTTP $method $url'
      '${status != null ? ' status=$status' : ''}'
      '${ms != null ? ' ms=$ms' : ''}'
      '${error != null ? ' error=$error' : ''}',
    );
  }

  static void localShow(String id, String title, String body, {String? source}) {
    log(
      'LocalNotification.show id=$id title=${_clip(title)} body=${_clip(body)}'
      '${source != null ? ' source=$source' : ''}',
    );
  }

  static void prefs({
    bool? jwtPresent,
    int? jwtLen,
    String? baseUrl,
    String? afterId,
    bool? pollEnabled,
    bool? seeded,
    int? seenCount,
  }) {
    log(
      'Prefs'
      '${jwtPresent != null ? ' jwt=${jwtPresent ? 'yes' : 'no'}' : ''}'
      '${jwtLen != null ? ' jwtLen=$jwtLen' : ''}'
      '${baseUrl != null ? ' baseUrl=$baseUrl' : ''}'
      '${afterId != null ? ' afterId=$afterId' : ''}'
      '${pollEnabled != null ? ' pollEnabled=$pollEnabled' : ''}'
      '${seeded != null ? ' seeded=$seeded' : ''}'
      '${seenCount != null ? ' seenCount=$seenCount' : ''}',
    );
  }

  static void androidChannel({
    required String channelId,
    required String importance,
    required String priority,
    bool? permissionGranted,
  }) {
    log(
      'Android channelId=$channelId importance=$importance priority=$priority'
      '${permissionGranted != null ? ' postNotif=${permissionGranted ? 'granted' : 'denied'}' : ''}',
    );
  }

  static void battery({
    required bool ignoringOptimizations,
    required bool fgServiceRunning,
  }) {
    log(
      'Battery ignoreOptimization=$ignoringOptimizations '
      'fgServiceRunning=$fgServiceRunning',
    );
  }

  static void transport(String mode, {String? detail}) {
    log('Transport mode=$mode${detail != null ? ' $detail' : ''}');
  }

  static void timing(String label, DateTime started, {String? extra}) {
    final ms = DateTime.now().difference(started).inMilliseconds;
    log('Timing $label ms=$ms${extra != null ? ' $extra' : ''}');
  }

  static String _clip(String s, [int max = 80]) =>
      s.length <= max ? s : '${s.substring(0, max)}…';
}
