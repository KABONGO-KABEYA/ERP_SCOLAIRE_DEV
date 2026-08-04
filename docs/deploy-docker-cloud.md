# DÃ©ployer l'API Cloud avec Docker Compose

## Objectif

Exposer l'API en **Mode Cloud** (lecture seule + notes) pour le tÃ©lÃ©phone hors Ã©tablissement.

```
TÃ©lÃ©phone 4G â†’ https://apiâ€¦ â†’ Conteneur API (Docker) â†’ SQL Cloud
PC Ã©cole     â†’ sync Local â†’ SQL Cloud
```

## PrÃ©requis

- Docker + Docker Compose v2
- Une base SQL Cloud joignable (ex. `169.58.93.203`)
- Ports ouverts sur le VPS : **1804** (ou 80/443 derriÃ¨re un reverse proxy)

## 1. PrÃ©parer les secrets (sur le PC Ã©cole)

```powershell
cd "d:\Mes Projet\ERP_Administration_Scolaire_2026"
.\scripts\prepare-docker-env.ps1
.\scripts\pack-docker-cloud.ps1
```

Cela gÃ©nÃ¨re :
- `.env` (gitignored) depuis `ServeurDonneesCloud.txt`
- `artifacts\erp-api-cloud-docker.zip` prÃªt Ã  copier sur le VPS

Ou manuellement :

```bash
cp .env.example .env
nano .env
```

Renseigner au minimum :

- `SQL_CONNECTION_STRING` â†’ SQL cloud
- `JWT_SECRET_KEY` â†’ clÃ© longue (â‰¥ 32 caractÃ¨res)

Exemple :

```env
SQL_CONNECTION_STRING=Server=169.58.93.203,1433;Database=SchoolManagementRDC;User Id=sa;Password=VOTRE_MDP;TrustServerCertificate=True;Encrypt=True
JWT_SECRET_KEY=une-cle-secrete-tres-longue-aleatoire-32+
ERP_CONFIG_ENCRYPTION_KEY=une-autre-cle-secrete-longue-pour-aes-identite
API_HOST_PORT=1804
```

## 2. Lancer l'API (sur le VPS avec Docker)

Copier `artifacts\erp-api-cloud-docker.zip` puis :

```bash
unzip erp-api-cloud-docker.zip -d erp-api-cloud
cd erp-api-cloud
docker compose up -d --build
```

VÃ©rifier :

```bash
docker compose ps
curl http://127.0.0.1:1804/api/v1/health
```

## 3. HTTPS (recommandÃ©)

Placez Caddy / Nginx / Traefik devant le conteneur pour `https://api.votredomaine.com` â†’ `127.0.0.1:1804`.

## 4. Sync depuis le PC Ã©cole

Toujours sur le **PC Ã©cole** (API Locale) :

```powershell
.\scripts\configure-cloud-sync.ps1 `
  -Server "169.58.93.203" `
  -Password "..." `
  -Actif 1
```

Puis Desktop â†’ Synchronisation cloud â†’ **Synchroniser maintenant**.

## 5. Brancher le tÃ©lÃ©phone

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

Puis crÃ©ez la base `SchoolManagementRDC` dans le conteneur SQL (l'API peut aussi la provisionner selon le seed au dÃ©marrage).

## DÃ©ploiement Coolify

Coolify cherche un `Dockerfile` **Ã  la racine** du dÃ©pÃ´t (`bil-hids/adsco-monol`).

Deux ports distincts :

| RÃ´le | Port | Exemple |
|------|------|---------|
| **API Cloud (public)** | **1804** | `http://IP_VPS:1804` â€” Coolify / Docker |
| **SQL Server distant** | **1433** (souvent) | dans `SQL_CONNECTION_STRING` â†’ `Server=169.58.93.203,1433;...` |

Le **1804** est le port public de lâ€™API (mobile / clients).  
La base distante garde son port SQL (souvent **1433**) â€” ce nâ€™est pas le mÃªme port.

RÃ©glages Coolify :

| Champ | Valeur |
|-------|--------|
| Build Pack | Dockerfile |
| Base Directory | `/` (racine) |
| Dockerfile Location | `Dockerfile` |
| Ports Exposes | **1804** |
| Port public | **1804** |
| Branch | `main` |

Variables d'environnement **obligatoires** (sinon healthcheck Â« unhealthy Â») :

> Coolify â†’ **Environment Variables** (runtime), pas seulement Build Variables.  
> ModÃ¨le : fichier `coolify.env.example` Ã  la racine du dÃ©pÃ´t.

```env
ASPNETCORE_ENVIRONMENT=Production
PORT=1804
ASPNETCORE_URLS=http://0.0.0.0:1804
Deployment__Role=Cloud
Deployment__ReadOnly=true
FILE_STORAGE_ROOT=/app/data/files
SQL_CONNECTION_STRING=Server=169.58.93.203,1433;Database=SchoolManagementRDC;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=True
Jwt__SecretKey=une-cle-secrete-tres-longue-32caracteres-min
ERP_CONFIG_ENCRYPTION_KEY=cle-aes-production-longue-et-unique
Jwt__Issuer=SchoolManagementRDC
Jwt__Audience=SchoolManagementClients
```

Si les logs montrent `SQL Server target: 169.58.93.203` alors que SQL tourne **dans Coolify** :
tu pointes encore lâ€™ancienne base distante. Corrige `SQL_CONNECTION_STRING` de lâ€™**application API** :

```env
# MÃªme serveur Coolify que le conteneur sqlserver (rÃ©seau Docker interne)
SQL_CONNECTION_STRING=Server=sqlserver,1433;Database=SchoolManagementRDC;User Id=sa;Password=MEME_MOT_DE_PASSE_QUE_MSSQL_SA_PASSWORD;TrustServerCertificate=True;Encrypt=True
```

RÃ¨gles :
- Le mot de passe doit Ãªtre **exactement** celui de `MSSQL_SA_PASSWORD` du service SQL
- Nâ€™utilise **pas** `169.58.93.203` si SQL est le conteneur local Coolify
- Port SQL = **1433** (interne) ; port public API = **1804**
- Sur le service SQL Coolify : retire le domaine `https://app.coolify.io` (inutile pour une base)
- CrÃ©e la base si besoin (Terminal SQL) :
  ` /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '...' -C -Q "IF DB_ID('SchoolManagementRDC') IS NULL CREATE DATABASE SchoolManagementRDC;" `

AprÃ¨s correction des variables de lâ€™**API** : **Redeploy** lâ€™API.

## Variables utiles

| Variable | RÃ´le |
|----------|------|
| `SQL_CONNECTION_STRING` | Connexion SQL (prioritaire, sans DPAPI) |
| `FILE_STORAGE_ROOT` | Dossier fichiers (dÃ©faut `/app/data/files`) |
| `Deployment__Role` | `Cloud` |
| `Deployment__ReadOnly` | `true` |
| `Jwt__SecretKey` | Secret JWT |
| `ERP_CONFIG_ENCRYPTION_KEY` | **Obligatoire** en Production/Cloud (chiffrement identité + config AES) ; jamais la clé de dev |

## DÃ©pannage

| SymptÃ´me | Action |
|----------|--------|
| `open Dockerfile: no such file or directory` | Utiliser le `Dockerfile` Ã  la racine ; Redeploy aprÃ¨s pull `main` |
| API refuse de dÃ©marrer (SQL) | VÃ©rifier IP/port/firewall SQL + chaÃ®ne dans `.env` |
| Health 403 sur POST | Normal en Mode Cloud (sauf auth / notes) |
| TÃ©lÃ©phone hors ligne | VÃ©rifier `CLOUD_API_BASE_URL` / `-CloudApiUrl` |
| Rebuild | `docker compose up -d --build --force-recreate` |

## Fichiers ajoutÃ©s

- `Dockerfile` (racine â€” Coolify / compose)
- `src/SchoolManagement.API/Dockerfile` (alias compat)
- `docker-compose.yml`
- `.env.example`
- `.dockerignore`
- `docs/deploy-docker-cloud.md` (ce guide)
- [docs/exploitation/server-identity-et-restauration.md](exploitation/server-identity-et-restauration.md) — sauvegarde/restauration de `ServerIdentity.json` avec la base SQL
