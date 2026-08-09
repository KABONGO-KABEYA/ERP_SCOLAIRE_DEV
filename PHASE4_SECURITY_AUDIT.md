# Audit sécurité — Phase 4 (migration vers le moteur de permissions)

**Date** : 2026-08-07  
**Statut** : **Document de référence** — base du plan d’exécution Phase 4  
**Périmètre** : dépôt `ERP_Administration_Scolaire_2026` (`src/`, outils de validation, hors modifications de code)  
**Contexte** : Phases 0–3 clôturées (catalogue BD, policies dynamiques, navigation, admin sécurité, catalogue Super Admin).  
**Consigne** : **aucune implémentation Phase 4** avant validation explicite de cet audit.

---

## 1. Synthèse exécutive

| Zone | État actuel | Effort migration estimé |
|------|-------------|-------------------------|
| **API — controllers « métier »** | ~80 % des routes protégées par policies **granulaires** (`Permissions.*`) | Moyen (compléter + aligner `admin.full`) |
| **API — bloc `admin.full`** | **49** attributs `[Authorize(Policy = Permissions.AdminFull)]` sur **9** controllers | Élevé (définir permissions métier manquantes) |
| **API — trou de policy** | `GeographyController` : **authentifié seulement** (6 GET) | Faible à moyen |
| **Couche Application** | Logique **rôles codés en dur** (cotation, validation résultats, calendrier pédagogique) | **Critique** |
| **Desktop** | Menu dynamique Phase 2 ; **garde-fous UI** surtout `IsAdministrator` / rôles JWT | Élevé |
| **Alias `IsAdministrator`** | Équivalent **ADMIN rôle OU `admin.full`** — bypass large | Central à uniformiser |

**Constat principal** : le moteur Phase 1–3 (effectif BD, policies, navigation) est **en place**, but **l’autorisation métier fine** repose encore sur **`IsAdministrator`**, le **code rôle JWT** (`HasRole` / `HasElevatedRole`) et la policy **`admin.full`** comme fourre-tout.

---

## 2. Méthodologie d’audit

- Recherche statique (`rg`) sur `*.cs` / patterns d’autorisation, revue des controllers API (44 fichiers), `ICurrentUserService`, Desktop `IAuthSessionService`, services Application concernés.
- Référence des permissions canoniques : `SchoolManagement.Shared.Constants.Permissions`.
- **Non audité en profondeur** : applications mobiles consommatrices, tests unitaires isolés, scripts ops (sauf harness Phase 1–3).

---

## 3. Usages de `HasRole(...)`

### 3.1 Méthode nommée `HasRole` (Application)

| Fichier | Rôle |
|---------|------|
| `Application/Grades/Services/GradeService.Cotation.cs` | `private bool HasRole(params string[] codes)` — lit `_currentUser.Roles` (claims JWT) |

**Appels dans `ResolveCotationAccessScope()`** :

| Condition | Rôles codés | Effet |
|-----------|-------------|--------|
| Accès élargi | `ADMIN`, `DIRECTION`, `PROMOTEUR` (+ `IsAdministrator` / `admin.full`) | `CotationAccessScope.Full` |
| Préfet | `PREFET`, `PREFET_ETUDES`, `PREFÉT`, `PREFÉT_ÉTUDES` | `CotationAccessScope.Prefet` |
| Titulaire | `TITULAIRE` | `CotationAccessScope.ClassHolder` |
| Défaut | — | `CotationAccessScope.Teacher` (périmètre enseignant) |

**Risque** : le rôle **`TITULAIRE`** n’apparaît **pas** dans le seed standard (`SecurityCatalogSeeder` / `InitialSetupService`) — logique possiblement **morte** ou alimentée par données manuelles.

### 3.2 Équivalent `HasElevatedRole` (Application)

| Fichier | Méthode | Rôles |
|---------|---------|-------|
| `Application/ResultValidation/Services/ResultValidationService.cs` | `HasElevatedRole(params string[] codes)` | Validation : `DIRECTION`, `PREFET`, `PROMOTEUR`, `ADMIN` — Lock/Unlock : `ADMIN`, `PROMOTEUR` |

Combiné avec `IsAdministrator`, `HasPermission(ResultsValidation*)` et **`HasPermission(AdminFull)` en double**.

### 3.3 Contrôles « style HasRole » sans méthode `HasRole`

| Fichier | Pattern |
|---------|---------|
| `Application/PedagogicalPeriods/Services/PedagogicalPeriodService.cs` | `EnsureAdministrator()` : `IsAdministrator` \|\| `admin.full` \|\| rôles `ADMIN`, `DIRECTION` |
| `Desktop/ViewModels/GradesViewModel.Cotation.cs` | `isElevated` : `IsAdministrator` \|\| rôles `ADMIN`, `DIRECTION`, `PROMOTEUR`, `PREFET`, `PREFET_ETUDES` |

### 3.4 `HasRole` absent ailleurs

Aucune autre occurrence de `HasRole(` dans `src/` (hors définition ci-dessus).  
Les rôles restent présents dans **JWT** (`ClaimTypes.Role`) via `JwtTokenService` — consommation indirecte partout où `ICurrentUserService.Roles` est utilisé.

---

## 4. Vérifications `IsAdmin` / `IsAdministrator`

### 4.1 Contrat `ICurrentUserService.IsAdministrator`

Définition (API / Application) :

- `Infrastructure/Services/HttpContextCurrentUserService.cs`
- `Infrastructure/Services/InfrastructureServices.cs` (variantes tests / fallback)

**Règle actuelle** :

1. Permission **`admin.full`** dans claims / liste permissions, **ou**
2. Rôle JWT **`ADMIN`** (ou chaîne contenant `"ADMIN"` — **matching partiel**).

### 4.2 Desktop `IAuthSessionService.IsAdministrator`

`Desktop/Services/ApiServices.cs` (`AuthSessionService`) — **même sémantique** : `admin.full` ou rôle `ADMIN` / contient `ADMIN`.

**Écart Desktop / API** : le Desktop **ne recalcule pas** l’effectif BD ; il s’appuie sur le **profil login** (`UserProfileDto.Permissions` + `Roles`).

### 4.3 Usages métier `IsAdministrator` (hors infra)

| Couche | Fichier | Usage |
|--------|---------|--------|
| Application | `GradeService.cs` | Suppression évaluation si notes existantes — **admin seulement** |
| Application | `GradeService.Cotation.cs` | `EnsureCanEnterGrades`, `ResolveCotationAccessScope` |
| Application | `PedagogicalPeriodService.cs` | `EnsureAdministrator()` mutations calendrier |
| Application | `ResultValidationService.cs` | `CanValidatePermission` / Lock / Unlock |
| Application | `PaymentService.cs` | Override FX si pas `PaymentFxOverride` |
| Application | `PaymentMutationPolicy.cs` | Mutations paiements encaissés — **admin uniquement** |
| Desktop VM | `EncaissementsViewModel`, `CollectPaymentViewModel`, `ExpenseMultiCurrencyAllocationViewModel` | Paiements payés, override taux |
| Desktop VM | `PricingCategoryAssignmentViewModel` | Affectation catégories tarifaires |
| Desktop View | `EncaissementActionWindow.xaml.cs` | `_canMutatePaidPayments` |
| Desktop VM | `GradesViewModel.Cotation.cs` | Élévation UI cotation |
| Infrastructure | `SecurityAdminServices.cs` | `IsAdminRole(Role)` — rôle code **`ADMIN`** (matrice système) |
| Infrastructure | `EffectivePermissionServices.cs` | Injection **`admin.full`** si rôle ADMIN |

### 4.4 `IsAdminRole` (domaine rôles BD)

`SecurityAdminServices.cs` : `string.Equals(role.Code, "ADMIN")` — verrouillage matrice rôle système (Phase 3, **hors** migration HasRole).

---

## 5. Utilisations de `AdminFull` (`admin.full`)

### 5.1 Policy ASP.NET `[Authorize(Policy = Permissions.AdminFull)]`

**Total recensé : 49 endpoints** sur les controllers suivants :

| Controller | Nb | Commentaire |
|------------|-----|-------------|
| `GeographyAdminController` | 18 | CRUD référentiel géographique admin |
| `PersonnelController` | 9 | **100 %** du controller |
| `PedagogicalPeriodsController` | 8 | Mutations calendrier (1 GET reste `grades.read`) |
| `AdminController` | 4 | **Teachers** CRUD (`users` déjà `security.users.manage`) |
| `UpdateController` | 3 | Mises à jour applicatives |
| `PaymentsController` | 3 | `cancel`, `notes`, 1 autre opération sensible |
| `CloudSyncController` | 2 | Sync cloud |
| `FinanceController` | 1 | Endpoint admin financier |
| `ParentActivationIssueController` | 1 | Support activation parent |

### 5.2 `admin.full` hors attributes

| Zone | Usage |
|------|--------|
| `PermissionAuthorizationHandler` | Succès policy si permission requise **ou** `admin.full` |
| `SecurityNavigationService` | Bypass filtre page si `hasAdminFull` |
| `EffectivePermissionServices` | Expansion effectif rôle ADMIN |
| `SecurityCatalogSeeder` | Pages nav : Géographie, Sync, Mises à jour, **Personnel**, **Calendrier pédagogique** |
| `SecurityAdminController` | Fallback lecture liste rôles / catalogue permissions |
| Application | Doublons explicites `HasPermission(AdminFull)` dans ResultValidation |

### 5.3 Permissions granulaires **sans** équivalent dédié aujourd’hui

Modules protégés quasi exclusivement par **`admin.full`** :

- **Personnel** (API + navigation)
- **Géographie admin** (API admin vs lecture `GeographyController`)
- **Calendrier pédagogique** (API + navigation)
- **Cloud sync / Updates**
- **Enseignants** (`AdminController` teachers)
- Certaines **mutations paiements** (annulation / notes)

→ Phase 4 devra **créer / assigner** des codes permission (ex. `personnel.*`, `geography.manage`, `pedagogical-periods.manage`, `payments.cancel`, `updates.manage`, `cloud-sync.manage`) ou réutiliser des actions catalogue existantes.

---

## 6. Policies historiques et modèles d’autorisation legacy

| Mécanisme | Statut | Détail |
|-----------|--------|--------|
| Policy **`admin.full`** | **Legacy fourre-tout** | 49 endpoints + navigation + handler bypass |
| Rôle JWT **`ADMIN`** | Legacy | Alimente `IsAdministrator` même sans effectif BD recalculé côté Desktop |
| Rôles métier **`DIRECTION`, `PREFET`, `PROMOTEUR`, `ENSEIGNANT`, …** | Legacy actif | Cotation + validation résultats + calendrier |
| **`UserRole` enum** (Domain) | Métadonnée seed | Lié aux rôles système / seed, pas aux policies API directement |
| Claims **`ClaimTypes.Role`** | Actif | Toujours émis au login |
| **`[Authorize]` seul** (sans policy) | **Gap** | Voir §7 |
| Policies **`security.*` / `platform.*`** | **Nouveau moteur** | Phase 3 — référence cible |
| Policies **granulaires métier** (`students.*`, `grades.*`, …) | **Nouveau moteur** | Majorité des controllers finance, élèves, résultats |

**Note** : il n’existe pas d’anciennes policies nommées différemment de `Permissions.*` dans les controllers — l’historique est surtout **`admin.full` + rôles**.

---

## 7. Contrôles d’autorisation codés en dur

### 7.1 API — endpoints authentifiés sans policy granulaire

| Controller | Problème |
|------------|----------|
| **`GeographyController`** | 6 GET (pays, provinces, villes, communes, adresse) — **`[Authorize]` uniquement** : tout utilisateur connecté de l’école |
| **`SecurityNavigationController`** | GET navigation — filtrage **service** (effectif), pas policy par route |
| **`DocumentBrandingController`** | GET `logos/primary/file` — `[Authorize]` seul (fichier logo) |
| **`SecurityAdminController`** | GET `roles` / GET permissions catalog — `[Authorize]` + **if manuel** `HasPermission` (3 codes + `admin.full`) |
| **`MobileSubscriptionController`** | GET subscription — authentifié ; logique premium **hors** moteur permissions |
| **`AuthController`** | Endpoints profil / logout — `[Authorize]` (acceptable) |

### 7.2 Application — règles métier non alignées sur `HasPermission` seul

| Service | Règle codée |
|---------|-------------|
| `GradeService.Cotation` | Périmètre classes par **rôle** (prefet, titulaire, enseignant) |
| `ResultValidationService` | Validation / lock / unlock : rôles **+** permissions **+** `IsAdministrator` |
| `PedagogicalPeriodService` | Mutations : admin / direction |
| `PaymentMutationPolicy` | **`IsAdministrator` only** (pas `payments.*`) |
| `TeacherService` | Accès classe : lien enseignant–classe ( **ownership** ), pas permission catalogue |
| `ParentService` | Accès élève : lien parent–élève |
| `GradeService` | Suppression évaluation avec notes : `IsAdministrator` |

### 7.3 Desktop — UI non pilotée par effectif (sauf navigation)

| Composant | Contrôle |
|-----------|----------|
| Finance (encaissements, catégories tarifaires, dépenses) | `IsAdministrator` |
| Cotation | Rôles + `IsAdministrator` |
| **Reste des écrans** | Visibilité via **menu dynamique** uniquement ; actions souvent **sans** `HasPermission` local |

**Exceptions positives** : `CollectPaymentViewModel` / `ExpenseMultiCurrencyAllocationViewModel` testent aussi **`payment-fx.update`**.

---

## 8. Composants **non** alignés sur le nouveau moteur (cartographie)

### 8.1 Déjà alignés (référence Phase 3)

- `SecurityAdminController`, `PlatformCatalogController`, `AdminController` (users/roles)
- Controllers avec policies **Students*, Schools*, Grades*, Payments* (partiel), Results*, Deliberation*, Accounting*, Currencies*, …** (majorité)
- Navigation Desktop **Phase 2** (`ISecurityNavigationService` + `DesktopViewRegistry`)
- Écrans **Security.*** + **Platform.Catalog**
- Auth login / refresh / JWT permissions codes (Phase 1)

### 8.2 Partiellement alignés (policy API OK, logique service legacy)

| Module | API | Service / Desktop |
|--------|-----|-------------------|
| **Cotation** | `grades.*` | **Rôles** cotation + VM |
| **Validation résultats** | `results-validation.*` | **HasElevatedRole** |
| **Calendrier pédagogique** | **`admin.full`** | **EnsureAdministrator** + rôles |
| **Paiements** | `payments.*` + **`admin.full`** (cancel) | **PaymentMutationPolicy** |
| **Périodes / notes** | `grades.read` (1 route pédago) | Service admin legacy |

### 8.3 Faible couverture moteur

| Module | Gap |
|--------|-----|
| **Personnel** | API 100 % `admin.full` ; nav `admin.full` |
| **Géographie** | Admin : `admin.full` ; lecture : **aucune policy** |
| **Enseignants admin** | `AdminController` teachers : `admin.full` |
| **Cloud / Updates** | `admin.full` |
| **Mobile subscription** | Hors catalogue permissions |
| **Desktop (global)** | Pas de service **`IEffectivePermissionClient`** pour boutons / champs |

---

## 9. Risques de régression (migration)

| ID | Risque | Impact | Mitigation recommandée |
|----|--------|--------|------------------------|
| R4-1 | Retrait **`admin.full`** sans permission de remplacement | **403** massifs ADMIN / COMPTABLE | Matrice par rôle seedée + période dual-check |
| R4-2 | Remplacement **HasRole cotation** par permissions | Enseignants / préfets perdent ou gagnent des classes | Tests harness **par persona** (ENSEIGNANT, PREFET, TITULAIRE) |
| R4-3 | **`IsAdministrator`** sur Desktop ≠ effectif BD | Boutons visibles mais API 403 (ou inverse) | Aligner Desktop sur **liste permissions login** ou endpoint effectif |
| R4-4 | Rôle **ADMIN** vs permission **`admin.full`** | COMPTABLE avec permissions fines mais pas ADMIN | Ne plus étendre `IsAdministrator` au seul rôle ADMIN à terme |
| R4-5 | **`Contains("ADMIN")`** dans matching rôle | Faux positifs (rôle custom) | Supprimer matching partiel |
| R4-6 | Navigation seed vs API | Menu visible, API refuse | Migrer **paires** nav `RequiredPermissionCode` + controller policy |
| R4-7 | **TITULAIRE** / rôles non seedés | Comportement implicite | Inventaire BD prod + normaliser rôles |
| R4-8 | **GeographyController** ouvert | Fuite référentiel | Décision produit : `schools.read` vs public authentifié |
| R4-9 | **Teacher/Parent** ownership | Hors RBAC classique | Garder contrôles **contextuels** + policies minimales |
| R4-10 | Caches effectif / navigation | Stale après changement rôle | Réutiliser invalidation Phase 1–2 ; tests E2E login |

---

## 10. Stratégie de migration progressive (sans interruption)

Principe : **strangler par module**, chaque lot = *permissions catalogue + seed rôles + API policies + service rules + Desktop UI + harness*.

### Phase 4.0 — Cadrage (sans code métier)

- Valider cet audit + matrice **personas** (ADMIN, DIRECTION, PREFET, ENSEIGNANT, COMPTABLE, CAISSIER, PARENT).
- Définir conventions : **plus de `HasRole` en Application** ; **`IsAdministrator`** réservé temporaire ; cible = **`HasPermission` + ownership**.

### Lot 1 — Infrastructure transverse (faible risque visible)

- Documenter / tester **`ICurrentUserService`** : une sémantique `IsAdministrator` (deprecated).
- Desktop : helper **`Can(permission)`** basé sur `CurrentUser.Permissions` (sans rôle ADMIN string).
- Harness Phase 4 : scénarios par persona (extension Phase 3).

### Lot 2 — Finance & paiements

- Introduire permissions fines : ex. `payments.cancel`, `payments.notes.update`, `payments.paid-mutation` (noms à valider).
- Remplacer `PaymentMutationPolicy.EnsureAdministrator` + endpoints `PaymentsController` `admin.full`.
- Desktop Encaissements : remplacer `IsAdministrator` par permissions.
- **Feature flag** : accepter `admin.full` **OU** nouvelle permission (1 sprint).

### Lot 3 — Référentiels admin (Personnel, Géographie admin, Updates, Cloud)

- Créer permissions module + migrer controllers **`PersonnelController`**, **`GeographyAdminController`**, **`UpdateController`**, **`CloudSyncController`**.
- Mettre à jour **SecurityCatalogSeeder** (pages nav).
- `AdminController` teachers → `personnel.manage` ou réutiliser extension teachers.

### Lot 4 — Calendrier pédagogique

- Remplacer **`PedagogicalPeriodsController`** `admin.full` + `PedagogicalPeriodService.EnsureAdministrator`.
- Permission dédiée alignée sur nav `PedagogicalPeriods.Main`.

### Lot 5 — Cotation & résultats (risque élevé)

- Modéliser périmètres **prefet / titulaire / enseignant** en permissions ou **claims métier** calculés (ex. `grades.scope.class`, `grades.scope.school`) — **atelier métier requis**.
- Supprimer `HasRole` / `HasElevatedRole` au profit d’effectif + règles **TeacherAssignment** explicites.
- `GradeService` suppression évaluation : permission `grades.delete` ou `grades.admin`.
- Desktop `GradesViewModel` : aligner élévation UI.

### Lot 6 — Finitions API & gouvernance

- **`GeographyController`** : policy lecture explicite.
- **`ResultValidationService`** : une source de vérité (`HasPermission` only).
- Déprécier **`admin.full`** sur rôles non ADMIN (matrice) ; conserver pour rôle système ADMIN jusqu’à dernière étape.
- Mobile : aligner feature flags sur permissions parent / abonnement (hors scope immédiat si produit distinct).

### Règles opérationnelles

1. **Ordre** : seed permission → assignation rôles → API → service → Desktop → nav.  
2. **Dual-run** : handler accepte ancien + nouveau chemin pendant 1 release.  
3. **Rollback** : flags configuration pour réactiver `admin.full` par module.  
4. **Validation** : harness Phase 4 + non-régression 1–3 à chaque lot.

---

## 11. Inventaire annexé — fichiers clés à migrer

### Application

- `Grades/Services/GradeService.Cotation.cs` — **HasRole**, cotation scope  
- `Grades/Services/GradeService.cs` — `IsAdministrator`  
- `ResultValidation/Services/ResultValidationService.cs` — **HasElevatedRole**  
- `PedagogicalPeriods/Services/PedagogicalPeriodService.cs` — rôles + admin  
- `Payments/Services/PaymentService.cs`, `PaymentMutationPolicy.cs`  

### API (bloc `admin.full`)

- `PersonnelController.cs`, `GeographyAdminController.cs`, `PedagogicalPeriodsController.cs`  
- `AdminController.cs` (teachers), `PaymentsController.cs`, `UpdateController.cs`, `CloudSyncController.cs`, `FinanceController.cs`, `ParentActivationIssueController.cs`  

### API (gaps policy)

- `GeographyController.cs`  

### Desktop

- `Services/ApiServices.cs` (`AuthSessionService.IsAdministrator`)  
- `ViewModels/EncaissementsViewModel.cs`, `CollectPaymentViewModel.cs`, `ExpenseMultiCurrencyAllocationViewModel.cs`, `PricingCategoryAssignmentViewModel.cs`  
- `ViewModels/GradesViewModel.Cotation.cs`  
- `Views/EncaissementActionWindow.xaml.cs`  

### Infrastructure (comportement à documenter, pas forcément supprimer)

- `HttpContextCurrentUserService.cs`, `PermissionAuthorizationHandler.cs`  
- `EffectivePermissionServices.cs` (injection admin.full ADMIN)  
- `SecurityNavigationService.cs` (bypass admin.full)  

---

## 12. Critères de validation de cet audit (go Phase 4 plan)

- [ ] Validation produit de la **liste des nouvelles permissions** (Personnel, Pédagogie, Paiements sensibles, etc.).  
- [ ] Décision **GeographyController** (lecture ouverte vs `schools.read`).  
- [ ] Décision **modèle titulaire / prefet** (permissions vs ownership enseignant).  
- [ ] Accord sur **stratégie dual-run** et ordre des lots (§10).  
- [ ] Rédaction **`PHASE4_EXECUTION_PLAN.md`** dérivé de ce document (après go).

---

## 13. Références projet

- `PHASE3_VALIDATION_REPORT.md` — état moteur Phase 3 (74/74 harness).  
- `PHASE3_EXECUTION_PLAN.md` — hors scope Phase 3 : *« remplacement généralisé HasRole (Phase 4) »*.  
- `Shared/Constants/Permissions.cs` — catalogue codes actuel.  
- `Infrastructure/Seeding/SecurityCatalogSeeder.cs` — navigation + rôles seed.

---

*Fin du rapport d’audit — Phase 4 — version 1.0*
