# Update Agent (Lots 2B-3 / 2B-4B)

Service Windows **ErpScolaireUpdateAgent** (compte dédié, pas LocalSystem).

`Agent:AutoDeploy` = **false** par défaut : check + staging seulement. Le déploiement réel n’est pas activé en production tant que ce drapeau n’est pas validé. Une reprise reprend un déploiement **déjà engagé** (Preflight…HealthChecking) même si AutoDeploy est false.

## Ordre de déploiement (2B-4B)

Verified → Preflight → Stop `ErpScolaireApi` → Backup COPY_ONLY+CHECKSUM+VERIFYONLY → `IMigrationEngine.ApplyLocalPackageAsync` (`MigrationManager.ApplyPackageAsync` sur le package local) → rename-swap (`Api`→`Api.Previous`, `Api.Incoming-{ver}`→`Api`) → Start service → Health ×3 → Completed.

`Api.Previous` est **conservé** après Completed (nettoyage seulement au déploiement suivant).

Restore SQL uniquement si `schemaAfter > schemaBefore` et nouvelle API KO, depuis le `.bak` enregistré dans l’état, whitelist, extension `.bak`, base ERP locale (`ExpectedDatabaseName`). Connexion restore = **master**. L’agent n’émet plus de `RESTORE DATABASE` brut : il exécute `dbo.ErpScolaire_RestoreSchoolDatabase` (procédure **signée**, labo 2B-5B sur `SchoolManagementRDC_UpdateIntegration`). Aucun nom de base Bootstrap.

Aucun GRANT SQL de production. Labo : [`docs/update-agent-permissions.md`](update-agent-permissions.md). Owner labo = `ErpScolaireRestoreOwner_Lab` (`DENY CONNECT`). Signataire = certificat : `CREATE ANY DATABASE` (verify) + `IMPERSONATE` du owner (`EXECUTE AS LOGIN` pour RESTORE, pas `EXECUTE AS OWNER`). L’UpdateAgent n’est pas owner.

## Health

`GET /api/health` : `status=ok`, `protocolVersion`, `version`, `schemaVersion` (snapshot au **démarrage**), `identity.serverInstanceId`. 3 OK / 2 s / budget 90 s.

## Permissions

Aucun GRANT SQL dans l’installeur. Dossier `Backups\` créé ; le service SQL devra y écrire plus tard. Swap API : ACL parent `{InstallRoot}` — lot permissions Windows/SQL ultérieur.
