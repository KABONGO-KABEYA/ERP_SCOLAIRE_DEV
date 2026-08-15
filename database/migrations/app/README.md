# Migrations versionnées du schéma école (Lot 2B-1)

**Baseline officielle : `AppSchemaVersion = 1`.**

Les bases actuelles (API `ApplicationUpdateSchemaInitializer`, script `007_ApplicationVersions.sql`) créent déjà cette valeur. `MigrationManager` utilise la même baseline.

## Moteur

`SchoolManagement.Updates.MigrationManager` applique un **package local** :

```
MigrationPackage/
├── manifest.json
├── Migration1_2.sql
├── Migration2_3.sql
└── …
```

Le moteur **ne télécharge jamais** de SQL. Le futur Update Agent télécharge et vérifie le package (SHA), puis passe le chemin local.

## Manifest

```json
{
  "schemaVersion": 3,
  "fromSchemaVersion": 1,
  "toSchemaVersion": 3,
  "migrations": [
    "Migration1_2.sql",
    "Migration2_3.sql"
  ]
}
```

`schemaVersion` = `toSchemaVersion`. Chaîne stricte `MigrationN_N+1.sql` sans rupture.

Package actuel (ce dossier) : **1 → 1**, aucune migration — le schéma vivant est encore posé par les *SchemaInitializers* au démarrage de l’API.

Lot 2B-2 : une release packagée ajoute `releaseVersion` et `files[].sha256`. Voir [`docs/release-package-contract.md`](../../docs/release-package-contract.md). `scripts/pack-release-artifacts.ps1` produit les zips.

## Historique vs futur

| Mécanisme | Rôle |
|-----------|------|
| `*SchemaInitializer` au start API | **Historique**, encore requis. **Ne plus en ajouter** pour les versions futures. |
| `MigrationManager` + ce dossier | **Officiel** pour les évolutions N→N+1 à venir. **Pas encore** branché au démarrage API. |
| Migrations EF `Infrastructure/Persistence/Migrations` | Outil de dev ; l’API n’appelle pas `Database.Migrate()`. |

## Permissions SQL (moteur uniquement — pas de GRANT ici)

Opérations : `SELECT`/`UPDATE` `dbo.AppSchemaVersion` ; `CREATE TABLE` de cette table si absente ; DDL des scripts `MigrationN_N+1` dans `dbo`.

Ne pas accorder `db_owner`, `db_ddladmin`, `sysadmin` dans ce lot. Compte Windows / GRANT du futur agent : lot ultérieur.

## Séquence agent (non implémentée ici)

`Backup` → vérification → `ApplyPackageAsync` (restore hors 2B-1).
