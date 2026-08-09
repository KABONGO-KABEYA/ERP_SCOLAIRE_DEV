import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../auth/auth_storage.dart';
import '../cache/cache_partition_policy.dart';
import '../cache/school_cache_purge_service.dart';
import '../../features/parent/notifications/parent_push_lifecycle.dart';
import '../../features/parent/offline/parent_offline_cache.dart';
import 'registered_schools_store.dart';
import 'school_already_registered_exception.dart';
import 'school_binding.dart';

/// Résultat de [SchoolBindingRepository.removeSchool].
enum RemoveSchoolOutcome {
  /// École retirée ; un autre établissement est devenu actif.
  switchedToOther,

  /// École retirée ; plus aucun établissement — parcours QR requis.
  registryEmpty,

  /// École retirée ; ce n'était pas l'actif (actif inchangé).
  removedInactive,
}

/// Seul point d'accès à la persistance `SchoolBinding` (architecture v2 §4.6).
///
/// Multi-établissements : registre N + [ActiveSchoolId] ;
/// [load] renvoie toujours l'établissement **actif** unique.
class SchoolBindingRepository {
  SchoolBindingRepository({
    FlutterSecureStorage? storage,
    RegisteredSchoolsStore? store,
  }) : _store = store ?? RegisteredSchoolsStore(storage: storage);

  /// Clé legacy (tests / docs) — déléguée au store.
  static const storageKey = RegisteredSchoolsStore.legacyBindingKey;

  final RegisteredSchoolsStore _store;

  /// Binding de l'établissement actif (ou null).
  Future<SchoolBinding?> load() async {
    final activeId = await _store.readActiveSchoolId();
    if (activeId == null) return null;
    final registry = await _store.loadRegistry();
    return registry[activeId];
  }

  /// Liste des établissements enregistrés (ordre stable par schoolId).
  Future<List<SchoolBinding>> loadAll() async {
    final registry = await _store.loadRegistry();
    final list = registry.values.toList()
      ..sort((a, b) => a.schoolName.toLowerCase().compareTo(b.schoolName.toLowerCase()));
    return list;
  }

  Future<String?> activeSchoolId() => _store.readActiveSchoolId();

  Future<bool> hasBinding() async {
    final binding = await load();
    return binding != null && binding.schoolId.isNotEmpty;
  }

  Future<bool> hasAnyRegisteredSchool() async {
    final registry = await _store.loadRegistry();
    return registry.isNotEmpty;
  }

  Future<bool> isRegistered(String schoolId) async {
    final id = CachePartitionPolicy.normalizeSchoolId(schoolId);
    if (id.isEmpty) return false;
    final registry = await _store.loadRegistry();
    return registry.containsKey(id);
  }

  /// Upsert du binding actif / sync `serverInstanceId` / migration JWT.
  ///
  /// - Même `schoolId` que l'actif → met à jour l'entrée registre (pas de purge).
  /// - Pas d'actif → enregistre et active.
  /// - Autre `schoolId` déjà connu → [setActive] (pas de purge).
  /// - Autre `schoolId` inconnu → ajoute puis active (compat mono / JWT).
  ///
  /// Pour un ajout QR avec refus de doublon, utiliser [addSchool].
  Future<void> save(SchoolBinding binding) async {
    if (binding.schoolId.isEmpty) {
      throw ArgumentError('schoolId requis');
    }
    final id = CachePartitionPolicy.normalizeSchoolId(binding.schoolId);
    final registry = await _store.loadRegistry();
    final activeId = await _store.readActiveSchoolId();

    registry[id] = binding;
    await _store.writeRegistry(registry);

    if (activeId == null || activeId.isEmpty) {
      await _store.writeActiveSchoolId(id);
      await ParentPushLifecycle.onActiveSchoolSwitched(
        previousSchoolId: null,
        newSchoolId: id,
      );
      await _safeEnsurePartition();
      return;
    }

    if (activeId == id) {
      // Mise à jour in-place (ex. serverInstanceId).
      return;
    }

    await setActive(id);
  }

  /// Ajoute un établissement via QR. Refuse les doublons.
  /// N'active pas automatiquement s'il existe déjà un actif
  /// (sauf registre vide → devient actif).
  Future<SchoolBinding> addSchool(
    SchoolBinding binding, {
    bool setAsActive = true,
  }) async {
    if (binding.schoolId.isEmpty) {
      throw ArgumentError('schoolId requis');
    }
    final id = CachePartitionPolicy.normalizeSchoolId(binding.schoolId);
    final registry = await _store.loadRegistry();
    if (registry.containsKey(id)) {
      throw SchoolAlreadyRegisteredException(
        binding.schoolId,
        schoolName: binding.schoolName,
      );
    }

    registry[id] = binding;
    await _store.writeRegistry(registry);

    final activeId = await _store.readActiveSchoolId();
    final shouldActivate =
        setAsActive || activeId == null || activeId.isEmpty;
    if (shouldActivate) {
      await setActive(id);
    }
    return binding;
  }

  /// Change l'établissement actif **sans** purger les autres.
  Future<void> setActive(String schoolId) async {
    final id = CachePartitionPolicy.normalizeSchoolId(schoolId);
    final registry = await _store.loadRegistry();
    if (!registry.containsKey(id)) {
      throw StateError('Établissement non enregistré: $schoolId');
    }

    final previousId = await _store.readActiveSchoolId();
    if (previousId == id) {
      await _safeEnsurePartition();
      return;
    }

    // Pas de purge ni d'effacement des sessions scopées des autres écoles.
    // La session runtime (Riverpod) est invalidée côté UI ; les tokens
    // partitionnés restent pour reprise au retour sur l'école.
    await _store.writeActiveSchoolId(id);
    await ParentPushLifecycle.onActiveSchoolSwitched(
      previousSchoolId: previousId,
      newSchoolId: id,
    );
    await _safeEnsurePartition();
  }

  /// Supprime un établissement et purge **uniquement** ses données locales.
  Future<RemoveSchoolOutcome> removeSchool(String schoolId) async {
    final id = CachePartitionPolicy.normalizeSchoolId(schoolId);
    final registry = await _store.loadRegistry();
    if (!registry.containsKey(id)) {
      throw StateError('Établissement non enregistré: $schoolId');
    }

    final previousActive = await _store.readActiveSchoolId();
    final wasActive = previousActive == id;

    if (wasActive) {
      await ParentPushLifecycle.resetTransport();
      await AuthStorage.clearSession();
    }

    await SchoolCachePurgeService.purgeSchoolScope(id);
    await ParentPushLifecycle.purgeSchoolPushData(id);
    await AuthStorage.clearSessionForSchool(id);

    registry.remove(id);
    await _store.writeRegistry(registry);

    if (registry.isEmpty) {
      await _store.writeActiveSchoolId(null);
      await ParentPushLifecycle.onActiveSchoolSwitched(
        previousSchoolId: previousActive,
        newSchoolId: null,
      );
      return RemoveSchoolOutcome.registryEmpty;
    }

    if (!wasActive) {
      return RemoveSchoolOutcome.removedInactive;
    }

    final nextId = registry.keys.first;
    await _store.writeActiveSchoolId(nextId);
    await ParentPushLifecycle.onActiveSchoolSwitched(
      previousSchoolId: previousActive,
      newSchoolId: nextId,
    );
    await _safeEnsurePartition();
    return RemoveSchoolOutcome.switchedToOther;
  }

  /// Efface tout le registre (tests / reset usine).
  Future<void> clear() async {
    final registry = await _store.loadRegistry();
    for (final id in registry.keys) {
      await SchoolCachePurgeService.purgeSchoolScope(id);
      await ParentPushLifecycle.purgeSchoolPushData(id);
      await AuthStorage.clearSessionForSchool(id);
    }
    await AuthStorage.clearSession();
    await ParentPushLifecycle.resetTransport();
    await _store.clearAll();
  }

  static Future<void> _safeEnsurePartition() async {
    try {
      await ParentOfflineCache.ensureActivePartition();
    } catch (_) {
      // Hive non initialisé (tests unitaires).
    }
  }
}
