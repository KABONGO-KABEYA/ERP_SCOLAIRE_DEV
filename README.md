# ERP Administration Scolaire RDC

Système d'information scolaire complet pour établissements en République Démocratique du Congo.

## Composants

| Composant | Technologie | Dossier |
|-----------|-------------|---------|
| API REST | ASP.NET Core 8 | `src/SchoolManagement.API` |
| Desktop | WPF + MVVM (.NET 8) | `src/SchoolManagement.Desktop` |
| Mobile | Flutter + Riverpod | `mobile/school_management_mobile` |
| Base de données | SQL Server | `database/` |

## Démarrage rapide

```bash
dotnet run --project src/SchoolManagement.API
dotnet run --project src/SchoolManagement.Desktop
dotnet test tests/SchoolManagement.IntegrationTests
```

Swagger : `https://localhost:7060/swagger`

Guide détaillé : [docs/guide-demarrage.md](docs/guide-demarrage.md)

## Comptes de démonstration

| Rôle | Identifiant | Mot de passe |
|------|-------------|--------------|
| Administrateur | `admin` | `Admin@2026` |
| Direction | `direction` | `Direction@2026` |
| Enseignant | `enseignant` | `Teacher@2026` |
| Parent | `parent` | `Parent@2026` |

## Modules Desktop

Tableau de bord · Paramétrage · Élèves · Académique · Notes · Financier · Documents · Statistiques · Administration

## Mobile par rôle

- **Parent** — enfants, paiements, bulletins
- **Enseignant** — cours, saisie des notes
- **Direction** — tableau de bord KPIs et rapports

## Configuration

- API : `src/SchoolManagement.API/appsettings.json`
- Desktop : `src/SchoolManagement.Desktop/appsettings.json`
- SQL : instance `localhost\HEROS_SQL19`, base `SchoolManagementRDC`

## Documentation

- [Architecture métier](docs/architecture.md)
- [Architecture connexion v2 — rapport final](docs/architecture/rapport-final-architecture-v2.md)
- [Déploiement production connexion](docs/exploitation/deploiement-production-connexion-v2.md)
- [Guide de démarrage](docs/guide-demarrage.md)
- [Référence API](docs/api-reference.md)
- [Modules métier](docs/modules/README.md)

## Statut du projet

**Architecture connexion parent / école / Bootstrap (v2.0.1) :** terminée — [rapport final](docs/architecture/rapport-final-architecture-v2.md).

Modules ERP (scaffolding → API → Desktop → Mobile → tests) : base **v1.0 démonstration** ; reprise du **métier** après clôture connexion.

## Licence

Projet privé — ERP Administration Scolaire 2026.
