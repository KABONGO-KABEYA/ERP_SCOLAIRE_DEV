# Phase 4 — Validation Lot 6 (Cotation / grades)

**Date** : 2026-08-07  
**Statut** : **VALIDÉ** (commanditaire) — Lot 6 clôturé  
**Gate métier** : [`PHASE4_LOT6_ATELIER_COTATION.md`](PHASE4_LOT6_ATELIER_COTATION.md) — **VALIDÉ** (+ règles d’architecture §A ci-dessous)  
**Harness** : `dotnet run --project tools/Phase4SecurityValidation` (Lot 0–6)

---

## A. Règles d’architecture figées (commanditaire)

| # | Règle | Mise en œuvre |
|---|--------|----------------|
| **R1** | **Publication ≠ validation officielle** | `GradeService.Publication.cs` : aucun appel à `results-validation.*` ni `_resultValidation`. Publish/unpublish = bascule `IsPublished` uniquement. |
| **R2** | **`grades.recalculate` = recalcul manuel admin** | `POST period-results/calculate` + `EnsureCanRecalculatePeriodResultsManually()`. Flux saisie / délibération / clôture examen → `RecalculatePeriodResultsAfterDataChangeAsync` (sans `grades.recalculate`). |

---

## 1. Migration Checklist (Lot 6)

| # | Critère | Statut | Preuve / commentaire |
|---|---------|--------|----------------------|
| 1 | Permissions créées et seedées | **OK** | 7 codes Lot 6 + deps (`Permissions.cs`, `SecurityCatalogSeeder`) |
| 2 | Rôles mis à jour | **OK** | DIRECTION, PREFET, PROMOTEUR, ENSEIGNANT (matrice gate §6) |
| 3 | Navigation mise à jour | **OK** | `Grades.Main` → `grades.read` (inchangé) |
| 4 | API migrées | **OK** | `GradesController` : delete, recalc, publish/unpublish |
| 5 | Services migrés | **OK** | `GradeService.Cotation` sans `HasRole` ; delete / recalc / publication |
| 6 | Desktop migré | **OK** | `GradesViewModel.Cotation` → `grades.cotation.delegate` |
| 7 | Harness du lot en succès | **OK** | **83/83** (`tools/Phase4SecurityValidation/out/summary.txt`) |
| 8 | Legacy supprimé (lot) | **OK** | **0** `HasRole(` dans `src/SchoolManagement.Application` ; cotation sans `IsAdministrator` |
| 9 | Documentation | **OK** | Gate + ce rapport |

---

## 2. Impact métier

| Ancien mécanisme | Nouveau mécanisme | Justification |
|------------------|-------------------|---------------|
| `HasRole` (DIRECTION, PREFET, TITULAIRE, …) pour périmètre session | `grades.cotation.delegate` + `grades.cotation.scope.class` | Périmètre assignable par rôle seed, sans JWT codé en dur. |
| `IsAdministrator` pour supprimer évaluation notée | `grades.evaluation.delete-with-grades` | Action sensible distincte de la suppression d’évaluation vide (`grades.delete`). |
| Recalcul manuel sous `grades.update` | `grades.recalculate` sur endpoint dédié | Séparation admin / flux normal de saisie (R2). |
| Publication parent inexistante en API | `grades.publish` / `grades.unpublish` | Visibilité portail parent indépendante de la validation officielle (R1). |
| Mot de passe enseignant contourné par rôle | `grades.cotation.delegate` | Même règle métier, permission explicite. |

---

## 3. Impact utilisateur

| Rôle | Avant | Après |
|------|-------|-------|
| **ENSEIGNANT** | Cotation via affectations ; rôle JWT pour UI session. | `create` / `update` / `delete` (éval. vide) ; identité verrouillée si compte lié ; pas de recalc manuel ni publish. |
| **PREFET** | Délégation via rôle JWT ; seed partiel `grades.read`. | **Delegate** + saisie + **recalc manuel** ; pas publish ni suppression éval. notée. |
| **DIRECTION** | Accès « full » via rôle. | Delegate + saisie + recalc + publish/unpublish + suppression éval. notée (seed). |
| **PROMOTEUR** | Lock validation + rôle. | **Publish/unpublish** cotation + validation résultats (Lot 5) ; pas cotation déléguée. |
| **ADMIN** | Bypass hérité. | `admin.full` couvre le catalogue. |

---

## 4. Séparation publication / validation (R1)

| Processus | Permissions | Couplage |
|-----------|-------------|----------|
| Publier cotation (parent) | `grades.publish` / `grades.unpublish` | **Aucun** appel validation officielle |
| Valider / verrouiller résultats | `results-validation.*` (Lot 5) | Inchangé |

---

## 5. Harness

```text
dotnet run --project tools/Phase4SecurityValidation
```

**Dernière exécution** : **83/83 passed** (Lot 0–6).

---

## 6. Demande de validation

- [x] Gate atelier + règles R1/R2  
- [x] Checklist Lot 6 complétée  
- [x] Impact métier + Impact utilisateur  
- [x] D1 / D2 respectés  

- [x] **Validation commanditaire Lot 6**
