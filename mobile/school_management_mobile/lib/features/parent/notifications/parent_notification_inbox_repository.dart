import '../models/parent_models.dart';
import '../parent_repository.dart';
import 'notification_service.dart';

/// Repository notifications parent — source de vérité = API uniquement.
class ParentNotificationInboxRepository {
  ParentNotificationInboxRepository({
    required ParentRepository parentRepository,
    required ParentNotificationService notificationService,
  })  : _parentRepository = parentRepository,
        _notificationService = notificationService;

  final ParentRepository _parentRepository;
  final ParentNotificationService _notificationService;

  Future<List<ParentNotificationItem>> getInbox({
    String? category,
    String? query,
  }) async {
    final remote = await _parentRepository.getNotifications(
      category: category,
      query: query,
    );
    // Dédupliquer par id, puis par contenu proche (double envoi serveur).
    final byId = <String, ParentNotificationItem>{};
    for (final item in remote) {
      final key = item.id.trim().toLowerCase();
      if (key.isEmpty) continue;
      byId.putIfAbsent(key, () => item);
    }
    final list = <ParentNotificationItem>[];
    final contentKeys = <String>{};
    final sorted = byId.values.toList()
      ..sort((a, b) => b.date.compareTo(a.date));
    for (final item in sorted) {
      final minute = item.date.toUtc().millisecondsSinceEpoch ~/ 60000;
      final contentKey =
          '${item.title.trim().toLowerCase()}|${item.message.trim().toLowerCase()}|$minute';
      if (!contentKeys.add(contentKey)) continue;
      list.add(item);
    }
    return list;
  }

  Future<int> unreadCount() => _parentRepository.getUnreadNotificationCount();

  Future<void> markRead(String notificationId) =>
      _parentRepository.markNotificationRead(notificationId);

  Future<void> markAllRead() => _parentRepository.markAllNotificationsRead();

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
