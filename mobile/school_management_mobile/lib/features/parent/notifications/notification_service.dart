/// Scaffolding notifications FCM — architecture prête, sans push serveur.
///
/// Firebase Messaging pourra brancher [ParentNotificationService] plus tard
/// sans toucher aux écrans.
library;

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

  /// Token FCM (null tant que Firebase n'est pas branché).
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

/// Implémentation locale (scaffolding) — pas de dépendance Firebase pour l'instant.
class LocalParentNotificationService implements ParentNotificationService {
  ParentPushPermissionStatus _permission = ParentPushPermissionStatus.unsupported;
  final List<ParentLocalPushMessage> _inbox = [];

  @override
  Future<void> initialize() async {
    // Prêt pour brancher Firebase.initializeApp + FirebaseMessaging.
    _permission = ParentPushPermissionStatus.unsupported;
  }

  @override
  Future<ParentPushPermissionStatus> requestPermission() async {
    // Sans FCM natif : on simule un refus soft "non supporté" pour UX honnête.
    _permission = ParentPushPermissionStatus.unsupported;
    return _permission;
  }

  @override
  Future<ParentPushPermissionStatus> getPermissionStatus() async => _permission;

  @override
  Future<String?> getDeviceToken() async => null;

  @override
  Stream<ParentLocalPushMessage> get foregroundMessages =>
      const Stream<ParentLocalPushMessage>.empty();

  @override
  Future<void> showLocalNotification(ParentLocalPushMessage message) async {
    _inbox.insert(0, message);
  }

  List<ParentLocalPushMessage> get localInbox => List.unmodifiable(_inbox);
}
