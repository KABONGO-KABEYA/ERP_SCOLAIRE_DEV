# Audit — séparation BD Production / Development

**Date :** 2026-08-03  
**Périmètre :** machine locale `localhost\HEROS_SQL19` + configuration dépôt ERP  
**Statut :** **Étape 1 uniquement — aucune modification effectuée**

---

## 1. Base actuellement utilisée

| Élément | Valeur constatée |
|---------|------------------|
| Instance SQL | `localhost\HEROS_SQL19` |
| Base **active** (API locale) | **`SchoolManagementRDC`** |
| Fichier de config | `src/SchoolManagement.API/ServeurDonnees.txt` → `BASE=SchoolManagementRDC` |
| Taille approx. | **656 Mo** |
| Objets | **118** tables, **3** vues, **10** procédures |
| Créée le | 2026-07-06 |

### Base déjà présente (attention)

| Base | Taille | Tables | Vues | Procédures | Créée |
|------|--------|--------|------|------------|-------|
| `SchoolManagementRDC` | 656 Mo | 118 | 3 | 10 | 2026-07-06 12:51 |
| `SchoolManagementRDC_Dev` | **16 Mo** | **45** | **0** | **0** | 2026-07-06 12:48 |

`SchoolManagementRDC_Dev` **n’est pas** une copie complète de la base active (schéma incomplet / ancien).  
Elle **ne doit pas** être réutilisée telle quelle comme « Development » sans validation.

### Nommage demandé vs réel

| Demande utilisateur | Réalité actuelle |
|---------------------|-----------------|
| `ERP_Scolaire` | **`SchoolManagementRDC`** |
| `ERP_Scolaire_Production` | n’existe pas |
| `ERP_Scolaire_Development` | n’existe pas (`SchoolManagementRDC_Dev` ≠ copie complète) |

**Décision à valider avant l’étape 2** (voir fin de rapport).

---

## 2. Chaînes de connexion — API .NET 8

### Ordre de résolution (`Program.cs`)

1. `ConnectionStrings:Default` / `DefaultConnection` (configuration)
2. Variable d’environnement **`SQL_CONNECTION_STRING`**
3. `ConnectionStrings__Default` / `ConnectionStrings__DefaultConnection`
4. Sinon → fichier **`ServeurDonnees.txt`** (mot de passe DPAPI) via `DatabaseConnectionBootstrap`

### Fichiers appsettings

| Fichier | Présent ? | Contient ConnectionStrings ? |
|---------|-----------|------------------------------|
| `src/SchoolManagement.API/appsettings.json` | Oui | **Non** (Jwt, Cors, Deployment, Serilog uniquement) |
| `appsettings.Development.json` | **Absent** du dépôt (et listé dans `.gitignore`) | — |
| `appsettings.Production.json` | **Absent** | — |
| `appsettings.Docker.json` | Oui | Non (Role=Cloud, ReadOnly) |

### Local (dev machine)

- Source : `ServeurDonnees.txt` (gitignored)
- `SERVEUR=localhost\HEROS_SQL19`
- `BASE=SchoolManagementRDC`
- `ASPNETCORE_ENVIRONMENT` (launchSettings) = **Development**
- `SQL_CONNECTION_STRING` process env : **absent** au moment de l’audit

### Docker / Coolify (cloud)

- `coolify.env.example` / `docker-compose.yml` :
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `SQL_CONNECTION_STRING=...;Database=SchoolManagementRDC;...`
  - `SEED_DATABASE=true` (exemple premier démarrage — risque si oublié)

### Desktop

- **Pas de connexion SQL directe** : appelle l’API HTTP (`Api:BaseUrl`).
- `src/SchoolManagement.Desktop/appsettings.json` → API locale + cloud distant.
- Contient `Dev:AutoLogin` + credentials démo (risque hors prod).

---

## 3. Flutter / mobile

**Aucune chaîne SQL** dans l’app Flutter.

Configuration réseau uniquement :

| Mécanisme | Rôle |
|-----------|------|
| `ApiConfig` (`--dart-define=LOCAL_API_*` / `CLOUD_API_*`) | URL HTTP API |
| `LocalServerDiscovery` | mDNS / last IP / scan → Mode Local |
| Cloud distant | Mode Distant (lecture seule côté écritures métier) |

Compatibilité Flutter : **inchangée** tant que l’API pointe vers la bonne BD selon l’environnement.  
Le mobile ne choisit **jamais** la base SQL.

---

## 4. Migrations EF Core

Emplacement : `src/SchoolManagement.Infrastructure/Persistence/Migrations/`

Migrations présentes (extrait) :

- `20260706114538_InitialCreate`
- `20260707084815_AddPedagogicalStructure`
- `20260709093000_AddDocumentBranding`
- `20260723140000_AddBranchAndPedagogicalClassCourse`
- `20260723153000_WidenCourseAndBranchCode`
- `20260725100000_AddMaximaParPeriode`
- `20260731140000_AddClassPeriodResultValidation`
- `20260731160000_AddClassPeriodDeliberationMinutes`
- `20260731170000_AddDeliberationCouncilDecisions`
- + `SchoolDbContextModelSnapshot.cs`

Design-time : `DesignTimeDbContextFactory` lit **`ServeurDonnees.txt`**.

### Point critique

**`Database.Migrate()` est interdit** dans le chemin Setup/API actuel.

Contrat : `001_InitialCreate_EF.sql` (baseline historique immuable) + `*SchemaInitializer` (mécanisme officiel d’évolution, idempotent au boot API et au Setup). Les migrations EF post-`InitialCreate` sont des artefacts de modèle ; elles doivent figurer dans `SchemaDeploymentCoverage`.

Conséquence pour la séparation Prod/Dev :

- Dupliquer la BD copie **l’état réel** (tables + données) — correct.
- Ne pas s’attendre à ce qu’EF « rattrape » tout uniquement via `dotnet ef database update`.
- Les initializers continueront à tourner sur **chaque** démarrage API (Prod et Dev) — ils doivent rester **idempotents** et sans seed métier.

---

## 5. Scripts SQL

Dossier `database/scripts/` :

| Script | Rôle |
|--------|------|
| `001_InitialCreate_EF.sql` | Schéma EF exporté |
| `002_Views_Procedures_Functions.sql` | Vues / fonctions / procédures |
| `003_SeedData.sql` | **Démo** (`USE [SchoolManagementRDC]`) |
| `004`–`008_*` | Évolutions pédagogiques / notes / versions |

`database/migrations/README.md` documente :  
`Server=localhost\HEROS_SQL19`, `Database=SchoolManagementRDC`.

Risque : scripts hardcodés sur `SchoolManagementRDC` — à adapter si renommage.

---

## 6. Seed actuel

Un seul `DatabaseSeeder` (`SeedAsync`) qui mélange :

| Type | Contenu |
|------|---------|
| Système | Permissions, rôle Admin, compte `admin` / `Admin@2026` |
| Démo | Parent démo, parent Kabeya, structure académique démo, enseignant, direction |

Déclenchement (`Program.cs`) :

- **Toujours** si `IsDevelopment()`
- **Aussi** si `SEED_DATABASE=true|1` (y compris Production/Cloud)

Donc aujourd’hui : un oubli `SEED_DATABASE=true` en prod **réinjecte de la démo** (idempotent partiel, mais dangereux).

`database/scripts/003_SeedData.sql` = deuxième source de seed démo (hors code C#).

---

## 7. Sécurité actuelle (constat)

| Protection | État |
|------------|------|
| Cloud read-only middleware | Oui (`Deployment:Role=Cloud`) |
| Seed conditionnel Production | Partiel (`SEED_DATABASE`) — **trop permissif** |
| Séparation BD Dev/Prod | **Absente** (une seule BD active) |
| Interdiction seed démo en Production | **Non** (même seeder) |
| `appsettings.Production.json` dédié | **Absent** |
| Guard « Development ne peut pas pointer Prod » | **Absent** |

---

## 8. Flutter — impact

Aucun changement SQL côté mobile requis pour la séparation.  
Seule l’API (Local vs Cloud / Dev vs Prod) doit cibler la bonne base.

---

## 9. Risques détectés (avant toute action)

1. **Nommage** : `SchoolManagementRDC` vs `ERP_Scolaire_*` — renommer casse scripts, Coolify, docs, ServeurDonnees.
2. **`SchoolManagementRDC_Dev` existante** incomplète — risque de confusion / écrasement.
3. **Pas de backup automatisé** encore — l’étape 2 exige un `.bak` complet **avant** duplication.
4. **Seed unique** — à scinder avant tout pointage Production.
5. **Coolify pointe déjà** `Database=SchoolManagementRDC` en Production exemple — clarifier si cloud = prod école ou autre rôle.
6. **Desktop AutoLogin** credentials en clair dans appsettings Desktop.
7. **Schéma hybride** EF + SchemaInitializers — la copie SQL Server (backup/restore) est la méthode sûre ; ne pas recréer via migrations seules.

---

## 10. Plan proposé (non exécuté — validation requise)

Conformément à vos règles : **arrêt ici**.

### Décisions à valider

**A. Noms des bases**

Option recommandée (compatibilité max, moins de churn) :

| Rôle | Nom proposé |
|------|-------------|
| Source actuelle (à sauvegarder) | `SchoolManagementRDC` |
| Development (copie complète) | `SchoolManagementRDC_Development` |
| Production (copie puis nettoyage) | `SchoolManagementRDC_Production` |

Option littérale demandée (`ERP_Scolaire_*`) : possible, mais implique mise à jour large (scripts, Coolify, ServeurDonnees, docs).

**B. Que faire de `SchoolManagementRDC_Dev` (16 Mo) ?**

- Renommer / archiver / supprimer **après backup** ?  
- Ne pas l’utiliser comme cible Development.

**C. Ordre d’exécution après validation**

1. Backup `.bak` de `SchoolManagementRDC`
2. Restore → `_Development` (données complètes conservées)
3. Restore → `_Production` puis script de purge données métier (FK-safe)
4. Config API : Development → `_Development` ; Production → `_Production`
5. Guards seed + env
6. Split seeder Prod / Dev
7. Vérifications + rapport final

---

## Synthèse

| Question | Réponse audit |
|----------|----------------|
| Quelle BD est utilisée ? | **`SchoolManagementRDC`** sur `HEROS_SQL19` |
| appsettings Dev/Prod dédiés ? | **Absents** |
| Flutter a une CS SQL ? | **Non** (HTTP uniquement) |
| Migrations EF ? | Présentes, mais boot = **SchemaInitializers**, pas `Migrate()` |
| Seed séparé Prod/Dev ? | **Non** (un seul seeder + flag risqué) |
| Modifications faites ? | **Aucune** |

**En attente de votre validation des points A et B avant l’étape 2 (duplication + backup).**
