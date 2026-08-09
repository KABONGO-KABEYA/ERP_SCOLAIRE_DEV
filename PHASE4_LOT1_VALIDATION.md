# Phase 4 — Validation Lot 1 (Finance & paiements)

**Date** : 2026-08-07  
**Statut** : **VALIDÉ** (commanditaire) — Lot 1 clôturé  
**Harness** : `dotnet run --project tools/Phase4SecurityValidation` (Lot 0 + Lot 1)

---

## 1. Migration Checklist (Lot 1)

| # | Critère | Statut | Preuve / commentaire |
|---|---------|--------|----------------------|
| 1 | Permissions créées et seedées | **OK** | `Permissions.cs` : `payments.cancel`, `payments.notes.update`, `payments.paid-mutation`, `pricing-categories.assign` ; metadata + deps + rôles dans `SecurityCatalogSeeder` |
| 2 | Rôles mis à jour | **OK** | **COMPTABLE** : cancel, notes, paid-mutation, pricing assign, `payment-fx.update` ; **CAISSIER** inchangé (sans actions sensibles) ; **ADMIN** via `admin.full` |
| 3 | Navigation mise à jour | **OK** | Page `Finance.CategoriesTarifaires` : `RequiredPermissionCode` → `payments.read` (COMPTABLE voit l’écran ; affectation via permission dédiée) |
| 4 | API migrées | **OK** | `PaymentsController` (cancel, notes, amount) ; `FinanceController` PUT pricing-assignments — plus de `AdminFull` |
| 5 | Services migrés | **OK** | `PaymentMutationPolicy`, `PaymentService`, `FinanceOperationService` ; tranches payées `SchoolFeeService` → `payments.paid-mutation` |
| 6 | Desktop migré | **OK** | Encaissements, CollectPayment, ExpenseMultiCurrency, PricingCategory, `EncaissementActionWindow` → `SessionPermissions.Can` |
| 7 | Harness du lot en succès | **OK** | **28/28 PASS** — `tools/Phase4SecurityValidation/out/summary.txt`, `out/evidence.json` |
| 8 | Legacy supprimé (lot) | **OK** | Grep harness : 0 `AdminFull` finance API ; 0 `IsAdministrator` périmètre Desktop finance ; policy sans `IsAdministrator` |
| 9 | Documentation | **OK** | `PHASE4_PERSONAS.md`, template checklist § Impact métier, ce rapport |

---

## 2. Impact métier

| Ancien mécanisme | Nouveau mécanisme | Justification fonctionnelle |
|------------------|-------------------|----------------------------|
| `[Authorize(Policy = admin.full)]` — POST `{id}/cancel` | `payments.cancel` | L’annulation d’un encaissement complet est une correction comptable ; réservée au profil comptable / admin, pas au caissier seul. |
| `[Authorize(Policy = admin.full)]` — PUT `{id}/notes` | `payments.notes.update` | Les notes peuvent être ajustées sans refondre le montant ; permission distincte pour traçabilité. |
| `[Authorize(Policy = admin.full)]` — PUT `{id}/amount` | `payments.paid-mutation` | Modifier un versement déjà encaissé (montant, détail) respecte la politique rétrograde ; hors périmètre encaissement courant. |
| `[Authorize(Policy = admin.full)]` — PUT `pricing-assignments` | `pricing-categories.assign` | Changer la catégorie tarifaire d’un élève impacte les montants dus ; action de gestion financière, pas lecture seule. |
| `PaymentMutationPolicy.EnsureAdministrator` / `ICurrentUser.IsAdministrator` (Application) | `HasPermission` sur `payments.cancel`, `payments.notes.update`, `payments.paid-mutation`, `pricing-categories.assign` | Alignement API ↔ services ↔ effectif BD ; plus de bypass implicite rôle JWT ADMIN. |
| `PaymentService` override FX : `IsAdministrator` OU `payment-fx.update` | `payment-fx.update` uniquement | Le taux forcé à l’encaissement est une exception contrôlée ; le comptable seedé reçoit la permission explicite. |
| Desktop `IsAdministrator` (Encaissements, fenêtre actions, catégories tarifaires, dépenses multi-devises) | `SessionPermissions.Can` + codes ci-dessus | Parité UI / JWT ; le caissier ne voit plus les actions d’annulation ou de mutation sur payé. |

**Hors périmètre Lot 1 (inchangé)** : `IAuthSessionService.IsAdministrator` conservé pour les lots futurs (ex. cotation) ; `SchoolFeeService` mutations tranches → `payments.paid-mutation` (finance liée).

---

## 3. Livrables Lot 1

| Zone | Fichiers principaux |
|------|---------------------|
| Catalogue | `Permissions.cs`, `SecurityCatalogSeeder.cs` |
| API | `PaymentsController.cs`, `FinanceController.cs` |
| Application | `PaymentMutationPolicy.cs`, `PaymentService.cs`, `FinanceOperationService.cs`, `SchoolFeeService.cs` |
| Desktop | `EncaissementsViewModel.cs`, `CollectPaymentViewModel.cs`, `ExpenseMultiCurrencyAllocationViewModel.cs`, `PricingCategoryAssignmentViewModel.cs`, `EncaissementActionWindow.xaml.cs` |
| Harness | `tools/Phase4SecurityValidation/Program.cs` (scénarios Lot 1 + seed) |

---

## 4. Harness

```text
dotnet run --project tools/Phase4SecurityValidation
```

Contrôles Lot 1 : grep legacy finance, présence des codes dans `Permissions.All`, personas **COMPTABLE** / **CAISSIER** sur permissions sensibles.

**Dernière exécution** : **28/28 PASS** (exit code 0, Lot 0 + Lot 1).

---

## 5. Grep legacy (Lot 1)

```text
rg "AdminFull" src/SchoolManagement.API/Controllers/PaymentsController.cs
rg "AdminFull" src/SchoolManagement.API/Controllers/FinanceController.cs
rg "IsAdministrator" src/SchoolManagement.Application/Payments/
rg "IsAdministrator" src/SchoolManagement.Application/Finance/
rg "IsAdministrator" src/SchoolManagement.Desktop/ViewModels/EncaissementsViewModel.cs
rg "IsAdministrator" src/SchoolManagement.Desktop/ViewModels/PricingCategoryAssignmentViewModel.cs
```

**Attendu** : 0 occurrence sur ces chemins.

---

## 6. Demande de validation

- [x] Checklist Lot 1 complétée  
- [x] Section Impact métier renseignée  
- [x] D1 / D2 respectés (cutover direct, pas de flag)  

**Validation Lot 1 demandée** pour enchaîner **Lot 2 — Personnel & enseignants admin**.
