import '../models/parent_models.dart';
import '../parent_repository.dart';
import 'notification_service.dart';

/// Repository notifications parent — API + boîte locale FCM scaffolding.
class ParentNotificationInboxRepository {
  ParentNotificationInboxRepository({
    required ParentRepository parentRepository,
    required ParentNotificationService notificationService,
  })  : _parentRepository = parentRepository,
        _notificationService = notificationService;

  final ParentRepository _parentRepository;
  final ParentNotificationService _notificationService;

  Future<List<ParentNotificationItem>> getInbox() async {
    final remote = await _parentRepository.getNotifications();
    final localService = _notificationService;
    final local = localService is LocalParentNotificationService
        ? localService.localInbox
            .map(
              (m) => ParentNotificationItem(
                id: m.id,
                title: m.title,
                message: m.body,
                date: m.receivedAt ?? DateTime.now(),
                isRead: false,
              ),
            )
            .toList()
        : const <ParentNotificationItem>[];

    final merged = [...local, ...remote];
    merged.sort((a, b) => b.date.compareTo(a.date));
    return merged;
  }

  Future<ParentPushDeviceRegistration?> currentRegistration() async {
    final token = await _notificationService.getDeviceToken();
    if (token == null || token.isEmpty) return null;
    return ParentPushDeviceRegistration(
      token: token,
      platform: 'mobile',
      updatedAt: DateTime.now(),
    );
  }

  Future<ParentPushPermissionStatus> ensurePermission() =>
      _notificationService.requestPermission();
}
