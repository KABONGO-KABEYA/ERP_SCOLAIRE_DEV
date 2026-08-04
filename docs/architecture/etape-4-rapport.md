# Rapport — Étape 4 (architecture v2.0.1)

**Périmètre :** discovery mobile filtrée par `SchoolBinding`, validation `health.identity.schoolId`, cloud via `binding.cloudBaseUrl`, détection `serverInstanceId`. **Hors scope :** login, notifications, sync, cache partitionné (étape 5), purge/logout sur changement d’instance.

**Date :** 2026-08-04

---

## 1. Comportement discovery

### Mode legacy (défaut)

`STRICT_SCHOOL_DISCOVERY=false` (dart-define, défaut compile-time) :

- Comportement **identique** à avant l’étape 4 (mDNS, last IP, scan, puis `ApiConfig` / `DiscoveryConstants` pour le cloud).
- Utilisateurs **sans** binding : inchangés.
- Utilisateurs **avec** binding : inchangés tant que le flag strict n’est pas activé.

### Mode filtré (opt-in)

`--dart-define=STRICT_SCHOOL_DISCOVERY=true` **et** `SchoolBinding` présent :

1. **Local** (last IP, mDNS, scan) : `GET /api/health` puis **`identity.schoolId` == `binding.schoolId`** (sinon candidat ignoré).
2. **Cloud** : URL = **`binding.cloudBaseUrl`** (pas le QR, pas `CLOUD_API_BASE_URL` seul).
3. **Distant** : même validation `schoolId` sur le health cloud.
4. **`serverInstanceId`** : si différent du binding avec même école → flag `DiscoveryResult.serverInstanceIdChanged` + mise à jour du binding en secure storage (**sans** purge cache / logout — étape 5).

---

## 2. Fichiers créés

| Fichier |
|---------|
| `lib/core/local_server_discovery/school_discovery_policy.dart` |
| `lib/core/school_binding/server_instance_binding_sync.dart` |
| `test/foundations/school_discovery_policy_test.dart` |
| `test/foundations/school_binding_gate_test.dart` |
| `docs/architecture/etape-4-rapport.md` (ce document) |

---

## 3. Fichiers modifiés

| Fichier | Changement |
|---------|------------|
| `local_server_discovery.dart` | Contexte binding, filtre schoolId, cloud binding, finalisation instance |
| `discovery_models.dart` | Champs `serverInstanceIdChanged`, ids observés |
| `school_binding_gate.dart` | `shouldFilterDiscoveryByBinding()` ← `STRICT_SCHOOL_DISCOVERY` + binding |
| `school_binding_repository.dart` | Commentaire (branché discovery) |
| `identite-ecole-decouverte-v2.md` | Étape 4 ✅ |

**Non modifié (contraintes) :** login, notifications, sync cloud, `ConnectionProbe` API publique.

---

## 4. Flag `STRICT_SCHOOL_DISCOVERY`

| Emplacement | Détail |
|-------------|--------|
| `binding_migration_config.dart` | `bool.fromEnvironment('STRICT_SCHOOL_DISCOVERY', defaultValue: false)` |
| `BindingMigrationPolicy.isStrictSchoolDiscoveryEnabled` | Lecteur unique |
| Activation prod | Build `--dart-define=STRICT_SCHOOL_DISCOVERY=true` quand la migration le permet |

---

## 5. Tests

| Fichier | Scénarios |
|---------|-----------|
| `school_discovery_policy_test.dart` | schoolId OK / KO, identity manquante, cloud URL, changement instance |
| `school_binding_gate_test.dart` | filtre désactivé si STRICT off même avec binding |
| `binding_migration_config_test.dart` | strict false par défaut (existant) |
| `health_info_compat_test.dart` | parse health v2 (existant) |

Exécution locale : `flutter test test/foundations` (depuis `mobile/school_management_mobile`).

---

## 6. Impacts éventuels

| Zone | Impact |
|------|--------|
| **STRICT=false (défaut)** | Aucun impact utilisateur |
| **STRICT=true + binding** | Seuls serveurs de l’école liée ; cloud forcé depuis le binding |
| **Autre école sur le LAN** | Ignorée en mode strict |
| **Changement instance serveur** | Binding mis à jour ; pas encore purge cache / re-login |
| **Staff sans binding** | Legacy discovery |

---

## 7. Reste pour l’étape 5 (non démarrée)

- Cache Hive / prefs **partitionné** par `binding.schoolId` (§4.9).
- Actions §4.10 complètes sur changement d’instance : purge cache, logout, re-login.
- Reset coordonné `serverInstanceId` + invalidation données offline.

---

## 8. Prochaine étape

**Étape 5** — uniquement après validation utilisateur explicite.
