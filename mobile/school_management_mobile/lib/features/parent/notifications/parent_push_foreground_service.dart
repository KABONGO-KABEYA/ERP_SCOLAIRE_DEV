import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_foreground_task/flutter_foreground_task.dart'
    hide NotificationVisibility;
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../core/auth/auth_storage.dart';
import '../../../core/config/api_config.dart';
import '../../../core/local_server_discovery/discovery_constants.dart';
import 'parent_push_audit_log.dart';
import 'parent_push_preferences.dart';
import 'parent_push_school_guard.dart';

/// Point d'entrée isolate du service foreground (top-level obligatoire).
@pragma('vm:entry-point')
void parentPushStartCallback() {
  FlutterForegroundTask.setTaskHandler(ParentPushTaskHandler());
}

/// Garde le polling actif en arrière-plan (SignalR est suspendu par Android).
class ParentPushForegroundService {
  ParentPushForegroundService._();

  static const baseUrlKey = 'parent_push_fg_base_url';
  static const tokenKey = 'parent_push_fg_access_token';
  static const prefsBaseUrlKey = ParentPushPreferences.fgBaseUrlBase;
  static const prefsTokenKey = ParentPushPreferences.fgTokenBase;
  static const pollEnabledKey = ParentPushPreferences.pollEnabledBase;
  static const cursorKey = ParentPushPreferences.changesCursorBase;
  static const seenKey = ParentPushPreferences.seenIdsBase;
  static const seededKey = ParentPushPreferences.seededBase;

  static var _initialized = false;

  static Future<void> init() async {
    if (_initialized) return;
    FlutterForegroundTask.init(
      androidNotificationOptions: AndroidNotificationOptions(
        channelId: 'erp_parent_push_service',
        channelName: 'Réception des alertes',
        channelDescription:
            'Maintient la réception des notifications scolaires en arrière-plan.',
        channelImportance: NotificationChannelImportance.LOW,
        priority: NotificationPriority.LOW,
        onlyAlertOnce: true,
      ),
      iosNotificationOptions: const IOSNotificationOptions(
        showNotification: false,
        playSound: false,
      ),
      foregroundTaskOptions: ForegroundTaskOptions(
        eventAction: ForegroundTaskEventAction.repeat(5000),
        autoRunOnBoot: false,
        autoRunOnMyPackageReplaced: false,
        allowWakeLock: true,
        allowWifiLock: true,
      ),
    );
    _initialized = true;
  }

  /// Miroir token + URL lisibles depuis l'isolate du service FG
  /// (FlutterSecureStorage ne marche pas de façon fiable hors isolate UI).
  static Future<void> syncCredentials({
    required String? baseUrl,
    required String? accessToken,
  }) async {
    await ParentPushPreferences.persistActiveSchoolContext();
    final prefs = await SharedPreferences.getInstance();
    final urlPrefsKey =
        await ParentPushPreferences.fgScopedKey(ParentPushPreferences.fgBaseUrlBase);
    final tokenPrefsKey =
        await ParentPushPreferences.fgScopedKey(ParentPushPreferences.fgTokenBase);

    if (baseUrl != null && baseUrl.isNotEmpty) {
      final normalized = ApiConfig.normalize(baseUrl);
      await FlutterForegroundTask.saveData(key: baseUrlKey, value: normalized);
      await prefs.setString(urlPrefsKey, normalized);
    }

    if (accessToken != null && accessToken.isNotEmpty) {
      await FlutterForegroundTask.saveData(key: tokenKey, value: accessToken);
      await prefs.setString(tokenPrefsKey, accessToken);
    }

    final schoolId = await ParentPushPreferences.readPersistedSchoolId();
    if (schoolId != null && schoolId.isNotEmpty) {
      await FlutterForegroundTask.saveData(
        key: ParentPushPreferences.activeSchoolIdKey,
        value: schoolId,
      );
    }
  }

  static Future<void> clearCredentials() async {
    await FlutterForegroundTask.removeData(key: tokenKey);
    await FlutterForegroundTask.removeData(key: baseUrlKey);
    await FlutterForegroundTask.removeData(
      key: ParentPushPreferences.activeSchoolIdKey,
    );
    final prefs = await SharedPreferences.getInstance();
    final tokenPrefsKey =
        await ParentPushPreferences.fgScopedKey(ParentPushPreferences.fgTokenBase);
    final urlPrefsKey =
        await ParentPushPreferences.fgScopedKey(ParentPushPreferences.fgBaseUrlBase);
    await prefs.remove(tokenPrefsKey);
    await prefs.remove(urlPrefsKey);
  }

  /// Active/désactive le poll FG (false quand SignalR UP + app au premier plan).
  static Future<void> setPollingEnabled(bool enabled) async {
    await FlutterForegroundTask.saveData(key: pollEnabledKey, value: enabled);
    await ParentPushPreferences.setPollEnabled(enabled);
  }

  static Future<void> ensureStarted(String? baseUrl) async {
    if (!Platform.isAndroid) return;
    await init();

    final token = await AuthStorage.accessToken;
    await syncCredentials(baseUrl: baseUrl, accessToken: token);

    final permission =
        await FlutterForegroundTask.checkNotificationPermission();
    if (permission != NotificationPermission.granted) {
      await FlutterForegroundTask.requestNotificationPermission();
    }

    if (await FlutterForegroundTask.isRunningService) {
      debugPrint('[Push] FG déjà actif');
      // Ne pas écraser le texte « Dernière vérif » géré par le poll.
      return;
    }

    final result = await FlutterForegroundTask.startService(
      serviceId: 2601,
      notificationTitle: 'ERP Scolaire — alertes actives',
      notificationText: 'Réception des notifications en arrière-plan',
      callback: parentPushStartCallback,
      serviceTypes: const [ForegroundServiceTypes.dataSync],
    );
    debugPrint('[Push] FG service start: $result');
    ParentPushAudit.fgLifecycle(
      'ensureStarted',
      data: {'startResult': result.toString()},
    );

    // Après démarrage : demander l'exemption batterie (OEM Tecno/Xiaomi).
    try {
      final ignoring = await FlutterForegroundTask.isIgnoringBatteryOptimizations;
      final running = await FlutterForegroundTask.isRunningService;
      ParentPushAudit.battery(
        ignoringOptimizations: ignoring,
        fgServiceRunning: running,
      );
      if (!ignoring) {
        await FlutterForegroundTask.requestIgnoreBatteryOptimization();
      }
    } catch (e) {
      ParentPushAudit.log('Battery request error: $e');
      debugPrint('[Push] batterie: $e');
    }
  }

  static Future<void> stop() async {
    if (!Platform.isAndroid) return;
    if (await FlutterForegroundTask.isRunningService) {
      await FlutterForegroundTask.stopService();
    }
  }
}

class ParentPushTaskHandler extends TaskHandler {
  static const _channelId = 'erp_parent_alerts_v2';
  static const _channelName = 'Alertes scolaires';

  final FlutterLocalNotificationsPlugin _plugin =
      FlutterLocalNotificationsPlugin();
  var _ready = false;
  var _busy = false;
  var _pollCount = 0;

  @override
  Future<void> onStart(DateTime timestamp, TaskStarter starter) async {
    ParentPushAudit.fgLifecycle(
      'onStart',
      data: {
        'starter': starter.name,
        'ts': timestamp.toIso8601String(),
      },
    );
    ParentPushAudit.log('Foreground service started');
    debugPrint('[Push] FG onStart (${starter.name})');
    const androidInit = AndroidInitializationSettings('@mipmap/ic_launcher');
    await _plugin.initialize(
      const InitializationSettings(android: androidInit),
    );
    final android = _plugin.resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();
    await android?.createNotificationChannel(
      const AndroidNotificationChannel(
        _channelId,
        _channelName,
        description: 'Paiements, notes, absences et messages de l’école',
        importance: Importance.max,
        playSound: true,
        enableVibration: true,
        showBadge: true,
        enableLights: true,
      ),
    );
    _ready = true;
    await _poll();
  }

  @override
  void onRepeatEvent(DateTime timestamp) {
    ParentPushAudit.fgLifecycle(
      'onRepeatEvent',
      data: {'ts': timestamp.toIso8601String()},
    );
    ParentPushAudit.poll('tick', data: {'pollCount': _pollCount + 1});
    unawaited(_poll());
  }

  @override
  Future<void> onDestroy(DateTime timestamp, bool isTimeout) async {
    ParentPushAudit.fgLifecycle(
      'onDestroy',
      data: {
        'isTimeout': isTimeout,
        'ts': timestamp.toIso8601String(),
        'reason': isTimeout ? 'service_timeout' : 'stopped_by_system_or_user',
      },
    );
    debugPrint('[Push] FG onDestroy timeout=$isTimeout');
  }

  Future<bool> _isPollEnabled() async {
    final fromFg = await FlutterForegroundTask.getData<bool>(
      key: ParentPushForegroundService.pollEnabledKey,
    );
    if (fromFg != null) return fromFg;
    final prefs = await SharedPreferences.getInstance();
    await prefs.reload();
    final key =
        await ParentPushPreferences.fgScopedKey(ParentPushPreferences.pollEnabledBase);
    return prefs.getBool(key) ?? true;
  }

  Future<void> _poll() async {
    if (_busy || !_ready) {
      ParentPushAudit.poll('skip', data: {
        'busy': _busy,
        'ready': _ready,
      });
      return;
    }
    _busy = true;
    _pollCount++;
    final pollStarted = DateTime.now();
    ParentPushAudit.poll('begin', data: {'pollCount': _pollCount});
    ParentPushAudit.log('Polling...');
    try {
      final pollEnabled = await _isPollEnabled();
      try {
        final ignoring = await FlutterForegroundTask.isIgnoringBatteryOptimizations;
        final running = await FlutterForegroundTask.isRunningService;
        ParentPushAudit.battery(
          ignoringOptimizations: ignoring,
          fgServiceRunning: running,
        );
      } catch (e) {
        ParentPushAudit.log('Battery check error: $e');
      }

      if (!pollEnabled) {
        ParentPushAudit.poll('disabled', data: {
          'reason': 'pollEnabled=false (SignalR UI actif?)',
        });
        await FlutterForegroundTask.updateService(
          notificationTitle: 'ERP Scolaire — alertes actives',
          notificationText: 'SignalR actif · secours en veille',
        );
        return;
      }

      final token = await _resolveToken();
      final baseUrl = await _resolveBaseUrl();
      final prefs = await SharedPreferences.getInstance();
      await prefs.reload();
      final cursorKey =
          await ParentPushPreferences.fgScopedKey(ParentPushPreferences.changesCursorBase);
      final seededKey =
          await ParentPushPreferences.fgScopedKey(ParentPushPreferences.seededBase);
      final seenKey =
          await ParentPushPreferences.fgScopedKey(ParentPushPreferences.seenIdsBase);
      final afterId = prefs.getString(cursorKey);
      final seeded = prefs.getBool(seededKey) == true;
      final seenList = prefs.getStringList(seenKey) ?? const [];

      ParentPushAudit.prefs(
        jwtPresent: token != null && token.isNotEmpty,
        jwtLen: token?.length,
        baseUrl: baseUrl,
        afterId: afterId,
        pollEnabled: pollEnabled,
        seeded: seeded,
        seenCount: seenList.length,
      );

      if (token == null || token.isEmpty) {
        ParentPushAudit.poll('abort', data: {'reason': 'no_jwt'});
        await FlutterForegroundTask.updateService(
          notificationTitle: 'ERP Scolaire — alertes',
          notificationText: 'Session absente — rouvrez l’app',
        );
        return;
      }

      if (baseUrl == null) {
        ParentPushAudit.poll('abort', data: {'reason': 'no_baseUrl'});
        await FlutterForegroundTask.updateService(
          notificationTitle: 'ERP Scolaire — alertes',
          notificationText: 'Serveur introuvable — rouvrez l’app',
        );
        return;
      }

      final dio = Dio(
        BaseOptions(
          baseUrl: baseUrl,
          connectTimeout: const Duration(seconds: 8),
          receiveTimeout: const Duration(seconds: 12),
          headers: {
            'Authorization': 'Bearer $token',
            'Accept': 'application/json',
          },
        ),
      );
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            options.extra['audit_start'] = DateTime.now().millisecondsSinceEpoch;
            ParentPushAudit.http(
              options.method,
              '${options.baseUrl}${options.path}',
            );
            handler.next(options);
          },
          onResponse: (response, handler) {
            final start = response.requestOptions.extra['audit_start'] as int?;
            final ms = start != null
                ? DateTime.now().millisecondsSinceEpoch - start
                : null;
            ParentPushAudit.http(
              response.requestOptions.method,
              '${response.requestOptions.baseUrl}${response.requestOptions.path}',
              status: response.statusCode,
              ms: ms,
            );
            handler.next(response);
          },
          onError: (e, handler) {
            ParentPushAudit.http(
              e.requestOptions.method,
              '${e.requestOptions.baseUrl}${e.requestOptions.path}',
              status: e.response?.statusCode,
              error: e.message,
            );
            handler.next(e);
          },
        ),
      );

      // Bootstrap : une seule fois télécharger l'inbox pour seed, puis /changes.
      if (!seeded || afterId == null || afterId.isEmpty) {
        ParentPushAudit.poll('bootstrap_inbox', data: {
          'seeded': seeded,
          'afterIdEmpty': afterId == null || afterId.isEmpty,
        });
        final httpStart = DateTime.now();
        final response =
            await dio.get<dynamic>('/api/v1/parent/notifications');
        ParentPushAudit.timing('GET /notifications (seed)', httpStart);
        final items = _parseList(response.data);
        ParentPushAudit.poll('bootstrap_result', data: {'count': items.length});
        final seen = seenList
            .map((e) => e.trim().toLowerCase())
            .where((e) => e.isNotEmpty)
            .toSet();
        String? newestId;
        DateTime? newestDate;
        for (final item in items) {
          if (!await ParentPushSchoolGuard.acceptsNotification(item)) {
            continue;
          }
          final id = item['id']?.toString().trim().toLowerCase() ?? '';
          if (id.isEmpty) continue;
          seen.add(id);
          final d = DateTime.tryParse(item['date']?.toString() ?? '');
          if (newestDate == null || (d != null && d.isAfter(newestDate))) {
            newestDate = d;
            newestId = id;
          }
        }
        await prefs.setStringList(seenKey, seen.toList());
        await prefs.setBool(seededKey, true);
        if (newestId != null) {
          await prefs.setString(cursorKey, newestId);
          ParentPushAudit.poll('cursor_set', data: {'afterId': newestId});
        }
        await FlutterForegroundTask.updateService(
          notificationTitle: 'ERP Scolaire — alertes actives',
          notificationText: 'Prêt · écoute en arrière-plan',
        );
        ParentPushAudit.timing('poll_total', pollStarted);
        return;
      }

      ParentPushAudit.poll('changes_request', data: {
        'afterId': afterId,
        'url': '$baseUrl/api/v1/parent/notifications/changes',
      });
      final httpStart = DateTime.now();
      final response = await dio.get<dynamic>(
        '/api/v1/parent/notifications/changes',
        queryParameters: {'afterId': afterId, 'take': 50},
      );
      ParentPushAudit.timing('GET /changes', httpStart, extra: 'status=${response.statusCode}');
      final items = _parseList(response.data);
      final ids = items
          .map((e) => e['id']?.toString().trim().toLowerCase() ?? '')
          .where((e) => e.isNotEmpty)
          .toList();
      ParentPushAudit.poll('changes_response', data: {
        'count': items.length,
        'ids': ids.isEmpty ? '(empty)' : ids.join(','),
      });

      final seen = seenList
          .map((e) => e.trim().toLowerCase())
          .where((e) => e.isNotEmpty)
          .toSet();

      var shown = 0;
      String? lastId = afterId;
      for (final item in items) {
        if (!await ParentPushSchoolGuard.acceptsNotification(item)) {
          continue;
        }
        final id = item['id']?.toString().trim().toLowerCase() ?? '';
        if (id.isEmpty) continue;
        lastId = id;
        if (seen.contains(id)) {
          ParentPushAudit.poll('dedupe_skip', data: {'id': id});
          continue;
        }
        seen.add(id);
        shown++;
        final title = item['title']?.toString() ?? 'Notification';
        final body = item['message']?.toString() ??
            item['body']?.toString() ??
            '';
        final showStart = DateTime.now();
        await _show(id, title, body);
        ParentPushAudit.timing('showLocalNotification', showStart, extra: 'id=$id');
        try {
          await dio.post('/api/v1/parent/notifications/$id/delivered');
        } catch (e) {
          ParentPushAudit.log('ACK delivered failed id=$id error=$e');
        }
      }

      if (items.isNotEmpty || shown > 0) {
        final trimmed =
            seen.toList().reversed.take(400).toList().reversed.toList();
        await prefs.setStringList(seenKey, trimmed);
        if (lastId != null) {
          await prefs.setString(cursorKey, lastId);
        }
        if (shown > 0) {
          FlutterForegroundTask.sendDataToMain({'type': 'inbox_changed'});
        }
      }

      ParentPushAudit.poll('end', data: {'shown': shown});

      final now = DateTime.now();
      final hh = now.hour.toString().padLeft(2, '0');
      final mm = now.minute.toString().padLeft(2, '0');
      final ss = now.second.toString().padLeft(2, '0');
      await FlutterForegroundTask.updateService(
        notificationTitle: 'ERP Scolaire — alertes actives',
        notificationText: shown > 0
            ? '$shown nouvelle(s) · $hh:$mm:$ss'
            : 'Dernière vérif. $hh:$mm:$ss',
      );
      ParentPushAudit.timing('poll_total', pollStarted);
    } catch (e, st) {
      ParentPushAudit.log('Poll exception: $e\n$st');
      debugPrint('[Push] FG poll #$_pollCount: $e');
      await FlutterForegroundTask.updateService(
        notificationTitle: 'ERP Scolaire — alertes',
        notificationText: 'Erreur réseau — nouvelle tentative…',
      );
    } finally {
      _busy = false;
    }
  }

  Future<String?> _resolveToken() async {
    final fromFg = await FlutterForegroundTask.getData<String>(
      key: ParentPushForegroundService.tokenKey,
    );
    if (fromFg != null && fromFg.isNotEmpty) return fromFg;

    final prefs = await SharedPreferences.getInstance();
    await prefs.reload();
    final tokenPrefsKey =
        await ParentPushPreferences.fgScopedKey(ParentPushPreferences.fgTokenBase);
    final fromPrefs = prefs.getString(tokenPrefsKey);
    if (fromPrefs != null && fromPrefs.isNotEmpty) return fromPrefs;

    try {
      return await AuthStorage.accessToken;
    } catch (e) {
      ParentPushAudit.log('AuthStorage.accessToken failed: $e');
      return null;
    }
  }

  Future<String?> _resolveBaseUrl() async {
    final saved = await FlutterForegroundTask.getData<String>(
      key: ParentPushForegroundService.baseUrlKey,
    );
    if (saved != null && saved.isNotEmpty) {
      return ApiConfig.normalize(saved);
    }
    final prefs = await SharedPreferences.getInstance();
    await prefs.reload();
    final urlPrefsKey =
        await ParentPushPreferences.fgScopedKey(ParentPushPreferences.fgBaseUrlBase);
    final fromPrefs = prefs.getString(urlPrefsKey);
    if (fromPrefs != null && fromPrefs.isNotEmpty) {
      return ApiConfig.normalize(fromPrefs);
    }
    final last = prefs.getString(DiscoveryConstants.lastKnownPrefsKey);
    if (last != null && last.isNotEmpty) {
      return ApiConfig.normalize(last);
    }
    return null;
  }

  List<Map<String, dynamic>> _parseList(dynamic data) {
    dynamic payload = data;
    if (payload is Map) {
      payload = payload['data'] ?? payload['Data'] ?? payload;
    }
    if (payload is! List) return const [];
    return payload
        .whereType<Map>()
        .map((e) => Map<String, dynamic>.from(e))
        .toList();
  }

  Future<void> _show(String id, String title, String body) async {
    ParentPushAudit.localShow(id, title, body, source: 'FG');
    ParentPushAudit.androidChannel(
      channelId: _channelId,
      importance: 'max',
      priority: 'max',
    );
    var hash = 0;
    for (final cu in id.codeUnits) {
      hash = (hash * 31 + cu) & 0x7fffffff;
    }
    if (hash == 0) hash = 1;

    await _plugin.show(
      hash,
      title,
      body,
      NotificationDetails(
        android: AndroidNotificationDetails(
          _channelId,
          _channelName,
          channelDescription:
              'Paiements, notes, absences et messages de l’école',
          importance: Importance.max,
          priority: Priority.max,
          category: AndroidNotificationCategory.message,
          styleInformation: BigTextStyleInformation(
            body,
            contentTitle: title,
            summaryText: 'ERP Scolaire',
          ),
          ticker: title,
          visibility: NotificationVisibility.public,
          autoCancel: true,
          playSound: true,
          enableVibration: true,
          enableLights: true,
          onlyAlertOnce: false,
          channelShowBadge: true,
          icon: '@mipmap/ic_launcher',
        ),
      ),
      payload: jsonEncode({'id': id}),
    );
    ParentPushAudit.log('LocalNotification.show completed id=$id');
  }
}

