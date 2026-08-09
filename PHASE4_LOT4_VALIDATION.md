# Phase 4 — Validation Lot 4 (Calendrier pédagogique)

**Date** : 2026-08-07  
**Statut** : **VALIDÉ** (commanditaire) — Lot 4 clôturé  
**Harness** : `dotnet run --project tools/Phase4SecurityValidation` (Lot 0–4)

---

## 1. Migration Checklist (Lot 4)

| # | Critère | Statut | Preuve / commentaire |
|---|---------|--------|----------------------|
| 1 | Permissions créées et seedées | **OK** | `pedagogical-periods.manage` (+ dep. `grades.read`) |
| 2 | Rôles mis à jour | **OK** | **DIRECTION** : `pedagogical-periods.manage` ; **ADMIN** : catalogue complet |
| 3 | Navigation mise à jour | **OK** | `PedagogicalPeriods.Main` → `pedagogical-periods.manage` |
| 4 | API migrées | **OK** | 8 routes admin → `pedagogical-periods.manage` ; `GET active` → `grades.read` (inchangé) |
| 5 | Services migrés | **OK** | `PedagogicalPeriodService` : `HasPermission` uniquement ; plus de rôles JWT codés |
| 6 | Desktop migré | **OK** | `PedagogicalPeriodsViewModel` : actions via `SessionPermissions` |
| 7 | Harness du lot en succès | **OK** | **52/52 PASS** — `tools/Phase4SecurityValidation/out/summary.txt` |
| 8 | Legacy supprimé (lot) | **OK** | 0× `AdminFull` / `EnsureAdministrator` / `IsAdministrator` dans le périmètre |
| 9 | Documentation | **OK** | Ce rapport, personas, plan (vigilance Lot 7 AllowAnonymous) |

---

## 2. Impact métier

| Ancien mécanisme | Nouveau mécanisme | Justification fonctionnelle |
|------------------|-------------------|----------------------------|
| 8× `[Authorize(Policy = admin.full)]` sur `PedagogicalPeriodsController` | `pedagogical-periods.manage` | Ouverture / clôture / verrouillage du calendrier = fonction d’administration pédagogique explicite. |
| `GET active` | `grades.read` (conservé) | Consultation de la période active pour la saisie des notes — pas une mutation calendrier. |
| `PedagogicalPeriodService.EnsureAdministrator()` (ADMIN/DIRECTION/`IsAdministrator`/`admin.full`) | `HasPermission(pedagogical-periods.manage)` | Une seule source de vérité ; fin du contournement par code rôle JWT. |
| Nav `PedagogicalPeriods.Main` — `admin.full` | `pedagogical-periods.manage` | Menu aligné API. |

---

## 3. Impact utilisateur

| Rôle | Avant | Après |
|------|-------|-------|
| **ADMIN** | Calendrier pédagogique via `admin.full`. | Équivalent (effectif complet). Menu sous permission explicite. |
| **DIRECTION** | Accès calendrier via rôle JWT **ou** `admin.full` implicite côté service. | **`pedagogical-periods.manage`** seedé : module visible, actions ouverture/clôture/verrouillage. |
| **PREFET / PROMOTEUR** | Pas de gestion calendrier (typ.). | Inchangé — pas de `pedagogical-periods.manage`. |
| **ENSEIGNANT** | Pas de module calendrier admin ; **`GET active`** via notes. | Inchangé pour l’admin calendrier ; période active toujours via `grades.read`. |
| **COMPTABLE / CAISSIER / PARENT** | Pas de module. | Inchangé. |

---

## 4. Vigilance transversale

- **Lots 5–6** : `teachers.manage` ≠ RH (`personnel.*`) — cf. `PHASE4_LOT3_VALIDATION.md` §4.
- **Lot 7 / clôture Phase 4** : **audit de tous les endpoints `[AllowAnonymous]`** — inventaire, justification métier, absence d’exposition sensible (demande commanditaire).

---

## 5. Harness

```text
dotnet run --project tools/Phase4SecurityValidation
```

**Dernière exécution** : **52/52 PASS** (exit code 0, Lots 0–4).

---

## 6. Demande de validation

- [x] Checklist Lot 4 complétée  
- [x] Impact métier + Impact utilisateur  
- [x] D1 / D2 respectés  

**Validation Lot 4 demandée** pour enchaîner **Lot 5 — Validation des résultats**.
