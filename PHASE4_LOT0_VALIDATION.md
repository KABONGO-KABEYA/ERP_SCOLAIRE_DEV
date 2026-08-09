# Phase 4 — Validation Lot 0 (préparation)

**Date** : 2026-08-07  
**Statut** : **VALIDÉ** (commanditaire) — Lot 0 clôturé  
**Harness** : `dotnet run --project tools/Phase4SecurityValidation`

---

## 1. Migration Checklist (Lot 0)

| # | Critère | Statut | Preuve / commentaire |
|---|---------|--------|----------------------|
| 1 | Permissions créées et seedées | **N/A** | Lot 0 : catalogue inchangé ; liste planifiée → `PHASE4_PLANNED_PERMISSIONS.md` |
| 2 | Rôles mis à jour | **N/A** | Matrice personas documentée → `PHASE4_PERSONAS.md` (seed existant Phase 3) |
| 3 | Navigation mise à jour | **N/A** | Aucune page Lot 0 |
| 4 | API migrées | **N/A** | — |
| 5 | Services migrés | **N/A** | — |
| 6 | Desktop migré | **OK** | `IAuthSessionService.HasPermission` + `SessionPermissions.Can` (sans fallback rôle ADMIN) |
| 7 | Harness du lot en succès | **OK** | **19/19 PASS** — `tools/Phase4SecurityValidation/out/summary.txt`, `out/evidence.json` |
| 8 | Legacy supprimé (lot) | **N/A** | Aucun périmètre métier migré en Lot 0 |
| 9 | Documentation | **OK** | `PHASE4_EXECUTION_PLAN.md` (§2.1 gouvernance), personas, permissions planifiées, checklist template |

---

## 2. Livrables Lot 0

| Livrable | Fichier |
|----------|---------|
| Gouvernance checklist | `PHASE4_MIGRATION_CHECKLIST_TEMPLATE.md` |
| Plan officiel (validé) | `PHASE4_EXECUTION_PLAN.md` |
| Personas | `PHASE4_PERSONAS.md` |
| Permissions futures | `PHASE4_PLANNED_PERMISSIONS.md` |
| Harness Phase 4 | `tools/Phase4SecurityValidation/` |
| Helper UI permissions | `SessionPermissions.cs`, `HasPermission` sur `AuthSessionService` |

**Décisions figées** :

- Lecture `GeographyController` → **`schools.read`** (Lot 3).
- Cotation → **atelier avant Lot 6** ; suppression cible de `HasRole`.

---

## 3. Harness

```text
dotnet run --project tools/Phase4SecurityValidation
```

Contrôles Lot 0 : documentation, helper Desktop, smoke **8 personas** via `EffectivePermissionService`.

**Dernière exécution** : 19/19 PASS (exit code 0). Persona ADMIN : assertion `MustHave` sur `admin.full` uniquement (l’expansion effective inclut l’ensemble du catalogue, dont `platform.catalog.manage`).

---

## 4. Non-régression

Recommandé avant merge Lot 1 : rejouer Phase 1–3 (inchangé par Lot 0 hors ajout interface Desktop).

---

## 5. Demande de validation

- [x] Checklist Lot 0 complétée (N/A justifiés)  
- [x] D1 / D2 respectés (aucun dual-run, aucun flag)  

**Validation Lot 0 demandée** pour enchaîner **Lot 1 — Finance & paiements**.
