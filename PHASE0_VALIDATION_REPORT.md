# Rapport de validation Phase 0 — Moteur de sécurité

**Date** : 2026-08-07  
**Périmètre** : fondations données (schéma + seed), sans Phase 1 (calcul effectif / runtime Auth)  
**Environnement audité** : base locale `SchoolManagementRDC_Development` via `DesignTimeDbContextFactory`  
**Statut global** : **VALIDÉ AVEC RÉSERVES** — fondations exploitables pour la Phase 1 après traitement des anomalies P1 listées ci-dessous  

---

## 1. Synthèse exécutive

| Domaine | Résultat |
|---------|----------|
| Schéma (7 tables + colonnes) | OK en base |
| Contraintes FK / index / CHECK | OK |
| Relation Permission → SecurityAction (sans cycle) | OK |
| Seed idempotent (3 exécutions) | OK — comptes stables |
| Rôles francophones (`ENSEIGNANT`, etc.) | OK (+ legacy `TEACHER` conservé) |
| Compatibilité données existantes | OK (users/roles/assignments préservés) |
| Migration EF Up/Down | OK ciblée Phase 0 ; **réserve** drift Snapshot |
| Application via `dotnet ef database update` | **KO** sur historique EF ancien (contournement initialiseur) |
| CloudSyncCatalog | Présent ; **réserves** ordre FK + audit |

Aucune fonctionnalité Phase 1 n’a été développée dans le cadre de cet audit.

---

## 2. Migrations EF Core

### 2.1 Fichiers

- `src/SchoolManagement.Infrastructure/Persistence/Migrations/20260807081353_SecurityEnginePhase0Foundation.cs`
- Designer + `SchoolDbContextModelSnapshot` associés (modèle complet)

### 2.2 Up — contenu effectif

Ordre correct et cohérent avec le plan :

1. Colonnes `UserAccounts.IsPlatformSuperAdmin`, `Roles.IsSystem|IsAssignable|SortOrder`, `Permissions.DisplayName|BusinessDescription|HelpText|IsActive|SecurityActionId`
2. Backfill SQL des métadonnées permissions
3. Création `SecurityModules` → `SecurityFunctions` → `SecurityPages` → `SecurityActions`
4. `PermissionDependencies`, `SecurityAuditLogs`, `UserPermissionExceptions`
5. Index + FK `Permissions.SecurityActionId` → `SecurityActions` (`ON DELETE SET NULL`)

**Point positif** : le Up a été **réécrit** pour exclure le drift destructif généré initialement par EF (centaines d’opérations hors Phase 0).

### 2.3 Down — contenu effectif

- Drop FK `Permissions` → `SecurityActions`
- Drop tables nouvelles (dépendances, exceptions, audit, actions, pages, fonctions, modules)
- Drop index / colonnes Phase 0

**Verdict Down** : cohérent et inverse du Up Phase 0.  
**Limite** : non testé en production (pas d’exécution `Migrate Down` sur BD réelle durant l’audit). Recommandation : tester Down sur BD jetable avant tout rollback.

### 2.4 Anomalie — double voie d’application

| Mécanisme | Rôle |
|-----------|------|
| Migration EF | Historique / Snapshot |
| `SecurityEnginePhase0SchemaInitializer` | Application **réelle** au démarrage API (pattern projet) |

`dotnet ef database update` **échoue** sur des migrations antérieures non alignées avec la BD (ex. `AddPedagogicalStructure` / `SchoolId` déjà présent).  
L’initialiseur SQL idempotent contourne correctement ce problème et enregistre `20260807081353_SecurityEnginePhase0Foundation` dans `__EFMigrationsHistory` si absent.

**Anomalie A1 (moyenne)** : le Designer/Snapshot reflète le modèle global actuel, alors que le Up/Down Phase 0 est volontairement partiel. Un futur `ef migrations add` non filtré risque de regénérer du drift. Mitigation Phase 1 : discipline « Up manuels ciblés » + initialiseurs idempotents (comme aujourd’hui).

**Anomalie A2 (basse)** : duplication logique Migration ↔ Initializer. Toute évolution de schéma Phase 0+ doit être faite **aux deux endroits** (ou migrer entièrement vers un seul mécanisme).

### 2.5 Présence en base

| Contrôle | Résultat |
|----------|----------|
| `__EFMigrationsHistory` contient `…SecurityEnginePhase0Foundation` | Oui (`1`) |

---

## 3. Clés étrangères, index, contraintes, DeleteBehavior

### 3.1 Matrice DeleteBehavior (EF / SQL)

| Relation | DeleteBehavior | Évaluation |
|----------|----------------|------------|
| Function → Module | Restrict | OK |
| Page → Function | Restrict | OK |
| Action → Page | Restrict | OK |
| Permission → SecurityAction | SetNull | OK (pas de cycle) |
| Dependency.PermissionId → Permission | Cascade | OK |
| Dependency.RequiresPermissionId → Permission | Restrict | OK (évite multi-cascade SQL Server) |
| Exception → School / User / Permission / GrantedBy | Restrict | OK (évite multi-cascade UserAccounts) |
| AuditLog → School | SetNull | OK |

**Verdict** : conforme au plan figé (Permission → Action unique ; pas de `SecurityActions.PermissionId`).

### 3.2 Contraintes vérifiées en base

| Contrôle | Résultat |
|----------|----------|
| FK `FK_Permissions_SecurityActions_SecurityActionId` | Présente |
| CHECK `CK_PermissionDependencies_NoSelf` | Présente |
| UX `(PermissionId, RequiresPermissionId)` | Présente |
| Auto-dépendances (`PermissionId = RequiresPermissionId`) | **0** |

### 3.3 Index uniques et soft-delete

Les index uniques (`Roles(SchoolId,Code)`, `SecurityModules(Code)`, etc.) **n’excluent pas** `IsDeleted = 1`.  
Le seeder gère ce cas pour Roles / RolePermissions via `IgnoreQueryFilters` + réactivation.

**Anomalie A3 (basse)** : un soft-delete manuel d’un module/page sans réactivation peut bloquer un ré-insert du même `Code`. Acceptable si l’admin réactive plutôt que recrée ; documenter en Phase 3 UI.

### 3.4 Exceptions utilisateur

Index de lookup `(UserId, PermissionId, Effect, ValidFrom, ValidTo)` **non unique** — correct (historique de fenêtres). Pas de chevauchement interdit en Phase 0 (validation métier = Phase 1/3).

---

## 4. Idempotence du `SecurityCatalogSeeder`

### 4.1 Protocole

1. `SecurityEnginePhase0SchemaInitializer.EnsureCreatedAsync`
2. `SecurityCatalogSeeder.SeedAsync` × **3**
3. Comparaison des compteurs

### 4.2 Résultats mesurés

| Run | Durée | Modules | Functions | Pages | Actions | Perms | Deps | Roles | RolePerms | Exceptions | Audit | SuperAdmin |
|-----|-------|---------|-----------|-------|---------|-------|------|-------|-----------|------------|-------|------------|
| 1 | 1185 ms | 13 | 19 | 40 | 40 | 54 | 34 | 9 | 110 | 0 | 0 | 0 |
| 2 | 578 ms | 13 | 19 | 40 | 40 | 54 | 34 | 9 | 110 | 0 | 0 | 0 |
| 3 | 518 ms | 13 | 19 | 40 | 40 | 54 | 34 | 9 | 110 | 0 | 0 | 0 |

**`IDEMPOTENT_COUNTS = True`**

### 4.3 Analyse

- Pas de duplication de modules / pages / dépendances / permissions.
- Runs 2–3 ~2× plus rapides (chemins « exists → skip/update léger »).
- Rôles : upsert avec `IgnoreQueryFilters` (corrigé après échecs initiaux sur index unique).
- RolePermissions : idempotent avec réactivation soft-delete.

**Verdict idempotence** : **OK** pour 1 / 2 / N exécutions sur la BD audité.

### 4.4 Limites d’idempotence (non bloquantes)

- Métadonnées permission : si `DisplayName` a déjà une valeur non vide différente du seed, elle **n’est pas écrasée** (volontaire). Re-seed n’« impose » pas le libellé FR à chaque fois.
- `SecurityActionId` n’est renseigné que si `null` — ne réassigne pas si action changée manuellement.

---

## 5. Tables et données générées

### 5.1 Tables Phase 0

| Table | Présente |
|-------|----------|
| SecurityModules | Oui |
| SecurityFunctions | Oui |
| SecurityPages | Oui |
| SecurityActions | Oui |
| PermissionDependencies | Oui |
| UserPermissionExceptions | Oui (vide) |
| SecurityAuditLogs | Oui (vide) |

### 5.2 Catalogue navigation

**Modules (13)** :  
`DASHBOARD`, `SETTINGS`, `PERSONNEL`, `STUDENTS`, `STUDENT_CARDS`, `ACADEMIC`, `PEDAGOGICAL_CALENDAR`, `GRADES`, `RESULTS`, `FINANCE`, `DOCUMENTS`, `STATISTICS`, `SECURITY`

| Métrique | Valeur | Attendu plan |
|----------|--------|--------------|
| Functions | 19 | — |
| Pages | 40 | ≥ menus Desktop + hubs |
| Actions | 40 | ≥ 1 OPEN / page |
| Pages sans `DesktopViewKey` | **0** | 0 |
| Permissions liées à une Action | 20 | partielle OK (Phase 0) |

### 5.3 Permissions

| Contrôle | Résultat |
|----------|----------|
| Nombre | 54 (= constantes `Permissions.All`) |
| DisplayName vides | 0 |
| HelpText vides | 0 |
| BusinessDescription / HelpText | renseignés |

Nouvelles permissions catalogue : `security.*`, `platform.*` (présentes ; **non utilisées** par le runtime Phase 0 — normal).

### 5.4 Dépendances

| Contrôle | Résultat |
|----------|----------|
| Arêtes | 34 |
| Cycles auto | 0 |
| Exemples couverts | create/update/delete → read ; validate/lock → read ; etc. |

### 5.5 Rôles système

Codes actifs distincts :  
`ADMIN`, `DIRECTION`, `ENSEIGNANT`, `PARENT`, `COMPTABLE`, `CAISSIER`, `PREFET`, `PROMOTEUR`, **`TEACHER` (legacy)**

| Contrôle | Résultat |
|----------|----------|
| `ENSEIGNANT` | Présent (1) |
| `TEACHER` legacy | Présent (1) — **non renommé** (conforme décision) |
| Rename ENSEIGNANT→TEACHER | Absent (conforme) |

### 5.6 Exceptions / Audit / Super Admin

| Contrôle | Résultat |
|----------|----------|
| UserPermissionExceptions | 0 ligne |
| SecurityAuditLogs | 0 ligne |
| `IsPlatformSuperAdmin = 1` | 0 (pas d’auto-promotion) |

---

## 6. Compatibilité avec les données existantes

| Donnée | Impact observé |
|--------|----------------|
| `UserAccounts` (6 actifs) | Conservés ; colonne Super Admin = 0 |
| `UserRoleAssignments` | Non détruits par le seed |
| `RolePermissions` | Enrichis (ajout manquants), pas de purge |
| `Permissions` historiques | Enrichies (colonnes + métadonnées) |
| Login / JWT / menus Desktop | Non modifiés par Phase 0 (hors brancher runtime) |

**Verdict** : migration sans perte métier constatée sur la BD de développement.

---

## 7. CloudSyncCatalog — risques

### 7.1 Entrées ajoutées

Ordre actuel (extrait) :

1. `Permissions`
2. `SecurityModules` → `Functions` → `Pages` → `Actions`
3. `PermissionDependencies`
4. `Roles` / `RolePermissions` / `UserAccounts` / …
5. `UserPermissionExceptions`
6. `SecurityAuditLogs`

### 7.2 Anomalies

**A4 (élevée pour sync cloud)** — Ordre FK `Permission.SecurityActionId` → `SecurityActions` :  
`Permissions` est synchronisé **avant** `SecurityActions`. Une permission avec `SecurityActionId` renseigné peut échouer en cloud (FK manquante).  
`EnsureStructuralParentsAsync` gère `RolePermission` / `UserRoleAssignment` / `Permission` (SchoolId) mais **pas** Module/Function/Page/Action ni `Permission.SecurityActionId`.

**A5 (moyenne)** — `SecurityAuditLogs` dans le sync : risque de volume, conflits d’IDs, et peu de valeur à répliquer. Préférer **exclure** le journal d’audit du sync cloud (écriture locale uniquement).

**A6 (basse)** — Catalogue global (sans `SchoolId`) + tenancy sync : vérifier que l’upsert cloud ne duplique pas les modules par école. Les entités globales doivent être traitées comme référentiel partagé (même Id).

### 7.3 Recommandations Cloud avant Phase 1 (ou en parallèle)

1. Réordonner SyncOrder : Modules → Functions → Pages → Actions → **Permissions** → Dependencies → Roles…
2. Étendre `EnsureStructuralParentsAsync` pour `SecurityFunction/Page/Action` et `Permission.SecurityActionId`
3. Retirer `SecurityAuditLogs` du catalogue sync (ou le marquer non-sync)

---

## 8. Performances

### 8.1 Seed

| Observation | Détail |
|-------------|--------|
| 1er run | ~1,2 s (acceptable) |
| Runs suivants | ~0,5 s |
| Pattern | Nombreux `SaveChanges` unitaires + `FirstOrDefault` par entité (N+1) |

**A7 (basse / perf)** : le seed navigation fait un round-trip par module/fonction/page/action. Suffisant pour Phase 0 ; optimiser (batch / cache dictionnaire) si le catalogue grossit fortement.

### 8.2 Requêtes principales futures (Phase 1)

Index déjà utiles :

- UX permission dependencies
- Index exceptions par user / school
- Index audit par school / action type
- Index `Permissions.SecurityActionId`, `SecurityPages.DesktopViewKey`

**Manque anticipé Phase 1** : pas d’index filtré « exceptions actives à `now` » — à évaluer si volumes élevés (`ValidFrom`/`ValidTo`).

---

## 9. Points faibles et améliorations avant Phase 1

### Priorité haute

| ID | Sujet | Action recommandée |
|----|-------|--------------------|
| A4 | Ordre CloudSync Permissions vs Actions | Réordonner + parents structurels |
| A5 | Sync des `SecurityAuditLogs` | Exclure du sync |

### Priorité moyenne

| ID | Sujet | Action recommandée |
|----|-------|--------------------|
| A1/A2 | Drift Snapshot / double schéma | Conserver discipline initialiseur + Up ciblé ; documenter pour l’équipe |
| — | Coexistence `TEACHER` / `ENSEIGNANT` | Phase 1 : alias dans résolution de rôles / HasRole |
| — | `Permissions.All` élargi | Policies ASP.NET enregistrent aussi `security.*` / `platform.*` dès le démarrage — OK, mais s’assurer qu’aucun endpoint ne les exige avant l’UI admin |

### Priorité basse

| ID | Sujet | Action recommandée |
|----|-------|--------------------|
| A3 | Uniques vs soft-delete | Documenter / filtres uniques filtrés plus tard |
| A7 | Perf seed N+1 | Batch si besoin |
| — | Seulement 20/54 permissions liées à une Action | Enrichir le lien catalogue en Phase 2/3 |
| — | Down non testé sur BD jetable | Test ponctuel avant rollback réel |

---

## 10. Critères de validation Phase 0 (reprise plan)

| # | Critère | Statut |
|---|---------|--------|
| 1 | Migration Up applicable (via initialiseur + historique) | **OK** |
| 2 | 7 tables + FK/index | **OK** |
| 3 | Colonnes Permissions/Roles/UserAccounts backfillées | **OK** |
| 4 | Permissions All + security/platform + Display/Help | **OK** |
| 5 | Dependencies sans cycle / auto-lien | **OK** |
| 6 | Modules/pages DesktopViewKey | **OK** (40 pages, 0 sans clé) |
| 7 | Rôles francophones, pas de rename ENSEIGNANT→TEACHER | **OK** |
| 8 | Exceptions/Audit vides | **OK** |
| 9 | Runtime Auth non branché (hors scope de ce rapport d’exécution, code conforme) | **OK** (revue code) |
| 10 | Pas de Super Admin auto | **OK** |
| 11 | Rapport de validation | **Ce document** |

---

## 11. Verdict et go / no-go Phase 1

### Verdict

**Phase 0 VALIDÉE pour démarrer la Phase 1**, sous réserve de traiter **A4** et **A5** (Cloud Sync) avant mise en production sync active, idéalement en tout début de Phase 1 ou correctif immédiat hors feature Auth.

Les fondations (schéma, seed idempotent, rôles, dépendances, HelpText, FK mono-sens) sont **solides**.

### Go Phase 1 autorisé pour

- `IEffectivePermissionService` (rôles ∪ grants \ denies + fermeture dépendances)
- Branchement login / refresh + contrôle `IsActive`
- Policies depuis BD (avec fallback aliases)

### No-go tant que non approuvé

- Développement UI navigation dynamique (Phase 2)
- UI admin rôles/exceptions (Phase 3)
- Toute feature hors correctifs A4/A5 listés

---

## 12. Annexes

### Fichiers clés audités

- `SecurityCatalog.cs` (entités)
- `SecurityConfigurations.cs`
- `SecurityEnginePhase0SchemaInitializer.cs`
- `20260807081353_SecurityEnginePhase0Foundation.cs`
- `SecurityCatalogSeeder.cs`
- `CloudSyncCatalog.cs` / `CloudSyncEngine.EnsureStructuralParentsAsync`
- `DatabaseSeeder.SeedSystemAsync` (appel catalogue)
- `Program.cs` (hook initialiseur)

### Preuve d’exécution

Outil temporaire `ErpPhase0Validate` (DesignTime + 3× seed), sortie :

```text
RUN1 1185ms … Perm=54 Dep=34 Roles=9 …
RUN2 578ms … (identique)
RUN3 518ms … (identique)
IDEMPOTENT_COUNTS=True
ROLE_CODES=ADMIN,CAISSIER,COMPTABLE,DIRECTION,ENSEIGNANT,PARENT,PREFET,PROMOTEUR,TEACHER
```

---

## Correctifs post-validation Phase 0 (appliqués)

- **A4** : `CloudSyncCatalog.SyncOrder` — Modules → Functions → Pages → Actions → **Permissions** → Dependencies → Roles… ; parents structurels étendus dans `CloudSyncEngine`.
- **A5** : `SecurityAuditLogs` **retiré** du sync cloud (journal local uniquement).
- Bootstrap référentiel `EnsureFinanceReferenceDataAsync` aligné sur le même ordre catalogue.
