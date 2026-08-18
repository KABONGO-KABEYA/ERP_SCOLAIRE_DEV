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

## Source de vérité de la version (Desktop)

La version utilisée pour le contrôle de mise à jour est celle **compilée dans l’assembly / csproj** (`InformationalVersion`, sinon `FileVersion`).

| Priorité | Source | Rôle |
|----------|--------|------|
| 1 | Assembly `InformationalVersion` (csproj) | Source de vérité |
| 2 | Assembly `FileVersion` | Fallback si InformationalVersion absent / 0.0.0 |
| 3 | `version.json` à côté de l’exe | Fallback uniquement si l’assembly n’expose aucune version utilisable |
| 4 | `0.0.0` | Dernier recours |

`version.json` ne doit **jamais** primer sur l’assembly :

```text
Assembly = 1.2.0
version.json = 1.1.0
→ version retenue = 1.2.0
```

- `Updates:CurrentVersion` n’est plus lu dans `appsettings.json`.
- Au démarrage, `%LocalAppData%\ERP_Scolaire\Updates\update-settings.json` est **écrasé** avec la version assembly (corrige les anciens `1.0.0` figés).

Comparaison SemVer : la métadonnée `+gitsha` est ignorée ; les pré-releases (`-beta`, `-rc.1`, …) sont conservées (`1.2.0-beta` &lt; `1.2.0`).

## HTTP / HTTPS (Lot 0)

Cible production du mécanisme de mise à jour : **HTTPS avec certificat valide**. Le client HttpClient `UpdateApi` n’accepte plus les certificats invalides.

Compatibilité transitoire DEV / LAN (à retirer plus tard) :

| URL | Résultat |
|-----|----------|
| `https://<hôte whitelisté>/…` | Autorisé (règle production) |
| `http://localhost` / `127.0.0.1` / `::1` | Autorisé (loopback) |
| `http://192.168.x.x`, `10.x.x.x`, `172.16–31.x.x` (hôte whitelisté) | Autorisé temporairement (LAN privé) |
| `http://` vers une IP publique (ex. `169.58.93.203`) | **Refusé** |
| Hôte hors whitelist | **Refusé** |

Le SHA256 du manifeste est **obligatoire** : s’il est absent, le téléchargement n’est pas lancé.

## Catalogue central (Lot 1) — Bootstrap

Source de vérité **progressive** des releases de production : base `SchoolManagementBootstrap`, API `SchoolManagement.Bootstrap.API`.  
**Pas** dans la base métier école. Les binaires ne sont **pas** en SQL (URL + SHA256 + taille seulement).

| Channel | Usage |
|---------|--------|
| `DEV` | Releases de développement (HTTP loopback/LAN autorisé pour les URLs) |
| `PROD` | Production (HTTPS obligatoire) |

`schoolId` sur `GET /api/v1/releases/check` est un **filtre de ciblage**, pas une preuve d’identité ni une autorisation de déploiement (Lot 2 : Update Agent).

Publication : header `X-Bootstrap-Release-Key` = `Bootstrap:ReleasePublishApiKey` (**distinct** de `RelayApiKey`).

```http
POST /api/v1/releases
GET  /api/v1/releases/check?channel=PROD&schoolId=&artifactType=Desktop
GET  /api/v1/releases/{id}
PUT  /api/v1/releases/{id}/status
```

Le Desktop continue d’utiliser `ApplicationVersions` / `GET /api/v1/update/check` sur l’API locale. Ce catalogue n’est **pas** encore branché aux écoles.

## Identité Update Agent (Lot 2A-1) — Bootstrap

Authentification **ClientId + ClientSecret → JWT court** `token_type=update_agent`.  
Le `schoolId` client n’est **jamais** une preuve d’identité. Bootstrap stocke uniquement le **hash** du secret.

| Clé | Usage |
|-----|--------|
| `Bootstrap:AgentProvisionApiKey` | Header `X-Bootstrap-Agent-Provision-Key` (création / rotate / revoke / list) |
| `Bootstrap:AgentJwtSigningKey` | HMAC des JWT agent (Coolify / secret manager ; **pas** de génération au démarrage) |
| `Bootstrap:AgentJwtMinutes` | TTL 5–60 min (défaut 30) |

Ces clés sont **distinctes** de `RelayApiKey` et `ReleasePublishApiKey`. Absentes ou HMAC &lt; 32 octets → **503**.

JWT : `sub` = ClientId, `jti` = **nouveau GUID à chaque** `POST /agent/token` (pas l’id du credential), `school_id` depuis la ligne, `aud` = `erp-scolaire-update-agent`, `iss` Bootstrap, `exp`.

```http
POST /api/v1/agent/credentials
POST /api/v1/agent/credentials/{id}/rotate
POST /api/v1/agent/credentials/{id}/revoke
GET  /api/v1/agent/credentials?schoolId=
POST /api/v1/agent/token
GET  /api/v1/agent/releases/check
```

`GET /api/v1/agent/releases/check` exige un Bearer ; le SchoolId vient du JWT/credential. Pas de `?schoolId=` d’identité. Credential révoqué / JWT mort / mauvais `aud` / mauvais `token_type` → 401. École inactive → 403.

Hors 2A-1 : processus Update Agent, fichier DPAPI école, heartbeat, rebind.

## Schéma SQL école versionné (Lot 2B-1)

**Baseline : `AppSchemaVersion = 1`.** Même valeur côté `MigrationManager` et initialiseurs API.

Cible Lot 2B (non branchée au Setup/API actuel) : `MigrationManager.ApplyPackageAsync` sur un **dossier local** (manifest + `MigrationN_N+1.sql`). Aucun téléchargement SQL. `SET XACT_ABORT ON` ; une transaction par pas ; bump de version uniquement après succès. Tant qu’il n’est pas branché, il ne remplace pas les SchemaInitializers.

Contrat actuel (Setup + API école) : **`001_InitialCreate_EF.sql`** = baseline historique immuable ; **`*SchemaInitializer`** = mécanisme officiel d’évolution du schéma (idempotent, Setup et démarrage API) ; **migrations EF** = artefacts de modèle, non exécutées automatiquement ; **`Database.Migrate()`** = interdit dans le chemin Setup/API. Toute nouvelle évolution SQL doit avoir une couverture `SchemaDeploymentCoverage` (Complete / Partial / Excluded). `MigrationManager` n’est **pas** branché au start API dans ce lot.

Package : `database/migrations/app/` (aujourd’hui 1→1, liste vide). Contrat agent 2B-4B : Stop API → Backup COPY_ONLY+VERIFYONLY → `MigrationManager.ApplyPackageAsync` (package local) → swap → health. Restore SQL uniquement si le schéma a avancé et que la nouvelle API échoue, depuis le `.bak` whitelisté de l’état.

Permissions moteur (pas de GRANT dans ce lot) : `SELECT`/`UPDATE` `AppSchemaVersion` ; DDL des scripts dans `dbo`. Pas `db_owner` / `db_ddladmin` / `sysadmin`.

## Package Release API + Migration (Lot 2B-2)

Contrat : [`docs/release-package-contract.md`](release-package-contract.md).

Une release Bootstrap paire **Api** et **Migration** (même `ReleaseId` / `Version`). Check agent : `GET /api/v1/agent/releases/check` renvoie les deux artifacts. Check Desktop public inchangé.

Packaging : `scripts/pack-release-artifacts.ps1` (zips + SHA256). `build-setup.ps1` reste l’installateur USB.

## Update Agent (Lot 2B-3)

Service Windows `ErpScolaireUpdateAgent` (compte dédié, pas LocalSystem) : JWT Bootstrap → check → staging → **machine d’état 2B-4B** (stop, backup, migration locale, swap, health). `Agent:AutoDeploy=false` par défaut : pas d’activation production. Détail : [`docs/update-agent.md`](update-agent.md).

## Publier une version

### Via Desktop (recommandé)

1. Connectez-vous en **admin**.
2. **Paramètres → Mises à jour**.
3. Remplissez version, URLs, SHA256, notes → **Publier**.
4. Ou cliquez **Activer** sur une ligne existante.

### Via SQL

1. Héberger `DesktopSetup.exe` / `SuperEcole.apk` (**HTTPS** pour la production) sur un hôte whitelisté.
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
- Vérification SHA256 **avant** téléchargement (hash attendu obligatoire) puis après écriture du fichier.
- Mise à jour obligatoire : fenêtre non fermable.
- TLS : pas de bypass de certificat sur le client de mise à jour.
