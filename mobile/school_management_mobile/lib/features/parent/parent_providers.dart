import 'dart:typed_data';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers/app_providers.dart';
import '../secretary/account/account_repository.dart';
import 'models/parent_models.dart';
import 'notifications/notification_providers.dart';
import 'offline/parent_cache_codecs.dart';
import 'offline/parent_offline_cache.dart';
import 'premium/parent_receipt_zip_service.dart';

export 'notifications/notification_providers.dart';

final parentAccountRepositoryProvider = Provider(
  (ref) => AccountRepository(ref.watch(apiClientProvider)),
);

final parentOfflineCacheProvider = Provider<ParentOfflineCache>((ref) {
  return ParentOfflineCache.instance;
});

/// Clés actuellement servies depuis le cache (bandeau hors ligne).
final parentOfflineCacheHitsProvider = StateProvider<Set<String>>((ref) => {});

void _markCacheHit(Ref ref, String key, {required bool fromCache}) {
  final current = {...ref.read(parentOfflineCacheHitsProvider)};
  if (fromCache) {
    current.add(key);
  } else {
    current.remove(key);
  }
  ref.read(parentOfflineCacheHitsProvider.notifier).state = current;
}

final parentChildrenProvider = FutureProvider<List<ParentChild>>((ref) async {
  final key = ParentCacheKeys.children();
  final cache = ref.watch(parentOfflineCacheProvider);
  final repo = ref.watch(parentRepositoryProvider);
  return cache.readThroughList(
    key: key,
    fetch: repo.getChildren,
    toJson: ParentCacheCodecs.childToJson,
    fromJson: ParentChild.fromJson,
    onCacheHit: () => _markCacheHit(ref, key, fromCache: true),
    onNetworkHit: () => _markCacheHit(ref, key, fromCache: false),
  );
});

final parentSubscriptionProvider = FutureProvider<ParentSubscription>((ref) async {
  final key = ParentCacheKeys.subscription();
  final cache = ref.watch(parentOfflineCacheProvider);
  final repo = ref.watch(parentRepositoryProvider);
  return cache.readThrough(
    key: key,
    fetch: repo.getSubscription,
    toJson: ParentCacheCodecs.subscriptionToJson,
    fromJson: ParentSubscription.fromJson,
    onCacheHit: () => _markCacheHit(ref, key, fromCache: true),
    onNetworkHit: () => _markCacheHit(ref, key, fromCache: false),
  );
});

final selectedChildIdProvider = StateProvider<String?>((ref) => null);

final selectedChildProvider = Provider<ParentChild?>((ref) {
  final children =
      ref.watch(parentChildrenProvider).valueOrNull ?? const <ParentChild>[];
  final selectedId = ref.watch(selectedChildIdProvider);
  if (children.isEmpty) return null;
  if (selectedId == null) return children.first;
  return children.firstWhere(
    (c) => c.studentId == selectedId,
    orElse: () => children.first,
  );
});

final parentPaymentsProvider =
    FutureProvider.family<List<ParentPayment>, String>((ref, studentId) async {
  final key = ParentCacheKeys.payments(studentId);
  final cache = ref.watch(parentOfflineCacheProvider);
  final repo = ref.watch(parentRepositoryProvider);
  return cache.readThroughList(
    key: key,
    fetch: () => repo.getPayments(studentId),
    toJson: ParentCacheCodecs.paymentToJson,
    fromJson: ParentPayment.fromJson,
    onCacheHit: () => _markCacheHit(ref, key, fromCache: true),
    onNetworkHit: () => _markCacheHit(ref, key, fromCache: false),
  );
});

final parentPaymentSummaryProvider =
    FutureProvider.family<ParentPaymentSummary, String>((ref, studentId) async {
  final key = ParentCacheKeys.paymentSummary(studentId);
  final cache = ref.watch(parentOfflineCacheProvider);
  final repo = ref.watch(parentRepositoryProvider);
  return cache.readThrough(
    key: key,
    fetch: () => repo.getPaymentSummary(studentId),
    toJson: ParentCacheCodecs.paymentSummaryToJson,
    fromJson: ParentPaymentSummary.fromJson,
    onCacheHit: () => _markCacheHit(ref, key, fromCache: true),
    onNetworkHit: () => _markCacheHit(ref, key, fromCache: false),
  );
});

final parentFeeSituationsProvider =
    FutureProvider.family<ParentFeeSituations, String>((ref, studentId) async {
  final key = ParentCacheKeys.feeSituations(studentId);
  final cache = ref.watch(parentOfflineCacheProvider);
  final repo = ref.watch(parentRepositoryProvider);
  return cache.readThrough(
    key: key,
    fetch: () => repo.getFeeSituations(studentId),
    toJson: ParentCacheCodecs.feeSituationsToJson,
    fromJson: ParentFeeSituations.fromJson,
    onCacheHit: () => _markCacheHit(ref, key, fromCache: true),
    onNetworkHit: () => _markCacheHit(ref, key, fromCache: false),
  );
});

final selectedFeeTypeIdProvider = StateProvider<String?>((ref) => null);

final parentBulletinsProvider =
    FutureProvider.family<List<ParentBulletin>, String>((ref, studentId) async {
  final key = ParentCacheKeys.bulletins(studentId);
  final cache = ref.watch(parentOfflineCacheProvider);
  final repo = ref.watch(parentRepositoryProvider);
  return cache.readThroughList(
    key: key,
    fetch: () => repo.getBulletins(studentId),
    toJson: ParentCacheCodecs.bulletinToJson,
    fromJson: ParentBulletin.fromJson,
    onCacheHit: () => _markCacheHit(ref, key, fromCache: true),
    onNetworkHit: () => _markCacheHit(ref, key, fromCache: false),
  );
});

final parentGradesProvider =
    FutureProvider.family<ParentGradesOverview, String>((ref, studentId) async {
  final key = ParentCacheKeys.grades(studentId);
  final cache = ref.watch(parentOfflineCacheProvider);
  final repo = ref.watch(parentRepositoryProvider);
  return cache.readThrough(
    key: key,
    fetch: () => repo.getGrades(studentId),
    toJson: ParentCacheCodecs.gradesToJson,
    fromJson: ParentGradesOverview.fromJson,
    onCacheHit: () => _markCacheHit(ref, key, fromCache: true),
    onNetworkHit: () => _markCacheHit(ref, key, fromCache: false),
  );
});

final parentCommunicationsProvider = FutureProvider.family<
    List<ParentCommunicationItem>, String>((ref, studentId) async {
  final key = ParentCacheKeys.communications(studentId);
  final cache = ref.watch(parentOfflineCacheProvider);
  final repo = ref.watch(parentRepositoryProvider);
  return cache.readThroughList(
    key: key,
    fetch: () => repo.getCommunications(studentId),
    toJson: ParentCacheCodecs.communicationToJson,
    fromJson: ParentCommunicationItem.fromJson,
    onCacheHit: () => _markCacheHit(ref, key, fromCache: true),
    onNetworkHit: () => _markCacheHit(ref, key, fromCache: false),
  );
});

/// Conservé : délègue à l'inbox FCM scaffolding (même nom de provider).
final parentNotificationsProvider =
    FutureProvider<List<ParentNotificationItem>>((ref) {
  return ref.watch(parentNotificationInboxProvider.future);
});

final parentAttendanceProvider =
    FutureProvider.family<List<ParentAttendanceDay>, String>((ref, studentId) async {
  final key = ParentCacheKeys.attendance(studentId);
  final cache = ref.watch(parentOfflineCacheProvider);
  final repo = ref.watch(parentRepositoryProvider);
  return cache.readThroughList(
    key: key,
    fetch: () => repo.getAttendance(studentId),
    toJson: ParentCacheCodecs.attendanceToJson,
    fromJson: ParentAttendanceDay.fromJson,
    onCacheHit: () => _markCacheHit(ref, key, fromCache: true),
    onNetworkHit: () => _markCacheHit(ref, key, fromCache: false),
  );
});

/// IDs de communications marquées lues localement (session).
final parentReadCommunicationIdsProvider = StateProvider<Set<String>>((ref) => {});

final parentChildPhotoProvider =
    FutureProvider.family<Uint8List?, String>((ref, studentId) {
  return ref.watch(parentRepositoryProvider).getChildPhotoBytes(studentId);
});

/// Service ZIP isolé (Paiements Premium) — n'altère pas ParentRepository.
final parentReceiptZipServiceProvider = Provider(
  (ref) => ParentReceiptZipService(ref.watch(apiClientProvider)),
);

/// Recherche historique paiements (état UI local à l'écran via providers légers).
final parentPaymentsSearchQueryProvider = StateProvider<String>((ref) => '');
final parentPaymentsPeriodFilterProvider = StateProvider<String?>((ref) => null);

bool parentHasOfflineCacheHit(Set<String> hits, Iterable<String> keys) =>
    keys.any(hits.contains);

void ensureChildSelected(WidgetRef ref, List<ParentChild> children) {
  if (children.isEmpty) return;
  final current = ref.read(selectedChildIdProvider);
  final exists = children.any((c) => c.studentId == current);
  if (current == null || !exists) {
    ref.read(selectedChildIdProvider.notifier).state = children.first.studentId;
  }
}
