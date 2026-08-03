# Rapport — Séparation BD Production / Development

**Date :** 2026-08-03  
**Décision nommage :** `SchoolManagementRDC_Development` + `SchoolManagementRDC_Production`  
**Nettoyage Production :** Option 1 (école + structure + admin conservés)

---

## 1. Bases SQL Server (`localhost\HEROS_SQL19`)

| Base | Rôle | État |
|------|------|------|
| `SchoolManagementRDC` | Source historique (inchangée) | 24 élèves, 12 users, données complètes |
| `SchoolManagementRDC_Development` | Développement / tests | Copie complète (données conservées) |
| `SchoolManagementRDC_Production` | Production écoles | Option 1 purgée |
| `SchoolManagementRDC_Dev` | Ancienne base incomplète | **Non utilisée** (laisser telle quelle) |

### Backup

`database/backups/SchoolManagementRDC_FULL_20260803_123553.bak`  
(COPY_ONLY, avant duplication — fichier `.bak` gitignored)

### Production après purge (Option 1)

| Élément | Avant | Après |
|---------|-------|-------|
| Schools | 1 (COLLEGE SAINT BENOIT) | **1** conservée |
| PedagogicalClasses | 239 | **239** |
| Permissions | 48 | **48** |
| Roles | 4 | **4** |
| UserAccounts | 12 | **1** (`admin`) |
| Students / Teachers / Guardians | >0 | **0** |
| Enrollments / Payments / Notifications | >0 | **0** |
| Tables / vues / procédures | 118 / 3 / 10 | **inchangé** (schéma intact) |
| Orphelins FK (échantillon) | — | **0** |

---

## 2. Fichiers modifiés / créés

### SQL
- `database/scripts/009_Purge_Production_Option1.sql` *(créé + exécuté)*
- `database/backups/.gitkeep`

### API / config
- `src/SchoolManagement.API/appsettings.Development.json` → BD `_Development`
- `src/SchoolManagement.API/appsettings.Production.json` → BD `_Production`
- `src/SchoolManagement.API/DatabaseEnvironmentGuard.cs` *(nouveau)*
- `src/SchoolManagement.API/Program.cs` — garde BD + seed scindé
- `src/SchoolManagement.API/ServeurDonnees.txt` (local, gitignored) → `BASE=SchoolManagementRDC_Development`
- `src/SchoolManagement.Application/.../DatabaseConfigurationManager.cs` — défaut nouveau fichier → Development
- `src/SchoolManagement.Application/.../CloudDatabaseConfigurationManager.cs` — défaut cloud → Production
- `src/SchoolManagement.Infrastructure/Seeding/DatabaseSeeder.cs` — `SeedSystemAsync` / `SeedDemoAsync`
- `coolify.env.example` — `Database=SchoolManagementRDC_Production`, `ALLOW_DEMO_SEED=false`
- `.gitignore` — versionne Development sans secrets ; ignore `*.bak`

### Docs
- `docs/audit-db-production-development.md` (audit étape 1)
- `docs/rapport-db-production-development.md` (ce rapport)

### Migrations EF
**Aucune nouvelle migration générée** (schéma non modifié, conformément aux règles).

---

## 3. Chaînes de connexion

| Environnement | Source | Base |
|---------------|--------|------|
| Development | `appsettings.Development.json` → `ConnectionStrings:Default` | `SchoolManagementRDC_Development` |
| Production (local) | `appsettings.Production.json` | `SchoolManagementRDC_Production` |
| Docker/Coolify | `SQL_CONNECTION_STRING` (prioritaire) | `SchoolManagementRDC_Production` |
| Fallback local | `ServeurDonnees.txt` | `SchoolManagementRDC_Development` |

Flutter : **inchangé** (HTTP uniquement).

---

## 4. Seed

| Méthode | Contenu | Quand |
|---------|---------|--------|
| `SeedSystemAsync` | Permissions, rôles admin, Super Admin | Development **ou** `SEED_DATABASE=true` |
| `SeedDemoAsync` | Parents/enseignants/direction démo | **Development uniquement** (jamais si BD Production) |
| `ALLOW_DEMO_SEED=true` | Contournement labo | Bloqué si base = `_Production` |

---

## 5. Sécurisations

1. `DatabaseEnvironmentGuard` : Development **ne peut pas** ouvrir `_Production` ni l’ancienne `SchoolManagementRDC`.
2. Production **ne peut pas** ouvrir `_Development`.
3. Seed démo impossible sur `_Production`.
4. Coolify exemple pointe Production + `ALLOW_DEMO_SEED=false`.

---

## 6. Données supprimées (Production uniquement)

Élèves, parents/tuteurs, enseignants, inscriptions, paiements & mouvements, notes/évaluations/résultats/délibération opérationnelle, notifications, tokens appareils, historiques login/audit/sync, cartes élèves émises, affectations cours enseignants, utilisateurs hors `admin`.

**Conservé :** école COLLEGE SAINT BENOIT, structure pédagogique, frais/config, rôles, permissions, admin, géographie, catalogues système.

---

## 7. Risques restants

| Risque | Mitigation |
|--------|------------|
| `SchoolManagementRDC` source encore présente | Ne plus l’utiliser en Dev ; garder comme archive / backup logique |
| `SchoolManagementRDC_Dev` (16 Mo) obsolète | Ne pas confondre avec `_Development` |
| Mot de passe admin `Admin@2026` | À changer avant ouverture clients |
| Desktop `Dev:AutoLogin` | Désactiver pour builds Production Desktop |
| Coolify encore sur ancienne CS | Mettre à jour `SQL_CONNECTION_STRING` vers `_Production` |
| Cloud sync | Vérifier `ServeurDonneesCloud.txt` si utilisé |

---

## 8. Vérification effectuée

- [x] Backup `.bak` avant duplication  
- [x] Restore `_Development` / `_Production` (118 tables)  
- [x] Purge Option 1 sur Production seule  
- [x] Development / Source intacts (24 élèves)  
- [x] Schéma : 118 tables, 3 vues, 10 procédures  
- [x] Contrôle orphelins FK (échantillon) = 0  
- [x] Config Dev/Prod + guards + seed scindé  

---

## 9. Prochaines actions recommandées

1. Relancer l’API en **Development** → doit se connecter à `_Development`.  
2. Tester login `admin` sur Production (profil Production).  
3. Mettre à jour Coolify `SQL_CONNECTION_STRING` → `_Production`.  
4. Changer le mot de passe Super Admin Production.  
5. (Optionnel) Archiver/renommer `SchoolManagementRDC_Dev` et `SchoolManagementRDC` après période de confiance.
