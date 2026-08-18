# Schéma école — contrat de déploiement

## Mécanisme officiel (actuel)

| Mécanisme | Rôle |
|-----------|------|
| `database/scripts/001_InitialCreate_EF.sql` | **Baseline historique immuable** (`20260706114538_InitialCreate`). Ne pas la régénérer à chaque version. |
| `*SchemaInitializer` (Setup + démarrage API) | **Évolution officielle du schéma.** Idempotent (`IF NOT EXISTS`). Toute nouvelle évolution SQL doit avoir un initializer, ou une exclusion dans `SchemaDeploymentCoverage`. |
| Migrations EF `Infrastructure/Persistence/Migrations` | **Artefacts de modèle / historique de développement.** Non exécutées par le Setup ni par l’API. |
| `Database.Migrate()` | **Interdit** dans le chemin Setup/API actuel. |

`__EFMigrationsHistory` après une installation Setup contient uniquement `InitialCreate`. Ce n’est pas une preuve que le schéma s’arrête à cette migration : les SchemaInitializers complètent le schéma sans inscrire les migrations EF.

Couverture obligatoire : `SchoolManagement.Infrastructure.Persistence.SchemaDeploymentCoverage`.
Le test `SchemaDeploymentCoverageTests` échoue si une migration EF post-baseline n’y figure pas.

## MigrationManager (Lot 2B-1, non branché)

`SchoolManagement.Updates.MigrationManager` peut appliquer un package local `MigrationN_N+1.sql`. **Il n’est pas branché** au démarrage API. Tant qu’il ne l’est pas, les SchemaInitializers restent le seul mécanisme officiel d’évolution.

## Permissions SQL (moteur uniquement — pas de GRANT ici)

Opérations : `SELECT`/`UPDATE` `dbo.AppSchemaVersion` ; `CREATE TABLE` de cette table si absente ; DDL des scripts `MigrationN_N+1` dans `dbo`.

Ne pas accorder `db_owner`, `db_ddladmin`, `sysadmin` dans ce lot.

## Séquence agent (non implémentée ici)

`Backup` → vérification → `ApplyPackageAsync` (restore hors 2B-1).
