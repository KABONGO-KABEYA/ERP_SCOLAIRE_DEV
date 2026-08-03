import 'dart:async';

import 'package:flutter/widgets.dart';
import 'package:flutter_foreground_task/flutter_foreground_task.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/auth_storage.dart';
import '../../../core/providers/app_providers.dart';
import '../models/parent_models.dart';
import 'notification_service.dart';
import 'parent_notification_inbox_repository.dart';
import 'parent_push_audit_log.dart';
import 'parent_push_foreground_service.dart';
import 'parent_push_realtime_client.dart';

final parentNotificationServiceProvider =
    Provider<ParentNotificationService>((ref) {
  final service = SystemParentNotificationService();
  ref.onDispose(service.dispose);
  return service;
});

final parentPushRealtimeClientProvider =
    Provider<ParentPushRealtimeClient>((ref) {
  final client =
      ParentPushRealtimeClient(ref.watch(parentNotificationServiceProvider));
  ref.onDispose(client.dispose);
  return client;
});

final parentNotificationInboxRepositoryProvider =
    Provider<ParentNotificationInboxRepository>((ref) {
  return ParentNotificationInboxRepository(
    parentRepository: ref.watch(parentRepositoryProvider),
    notificationService: ref.watch(parentNotificationServiceProvider),
  );
});

final parentPushPermissionProvider =
    FutureProvider<ParentPushPermissionStatus>((ref) async {
  final service = ref.watch(parentNotificationServiceProvider);
  await service.initialize();
  return service.getPermissionStatus();
});

final parentFcmTokenProvider = FutureProvider<String?>((ref) async {
  final service = ref.watch(parentNotificationServiceProvider);
  await service.initialize();
  return service.getDeviceToken();
});

/// Filtre catégorie (null = toutes).
final parentNotificationCategoryFilterProvider =
    StateProvider<String?>((ref) => null);

final parentNotificationSearchProvider = StateProvider<String>((ref) => '');

final parentNotificationInboxProvider =
    FutureProvider<List<ParentNotificationItem>>((ref) {
  final category = ref.watch(parentNotificationCategoryFilterProvider);
  final search = ref.watch(parentNotificationSearchProvider);
  return ref.watch(parentNotificationInboxRepositoryProvider).getInbox(
        category: category,
        query: search.isEmpty ? null : search,
      );
});

final parentNotificationUnreadCountProvider = FutureProvider<int>((ref) async {
  ref.watch(parentNotificationInboxProvider);
  return ref.watch(parentNotificationInboxRepositoryProvider).unreadCount();
});

/// Bootstrap push :
/// - App ouverte + SignalR OK → pas de polling UI (temps réel uniquement)
/// - SignalR coupé → fallback polling /changes
/// - FG service = filet de sécurité en arrière-plan (aussi /changes)
final parentNotificationPollingProvider = Provider<void>((ref) {
  Timer? fallbackTimer;
  final push = ref.watch(parentPushRealtimeClientProvider);
  final service = ref.watch(parentNotificationServiceProvider);
  var signalrConnected = false;
  var appResumed = true;

  push.onInboxChanged = () {
    ref.invalidate(parentNotificationInboxProvider);
  };

  unawaited(() async {
    await service.initialize();
    await service.requestPermission();
    await ParentPushForegroundService.init();
    final connection = ref.read(connectionModeProvider);
    await ParentPushForegroundService.ensureStarted(connection.baseUrl);
  }());

  void onTaskData(Object data) {
    if (data is Map && data['type'] == 'inbox_changed') {
      ref.invalidate(parentNotificationInboxProvider);
    }
  }

  FlutterForegroundTask.addTaskDataCallback(onTaskData);

  Future<void> syncCredentialsOnly() async {
    final connection = ref.read(connectionModeProvider);
    final token = await AuthStorage.accessToken;
    await ParentPushForegroundService.syncCredentials(
      baseUrl: connection.baseUrl,
      accessToken: token,
    );
    // Indique au FG s'il doit poller (seulement si SignalR down / app minimisée).
    await ParentPushForegroundService.setPollingEnabled(
      !signalrConnected || !appResumed,
    );
    ParentPushAudit.transport(
      !signalrConnected || !appResumed ? 'FG_poll_enabled' : 'FG_poll_disabled',
      detail:
          'signalr=$signalrConnected resumed=$appResumed pollEnabled=${!signalrConnected || !appResumed}',
    );
  }

  Future<void> bootstrapSeedOnly() async {
    try {
      final items =
          await ref.read(parentNotificationInboxRepositoryProvider).getInbox();
      await push.reloadSeen();
      if (!await push.isSeeded) {
        await push.seedExistingWithoutAlert(items.map((e) => e.id));
      }
      if (items.isNotEmpty) {
        await push.advanceCursor(items.first.id);
      }
      ref.invalidate(parentNotificationInboxProvider);
    } catch (e) {
      debugPrint('[Push] seed: $e');
    }
  }

  /// Fallback uniquement si SignalR n'est PAS connecté (app ouverte).
  Future<void> fallbackPollAndNotify() async {
    if (signalrConnected) return;
    final connection = ref.read(connectionModeProvider);
    await push.ensureStarted(connection);
    if (push.isConnected) {
      signalrConnected = true;
      return;
    }

    await syncCredentialsOnly();
    try {
      final changes =
          await ref.read(parentRepositoryProvider).getNotificationChanges(
                afterId: await push.getChangesCursor(),
              );
      await push.reloadSeen();
      for (final n in changes) {
        await push.notifyIfNew(
          ParentLocalPushMessage(
            id: n.id,
            title: n.title,
            body: n.message,
            data: {
              'category': n.category,
              if (n.deepLink != null) 'deepLink': n.deepLink!,
            },
            receivedAt: n.date,
          ),
        );
        await push.acknowledgeDelivered(n.id);
        await push.advanceCursor(n.id);
      }
      if (changes.isNotEmpty) {
        ref.invalidate(parentNotificationInboxProvider);
      }
    } catch (e) {
      debugPrint('[Push] fallback: $e');
    }
  }

  void stopFallbackTimer() {
    fallbackTimer?.cancel();
    fallbackTimer = null;
  }

  void startFallbackTimer() {
    stopFallbackTimer();
    // Secours uniquement — pas de poll tant que SignalR est UP.
    fallbackTimer = Timer.periodic(const Duration(seconds: 20), (_) {
      unawaited(fallbackPollAndNotify());
    });
  }

  void reconfigureTransport() {
    if (signalrConnected && appResumed) {
      stopFallbackTimer();
      unawaited(ParentPushForegroundService.setPollingEnabled(false));
      debugPrint('[Push] mode=SignalR (pas de polling UI)');
      ParentPushAudit.transport('SignalR', detail: 'UI poll off FG poll off');
    } else {
      startFallbackTimer();
      unawaited(ParentPushForegroundService.setPollingEnabled(true));
      debugPrint(
        '[Push] mode=fallback (signalr=$signalrConnected resumed=$appResumed)',
      );
      ParentPushAudit.transport(
        'fallback',
        detail: 'signalr=$signalrConnected resumed=$appResumed FG poll on',
      );
      unawaited(fallbackPollAndNotify());
    }
  }

  // Démarrage
  unawaited(() async {
    final connection = ref.read(connectionModeProvider);
    await push.ensureStarted(connection);
    signalrConnected = push.isConnected;
    await bootstrapSeedOnly();
    await syncCredentialsOnly();
    await ParentPushForegroundService.ensureStarted(connection.baseUrl);
    reconfigureTransport();
  }());

  push.connectionChanges.listen((connected) {
    signalrConnected = connected;
    reconfigureTransport();
    if (connected) {
      ref.invalidate(parentNotificationInboxProvider);
    }
  });

  ref.listen(connectionModeProvider, (_, next) {
    unawaited(() async {
      await push.ensureStarted(next);
      signalrConnected = push.isConnected;
      final token = await AuthStorage.accessToken;
      await ParentPushForegroundService.syncCredentials(
        baseUrl: next.baseUrl,
        accessToken: token,
      );
      reconfigureTransport();
    }());
  });

  final observer = _AppLifecycleRefresh(
    onResume: () {
      appResumed = true;
      unawaited(() async {
        final connection = ref.read(connectionModeProvider);
        await push.ensureStarted(connection);
        signalrConnected = push.isConnected;
        await syncCredentialsOnly();
        // Refresh UI sans re-alerter.
        try {
          final items = await ref
              .read(parentNotificationInboxRepositoryProvider)
              .getInbox();
          await push.reloadSeen();
          await push.markSeen(items.map((e) => e.id));
          ref.invalidate(parentNotificationInboxProvider);
        } catch (e, st) {
          ParentPushAudit.log('onResume inbox refresh: $e\n$st');
        }
        reconfigureTransport();
      }());
    },
    onPause: () {
      appResumed = false;
      ParentPushAudit.fgLifecycle('app_onPause', data: {'pollEnabled': true});
      unawaited(() async {
        await syncCredentialsOnly();
        final connection = ref.read(connectionModeProvider);
        await ParentPushForegroundService.ensureStarted(connection.baseUrl);
        await ParentPushForegroundService.setPollingEnabled(true);
      }());
    },
  );
  WidgetsBinding.instance.addObserver(observer);

  ref.onDispose(() {
    stopFallbackTimer();
    push.onInboxChanged = null;
    WidgetsBinding.instance.removeObserver(observer);
    FlutterForegroundTask.removeTaskDataCallback(onTaskData);
  });
});

class _AppLifecycleRefresh with WidgetsBindingObserver {
  _AppLifecycleRefresh({required this.onResume, required this.onPause});

  final VoidCallback onResume;
  final VoidCallback onPause;

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) onResume();
    if (state == AppLifecycleState.paused ||
        state == AppLifecycleState.inactive) {
      onPause();
    }
  }
}
