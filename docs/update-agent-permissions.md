# Permissions Update Agent (Lot 2B-5A)

**Ne pas appliquer en production.** `Agent:AutoDeploy` reste `false`. Aucun GRANT sur les bases école.

Scripts labo (non exécutés contre la prod) :

- [`scripts/update-agent-windows-acl-lab.ps1`](../scripts/update-agent-windows-acl-lab.ps1) — défaut `-WhatIf`
- [`scripts/update-agent-restore-signed-proc-lab.sql`](../scripts/update-agent-restore-signed-proc-lab.sql) — certificat + procédure signée, **UpdateIntegration uniquement**

## SQL — architecture C (labo 2B-5B)

`Agent:AutoDeploy` reste `false`. UpdateAgent **n’est pas** owner, **pas** `db_owner`, **pas** sysadmin, **pas** dbcreator.

| Principal | Droits |
|---|---|
| `ErpScolaireUA_Lab2B5B` | `CONNECT` ; `BACKUP DATABASE` ; rôle `ua_migrator` ; `EXECUTE` sur `ErpScolaire_RestoreSchoolDatabase` **et** `ErpScolaire_VerifySchoolBackup` |
| `ErpScolaireRestoreCert_Lab` (login certificat, `DENY CONNECT SQL`) | `CREATE ANY DATABASE` (HEADERONLY/VERIFYONLY, hors rôle `dbcreator`) + `IMPERSONATE` sur `ErpScolaireRestoreOwner_Lab` pour `RESTORE` ; **pas** owner (Msg 15353) |
| `ErpScolaireRestoreOwner_Lab` (`DENY CONNECT SQL`) | `owner_sid` de `SchoolManagementRDC_UpdateIntegration` — utilisé uniquement via `EXECUTE AS LOGIN` dans les procédures signées |

`RESTORE DATABASE` direct par l’agent → Msg 3110. Restore autorisé uniquement via la procédure signée (base + répertoire Backup figés, header `.bak`, CHECKSUM, pas UNC).
`TRUSTWORTHY` reste OFF. Pas d’`EXECUTE AS OWNER`.

## Windows — compte `ErpScolaireUpdateAgent`

Autorisé :

| Droit | Cible |
|---|---|
| Modify | `%ProgramData%\ERP_SCOLAIRE\UpdateAgent` (credential, state, staging, logs) |
| Modify | `%ProgramData%\ERP_SCOLAIRE\Backups` (rétention `.bak` côté agent) |
| Start / Stop / Query | service **ErpScolaireApi** uniquement (`sc sdset` limité) |
| Modify (rename) | parent d’`Api` (`Api`, `Api.Previous`, `Api.Incoming-*`) — même volume NTFS |
| SeServiceLogonRight | logon du service agent |

Interdit :

- LocalSystem, Administrators
- Share UNC élèves, Desktop, Mobile, Bootstrap
- Contrôle d’autres services Windows
- Écriture dans `Api\` fichier par fichier (swap par rename uniquement)

Le service SQL (`NT SERVICE\MSSQL$<instance>`) doit avoir **Modify** sur `Backups\` : c’est lui qui écrit le `.bak`, pas l’agent.

## SQL — login labo, pas sysadmin

Nom de base : **configuration locale** (`Agent:ExpectedDatabaseName` = `InitialCatalog`), jamais Bootstrap.

Expérience 2B-5A sur `SchoolManagementRDC_UpdateIntegration` uniquement (login `ErpScolaireUA_Lab2B5`, **pas** sysadmin / **pas** dbcreator) :

| Opération | Résultat |
|---|---|
| `BACKUP DATABASE` | OK |
| `SELECT` / `UPDATE dbo.AppSchemaVersion` | OK |
| DDL `CREATE TABLE` + `ALTER SCHEMA::dbo` | OK |
| `ALTER DATABASE … SINGLE_USER` / `MULTI_USER` (depuis `master`) | OK |
| `RESTORE DATABASE` sans être propriétaire | **Msg 3110** |
| rôle `db_owner` puis `RESTORE` depuis `master` | **Msg 3110** (insuffisant) |
| `ALTER AUTHORIZATION` (propriétaire réel `owner_sid`) puis `RESTORE` depuis `master` | **OK** |

Expérience 2B-5A (contexte) : `db_owner` ≠ permission RESTORE (Msg 3110). Le propriétaire réel (`owner_sid`) est requis. **2B-5B** : ce propriétaire est le **login certificat**, pas l’UpdateAgent.
