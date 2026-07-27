import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers/app_providers.dart';
import '../models/parent_models.dart';
import 'notification_service.dart';
import 'parent_notification_inbox_repository.dart';

final parentNotificationServiceProvider = Provider<ParentNotificationService>((ref) {
  return LocalParentNotificationService();
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

final parentNotificationInboxProvider =
    FutureProvider<List<ParentNotificationItem>>((ref) {
  return ref.watch(parentNotificationInboxRepositoryProvider).getInbox();
});
