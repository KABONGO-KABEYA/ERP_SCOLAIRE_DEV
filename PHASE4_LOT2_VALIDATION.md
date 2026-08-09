# Phase 4 — Validation Lot 2 (Personnel & enseignants admin)

**Date** : 2026-08-07  
**Statut** : **VALIDÉ** (commanditaire) — Lot 2 clôturé  
**Harness** : `dotnet run --project tools/Phase4SecurityValidation` (Lot 0–2)

---

## 1. Migration Checklist (Lot 2)

| # | Critère | Statut | Preuve / commentaire |
|---|---------|--------|----------------------|
| 1 | Permissions créées et seedées | **OK** | `personnel.read`, `personnel.manage`, `teachers.manage` — metadata, deps, catalogue |
| 2 | Rôles mis à jour | **OK** | **DIRECTION** : `teachers.manage` (config cours) ; **ADMIN** : toutes via seed catalogue ; personnel RH non assigné aux rôles métier hors ADMIN |
| 3 | Navigation mise à jour | **OK** | Pages `Personnel.*` : read/manage alignées ; liste vs création / org |
| 4 | API migrées | **OK** | `PersonnelController` (9 routes) ; `AdminController` teachers (3 routes GET/POST/PUT) |
| 5 | Services migrés | **OK** | Aucune règle legacy Application (policies API uniquement) |
| 6 | Desktop migré | **OK** | `SettingsViewModel` : section Enseignants + CRUD via `teachers.manage` ; module Personnel via nav dynamique |
| 7 | Harness du lot en succès | **OK** | **35/35 PASS** — `tools/Phase4SecurityValidation/out/summary.txt` |
| 8 | Legacy supprimé (lot) | **OK** | 0× `AdminFull` sur `PersonnelController` ; routes teachers sans `AdminFull` |
| 9 | Documentation | **OK** | Personas, template § Impact utilisateur, ce rapport |

**Hors périmètre Lot 2 (conservé)** : `AdminController` `reset-enrollment-data` reste sur `admin.full` (lot futur / gouvernance).

---

## 2. Impact métier

| Ancien mécanisme | Nouveau mécanisme | Justification fonctionnelle |
|------------------|-------------------|----------------------------|
| `[Authorize(Policy = admin.full)]` sur **toutes** les routes `PersonnelController` | GET → `personnel.read` ; POST/PUT → `personnel.manage` | Séparer consultation RH et mutations (fiches, départements, fonctions). |
| `[Authorize(Policy = admin.full)]` sur `AdminController` **teachers** (GET/POST/PUT) | `teachers.manage` | Administration des fiches enseignants distincte du fourre-tout `admin.full`. |
| Navigation seed `Personnel.*` → `admin.full` | `personnel.read` (liste) ; `personnel.manage` (nouveau, fonctions, départements) | Alignement menu ↔ API. |
| Accès implicite via rôle JWT ADMIN / `IsAdministrator` (enseignants dans Paramètres) | `SessionPermissions.Can(..., teachers.manage)` | Décision sur effectif JWT, pas sur le code rôle seul. |

---

## 3. Impact utilisateur

| Rôle | Avant | Après |
|------|-------|-------|
| **ADMIN** | Module Personnel et enseignants visibles via menu + `admin.full`. | Inchangé en pratique (effectif complet via rôle ADMIN seedé). Menus Personnel filtrés par `personnel.read` / `personnel.manage` ; section **Enseignants** si `teachers.manage`. |
| **DIRECTION** | Pas d’accès API enseignants admin ; configuration des cours pouvait échouer silencieusement sur la liste enseignants. | **Pas** de module Personnel RH. **Peut** lister/gérer les enseignants via API (`teachers.manage`) pour la config des cours ; section Enseignants visible dans Paramètres si ouverte. |
| **PREFET / PROMOTEUR** | Pas de module Personnel. | Inchangé — pas de personnel ni enseignants admin. |
| **ENSEIGNANT** | Pas de module Personnel. | Inchangé — pas d’accès RH ni admin enseignants. |
| **COMPTABLE / CAISSIER** | Pas de module Personnel. | Inchangé. |
| **PARENT** | Pas de module Personnel. | Inchangé. |

---

## 4. Harness

```text
dotnet run --project tools/Phase4SecurityValidation
```

**Dernière exécution** : **35/35 PASS** (exit code 0, Lots 0–2).

---

## 5. Demande de validation

- [x] Checklist Lot 2 complétée  
- [x] Impact métier + Impact utilisateur renseignés  
- [x] D1 / D2 respectés  

**Validation Lot 2 demandée** pour enchaîner **Lot 3 — Géographie, cloud, mises à jour, activation parent**.
