# Synchronisation cloud (local → distant) — architecture production

## Principe

- La **base SQL locale** est la source de vérité.
- Le **cloud** est une copie synchronisée (lecture / secours / futures apps).
- Sens unique : **Local → Cloud**.
- Les utilisateurs travaillent toujours en local, même hors ligne.

## Architecture (v2 — outbox)

```
SaveChanges (métier)
    → capture INSERT/UPDATE/DELETE
    → file SyncOutboxUnit + SyncOutboxItem (persistante)
CloudSyncHostedService
    → drain critique (~30 s) : paiements / encaissements
    → catch-up watermark + drain complet (INTERVALLE_MINUTES)
    → transaction SQL distante **par unité** (tout ou rien)
    → SyncJournal + watermarks
```

### Mécanisme de détection des changements

**Outbox + `CreatedAt` / `UpdatedAt` / `DeletedAt`** (déjà présents sur `AuditableEntity`).

| Option | Pourquoi non retenu comme principal |
|--------|-------------------------------------|
| SQL Change Tracking | Maintenance / droits SQL plus lourds |
| RowVersion partout | Migration intrusive sur toutes les tables |
| Full table upsert (v1) | Conservé uniquement pour **bootstrap** initial |

### Priorités

| Priorité | Tables |
|----------|--------|
| Critical | Payments, PaymentLines, PaymentReversals, CashMovements, FinRepartitionRecette, FinRetenueApplication, StudentFeeBalances |
| Normal | Élèves, classes, notes… |
| Low | Paramètres / référentiels |

Un **paiement** est regroupé en une **unité transactionnelle** (agrégat `Payment`) : paiement + lignes + répartitions + retenues + mouvements + soldes.

## Tables locales (métadonnées sync)

| Table | Rôle |
|-------|------|
| `SyncOutboxUnit` | Unité transactionnelle (priorité, statut, tentatives) |
| `SyncOutboxItem` | Ligne (table, Id, INSERT/UPDATE/DELETE) |
| `SyncJournal` | Historique des exécutions |
| `SyncWatermark` | Filigrane catch-up par table |

Créées au démarrage API via `CloudSyncSchemaInitializer`.

## Fichiers de configuration

| Fichier | Rôle |
|---------|------|
| `ServeurDonnees.txt` | SQL local |
| `ServeurDonneesCloud.txt` | SQL distant — **gitignored**, mot de passe DPAPI |
| `CloudSyncState.txt` | Dernier résultat (complément du journal SQL) |

```powershell
cd "d:\Mes Projet\ERP_Administration_Scolaire_2026"
.\scripts\configure-cloud-sync.ps1 `
  -Server "161.97.105.22" `
  -User "sa" `
  -Password "VOTRE_MOT_DE_PASSE" `
  -Database "SchoolManagementRDC" `
  -Actif 1 `
  -IntervalMinutes 5 `
  -ApiDir "src\SchoolManagement.API\bin\Debug\net8.0"
```

## API

| Méthode | Route | Description |
|---------|-------|-------------|
| GET | `/api/v1/cloud-sync/status` | Tableau de bord |
| POST | `/api/v1/cloud-sync/synchronize` | Forcer une sync (`?criticalOnly=true` optionnel) |

Permission : `admin.full`.

## Desktop

**Paramètres → Administration système → Synchronisation cloud**

- état connexion cloud
- dernière sync réussie
- file en attente / critiques / dead-letter
- durée moyenne
- journal récent
- bouton **Synchroniser maintenant**

## Contrats applicatifs

- `ICloudSyncFacade` — API / UI
- `ICloudSyncEngine` — moteur remplaçable
- `ICloudSyncOutboxWriter` — enfilement post-`SaveChanges`

## Tolérance aux pannes

- Internet coupé → outbox conserve les opérations ; reprise auto.
- Redémarrage → unités `InProgress` périmées repassent en `Pending`.
- Échec SQL → rollback transaction distante ; unité `Failed` puis `DeadLetter` après 8 essais.
- Idempotence → upsert par `Id` (pas de doublon).

## Évolutions prévues (sans refonte)

- `SchoolId` sur `SyncOutboxUnit` (multi-écoles)
- Remplacement du moteur EF par API cloud centralisée (`ICloudSyncEngine`)
- Consommation cloud pour mobile / portail promoteur

## Sécurité

- Ne jamais committer `ServeurDonneesCloud.txt`.
- Préférer un compte SQL dédié `erp_sync` (droits limités) plutôt que `sa`.
