# Rapport — isolation multi-école systémique

Date : 2026-08-05  
Objectif : empêcher tout accès aux données d’une autre école, y compris via de futurs services.

## 1. Mécanismes de défense (couches)

| Couche | Rôle |
|--------|------|
| **Filtres EF globaux** (`SchoolTenantQueryFilterExtensions`) | Entités avec `SchoolId` : `!IsDeleted` + `SchoolId == tenant`. |
| **Filtres EF indirects** (`IndirectSchoolTenantQueryFilters`) | Entités sans `SchoolId` : filtre via navigation documentée (ex. `Evaluation → ClassRoom.SchoolId`). |
| **`SchoolDbContext`** | `EffectiveTenantSchoolId` (JWT / override), `IgnoreSchoolScope` (seed/sync/admin), règles `SaveChanges`. |
| **`Repository.GetByIdAsync`** | LINQ sur `Id` (respecte les filtres), plus `FindAsync` seul. |
| **`ISchoolTenancyService`** | `RequireForSchoolAsync`, `TryGetForSchoolAsync`, `EnsureBelongsToSchoolAsync`, `TryResolveSchoolIdAsync`. |
| **Extensions dépôt** | `GetByIdForSchoolAsync` / `RequireByIdForSchoolAsync` pour `ISchoolScoped`. |
| **`SchoolTenancyCatalog`** | Référentiel des chaînes d’appartenance + entités globales. |

## 2. Entités auditées

### 2.1 Entités globales (hors tenant scolaire)

Documentées dans `SchoolTenancyCatalog.GlobalEntities` : géographie, `Permission`, devises/taux (`CurrencyDefinition`, `ExchangeRate*`), `ApplicationVersion`, entité racine `School`.

### 2.2 Entités avec `SchoolId` direct

Toutes les autres entités métier `AuditableEntity` possédant une propriété `SchoolId` sont filtrées automatiquement (ex. `Student`, `ClassRoom`, `Payment`, `UserAccount`, `StudentCard`, délibération, RH, etc.).

### 2.3 Entités sans `SchoolId` direct (chaîne d’appartenance)

| Entité | Chaîne vers l’école |
|--------|---------------------|
| `Enrollment` | `Student.SchoolId` (filtre EF ; doc : aussi `ClassRoom.SchoolId`) |
| `StudentGuardian` | `Student.SchoolId` |
| `StudentDocument` | `Student.SchoolId` |
| `EnrollmentPricingCategoryHistory` | `Enrollment.Student.SchoolId` |
| `StudentStatusHistory` | `Student.SchoolId` |
| `Evaluation` | `ClassRoom.SchoolId` |
| `GradeEntry` | `Student.SchoolId` |
| `ReportCard` | `Student.SchoolId` |
| `ReportCardDetail` | `ReportCard.Student.SchoolId` |
| `CourseAssignment` | `ClassRoom.SchoolId` |
| `ScheduleSlot` | `CourseAssignment.ClassRoom.SchoolId` |
| `PaymentLine` | `Payment.SchoolId` |
| `PaymentReversal` | `Payment.SchoolId` |
| `StudentFeeBalance` | `Student.SchoolId` |
| `RevenueAllocationKeyDetail` | `AllocationKey.SchoolId` |
| `NotificationRecipient` | `Notification.SchoolId` |
| `UserRoleAssignment` | `User.SchoolId` |
| `RolePermission` | `Role.SchoolId` |
| `RefreshToken` | `User.SchoolId` |
| `SyncOutboxItem` | `Unit.SchoolId` |
| `StudentRemedialCourse` | `RemedialSession.SchoolId` |

Source de vérité code : `SchoolTenancyCatalog.IndirectOwnershipChains` + `IndirectSchoolTenantQueryFilters`.

## 3. Méthodes sécurisées créées / à utiliser

- **`ISchoolTenancyService.RequireForSchoolAsync<T>`** — charge par id sous tenant imposé ; lève `SchoolTenancyAccessDeniedException` si absent (autre école ou inexistant).
- **`TryGetForSchoolAsync`**, **`EnsureBelongsToSchoolAsync`**, **`TryResolveSchoolIdAsync`** (résolution partielle pour quelques types).
- **`IRepository` + `RequireByIdForSchoolAsync` / `GetByIdForSchoolAsync`** — entités `ISchoolScoped`.
- **`EnsureEntityBelongsToSchool`** — garde sur entité déjà chargée.
- **Exception** : `SchoolTenancyAccessDeniedException` — à mapper en **404** (ou 403) côté API pour ne pas révéler l’existence cross-tenant.

Convention pour les futurs développements :

1. Préférer les `DbSet` / `IRepository.FindAsync` avec le contexte tenant JWT actif (filtres EF).
2. Pour tout chargement par **id seul** sur entité indirecte ou sensible : **`RequireForSchoolAsync`** ou **`RequireByIdForSchoolAsync`**.
3. Ne pas utiliser `IgnoreSchoolScope` dans la couche Application.
4. Documenter toute nouvelle entité sans `SchoolId` dans le catalogue + filtre indirect.

## 4. Fichiers modifiés (phase isolation systémique)

**Application**

- `Common/Tenancy/SchoolTenancyCatalog.cs`
- `Common/SchoolTenancyAccessDeniedException.cs`
- `Common/Interfaces/ISchoolTenancyService.cs`
- `Common/SchoolScopedRepositoryExtensions.cs`
- `Common/Interfaces/IRepository.cs` (documentation)
- `StudentCards/Services/StudentCardService.cs`, `Mentions/Services/ResultMentionService.cs`,
  `Students/Services/StudentService.cs` : refus cross-école en 404 au lieu de 409

**Infrastructure**

- `Persistence/IndirectSchoolTenantQueryFilters.cs`
- `Persistence/SchoolDbContext.cs` (application des filtres indirects)
- `Persistence/SchoolTenancySchemaInitializer.cs` (colonnes `SchoolId` manquantes, idempotent)
- `Tenancy/SchoolTenancyService.cs`
- `DependencyInjection/InfrastructureServiceRegistration.cs` (+ enregistrement DI, using `ParentActivation`)

**API**

- `Program.cs` (exécution de `SchoolTenancySchemaInitializer` au démarrage)
- `Middleware/ExceptionHandlingMiddleware.cs` (`SchoolTenancyAccessDeniedException` → 404)

**Tests**

- `tests/SchoolManagement.UnitTests/Tenancy/SchoolTenancyFilterTests.cs`
- `tests/SchoolManagement.UnitTests/Tenancy/SchoolTenancyCatalogTests.cs`
- `tests/SchoolManagement.IntegrationTests/MultiTenant/` (banc cross-école complet + rapport)

## 5. Tests ajoutés et résultats

| Test | Résultat |
|------|----------|
| `Evaluation_from_other_school_is_hidden_by_query_filter` | OK |
| `Enrollment_from_other_school_is_hidden_by_query_filter` | OK |
| `RequireForSchoolAsync_throws_when_evaluation_belongs_to_other_school` | OK |
| `RequireForSchoolAsync_returns_entity_for_own_school` | OK |
| `SchoolTenancyCatalogTests` (documentation + chaînes) | OK |
| `Cross_tenant_access_is_denied_on_every_business_resource` (intégration API, 108 scénarios) | OK |

Commandes :

- `dotnet test tests/SchoolManagement.UnitTests --filter FullyQualifiedName~Tenancy`
- `dotnet test tests/SchoolManagement.IntegrationTests --filter Category=MultiTenant`

La preuve de fonctionnement bout en bout (deux écoles réelles, JWT distincts, attaques croisées
dans les deux sens sur chaque ressource) est détaillée dans
[`rapport-tests-cross-tenant.md`](rapport-tests-cross-tenant.md), régénéré à chaque exécution.

## 6. Risques résiduels connus

- **Nouvelle entité sans `SchoolId`** : obligatoire d’ajouter filtre indirect + entrée catalogue (sinon seul soft-delete s’applique).
- **Filtres indirects et traduction SQL** : la condition tenant doit rester écrite en ligne dans la
  lambda `HasQueryFilter`. Toute extraction vers une méthode utilitaire casse la traduction SQL Server
  et empêche l'API de démarrer, alors que le provider InMemory des tests unitaires l'accepte
  (évaluation client). Les tests d'intégration sur SQL sont la seule garde efficace.
- **Requêtes raw SQL / `FromSqlRaw`** : hors couverture EF ; revue manuelle.
- **`IgnoreSchoolScope`** : réservé seed, sync, migrations — ne pas exposer via services métier.
- **Compte utilisateur même login multi-école** : politique produit / auth à clarifier.

## 7. Recommandation revue de code

Checklist PR : « Cette PR introduit-elle une entité ou un `GetById` sans tenant ? » → catalogue + filtre + test cross-école.
