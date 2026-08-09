# Phase 4 — Matrice personas (Lot 0)

Référence pour harness et tests par lot. Permissions **effectives** attendues (via seed `SecurityCatalogSeeder`) — base Lot 0 ; les **nouvelles** permissions Phase 4 seront ajoutées lot par lot (`PHASE4_PLANNED_PERMISSIONS.md`).

| Persona | Rôle code | Permissions effectives clés (non exhaustif) | Interdit (smoke) |
|---------|-----------|-----------------------------------------------|------------------|
| **ADMIN** | `ADMIN` | `admin.full` + catalogue complet seed | — |
| **DIRECTION** | `DIRECTION` | … **`grades.cotation.delegate`**, **`grades.recalculate`**, **`grades.publish`**, saisie notes | pas **`results-validation.lock/unlock`** |
| **PREFET** | `PREFET` | **`grades.cotation.delegate`**, saisie, **`grades.recalculate`**, validation résultats | **`grades.publish`**, **`delete-with-grades`** |
| **PROMOTEUR** | `PROMOTEUR` | **`grades.publish` / `unpublish`**, validation + lock résultats | **`grades.cotation.delegate`**, saisie notes |
| **ENSEIGNANT** | `ENSEIGNANT` | **`grades.create/update/delete`**, lecture validation | **`grades.recalculate`**, **`grades.publish`**, delegate |
| **COMPTABLE** | `COMPTABLE` | `payments.*` (dont cancel, notes, paid-mutation), `pricing-categories.assign`, `payment-fx.update`, **`schools.read`**, `accounting.read`, `students.read` | `security.roles.manage` |
| **CAISSIER** | `CAISSIER` | `payments.read/create`, **`schools.read`**, `students.read` | `payments.validate` (typ.) |
| **PARENT** | `PARENT` | `payments.read`, `grades.read`, `reports.read` | `security.*`, `students.create` |

Harness Lot 0 : utilisateurs jetables `__p4v_*` + `EffectivePermissionService.ResolveAsync` (même BD que Phases 1–3).

**Décisions Lot 0 (figées pour la suite)** :

- **GeographyController (Lot 3)** : policy lecture = **`schools.read`** (formulaires référentiel).
- **Cotation (Lot 6)** : atelier métier **obligatoire** avant merge Lot 6 ; pas de `HasRole` en cible.
