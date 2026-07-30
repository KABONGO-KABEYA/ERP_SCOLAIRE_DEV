# Mises à jour automatiques

## Architecture

| Couche | Emplacement |
|--------|-------------|
| Module client .NET | `src/SchoolManagement.Updates/` (`VersionManager`, `DownloadManager`, `UpdateManager`, `MigrationManager`, settings/history) |
| API | `GET /api/v1/update/check?platform=desktop\|mobile&currentVersion=` |
| Table SQL | `ApplicationVersions`, `AppSchemaVersion` (`database/scripts/007_ApplicationVersions.sql`) |
| Desktop | `SchoolManagement.Desktop/Updates/` + Paramètres > Mises à jour |
| Mobile | `lib/core/updates/` (miroir Dart) |

Desktop et Mobile ne partagent pas le même binaire : le **contrat API** et les algorithmes (semver, SHA256, whitelist d’hôtes) sont alignés.

## Publier une version

### Via Desktop (recommandé)

1. Connectez-vous en **admin**.
2. **Paramètres → Mises à jour**.
3. Remplissez version, URLs, SHA256, notes → **Publier**.
4. Ou cliquez **Activer** sur une ligne existante.

### Via SQL

1. Héberger `DesktopSetup.exe` / `SuperEcole.apk` (HTTPS recommandé) sur un hôte whitelisté.
2. Calculer le SHA256 du fichier.
3. Insérer / activer une ligne dans `ApplicationVersions` (`Active=1`).
4. Les clients détectent automatiquement (démarrage + toutes les 6 h).

## Endpoint

```http
GET /api/v1/update/check?platform=mobile&currentVersion=1.0.0
```

Réponse `ApiResponse` avec `data` au format demandé (latestVersion, mandatory, urls, sha256, size, releaseNotes…).

## Sécurité

- Whitelist d’hôtes (`Updates:AllowedHosts` / `_allowedHosts` mobile).
- Vérification SHA256 avant installation.
- Mise à jour obligatoire : fenêtre non fermable.
