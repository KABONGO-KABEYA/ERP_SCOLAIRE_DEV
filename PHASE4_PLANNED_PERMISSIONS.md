# Phase 4 — Permissions planifiées (catalogue à créer par lot)

Validées avec le plan d’exécution Phase 4.

| Code | Lot | Usage |
|------|-----|--------|
| `personnel.read` | 2 | ✅ PersonnelController GET |
| `personnel.manage` | 2 | ✅ PersonnelController mutations |
| `teachers.manage` | 2 | ✅ AdminController teachers |
| `geography.manage` | 3 | ✅ GeographyAdminController |
| `cloud-sync.manage` | 3 | ✅ CloudSyncController |
| `updates.manage` | 3 | ✅ UpdateController |
| `parent-activation.manage` | 3 | ✅ ParentActivationIssueController |
| `pedagogical-periods.manage` | 4 | ✅ PedagogicalPeriodsController mutations |
| `payments.cancel` | 1 | ✅ Annulation paiement |
| `payments.notes.update` | 1 | ✅ Notes paiement |
| `payments.paid-mutation` | 1 | ✅ PaymentMutationPolicy + Desktop |
| `pricing-categories.assign` | 1 | ✅ PricingCategoryAssignmentViewModel |
| `finance.admin` | 1 | _Non retenu_ — couvert par `pricing-categories.assign` sur la route Finance |

## Lot 6 — Cotation (gate [`PHASE4_LOT6_ATELIER_COTATION.md`](PHASE4_LOT6_ATELIER_COTATION.md))

| Code | Statut | Usage |
|------|--------|--------|
| `grades.read` | ✅ | Consultation |
| `grades.create` | ✅ | Créer évaluation ; saisie notes |
| `grades.update` | ✅ | Modifier notes / évaluations ; recalc auto post-saisie |
| `grades.delete` | ✅ | Supprimer évaluation sans notes |
| `grades.evaluation.delete-with-grades` | ✅ | Supprimer évaluation notée |
| `grades.recalculate` | ✅ | Recalcul manuel `POST period-results/calculate` |
| `grades.publish` | ✅ | Publication visibilité parent |
| `grades.unpublish` | ✅ | Annulation publication |
| `grades.cotation.delegate` | ✅ | Session cotation pour un autre enseignant |
| `grades.cotation.scope.class` | ✅ | Périmètre titulaire (assignation rôle) |
