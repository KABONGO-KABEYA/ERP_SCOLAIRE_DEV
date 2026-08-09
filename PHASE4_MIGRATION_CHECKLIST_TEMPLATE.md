# Phase 4 — Migration Checklist (modèle)

**Lot** : _LOT n — nom du module_  
**Date** : _AAAA-MM-JJ_  
**Auteur / PR** : _…_  
**Harness** : `dotnet run --project tools/Phase4SecurityValidation` → _score_

> À remplir **intégralement** avant toute demande de validation du lot.  
> Copier ce modèle dans `PHASE4_LOT{n}_VALIDATION.md` (section 1).

---

## Checklist

| # | Critère | Statut | Preuve / commentaire |
|---|---------|--------|----------------------|
| 1 | **Permissions** créées dans `Permissions.cs`, catalogue seed (metadata), dépendances si besoin | ☐ OK · ☐ N/A | |
| 2 | **Rôles** mis à jour (`SecurityCatalogSeeder` / matrice) | ☐ OK · ☐ N/A | |
| 3 | **Navigation** : `RequiredPermissionCode` / pages nav alignées API | ☐ OK · ☐ N/A | |
| 4 | **API** : policies migrées ; `[Authorize(Policy = AdminFull)]` retiré du périmètre | ☐ OK · ☐ N/A | |
| 5 | **Services** Application : plus de `HasRole` / `HasElevatedRole` / `IsAdministrator` legacy dans le périmètre | ☐ OK · ☐ N/A | |
| 6 | **Desktop** : UI sur `HasPermission` / `SessionPermissions.Can` (pas rôle ADMIN JWT) | ☐ OK · ☐ N/A | |
| 7 | **Harness** du lot : **PASS**, preuves `tools/Phase4SecurityValidation/out/` | ☐ OK · ☐ N/A | |
| 8 | **Legacy supprimé** (grep périmètre lot) : `HasRole`, `HasElevatedRole`, `IsAdministrator` métier, `admin.full` sur routes migrées | ☐ OK · ☐ N/A | |
| 9 | **Documentation** mise à jour (plan, README, commentaires métier) | ☐ OK · ☐ N/A | |

---

## Grep legacy (lot)

```text
# Exemples — adapter chemins du lot
rg "AdminFull" src/SchoolManagement.API/Controllers/<Module>*.cs
rg "IsAdministrator|HasRole|HasElevatedRole" src/SchoolManagement.Application/<Module>/
rg "IsAdministrator" src/SchoolManagement.Desktop/ViewModels/<...>
```

**Résultat attendu** : _0 occurrence_ sur le périmètre migré (sauf Lot 7 global).

---

## Impact métier (lots ≥ 1)

Tableau obligatoire : remplacement des anciens mécanismes d'autorisation par les nouvelles permissions.

| Ancien mécanisme | Nouveau mécanisme | Justification fonctionnelle |
|------------------|-------------------|----------------------------|
| _ex. `admin.full` sur POST cancel_ | _ex. `payments.cancel`_ | _ex. Seuls les profils habilités à corriger un encaissement peuvent annuler._ |
| _…_ | _…_ | _…_ |

---

## Impact utilisateur (lots ≥ 2)

Décrire, **par rôle** (ADMIN, DIRECTION, ENSEIGNANT, COMPTABLE, CAISSIER, PARENT, …), les changements **visibles** dans l’application (menu, boutons, messages) après migration du lot.  
Objectif : alimenter guides utilisateurs, formation et notes de version.

| Rôle | Avant (comportement visible) | Après (comportement visible) |
|------|------------------------------|------------------------------|
| _ADMIN_ | _…_ | _…_ |
| _DIRECTION_ | _…_ | _…_ |

---

## Non-régression

| Harness | Résultat |
|---------|----------|
| Phase 4 (lot courant) | |
| Phase 3 | |
| Phase 2 | |
| Phase 1 | |

---

## Validation demandée

- [ ] Checklist complète  
- [ ] Pas de dual-run (D1)  
- [ ] Pas de feature flag rollback (D2)  

**Demande de validation** : _date / destinataire_
