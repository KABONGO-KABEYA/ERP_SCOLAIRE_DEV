# Contrat de package Release — API + Migration (Lot 2B-2)

**Baseline schéma : `AppSchemaVersion = 1`.**  
**ProtocolVersion API (health / discovery) : `2`.**  
Aucun Update Agent, aucun déploiement automatique, aucun branchement au start API.

## Architecture

Une release Bootstrap lie **deux zips** à la même ligne `UpdateRelease` :

```
Release {version}
│  ReleaseId, Channel, ProtocolVersion = 2
│  FromSchemaVersion, SchemaVersion (= toSchema)
│
├── Artifact Api        ZIP + SHA256 + taille + URL HTTPS
└── Artifact Migration  ZIP + SHA256 + taille + URL HTTPS
```

Le moteur `MigrationManager` ne télécharge jamais de SQL : l’agent futur unzip, vérifie, puis passe un **dossier local**.

## ZIP API

- Contenu : `dotnet publish` win-x64 + `api-manifest.json`
- **Exclus** : `ServeurDonnees.txt`, `ServeurDonneesCloud.txt`, `ServeurFichiers.txt`
- Pas le Setup.exe / payload USB (`build-setup.ps1` reste l’installateur)

```json
{
  "artifactType": "Api",
  "releaseVersion": "1.2.0",
  "requiredSchemaVersion": 3,
  "protocolVersion": 2,
  "runtime": "win-x64"
}
```

`requiredSchemaVersion` = `AppSchemaContract.RequiredSchemaVersion` = `toSchemaVersion` du package migration.

## ZIP Migration

```json
{
  "schemaVersion": 3,
  "fromSchemaVersion": 1,
  "toSchemaVersion": 3,
  "releaseVersion": "1.2.0",
  "migrations": ["Migration1_2.sql", "Migration2_3.sql"],
  "files": [
    { "name": "Migration1_2.sql", "sha256": "…" },
    { "name": "Migration2_3.sql", "sha256": "…" }
  ]
}
```

Packages de test 2B-1 sans `files` restent valides en local. Une release packagée **exige** `files`.

## Liaison

- `Artifact.Version` = `Release.Version`
- Api et Migration **ensemble** (XOR interdit)
- Check public Desktop inchangé (`GET /api/v1/releases/check`)
- Check agent : `GET /api/v1/agent/releases/check` → **Api + Migration** de la **même** `ReleaseId`

## Compatibilité (futur agent)

| Axe | Règle |
|-----|--------|
| DesktopVersion | Hors ce lot. Desktop reste sur `ApplicationVersions`. |
| API Version | InformationalVersion du DLL = release. Overlay **après** schéma cible. |
| SchemaVersion | Monotone. `current > to` refus ; `current < from` refus. |
| ProtocolVersion | Après update, `/api/health` = 2 = catalogue. |

Séquence prévue : Backup → vérif hashes → migration SQL → stop API → overlay (sans secrets) → start. Restore hors périmètre.

## Packaging

```powershell
.\scripts\pack-release-artifacts.ps1 -Version 1.2.0
```

Produit `dist/releases/{channel}/{version}/` (immuable) : zips, `release-bundle.json`, `SHA256SUMS`.

## Stockage VPS

```
/releases/{channel}/{version}/   # jamais écrasé
```

HTTPS. SQL Bootstrap = URL + SHA, pas de BLOB. `Blocked` ne supprime pas les fichiers.

## Rollback

- Check = plus haut SemVer encore `Published`
- Rollback catalogue = `Blocked`
- Rollback API seulement si `RequiredSchemaVersion` de l’ancienne API = schéma actuel
- Pas de SQL inverse. Downgrade schéma = restore
