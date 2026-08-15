# Migrations EF Core

Les migrations EF Core sont générées dans :

`src/SchoolManagement.Infrastructure/Persistence/Migrations/`

## Connexion SQL Server

```
Server=localhost\HEROS_SQL19
Database=SchoolManagementRDC_Development
Trusted_Connection=True
```

> Bases séparées : Development = `SchoolManagementRDC_Development`, Production = `SchoolManagementRDC_Production`.  
> Voir `docs/rapport-db-production-development.md`.

> L'instance détectée sur cette machine est **HEROS_SQL19** (SQL Server 2019).

## Commandes

```bash
# Ajouter une migration
.tools\dotnet-ef migrations add <NomMigration> \
  --project src/SchoolManagement.Infrastructure \
  --startup-project src/SchoolManagement.API \
  --output-dir Persistence/Migrations

# Appliquer à la base
$env:ASPNETCORE_ENVIRONMENT='Production'
.tools\dotnet-ef database update \
  --project src/SchoolManagement.Infrastructure \
  --startup-project src/SchoolManagement.API

# Exporter script SQL idempotent
.tools\dotnet-ef migrations script --idempotent \
  -o database/scripts/001_InitialCreate_EF.sql \
  --project src/SchoolManagement.Infrastructure \
  --startup-project src/SchoolManagement.API
```

## Scripts SQL complémentaires (déploiement manuel)

| Script | Contenu |
|--------|---------|
| `database/scripts/001_InitialCreate_EF.sql` | Tables EF Core (idempotent) |
| `database/scripts/002_Views_Procedures_Functions.sql` | Vues, fonctions, procédures stockées |
| `database/scripts/003_SeedData.sql` | Données de démonstration |
| `database/scripts/004_PedagogicalStructure.sql` | Structure pédagogique RDC + locaux |

Ordre d'exécution : `001` → `002` → `003` → `004`

## Migrations versionnées N→N+1 (Lot 2B-1)

Voir [`app/README.md`](app/README.md). Baseline `AppSchemaVersion = 1`. Distinct des migrations EF ci-dessus.
