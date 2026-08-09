# Phase 4 — Validation Lot 3 (Géographie, cloud, mises à jour, activation parent)

**Date** : 2026-08-07  
**Statut** : **VALIDÉ** (commanditaire) — Lot 3 clôturé  
**Harness** : `dotnet run --project tools/Phase4SecurityValidation` (Lot 0–3)

---

## 1. Migration Checklist (Lot 3)

| # | Critère | Statut | Preuve / commentaire |
|---|---------|--------|----------------------|
| 1 | Permissions créées et seedées | **OK** | `geography.manage`, `cloud-sync.manage`, `updates.manage`, `parent-activation.manage` |
| 2 | Rôles mis à jour | **OK** | **ADMIN** : catalogue complet ; **ENSEIGNANT / COMPTABLE / CAISSIER** : `schools.read` (référentiel adresse Lot 3) |
| 3 | Navigation mise à jour | **OK** | `Settings.Geographie`, `SyncCloud`, `MisesAJour`, **`Settings.ParentActivation`** (nouvelle page nav) |
| 4 | API migrées | **OK** | 4 controllers Lot 3 ; `GeographyController` lecture → `schools.read` |
| 5 | Services migrés | **OK** | Pas de legacy Application dans le périmètre |
| 6 | Desktop migré | **OK** | Arbre Paramètres : sections sensibles masquées sans permission ; `DesktopViewRegistry` ParentActivation |
| 7 | Harness du lot en succès | **OK** | **45/45 PASS** — `tools/Phase4SecurityValidation/out/summary.txt` |
| 8 | Legacy supprimé (lot) | **OK** | 0× `AdminFull` sur controllers Lot 3 |
| 9 | Documentation | **OK** | Ce rapport, personas, permissions planifiées |

---

## 2. Impact métier

| Ancien mécanisme | Nouveau mécanisme | Justification fonctionnelle |
|------------------|-------------------|----------------------------|
| `GeographyAdminController` — 18× `admin.full` | `geography.manage` | Administration du référentiel géographique réservée aux profils habilités. |
| `GeographyController` — `[Authorize]` seul (tout utilisateur connecté) | `schools.read` sur les 6 GET | Lecture référentiel pour formulaires adresse, alignée établissement (décision Lot 0). |
| `CloudSyncController` — `admin.full` | `cloud-sync.manage` | Sync cloud = opération d’infrastructure établissement. |
| `UpdateController` (admin) — `admin.full` | `updates.manage` | Publication / activation versions (hors `check` public anonyme). |
| `ParentActivationIssueController` — `admin.full` | `parent-activation.manage` | Émission jetons activation parent = support contrôlé. |
| Nav seed Paramètres (Géo, Sync, MàJ) — `admin.full` | Permissions dédiées ci-dessus | Parité menu ↔ API. |

---

## 3. Impact utilisateur

| Rôle | Avant | Après |
|------|-------|-------|
| **ADMIN** | Accès via `admin.full` à géo admin, sync, mises à jour, activation parent. | Comportement équivalent (effectif complet). Menus Paramètres filtrés par permission explicite. |
| **DIRECTION** | Référentiel adresse : accès implicite ; pas d’admin géo/sync/MàJ. | **`schools.read`** : listes pays/provinces dans les formulaires. **Pas** de pages Géographie admin, Sync, MàJ, Activation parent (sans permissions manage). |
| **ENSEIGNANT / COMPTABLE / CAISSIER** | Référentiel adresse ouvert à tout compte connecté. | **`schools.read`** ajouté au seed : adresses dans les écrans métier. **Pas** d’administration Lot 3. |
| **PARENT** | Pas d’usage Desktop Lot 3. | Inchangé (`payments.read` / `grades.read` — pas de `schools.read` Desktop parent typique). |
| **PREFET / PROMOTEUR** | Idem DIRECTION pour adresses si `schools.read`. | Pas de modules admin Lot 3. |

---

## 4. Vigilance transversale (Lots 5–6)

**`teachers.manage`** reste strictement **administration pédagogique** (fiches enseignants, liste pour config cours). Lors des Lots 5–6, vérifier qu’aucune route ou écran RH Personnel n’est accessible via `teachers.manage` — le domaine **Personnel** reste sur `personnel.read` / `personnel.manage`.

---

## 5. Harness

```text
dotnet run --project tools/Phase4SecurityValidation
```

**Dernière exécution** : **45/45 PASS** (exit code 0, Lots 0–3).

---

## 6. Demande de validation

- [x] Checklist Lot 3 complétée  
- [x] Impact métier + Impact utilisateur  
- [x] D1 / D2 respectés  

**Validation Lot 3 demandée** pour enchaîner **Lot 4 — Calendrier pédagogique**.
