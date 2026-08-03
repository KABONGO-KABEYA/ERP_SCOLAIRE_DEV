import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../../../core/auth/auth_storage.dart';
import '../../../core/config/api_config.dart';
import '../../../core/connection/connection_mode.dart';
import 'notification_service.dart';
import 'parent_push_audit_log.dart';

typedef ParentPushInboxListener = void Function();

/// Écoute SignalR (canal privé serveur) + dédup + ACK livraison.
class ParentPushRealtimeClient {
  ParentPushRealtimeClient(this._notifications);

  final ParentNotificationService _notifications;

  HubConnection? _hub;
  String? _baseUrl;
  var _connecting = false;
  final _seenIds = <String>{};
  static const _prefsKey = 'parent_push_seen_ids';
  static const _seededKey = 'parent_push_seeded';
  static const _cursorKey = 'parent_push_changes_after_id';

  final _connectionController = StreamController<bool>.broadcast();
  ParentPushInboxListener? onInboxChanged;

  Stream<bool> get connectionChanges => _connectionController.stream;

  bool get isConnected =>
      _hub != null && _hub!.state == HubConnectionState.Connected;

  Future<void> ensureStarted(ConnectionSnapshot connection) async {
    if (!connection.mode.isOnline || connection.baseUrl == null) {
      await stop();
      return;
    }

    final base = ApiConfig.normalize(connection.baseUrl!);
    if (_hub != null && _baseUrl == base) {
      if (_hub!.state == HubConnectionState.Connected) return;
      if (_hub!.state == HubConnectionState.Connecting ||
          _hub!.state == HubConnectionState.Reconnecting) {
        return;
      }
    }

    await _connect(base);
  }

  Future<void> _connect(String baseUrl) async {
    if (_connecting) return;
    _connecting = true;
    try {
      await stop(notify: false);
      await _loadSeen();

      final hubUrl = '$baseUrl/hubs/parent-notifications';
      final httpOptions = HttpConnectionOptions(
        accessTokenFactory: () async => await AuthStorage.accessToken ?? '',
      );

      final hub = HubConnectionBuilder()
          .withUrl(hubUrl, options: httpOptions)
          .withAutomaticReconnect()
          .build();

      hub.on('notification', _onNotificationArgs);
      hub.onclose(({Exception? error}) {
        debugPrint('[Push] SignalR fermé: $error');
        _emitConnection(false);
      });
      hub.onreconnecting(({Exception? error}) {
        debugPrint('[Push] SignalR reconnexion… $error');
        _emitConnection(false);
      });
      hub.onreconnected(({String? connectionId}) {
        debugPrint('[Push] SignalR reconnecté ($connectionId)');
        _emitConnection(true);
        // Catch-up des notifs manquées pendant la coupure.
        unawaited(_recoverMissedViaChanges(baseUrl));
      });

      await hub.start();
      _hub = hub;
      _baseUrl = baseUrl;
      _emitConnection(true);
      debugPrint('[Push] SignalR connecté → $hubUrl');
      unawaited(_recoverMissedViaChanges(baseUrl));
    } catch (e) {
      debugPrint('[Push] SignalR échec: $e');
      _hub = null;
      _baseUrl = null;
      _emitConnection(false);
    } finally {
      _connecting = false;
    }
  }

  void _emitConnection(bool connected) {
    if (!_connectionController.isClosed) {
      _connectionController.add(connected);
    }
  }

  void _onNotificationArgs(List<Object?>? args) {
    if (args == null || args.isEmpty) return;
    final raw = args.first;
    Map<String, dynamic>? map;
    if (raw is Map) {
      map = Map<String, dynamic>.from(raw);
    } else if (raw is String) {
      try {
        map = Map<String, dynamic>.from(jsonDecode(raw) as Map);
      } catch (e) {
        ParentPushAudit.log('SignalR payload JSON parse failed: $e');
        return;
      }
    }
    if (map == null) return;

    final id = (map['id'] ?? map['Id'] ?? map['notificationId'])
            ?.toString()
            .trim()
            .toLowerCase() ??
        '';
    if (id.isEmpty) {
      debugPrint('[Push] SignalR sans id — ignoré (évite doublon)');
      return;
    }
    final title =
        map['title'] as String? ?? map['Title'] as String? ?? 'Notification';
    final body = map['message'] as String? ??
        map['Message'] as String? ??
        map['body'] as String? ??
        '';
    unawaited(() async {
      await notifyIfNew(
        ParentLocalPushMessage(
          id: id,
          title: title,
          body: body,
          data: {
            if (map!['category'] != null) 'category': map['category'].toString(),
            if (map['deepLink'] != null) 'deepLink': map['deepLink'].toString(),
            if (map['studentId'] != null)
              'studentId': map['studentId'].toString(),
          },
          receivedAt: DateTime.tryParse(map['date']?.toString() ?? '') ??
              DateTime.now(),
        ),
      );
      await acknowledgeDelivered(id);
      await advanceCursor(id);
      onInboxChanged?.call();
    }());
  }

  Future<void> notifyIfNew(ParentLocalPushMessage message) async {
    final id = message.id.trim().toLowerCase();
    if (id.isEmpty || _seenIds.contains(id)) {
      ParentPushAudit.poll('signalr_dedupe_skip', data: {'id': id});
      return;
    }
    _seenIds.add(id);
    await _saveSeen();
    await _notifications.showLocalNotification(
      ParentLocalPushMessage(
        id: id,
        title: message.title,
        body: message.body,
        data: message.data,
        receivedAt: message.receivedAt,
      ),
    );
  }

  Future<void> markSeen(Iterable<String> ids) async {
    var changed = false;
    for (final raw in ids) {
      final id = raw.trim().toLowerCase();
      if (id.isEmpty) continue;
      if (_seenIds.add(id)) changed = true;
    }
    if (changed) await _saveSeen();
  }

  bool hasSeen(String id) => _seenIds.contains(id.trim().toLowerCase());

  Future<void> seedExistingWithoutAlert(Iterable<String> ids) async {
    await _loadSeen();
    final prefs = await SharedPreferences.getInstance();
    if (prefs.getBool(_seededKey) == true) return;
    await markSeen(ids);
    final last = ids
        .map((e) => e.trim().toLowerCase())
        .where((e) => e.isNotEmpty)
        .toList();
    if (last.isNotEmpty) {
      await advanceCursor(last.first);
    }
    await prefs.setBool(_seededKey, true);
  }

  Future<bool> get isSeeded async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getBool(_seededKey) == true;
  }

  Future<void> reloadSeen() => _loadSeen();

  Future<String?> getChangesCursor() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_cursorKey);
  }

  Future<void> advanceCursor(String notificationId) async {
    final id = notificationId.trim().toLowerCase();
    if (id.isEmpty) return;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_cursorKey, id);
  }

  /// ACK livrée : hub SignalR si possible, sinon HTTP.
  Future<void> acknowledgeDelivered(String notificationId) async {
    final id = notificationId.trim();
    if (id.isEmpty) return;
    try {
      final hub = _hub;
      if (hub != null && hub.state == HubConnectionState.Connected) {
        await hub.invoke('AcknowledgeDelivery', args: <Object>[id]);
        return;
      }
    } catch (e) {
      debugPrint('[Push] ACK hub: $e');
    }

    final base = _baseUrl;
    final token = await AuthStorage.accessToken;
    if (base == null || token == null || token.isEmpty) return;
    try {
      final dio = Dio(BaseOptions(
        baseUrl: base,
        connectTimeout: const Duration(seconds: 6),
        receiveTimeout: const Duration(seconds: 8),
        headers: {
          'Authorization': 'Bearer $token',
          'Accept': 'application/json',
        },
      ));
      await dio.post('/api/v1/parent/notifications/$id/delivered');
    } catch (e) {
      debugPrint('[Push] ACK http: $e');
    }
  }

  /// Catch-up après reconnexion / démarrage via API changes (pas toute la boîte).
  Future<void> _recoverMissedViaChanges(String baseUrl) async {
    final token = await AuthStorage.accessToken;
    if (token == null || token.isEmpty) return;
    final afterId = await getChangesCursor();
    if (afterId == null || afterId.isEmpty) return;

    try {
      await _loadSeen();
      final dio = Dio(BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: const Duration(seconds: 8),
        receiveTimeout: const Duration(seconds: 12),
        headers: {
          'Authorization': 'Bearer $token',
          'Accept': 'application/json',
        },
      ));
      final response = await dio.get<dynamic>(
        '/api/v1/parent/notifications/changes',
        queryParameters: {'afterId': afterId, 'take': 50},
      );
      ParentPushAudit.http(
        'GET',
        '$baseUrl/api/v1/parent/notifications/changes',
        status: response.statusCode,
      );
      final items = _parseList(response.data);
      for (final item in items) {
        final id = item['id']?.toString().trim().toLowerCase() ?? '';
        if (id.isEmpty) continue;
        await notifyIfNew(
          ParentLocalPushMessage(
            id: id,
            title: item['title']?.toString() ?? 'Notification',
            body: item['message']?.toString() ??
                item['body']?.toString() ??
                '',
            data: {
              if (item['category'] != null)
                'category': item['category'].toString(),
              if (item['deepLink'] != null)
                'deepLink': item['deepLink'].toString(),
            },
            receivedAt: DateTime.tryParse(item['date']?.toString() ?? '') ??
                DateTime.now(),
          ),
        );
        await acknowledgeDelivered(id);
        await advanceCursor(id);
      }
      if (items.isNotEmpty) onInboxChanged?.call();
    } catch (e, st) {
      ParentPushAudit.log('recover changes failed: $e\n$st');
      debugPrint('[Push] recover changes: $e');
    }
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

  Future<void> _loadSeen() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.reload();
    final list = prefs.getStringList(_prefsKey) ?? const [];
    _seenIds
      ..clear()
      ..addAll(
        list.map((e) => e.trim().toLowerCase()).where((e) => e.isNotEmpty),
      );
  }

  Future<void> _saveSeen() async {
    final prefs = await SharedPreferences.getInstance();
    final trimmed =
        _seenIds.toList().reversed.take(400).toList().reversed.toList();
    await prefs.setStringList(_prefsKey, trimmed);
  }

  Future<void> stop({bool notify = true}) async {
    final hub = _hub;
    _hub = null;
    _baseUrl = null;
    if (notify) _emitConnection(false);
    if (hub == null) return;
    try {
      await hub.stop();
    } catch (e) {
      ParentPushAudit.log('SignalR hub.stop: $e');
    }
  }

  void dispose() {
    unawaited(stop());
    _connectionController.close();
  }
}
