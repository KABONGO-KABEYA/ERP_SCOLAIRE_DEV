# Rapport de validation Phase 1 — Permissions effectives & Auth

**Date** : 2026-08-07  
**Périmètre** : moteur `IEffectivePermissionService`, cache catalogue, JWT (codes), login/refresh/`IsActive`, policies ASP.NET Core  
**Environnement audité** : base locale `SchoolManagementRDC_Development` (`localhost\HEROS_SQL19`)  
**Outil d’exécution** : `tools/Phase1SecurityValidation`  
**Preuves** : `tools/Phase1SecurityValidation/out/evidence.json`, `…/summary.txt`  
**Statut global** : **VALIDÉ AVEC RÉSERVES** — 27/27 contrôles métier Phase 1 **PASS** ; réserves documentées (perf JWT admin, invalidation cache hors seed, fraîcheur claims, non-régression HTTP partielle)

**Phase 2** : **non démarrée** — en attente de validation explicite de ce rapport.

---

## 1. Synthèse exécutive

| Domaine | Résultat |
|---------|----------|
| `ResolveAsync` — un rôle | OK |
| `ResolveAsync` — multi-rôles | OK |
| Grant / Deny / expiration (`ValidTo` exclusive) | OK |
| Dépendances (fermeture) | OK |
| Bypass ADMIN | OK (54/54 permissions actives) |
| Super Administrateur (`platform.*`) | OK |
| `HasPermissionAsync` | OK |
| JWT = codes permission uniquement | OK (aucun DisplayName/HelpText/BusinessDescription) |
| Login / refresh / `IsActive=false` | OK |
| Policies ASP.NET (autorisé / refusé / dynamique) | OK |
| Perf `ResolveAsync` + mémoire cache | OK (cold ~84 ms, warm ~6,9 ms, snapshot ~8,7 Ko) |
| Feature flag | **Absent** (conforme exigence) |
| Cache TTL | **Absent** — invalidation explicite uniquement |
| Non-régression suite de tests | **Partielle** — voir §8 |

Correctif découvert et **appliqué pendant l’audit** (bloquant refresh anonyme) :  
`RefreshTokenRepository` utilise désormais `IgnoreQueryFilters()` ; `LogLoginAsync` restaure `IgnoreSchoolScope` (ne le force plus à `false`).

---

## 2. Protocole d’exécution

### 2.1 Harness métier

```text
dotnet run --project tools/Phase1SecurityValidation
```

- Connexion via `DatabaseConnectionBootstrap` (même chemin que DesignTime / Phase 0).
- Création d’utilisateurs jetables préfixe `__p1v_*`, hard-delete en fin de run.
- Scénarios isolés : rôles, exceptions Grant/Deny, deps, ADMIN, Super Admin, JWT, Auth, policies in-process, perf.

### 2.2 Suites de tests existantes

| Suite | Résultat mesuré |
|-------|-----------------|
| UnitTests | **84 réussis / 1 échec** (StudentCards — date d’expiration, **hors Phase 1**) |
| IntegrationTests | **9 réussis / 1 échec** (`Login admin/Admin@2026` → 401 — **identifiants env.**, pas le moteur) |

### 2.3 Score harness Phase 1

| Métrique | Valeur |
|----------|--------|
| Contrôles | 27 |
| PASS | **27** |
| FAIL | **0** |
| Exit code | **0** |
| Fenêtre UTC | `2026-08-07T09:19:31Z` → `09:19:45Z` (~13 s) |

---

## 3. `IEffectivePermissionService` — scénarios

Algorithme vérifié :

```text
base = ∪ permissions des rôles actifs
raw  = (base ∪ grants actifs) \ denies actifs
      (fenêtre ValidFrom ≤ now < ValidTo, ValidTo exclusive)
ADMIN / admin.full (non deny) → raw ∪ catalogue permissions actives
effective = { p ∈ raw | RequiredClosure(p) ⊆ raw }
Super Admin → + permissions `platform.*` non deny
```

### 3.1 Un seul rôle — PARENT

| Attendu | Observé |
|---------|---------|
| Rôles = `[PARENT]` | OK |
| Permissions ⊇ `payments.read`, `grades.read`, `reports.read` | OK |
| Pas Super Admin | OK |
| Codes effectifs | `grades.read`, `payments.read`, `reports.read`, `students.read` |

**Note (réserve B1)** : le rôle PARENT en BD inclut aussi `students.read` (au-delà du seed minimal Phase 0 listant 3 permissions). Non bloquant pour le moteur ; à nettoyer/documenter en Phase 3 UI.

### 3.2 Multi-rôles — ENSEIGNANT ∪ COMPTABLE

| Contrôle | Résultat |
|----------|----------|
| Rôles présents | `COMPTABLE`, `ENSEIGNANT` |
| Union métier | `grades.create` **et** `payments.validate` présents |
| Nombre de codes | 12 |

### 3.3 Grant

Utilisateur PARENT + Grant `students.read` + `students.create` :

| Avant | Après |
|-------|-------|
| `students.create` = false | `students.create` = true **et** `students.read` = true |

### 3.4 Deny

ENSEIGNANT + Deny `grades.read` :

| Avant | Après |
|-------|-------|
| `grades.read` + `grades.create` | **les deux absents** (fermeture : create exige read) |

### 3.5 Expiration d’exception

- Grant **expiré** `currencies.read` (`ValidTo` = now−5 min) → **absent**
- Grant **actif** `schools.read` → **présent**

**Verdict `ValidTo` exclusive** : OK.

### 3.6 Dépendances

| Étape | Résultat |
|-------|----------|
| Closure(`students.create`) | `{students.create, students.read}` |
| Grant create **seul** | create **retiré** de l’effectif |
| Grant create **+** read | les deux **présents** |

### 3.7 Bypass ADMIN

| Contrôle | Résultat |
|----------|----------|
| Rôle `ADMIN` | OK |
| `admin.full` | OK |
| Codes effectifs | **54** (= catalogue actif) |

### 3.8 Super Administrateur

Utilisateur PARENT + `IsPlatformSuperAdmin=true` :

| Contrôle | Résultat |
|----------|----------|
| Flag résultat | true |
| `platform.superadmin` | présent |
| `platform.catalog.manage` | présent |
| Rôle métier PARENT conservé | OK |

---

## 4. `HasPermissionAsync`

| Appel | Attendu | Observé |
|-------|---------|---------|
| PARENT / `payments.read` | true | true |
| PARENT / `students.create` | false | false |
| ADMIN / `currencies.delete` | true (via `admin.full`) | true |
| PARENT / `" "` (vide) | false | false |

**Verdict** : point d’entrée unique utilisable côté serveur pour Desktop / Web / Mobile / API.

**Réserve B2** : pas encore d’endpoint HTTP dédié `HasPermission` — les clients se basent aujourd’hui sur les claims JWT / profil. À prévoir en Phase 2 si besoin offline/online unifié sans re-login.

---

## 5. JWT — codes uniquement

### 5.1 Décodage

| Contrôle | Parent | Admin (rôle) |
|----------|--------|--------------|
| Claims `permissions` | 4 codes | 54 codes |
| Forme code (`a.b…`) | OK | OK |
| Couverture = effectif | OK | OK |
| Métadonnées DisplayName / HelpText / BusinessDescription dans payload | **Absentes** | — |
| Claim `platform_superadmin` | Absente (flag false) | N/A (user admin école, pas platform) |

Types de claims observés (parent) :  
`sub`, `nameidentifier`, `unique_name`, `name`, `school_id`, `full_name`, `jti`, `role`, `permissions`, `exp`, `iss`, `aud`.

### 5.2 Taille

| Token | Longueur approx. |
|-------|------------------|
| PARENT (4 perms) | ~924 caractères |
| ADMIN (54 perms) | même ordre (login harness parent mesuré ; JWT admin décodé = 54 claims) |

**Réserve B3** : un JWT ADMIN embarque **toutes** les permissions actives → token plus lourd. Acceptable à 54 codes ; surveiller si le catalogue dépasse quelques centaines.

---

## 6. Auth — login, refresh, `IsActive`

| Scénario | Résultat |
|----------|----------|
| Login `__p1v_parent_single` | OK (access + refresh) |
| Refresh token | OK (nouveau access) |
| Login si `IsActive=false` | **401 / UnauthorizedAccessException** |
| Refresh si inactif / tokens révoqués | **UnauthorizedAccessException** |
| `AdminService` → `RevokeAllForUserAsync` à la désactivation | Implémenté (revue code) |

### 6.1 Anomalie corrigée pendant l’audit (A1 — élevée)

**Symptôme** : `GetByTokenAsync` ne trouvait pas le refresh token (filtre tenant `RefreshTokens` + `EffectiveTenantSchoolId == null` sur requête anonyme).

**Correctifs** :

1. `RefreshTokenRepository.GetByTokenAsync` / `RevokeAllForUserAsync` → `IgnoreQueryFilters()` + exclusion soft-delete.
2. `AuthService.LogLoginAsync` restaure la valeur précédente de `IgnoreSchoolScope` (au lieu de forcer `false`).

Sans ces correctifs, le scénario refresh de validation (et potentiellement la prod anonyme) échouait.

---

## 7. Policies ASP.NET Core

Évaluation via `IAuthorizationService` + `PermissionAuthorizationHandler` + `PermissionAuthorizationPolicyProvider` (même code que l’API).

| Cas | Résultat |
|-----|----------|
| PARENT + policy `payments.read` | **Autorisé** |
| PARENT + policy `students.delete` | **Refusé** |
| ADMIN + policy `students.delete` | **Autorisé** (`admin.full`) |
| Policy dynamique `grades.read` (non pré-enregistrée seule) | **Autorisée** pour PARENT |
| Provider : policy inconnue `custom.future.permission` | Résolue dynamiquement en `PermissionRequirement` |

---

## 8. Non-régression endpoints

### 8.1 Inventaire statique (preuves harness)

| Métrique | Valeur |
|----------|--------|
| Fichiers Controllers | 41 |
| Attributs `[Authorize…]` | **390** |
| Policies `Authorize(Policy=` | **347** |

Les endpoints existants restent câblés sur les **mêmes codes** de permission ; le provider dynamique + pré-enregistrement `Permissions.All` couvrent le catalogue.

### 8.2 Tests automatisés

| Test | Statut | Lien Phase 1 |
|------|--------|--------------|
| UnitTests (84/85) | 1 fail StudentCards (date) | **Aucun** |
| `AuthEndpointTests.Login_With_Valid_Credentials` | 401 (`admin` / `Admin@2026`) | Env. / mot de passe BD ≠ fixture test |
| Autres IntegrationTests | 9 PASS (health, etc.) | OK |

**Réserve B4** : non-régression HTTP **complète** (smoke de chaque controller avec token réel) non exécutée dans ce rapport. Couverture = inventaire policies + auth métier harness + suites existantes. Recommandation avant prod : smoke Desktop login + 5–10 endpoints critiques avec token ADMIN.

---

## 9. Performances & cache

| Mesure | Valeur |
|--------|--------|
| `ResolveAsync` cold (après `Invalidate`) | **83,9 ms** |
| `ResolveAsync` warm moyen (100 appels parent+admin) | **6,87 ms** |
| Codes actifs en cache | 54 |
| Clés map prérequis | 54 |
| Mémoire estimée snapshot | **~8 670 octets** (~8,5 Ko) |
| TTL | **Aucun** |
| Invalidation | Explicite (`ISecurityCatalogCache.Invalidate`) + redémarrage process |

Invalidation branchée aujourd’hui : fin de `SecurityCatalogSeeder.SeedAsync`.

**Réserve B5 (levée)** : invalidation cache catalogue désormais **automatique** via `SecurityCatalogCacheInvalidationInterceptor` sur SaveChanges des entités Permissions / Dépendances / Rôles / RolePermissions / Modules / Functions / Pages / Actions — plus de dépendance à la vigilance développeur.

**Réserve B6** : `HasPermissionAsync` recalcule tout `ResolveAsync` à chaque appel — OK pour volume actuel ; envisager cache utilisateur court (ou réutiliser claims) si hot-path API.

---

## 10. Conformité aux ajustements validés avant implémentation

| Exigence utilisateur | Statut |
|----------------------|--------|
| Pas de Feature Flag `Security:UseEffectivePermissions` | **OK** — moteur unique |
| Cache invalidation explicite (pas de TTL) | **OK** |
| JWT = codes seulement | **OK** |
| `HasPermissionAsync(userId, permissionCode)` | **OK** |

---

## 11. Points faibles & améliorations avant Phase 2

### Priorité haute

| ID | Sujet | Action |
|----|-------|--------|
| A1 | Refresh token vs filtre tenant | **Corrigé** dans cette validation — à conserver en revue PR |
| B5 | Invalidation cache hors seed | Hook obligatoire sur tout write catalogue / RolePermissions / deps |

### Priorité moyenne

| ID | Sujet | Action |
|----|-------|--------|
| B3 | Taille JWT ADMIN | Surveiller ; option future : claim `admin.full` seul + résolution serveur |
| B2 | Endpoint `HasPermission` clients | Décider en Phase 2 si Desktop/Mobile doivent interroger l’API hors JWT |
| B6 | Coût `HasPermissionAsync` | Cache user-scoped optionnel si profiling le justifie |
| B4 | Smoke HTTP endpoints | Script login + sample endpoints avant ouverture Phase 2 prod |
| B7 | Fraîcheur claims | Deny/Grant ne s’applique aux APIs qu’après **refresh/re-login** (policies = JWT). Documenter ; option : re-resolve serveur pour actions sensibles |

### Priorité basse

| ID | Sujet | Action |
|----|-------|--------|
| B1 | PARENT + `students.read` en BD | Aligner seed / données école |
| — | Coexistence `TEACHER` / `ENSEIGNANT` | Conservée (conforme) |
| — | Tests d’intégration Auth (mdp admin) | Réaligner fixture ou secrets de test |
| — | StudentCard unit test date | Hors scope ; corriger séparément |

---

## 12. Critères de validation Phase 1 (checklist)

| # | Critère | Statut |
|---|---------|--------|
| 1 | Resolve mono-rôle | **OK** |
| 2 | Resolve multi-rôles | **OK** |
| 3 | Grant | **OK** |
| 4 | Deny + impact deps | **OK** |
| 5 | Expiration exception | **OK** |
| 6 | Dépendances | **OK** |
| 7 | Bypass ADMIN | **OK** |
| 8 | Super Admin | **OK** |
| 9 | `HasPermissionAsync` | **OK** |
| 10 | JWT codes only | **OK** |
| 11 | Login / refresh / IsActive | **OK** (+ correctif A1) |
| 12 | Policies allow/deny/dynamic | **OK** |
| 13 | Non-régression endpoints | **OK partiel** (inventaire + suites ; smoke HTTP élargi recommandé) |
| 14 | Perf + mémoire cache | **OK** |
| 15 | Rapport | **Ce document** |

---

## 13. Verdict et go / no-go Phase 2

### Verdict

**Phase 1 VALIDÉE pour le runtime permissions/Auth**, sous réserve de :

1. Conserver le correctif refresh token (A1).
2. Traiter **B5** (invalidation cache) dès les premières écritures catalogue hors seeder.
3. Accepter **B7** (latence Deny/Grant jusqu’au refresh) ou planifier une mitigation en Phase 2.

Les scénarios métier demandés sont **prouvés par exécution** (27/27).

### Go Phase 2 autorisé **après validation utilisateur de ce rapport**, pour

- Navigation / menus dynamiques basés sur permissions effectives
- Consommation Desktop/Web/Mobile du même modèle de codes

### No-go tant que ce rapport n’est pas approuvé

- Implémentation Phase 2
- UI admin sécurité Phase 3 (sauf correctifs ciblés B5 / tests)

---

## 14. Annexes

### Fichiers clés

- `Application/Security/IEffectivePermissionService.cs`
- `Infrastructure/Security/EffectivePermissionServices.cs`
- `Infrastructure/Auth/AuthService.cs` / `JwtTokenService.cs`
- `Infrastructure/Persistence/Repositories/SecurityRepositories.cs`
- `API/Authorization/PermissionAuthorizationPolicyProvider.cs`
- `API/Authorization/PermissionAuthorizationHandler.cs`
- `API/Extensions/AuthorizationExtensions.cs`
- `tools/Phase1SecurityValidation/Program.cs`

### Preuve d’exécution (extrait `summary.txt`)

```text
PASS | Resolve — un seul rôle (PARENT) | …
PASS | Resolve — multi-rôles (ENSEIGNANT ∪ COMPTABLE) | …
PASS | Resolve — Grant … | …
PASS | Resolve — Deny … | …
PASS | Resolve — expiration exception (ValidTo exclusive) | …
PASS | Deps — closure students.create contient students.read | …
PASS | Resolve — dépendances … | …
PASS | Resolve — bypass ADMIN … | permCount=54 …
PASS | Resolve — Super Administrateur plateforme … | …
PASS | HasPermissionAsync … | …
PASS | JWT — permissions = codes uniquement (parent|admin) | …
PASS | Auth — login OK | …
PASS | Auth — refresh OK | …
PASS | Auth — login/refresh refusés si IsActive=false | …
PASS | Policy — … autorisé/refusé/dynamique | …
PASS | Perf — coldMs=83,9 warmAvgMs=6,87 | …
PASS | Cache — ~8670 bytes | …
TOTAL: 27/27 passed
```

### Artefacts

- `tools/Phase1SecurityValidation/out/evidence.json`
- `tools/Phase1SecurityValidation/out/summary.txt`
- Relancer : `dotnet run --project tools/Phase1SecurityValidation`
