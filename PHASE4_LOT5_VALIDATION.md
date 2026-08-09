# Phase 4 — Validation Lot 5 (Validation des résultats)

**Date** : 2026-08-07  
**Statut** : **VALIDÉ** (commanditaire) — Lot 5 clôturé  
**Harness** : `dotnet run --project tools/Phase4SecurityValidation` (Lot 0–5)

**Gate Lot 5** : périmètre audit R4-2 (validation résultats) — acté par démarrage commanditaire du lot.

---

## 1. Migration Checklist (Lot 5)

| # | Critère | Statut | Preuve / commentaire |
|---|---------|--------|----------------------|
| 1 | Permissions créées et seedées | **N/A** | Réutilisation `results-validation.*` (catalogue Phase 0) |
| 2 | Rôles mis à jour | **OK** | Matrice seed inchangée et cohérente (PREFET validate ; PROMOTEUR lock/unlock ; …) |
| 3 | Navigation mise à jour | **OK** | `Results.ValidationResultats` → `results-validation.read` (déjà aligné) |
| 4 | API migrées | **OK** | `ResultValidationController` — policies granulaires (déjà en place, sans `admin.full`) |
| 5 | Services migrés | **OK** | `ResultValidationService` : `HasPermission` uniquement |
| 6 | Desktop migré | **OK** | UI pilotée par flags API (`CanValidate` / …) — pas de legacy local |
| 7 | Harness du lot en succès | **OK** | **62/62** (`tools/Phase4SecurityValidation/out/summary.txt`) |
| 8 | Legacy supprimé (lot) | **OK** | 0× `HasElevatedRole`, `IsAdministrator`, `AdminFull` dans `ResultValidationService` |
| 9 | Documentation | **OK** | Ce rapport (§3 séparation permissions) |

---

## 2. Impact métier

| Ancien mécanisme | Nouveau mécanisme | Justification fonctionnelle |
|------------------|-------------------|----------------------------|
| `CanValidatePermission` : `IsAdministrator` + `admin.full` + rôles JWT DIRECTION/PREFET/PROMOTEUR/ADMIN | `HasPermission(results-validation.validate)` | La validation des bulletins est une action métier distincte, assignable par rôle seed. |
| `CanLockPermission` / `CanUnlockPermission` : idem + rôles ADMIN/PROMOTEUR | `results-validation.lock` / `results-validation.unlock` | Verrouillage = étape sensible séparée de la validation (souvent promoteur / admin). |
| `HasElevatedRole(...)` | _Supprimé_ | Fin du double chemin JWT rôle vs effectif BD. |
| API (déjà migrée Phase antérieure) | `read` / `validate` / `lock` / `unlock` sur routes dédiées | Cohérence avec le service. |

---

## 3. Séparation des permissions (vérification explicite)

| Besoin fonctionnel | Code permission | Endpoint / couche | Recouvrement avec les autres ? |
|--------------------|-----------------|-------------------|--------------------------------|
| **Consulter la période active** (saisie notes, contexte) | `grades.read` | `GET pedagogical-periods/active` | **Non** — lecture calendrier « courant », pas gestion ni validation résultats. |
| **Gérer le calendrier pédagogique** (structure, ouvrir/clôturer périodes) | `pedagogical-periods.manage` | `PedagogicalPeriodsController` (sauf `active`) | **Non** — aucune référence dans `ResultValidationService`. |
| **Consulter la feuille / readiness validation** | `results-validation.read` | `ResultValidationController` GET | **Non** — ENSEignant peut lire sans valider. |
| **Valider / annuler validation** des résultats | `results-validation.validate` | POST validate / cancel | **Non** — distinct de lock/unlock et du calendrier. |
| **Verrouiller** les résultats | `results-validation.lock` | POST lock | **Non** — PROMOTEUR seed ; PREFET/DIRECTION sans lock. |
| **Déverrouiller** les résultats | `results-validation.unlock` | POST unlock | **Non** — séparé de validate et lock. |

**Harness** : grep « séparation domaines » + cross-check `PedagogicalPeriodsController` + policies API validation.

---

## 4. Impact utilisateur

| Rôle | Avant | Après |
|------|-------|-------|
| **ADMIN** | Validation via rôle / `admin.full` / effectif mixte. | Effectif complet ; boutons alignés sur permissions explicites. |
| **DIRECTION** | Validation souvent possible via rôle JWT même sans effectif fin. | **`results-validation.validate`** seedé ; **pas** de lock/unlock ni calendrier si non seedé. |
| **PREFET** | Validation via rôle JWT + permission. | **Validate uniquement** (pas lock/unlock) — cohérent seed. |
| **PROMOTEUR** | Lock via rôle JWT + permission. | **Validate + lock + unlock** via permissions seed. |
| **ENSEIGNANT** | Lecture feuille possible ; validation bloquée côté service si pas rôle élevé. | **`results-validation.read`** ; **pas** validate — plus de bypass rôle JWT. |
| **COMPTABLE / CAISSIER / PARENT** | Pas de module validation. | Inchangé. |

---

## 5. Harness

```text
dotnet run --project tools/Phase4SecurityValidation
```

**Dernière exécution** : **62/62 passed** (Lot 0–5, personas + grep legacy + séparation calendrier / validation).

---

## 6. Demande de validation

- [x] Checklist Lot 5 complétée  
- [x] Impact métier + Impact utilisateur + **§3 séparation permissions**  
- [x] D1 / D2 respectés  
- [x] **Validation commanditaire Lot 5**

**Lot 5 validé** — **Lot 6** démarré (gate atelier : `PHASE4_LOT6_ATELIER_COTATION.md`).
