# Synchronisation cloud (local â†’ distant) â€” architecture production

## Principe

- La **base SQL locale** est la source de vÃ©ritÃ©.
- Le **cloud** est une copie synchronisÃ©e (lecture / secours / futures apps).
- Sens unique : **Local â†’ Cloud**.
- Les utilisateurs travaillent toujours en local, mÃªme hors ligne.

## Architecture (v2 â€” outbox)

```
SaveChanges (mÃ©tier)
    â†’ capture INSERT/UPDATE/DELETE
    â†’ file SyncOutboxUnit + SyncOutboxItem (persistante)
CloudSyncHostedService
    â†’ drain critique (~30 s) : paiements / encaissements
    â†’ catch-up watermark + drain complet (INTERVALLE_MINUTES)
    â†’ transaction SQL distante **par unitÃ©** (tout ou rien)
    â†’ SyncJournal + watermarks
```

### MÃ©canisme de dÃ©tection des changements

**Outbox + `CreatedAt` / `UpdatedAt` / `DeletedAt`** (dÃ©jÃ  prÃ©sents sur `AuditableEntity`).

| Option | Pourquoi non retenu comme principal |
|--------|-------------------------------------|
| SQL Change Tracking | Maintenance / droits SQL plus lourds |
| RowVersion partout | Migration intrusive sur toutes les tables |
| Full table upsert (v1) | ConservÃ© uniquement pour **bootstrap** initial |

### PrioritÃ©s

| PrioritÃ© | Tables |
|----------|--------|
| Critical | Payments, PaymentLines, PaymentReversals, CashMovements, FinRepartitionRecette, FinRetenueApplication, StudentFeeBalances |
| Normal | Ã‰lÃ¨ves, classes, notesâ€¦ |
| Low | ParamÃ¨tres / rÃ©fÃ©rentiels |

Un **paiement** est regroupÃ© en une **unitÃ© transactionnelle** (agrÃ©gat `Payment`) : paiement + lignes + rÃ©partitions + retenues + mouvements + soldes.

## Tables locales (mÃ©tadonnÃ©es sync)

| Table | RÃ´le |
|-------|------|
| `SyncOutboxUnit` | UnitÃ© transactionnelle (prioritÃ©, statut, tentatives) |
| `SyncOutboxItem` | Ligne (table, Id, INSERT/UPDATE/DELETE) |
| `SyncJournal` | Historique des exÃ©cutions |
| `SyncWatermark` | Filigrane catch-up par table |

CrÃ©Ã©es au dÃ©marrage API via `CloudSyncSchemaInitializer`.

## Fichiers de configuration

| Fichier | RÃ´le |
|---------|------|
| `ServeurDonnees.txt` | SQL local |
| `ServeurDonneesCloud.txt` | SQL distant â€” **gitignored**, mot de passe DPAPI |
| `CloudSyncState.txt` | Dernier rÃ©sultat (complÃ©ment du journal SQL) |

```powershell
cd "d:\Mes Projet\ERP_Administration_Scolaire_2026"
.\scripts\configure-cloud-sync.ps1 `
  -Server "169.58.93.203" `
  -User "sa" `
  -Password "VOTRE_MOT_DE_PASSE" `
  -Database "SchoolManagementRDC" `
  -Actif 1 `
  -IntervalMinutes 5 `
  -ApiDir "src\SchoolManagement.API\bin\Debug\net8.0"
```

## API

| MÃ©thode | Route | Description |
|---------|-------|-------------|
| GET | `/api/v1/cloud-sync/status` | Tableau de bord |
| POST | `/api/v1/cloud-sync/synchronize` | Forcer une sync (`?criticalOnly=true` optionnel) |

Permission : `admin.full`.

## Desktop

**ParamÃ¨tres â†’ Administration systÃ¨me â†’ Synchronisation cloud**

- Ã©tat connexion cloud
- derniÃ¨re sync rÃ©ussie
- file en attente / critiques / dead-letter
- durÃ©e moyenne
- journal rÃ©cent
- bouton **Synchroniser maintenant**

## Contrats applicatifs

- `ICloudSyncFacade` â€” API / UI
- `ICloudSyncEngine` â€” moteur remplaÃ§able
- `ICloudSyncOutboxWriter` â€” enfilement post-`SaveChanges`

## TolÃ©rance aux pannes

- Internet coupÃ© â†’ outbox conserve les opÃ©rations ; reprise auto.
- RedÃ©marrage â†’ unitÃ©s `InProgress` pÃ©rimÃ©es repassent en `Pending`.
- Ã‰chec SQL â†’ rollback transaction distante ; unitÃ© `Failed` puis `DeadLetter` aprÃ¨s 8 essais.
- Idempotence â†’ upsert par `Id` (pas de doublon).

## Ã‰volutions prÃ©vues (sans refonte)

- `SchoolId` sur `SyncOutboxUnit` (multi-Ã©coles)
- Remplacement du moteur EF par API cloud centralisÃ©e (`ICloudSyncEngine`)
- Consommation cloud pour mobile / portail promoteur

## SÃ©curitÃ©

- Ne jamais committer `ServeurDonneesCloud.txt`.
- PrÃ©fÃ©rer un compte SQL dÃ©diÃ© `erp_sync` (droits limitÃ©s) plutÃ´t que `sa`.
