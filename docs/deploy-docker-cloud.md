# Déployer l'API Cloud avec Docker Compose

## Objectif

Exposer l'API en **Mode Cloud** (lecture seule + notes) pour le téléphone hors établissement.

```
Téléphone 4G → https://api… → Conteneur API (Docker) → SQL Cloud
PC école     → sync Local → SQL Cloud
```

## Prérequis

- Docker + Docker Compose v2
- Une base SQL Cloud joignable (ex. `161.97.105.22`)
- Ports ouverts sur le VPS : **1804** (ou 80/443 derrière un reverse proxy)

## 1. Préparer les secrets (sur le PC école)

```powershell
cd "d:\Mes Projet\ERP_Administration_Scolaire_2026"
.\scripts\prepare-docker-env.ps1
.\scripts\pack-docker-cloud.ps1
```

Cela génère :
- `.env` (gitignored) depuis `ServeurDonneesCloud.txt`
- `artifacts\erp-api-cloud-docker.zip` prêt à copier sur le VPS

Ou manuellement :

```bash
cp .env.example .env
nano .env
```

Renseigner au minimum :

- `SQL_CONNECTION_STRING` → SQL cloud
- `JWT_SECRET_KEY` → clé longue (≥ 32 caractères)

Exemple :

```env
SQL_CONNECTION_STRING=Server=161.97.105.22,1433;Database=SchoolManagementRDC;User Id=sa;Password=VOTRE_MDP;TrustServerCertificate=True;Encrypt=True
JWT_SECRET_KEY=une-cle-secrete-tres-longue-aleatoire-32+
API_HOST_PORT=1804
```

## 2. Lancer l'API (sur le VPS avec Docker)

Copier `artifacts\erp-api-cloud-docker.zip` puis :

```bash
unzip erp-api-cloud-docker.zip -d erp-api-cloud
cd erp-api-cloud
docker compose up -d --build
```

Vérifier :

```bash
docker compose ps
curl http://127.0.0.1:1804/api/v1/health
```

## 3. HTTPS (recommandé)

Placez Caddy / Nginx / Traefik devant le conteneur pour `https://api.votredomaine.com` → `127.0.0.1:1804`.

## 4. Sync depuis le PC école

Toujours sur le **PC école** (API Locale) :

```powershell
.\scripts\configure-cloud-sync.ps1 `
  -Server "161.97.105.22" `
  -Password "..." `
  -Actif 1
```

Puis Desktop → Synchronisation cloud → **Synchroniser maintenant**.

## 5. Brancher le téléphone

```powershell
.\run-on-phone.ps1 `
  -LocalApiUrl "http://IP_PC_ECOLE:5041" `
  -CloudApiUrl "https://api.votredomaine.com"
```

Sans HTTPS encore (test) :

```powershell
.\run-on-phone.ps1 `
  -LocalApiUrl "http://IP_PC_ECOLE:5041" `
  -CloudApiUrl "http://IP_VPS:1804"
```

## Profil optionnel : SQL dans Compose

Pour un lab complet (API + SQL dans Docker) :

```bash
# dans .env, pointer vers le service sql :
# SQL_CONNECTION_STRING=Server=sql,1433;Database=master;User Id=sa;Password=Your_strong_Password123;TrustServerCertificate=True;Encrypt=False

docker compose --profile full up -d --build
```

Puis créez la base `SchoolManagementRDC` dans le conteneur SQL (l'API peut aussi la provisionner selon le seed au démarrage).

## Déploiement Coolify

Coolify cherche un `Dockerfile` **à la racine** du dépôt (`bil-hids/adsco-monol`).

Deux ports distincts :

| Rôle | Port | Exemple |
|------|------|---------|
| **API Cloud (public)** | **1804** | `http://IP_VPS:1804` — Coolify / Docker |
| **SQL Server distant** | **1433** (souvent) | dans `SQL_CONNECTION_STRING` → `Server=161.97.105.22,1433;...` |

Le **1804** est le port public de l’API (mobile / clients).  
La base distante garde son port SQL (souvent **1433**) — ce n’est pas le même port.

Réglages Coolify :

| Champ | Valeur |
|-------|--------|
| Build Pack | Dockerfile |
| Base Directory | `/` (racine) |
| Dockerfile Location | `Dockerfile` |
| Ports Exposes | **1804** |
| Port public | **1804** |
| Branch | `main` |

Variables d'environnement **obligatoires** (sinon le conteneur quitte → healthcheck « unhealthy ») :

```env
ASPNETCORE_ENVIRONMENT=Production
PORT=1804
ASPNETCORE_URLS=http://0.0.0.0:1804
Deployment__Role=Cloud
Deployment__ReadOnly=true
FILE_STORAGE_ROOT=/app/data/files
SQL_CONNECTION_STRING=Server=161.97.105.22,1433;Database=SchoolManagementRDC;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=True
Jwt__SecretKey=une-cle-secrete-tres-longue-32caracteres-min
Jwt__Issuer=SchoolManagementRDC
Jwt__Audience=SchoolManagementClients
```

Healthcheck Coolify :

| Champ | Valeur |
|-------|--------|
| Path | `/api/v1/health` |
| Port | `1804` |
| Return code | `200` |

Si le healthcheck reste unhealthy : ouvrir les **logs du conteneur** — cause #1 = `SQL_CONNECTION_STRING` manquante ou SQL inaccessible depuis le VPS (firewall 1433).

Après un push sur `main`, **Redeploy**.## Variables utiles

| Variable | Rôle |
|----------|------|
| `SQL_CONNECTION_STRING` | Connexion SQL (prioritaire, sans DPAPI) |
| `FILE_STORAGE_ROOT` | Dossier fichiers (défaut `/app/data/files`) |
| `Deployment__Role` | `Cloud` |
| `Deployment__ReadOnly` | `true` |
| `Jwt__SecretKey` | Secret JWT |
| `ERP_CONFIG_ENCRYPTION_KEY` | AES Linux si fichiers config utilisés |

## Dépannage

| Symptôme | Action |
|----------|--------|
| `open Dockerfile: no such file or directory` | Utiliser le `Dockerfile` à la racine ; Redeploy après pull `main` |
| API refuse de démarrer (SQL) | Vérifier IP/port/firewall SQL + chaîne dans `.env` |
| Health 403 sur POST | Normal en Mode Cloud (sauf auth / notes) |
| Téléphone hors ligne | Vérifier `CLOUD_API_BASE_URL` / `-CloudApiUrl` |
| Rebuild | `docker compose up -d --build --force-recreate` |

## Fichiers ajoutés

- `Dockerfile` (racine — Coolify / compose)
- `src/SchoolManagement.API/Dockerfile` (alias compat)
- `docker-compose.yml`
- `.env.example`
- `.dockerignore`
- `docs/deploy-docker-cloud.md` (ce guide)
