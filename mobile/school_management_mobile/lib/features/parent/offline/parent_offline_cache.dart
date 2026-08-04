import 'dart:convert';
import 'dart:io';

import 'package:hive_flutter/hive_flutter.dart';

import '../../../core/cache/cache_partition_policy.dart';

/// Cache Hive isolé pour le portail parent (Sprint 4).
/// Étape 5 : box scopée par `schoolId` si `STRICT_SCHOOL_DISCOVERY`.
class ParentOfflineCache {
  ParentOfflineCache._(this._box);

  static const boxNameBase = 'parent_offline_v1';
  static ParentOfflineCache? _instance;
  static bool _hiveReady = false;

  final Box<String> _box;

  static Future<ParentOfflineCache> init() async {
    await _ensureHiveInitialized();
    await ensureActivePartition();
    return instance;
  }

  static Future<void> _ensureHiveInitialized() async {
    if (_hiveReady) return;
    try {
      await Hive.initFlutter();
    } catch (_) {
      final fallback = Directory('${Directory.systemTemp.path}/erp_mobile_cache');
      if (!await fallback.exists()) {
        await fallback.create(recursive: true);
      }
      Hive.init(fallback.path);
    }
    _hiveReady = true;
  }

  static Future<String> _resolveBoxName() async {
    final schoolId = await CachePartitionPolicy.activeSchoolId();
    if (schoolId == null) return boxNameBase;
    return CachePartitionPolicy.hiveBoxName(boxNameBase, schoolId);
  }

  static Future<void> ensureActivePartition() async {
    await _ensureHiveInitialized();
    final name = await _resolveBoxName();
    if (_instance != null && _instance!._box.name == name) return;

    if (_instance != null) {
      await _instance!._box.close();
      _instance = null;
    }

    final box = await Hive.openBox<String>(name);
    _instance = ParentOfflineCache._(box);
  }

  static ParentOfflineCache get instance {
    final current = _instance;
    if (current == null) {
      throw StateError(
        'ParentOfflineCache non initialisé. Appeler init() dans main().',
      );
    }
    return current;
  }

  static Future<void> purgeForSchool(String schoolId) async {
    await _ensureHiveInitialized();
    final name = CachePartitionPolicy.hiveBoxName(boxNameBase, schoolId);
    if (Hive.isBoxOpen(name)) {
      await Hive.box<String>(name).close();
    }
    if (await Hive.boxExists(name)) {
      await Hive.deleteBoxFromDisk(name);
    }
    if (_instance?._box.name == name) {
      _instance = null;
      await ensureActivePartition();
    }
  }

  Future<T> readThrough<T>({
    required String key,
    required Future<T> Function() fetch,
    required Map<String, dynamic> Function(T value) toJson,
    required T Function(Map<String, dynamic> json) fromJson,
    void Function()? onCacheHit,
    void Function()? onNetworkHit,
  }) async {
    try {
      final fresh = await fetch();
      await _write(key, toJson(fresh));
      onNetworkHit?.call();
      return fresh;
    } catch (_) {
      final cached = _read(key);
      if (cached != null) {
        onCacheHit?.call();
        return fromJson(cached);
      }
      rethrow;
    }
  }

  Future<List<T>> readThroughList<T>({
    required String key,
    required Future<List<T>> Function() fetch,
    required Map<String, dynamic> Function(T value) toJson,
    required T Function(Map<String, dynamic> json) fromJson,
    void Function()? onCacheHit,
    void Function()? onNetworkHit,
  }) async {
    try {
      final fresh = await fetch();
      await _write(
        key,
        {
          'items': fresh.map(toJson).toList(),
        },
      );
      onNetworkHit?.call();
      return fresh;
    } catch (_) {
      final cached = _read(key);
      if (cached != null) {
        onCacheHit?.call();
        final items = cached['items'] as List<dynamic>? ?? const [];
        return items
            .map((e) => fromJson(Map<String, dynamic>.from(e as Map)))
            .toList();
      }
      rethrow;
    }
  }

  Future<void> _write(String key, Map<String, dynamic> json) async {
    await _box.put(key, jsonEncode(json));
    await _box.put(_metaLastWriteKey, DateTime.now().toIso8601String());
  }

  static const _metaLastWriteKey = '__meta_last_write_at';

  DateTime? get lastWriteAt {
    final raw = _box.get(_metaLastWriteKey);
    if (raw == null || raw.isEmpty) return null;
    return DateTime.tryParse(raw);
  }

  Map<String, dynamic>? _read(String key) {
    final raw = _box.get(key);
    if (raw == null || raw.isEmpty) return null;
    try {
      final decoded = jsonDecode(raw);
      if (decoded is Map<String, dynamic>) return decoded;
      if (decoded is Map) return Map<String, dynamic>.from(decoded);
    } catch (_) {}
    return null;
  }

  Future<void> clear() => _box.clear();
}

abstract final class ParentCacheKeys {
  static String children() => 'children';
  static String subscription() => 'subscription';
  static String payments(String studentId) => 'payments_$studentId';
  static String paymentSummary(String studentId) => 'payment_summary_$studentId';
  static String feeSituations(String studentId) => 'fee_situations_$studentId';
  static String grades(String studentId) => 'grades_$studentId';
  static String bulletins(String studentId) => 'bulletins_$studentId';
  static String attendance(String studentId) => 'attendance_$studentId';
  static String communications(String studentId) => 'communications_$studentId';
}
