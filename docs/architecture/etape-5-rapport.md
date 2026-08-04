# Rapport — Étape 5 (architecture v2.0.1)

**Périmètre :** cache partitionné par école, récupération complète sur changement de `serverInstanceId`. **Hors scope :** notifications (étape 6), sync cloud, Bootstrap, protocole d’activation.

**Date :** 2026-08-04

---

## 1. Partitionnement cache

Actif uniquement si **`STRICT_SCHOOL_DISCOVERY=true`** et **`SchoolBinding`** présent (même règle que l’étape 4).

| Stockage | Legacy | Partitionné |
|----------|--------|-------------|
| **Hive parent** | `parent_offline_v1` | `parent_offline_v1_{schoolIdSansTirets}` |
| **SharedPreferences** | clés historiques | `school.{schoolId}.` + clé métier |
| **Session auth (secure)** | `access_token`, … | mêmes noms scopés via `CachePartitionPolicy.scopeKey` |

Composants :

- `CachePartitionPolicy` — activation, préfixes, noms de box
- `SchoolScopedPreferences` — lecture/écriture prefs scopées
- `SchoolCachePurgeService.purgeSchoolScope(schoolId)` — Hive + prefs préfixe

**Séparation deux écoles :** à l’enregistrement d’un binding dont le `schoolId` change (mode strict), purge du scope de l’ancienne école.

**Non partitionné (volontaire) :** `device_id`, `school_binding`.

**Étape 6 :** prefs push / cursor / FG scopées en mode strict (voir [etape-6-rapport.md](etape-6-rapport.md)).

---

## 2. Changement de `serverInstanceId` (§4.10 complet en mode strict)

Flux (`ServerInstanceRecoveryService`) :

1. Détection (inchangée, discovery filtrée).
2. **Purge** offline + prefs scopées de l’école.
3. **Déconnexion** : `AuthRepository.logout` si API joignable, sinon `AuthStorage.clearSession` (sans `deleteAll` — préserve device/binding).
4. **Mise à jour** `SchoolBinding.serverInstanceId`.
5. **Réouverture** box Hive active.
6. **UI** : `ConnectionSnapshot.requiresReauthentication` → redirect `/login?reason=server_instance`.

**Legacy (`STRICT=false`) :** pas de purge ni logout automatique ; sync instance étape 4 inchangée.

**Non implémenté (étape 5 cible atteinte côté mobile) :** invalidation push / SignalR (étape 6).

---

## 3. Nouveaux composants

| Composant |
|-----------|
| `lib/core/cache/cache_partition_policy.dart` |
| `lib/core/cache/school_scoped_preferences.dart` |
| `lib/core/cache/school_cache_purge_service.dart` |
| `lib/core/school_binding/server_instance_recovery_service.dart` |

---

## 4. Fichiers modifiés

| Fichier | Changement |
|---------|------------|
| `parent_offline_cache.dart` | Box scopée, `purgeForSchool`, `ensureActivePartition` |
| `auth_storage.dart` | Clés scopées, `clearSession` (plus de `deleteAll`) |
| `local_server_discovery.dart` | Prefs scopées, recovery sur changement d’instance |
| `server_instance_binding_sync.dart` | `copyWithInstance` public |
| `school_binding_repository.dart` | Purge ancien scope si changement d’école |
| `school_activation_service.dart` | Rebind Hive après activation |
| `update_manager.dart` | Prefs updates scopées |
| `connection_mode.dart` / `connection_probe.dart` | `requiresReauthentication` |
| `connection_mode_notifier.dart` | Snapshot inclut reauth |
| `app_router.dart` | Redirect login après recovery |
| `login_screen.dart` | Message `reason=server_instance` |
| `identite-ecole-decouverte-v2.md` | Étape 5 ✅ |

---

## 5. Scénarios purge / récupération

| Scénario | Comportement |
|----------|--------------|
| Strict off | Aucune partition ; pas de recovery auto |
| Strict on, instance identique | Cache conservé ; session intacte |
| Strict on, instance change | Purge scope école + logout + login |
| Changement d’école (nouveau binding) | Purge scope ancienne école |
| Activation complete | Ouverture box Hive de la nouvelle école |

---

## 6. Tests

| Fichier | Contenu |
|---------|---------|
| `cache_partition_policy_test.dart` | Préfixes, box Hive, legacy keys |
| Fondations étapes 2–4 | Inchangés / toujours valides |

Exécution : `flutter test test/foundations` dans `mobile/school_management_mobile`.

---

## 7. Impacts

| Zone | Impact |
|------|--------|
| **STRICT=false (défaut)** | Comportement identique pré-étape 5 |
| **STRICT=true** | Données offline isolées par école ; recovery invasive sur réinstall serveur |
| **Logout** | Ne efface plus tout le secure storage (fix : device + binding préservés) |
| **Notifications** | Non modifiées (credentials push gérés par logout existant) |

---

## 8. Étape 6 (non démarrée)

- Partition / scope **notifications** et `deviceId` côté ERP
- Affinage push après recovery instance

Validation utilisateur requise avant implémentation.
