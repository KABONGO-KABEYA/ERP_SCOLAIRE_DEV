/// Notifications système parent (barre de statut Android, comme WhatsApp).
library;

import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:permission_handler/permission_handler.dart';

import 'parent_push_audit_log.dart';

enum ParentPushPermissionStatus {
  unknown,
  granted,
  denied,
  provisional,
  unsupported,
}

class ParentPushDeviceRegistration {
  const ParentPushDeviceRegistration({
    required this.token,
    required this.platform,
    this.updatedAt,
  });

  final String token;
  final String platform;
  final DateTime? updatedAt;
}

abstract class ParentNotificationService {
  Future<void> initialize();

  Future<ParentPushPermissionStatus> requestPermission();

  Future<ParentPushPermissionStatus> getPermissionStatus();

  Future<String?> getDeviceToken();

  Stream<ParentLocalPushMessage> get foregroundMessages;

  Future<void> showLocalNotification(ParentLocalPushMessage message);
}

class ParentLocalPushMessage {
  const ParentLocalPushMessage({
    required this.id,
    required this.title,
    required this.body,
    this.data = const {},
    this.receivedAt,
  });

  final String id;
  final String title;
  final String body;
  final Map<String, String> data;
  final DateTime? receivedAt;
}

/// Notifications Android système (tray + heads-up).
class SystemParentNotificationService implements ParentNotificationService {
  SystemParentNotificationService();

  static const _channelId = 'erp_parent_alerts_v2';
  static const _channelName = 'Alertes scolaires';
  static const _channelDesc =
      'Paiements, notes, absences et messages de l’école';

  final FlutterLocalNotificationsPlugin _plugin =
      FlutterLocalNotificationsPlugin();
  final _foregroundController =
      StreamController<ParentLocalPushMessage>.broadcast();

  ParentPushPermissionStatus _permission = ParentPushPermissionStatus.unknown;
  var _initialized = false;

  @override
  Future<void> initialize() async {
    if (_initialized) return;

    const androidInit = AndroidInitializationSettings('@mipmap/ic_launcher');
    const initSettings = InitializationSettings(android: androidInit);

    await _plugin.initialize(
      initSettings,
      onDidReceiveNotificationResponse: (response) {
        debugPrint('[Push] Notification tapée: ${response.payload}');
      },
    );

    final android = _plugin.resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();
    await android?.createNotificationChannel(
      const AndroidNotificationChannel(
        _channelId,
        _channelName,
        description: _channelDesc,
        importance: Importance.max,
        playSound: true,
        enableVibration: true,
        showBadge: true,
        enableLights: true,
      ),
    );

    _permission = await getPermissionStatus();
    _initialized = true;
  }

  @override
  Future<ParentPushPermissionStatus> requestPermission() async {
    await initialize();

    if (defaultTargetPlatform == TargetPlatform.android) {
      final status = await Permission.notification.request();
      final android = _plugin.resolvePlatformSpecificImplementation<
          AndroidFlutterLocalNotificationsPlugin>();
      await android?.requestNotificationsPermission();

      _permission = (status.isGranted || status.isLimited)
          ? ParentPushPermissionStatus.granted
          : ParentPushPermissionStatus.denied;
      return _permission;
    }

    _permission = ParentPushPermissionStatus.unsupported;
    return _permission;
  }

  @override
  Future<ParentPushPermissionStatus> getPermissionStatus() async {
    if (defaultTargetPlatform != TargetPlatform.android) {
      return ParentPushPermissionStatus.unsupported;
    }
    final status = await Permission.notification.status;
    if (status.isGranted || status.isLimited) {
      return ParentPushPermissionStatus.granted;
    }
    if (status.isDenied || status.isPermanentlyDenied) {
      return ParentPushPermissionStatus.denied;
    }
    return ParentPushPermissionStatus.unknown;
  }

  @override
  Future<String?> getDeviceToken() async => null;

  @override
  Stream<ParentLocalPushMessage> get foregroundMessages =>
      _foregroundController.stream;

  @override
  Future<void> showLocalNotification(ParentLocalPushMessage message) async {
    await initialize();
    final perm = await getPermissionStatus();
    ParentPushAudit.androidChannel(
      channelId: _channelId,
      importance: 'max',
      priority: 'max',
      permissionGranted: perm == ParentPushPermissionStatus.granted,
    );
    if (_permission != ParentPushPermissionStatus.granted) {
      await requestPermission();
    }

    ParentPushAudit.localShow(
      message.id,
      message.title,
      message.body,
      source: 'UI',
    );

    // Ne pas alimenter une boîte locale (source de vérité = API).
    if (!_foregroundController.isClosed) {
      _foregroundController.add(message);
    }

    final id = _stableNotificationId(message.id);
    final payload = jsonEncode({
      'id': message.id,
      ...message.data,
    });

    await _plugin.show(
      id,
      message.title,
      message.body,
      NotificationDetails(
        android: AndroidNotificationDetails(
          _channelId,
          _channelName,
          channelDescription: _channelDesc,
          importance: Importance.max,
          priority: Priority.max,
          category: AndroidNotificationCategory.message,
          styleInformation: BigTextStyleInformation(
            message.body,
            contentTitle: message.title,
            summaryText: 'ERP Scolaire',
          ),
          ticker: message.title,
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
      payload: payload,
    );
    ParentPushAudit.log(
      'LocalNotification.show completed id=${message.id} channel=$_channelId',
    );
  }

  int _stableNotificationId(String id) {
    var hash = 0;
    for (final cu in id.codeUnits) {
      hash = (hash * 31 + cu) & 0x7fffffff;
    }
    return hash == 0 ? 1 : hash;
  }

  void dispose() {
    _foregroundController.close();
  }
}

typedef LocalParentNotificationService = SystemParentNotificationService;
