# Rapport — Étape 6 (architecture v2.0.1)

**Périmètre :** partition push / SignalR par école, garde-fou cross-école, cycle de vie après changement de binding, recovery `serverInstanceId` ou réauthentification. **Hors scope :** activation, Bootstrap, discovery (étapes 3–4), durcissement API parent / fin migration JWT (étape 7).

**Date :** 2026-08-04

---

## 1. Partitionnement notifications / push

Actif lorsque **`STRICT_SCHOOL_DISCOVERY=true`** et **`SchoolBinding`** présent (aligné étapes 4–5). En **legacy** (`STRICT=false`), clés prefs push inchangées et garde-fou désactivé.

| Donnée | Legacy | Mode strict |
|--------|--------|-------------|
| `seen_ids`, cursor `/changes`, seed inbox | clés globales | `school.{schoolId}.` + clé métier |
| Credentials miroir FG (URL, JWT) | globales | scopées par école |
| Contexte actif pour isolate FG | — | `parent_push_active_school_id` (global, non scopé) |

Composant central : **`ParentPushPreferences`**.

---

## 2. SignalR et contexte école

- À chaque connexion / reconnexion hub **`/hubs/parent-notifications`**, le client enregistre l’école active (`ParentPushSchoolGuard.bindHubSchool`) et persiste le contexte FG.
- **Protocole hub inchangé** : groupe privé `parent-{UserAccountId}` (JWT).
- **Validation client** : chaque payload SignalR, réponse `/changes` et poll FG passe par **`ParentPushSchoolGuard.acceptsNotification`** :
  - compare `schoolId` / `SchoolId` du payload au binding (ou contexte persisté en isolate FG) ;
  - en strict sans `schoolId` dans le payload, le contexte hub lié à la connexion sert de référence ;
  - rejet explicite si école différente.

**API école :** `ParentNotificationDto` inclut désormais **`SchoolId`** (inbox, changes, SignalR) pour une isolation explicite côté mobile.

---

## 3. Cycle de vie (`ParentPushLifecycle`)

| Événement | Action |
|-----------|--------|
| Sync credentials (URL + JWT) | `persistActiveSchoolContext` |
| Changement `SchoolBinding.schoolId` | `resetTransport` (SignalR stop, FG stop, credentials FG), purge push de l’ancienne école |
| Recovery `serverInstanceId` (strict) | idem + purge push scope école + clear contexte actif |
| `requiresReauthentication` | stop transport sans purge binding |

Intégrations :

- `SchoolBindingRepository.save` (changement d’école)
- `ServerInstanceRecoveryService.handleInstanceChange`
- `notification_providers.dart` (enregistrement client, écoute reauth)

Après **re-login** ou nouvelle connexion, le provider push existant relance `ensureStarted` / sync FG via `connectionModeProvider`.

---

## 4. Nouveaux composants

| Composant |
|-----------|
| `lib/features/parent/notifications/parent_push_preferences.dart` |
| `lib/features/parent/notifications/parent_push_school_guard.dart` |
| `lib/features/parent/notifications/parent_push_lifecycle.dart` |
| `test/foundations/parent_push_preferences_test.dart` |
| `test/foundations/parent_push_school_guard_test.dart` |

---

## 5. Fichiers modifiés

| Fichier | Changement |
|---------|------------|
| `parent_push_realtime_client.dart` | Prefs scopées, garde école, binding hub à la connexion |
| `parent_push_foreground_service.dart` | Prefs / poll scopés, garde sur inbox et `/changes` |
| `notification_providers.dart` | Lifecycle, reauth, garde fallback UI |
| `school_binding_repository.dart` | Lifecycle changement d’école |
| `server_instance_recovery_service.dart` | Lifecycle recovery instance |
| `parent_models.dart` | Champ optionnel `schoolId` sur inbox |
| `NotificationDtos.cs` / `NotificationService.cs` | `SchoolId` sur DTO parent |
| `identite-ecole-decouverte-v2.md` | Étape 6 ✅ |

**Non modifié (contraintes respectées) :** protocole activation, Bootstrap, discovery.

---

## 6. Comportement après changement d’école ou de serveur

### Changement d’école (strict)

1. Purge cache école précédente (étape 5) + purge état push (seen, cursor, seed, FG).
2. Arrêt SignalR et service foreground ; credentials FG effacés.
3. Nouveau binding enregistré ; au prochain sync session, nouveau namespace prefs + reconnexion hub vers le serveur courant.

### Changement `serverInstanceId` (strict, §4.10)

1. Recovery étape 5 (purge offline, logout, redirect login).
2. **En plus :** reset push complet pour l’école, contexte actif effacé.
3. Après authentification : seed inbox / cursor repart à zéro pour ce scope ; enregistrement device token inchangé côté API (déjà scopé `SchoolId` serveur).

### Legacy

Comportement push identique à avant l’étape 6 (pas de filtrage ni partition prefs).

---

## 7. Tests réalisés

| Test | Résultat |
|------|----------|
| `dotnet build` solution | **OK** (0 erreur) |
| `parent_push_preferences_test.dart` | Non exécuté ici — `flutter` absent du PATH agent |
| `parent_push_school_guard_test.dart` | Idem |

**Recommandé en local :**

```bash
cd mobile/school_management_mobile
flutter test test/foundations/parent_push_preferences_test.dart test/foundations/parent_push_school_guard_test.dart
```

Tests manuels suggérés : strict ON, recevoir une notif école A ; simuler payload `schoolId` B → pas d’alerte ; recovery instance → pas de notif avant re-login ; changement binding → pas de cursor/seen de l’ancienne école.

---

## 8. Impacts et limites

| Sujet | Impact |
|-------|--------|
| **Clients mobile anciens** | Nouveau champ `schoolId` JSON — ignoré s’ils ne le lisent pas ; compatible. |
| **DTO API** | Contrat JSON enrichi (champ supplémentaire) — non breaking pour consommateurs tolérants. |
| **`deviceId` ERP** (titre doc étape 6) | **Non implémenté** : table `ParentDeviceTokens` sans colonne `DeviceId` ; enregistrement token reste `(schoolId, userAccountId, token)`. Peut être ajouté sans toucher activation/discovery. |
| **Hub SignalR** | Pas de second groupe par école (isolation JWT + garde client + `SchoolId` payload). |
| **Étape 7** | Non démarrée — durcissement API parent / migration JWT. |

---

## 9. Prochaine étape

**Étape 7** — durcissement API parent + fin migration JWT (après validation explicite de l’étape 6).
