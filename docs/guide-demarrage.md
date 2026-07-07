# Guide de démarrage — ERP Administration Scolaire RDC

## Prérequis

| Outil | Version |
|-------|---------|
| .NET SDK | 8.0+ |
| SQL Server | 2019+ (instance `localhost\HEROS_SQL19` en démo) |
| Flutter | 3.5+ (mobile uniquement) |
| Visual Studio 2022 | Desktop WPF |

## Installation base de données

1. Créer la base `SchoolManagementRDC` sur SQL Server.
2. Exécuter les scripts dans `database/scripts/` :
   - `001_InitialCreate_EF.sql`
   - `002_Views_Procedures_Functions.sql`
   - `003_SeedData.sql`
3. Ou appliquer la migration EF :
   ```bash
   .tools\dotnet-ef database update --project src/SchoolManagement.Infrastructure --startup-project src/SchoolManagement.API
   ```

## Configuration API

Fichier : `src/SchoolManagement.API/appsettings.json`

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost\\HEROS_SQL19;Database=SchoolManagementRDC;..."
}
```

## Lancement

```bash
# Terminal 1 — API
dotnet run --project src/SchoolManagement.API
# Swagger : https://localhost:7060/swagger

# Terminal 2 — Desktop
dotnet run --project src/SchoolManagement.Desktop

# Terminal 3 — Mobile
cd mobile/school_management_mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=https://localhost:7060
```

## Comptes de démonstration

| Rôle | Identifiant | Mot de passe | Accès |
|------|-------------|--------------|-------|
| Administrateur | `admin` | `Admin@2026` | Desktop complet |
| Direction | `direction` | `Direction@2026` | Mobile — tableau de bord |
| Enseignant | `enseignant` | `Teacher@2026` | Mobile — cours et notes |
| Parent | `parent` | `Parent@2026` | Mobile — enfants, paiements |

Les comptes démo (hors admin) sont créés au premier démarrage de l'API via `DatabaseSeeder`.

## Tests

```bash
dotnet test tests/SchoolManagement.IntegrationTests
```

## Modules Desktop

| Module | Description |
|--------|-------------|
| Tableau de bord | État API |
| Paramétrage | École, années scolaires |
| Élèves | CRUD élèves |
| Académique | Classes, cours, inscriptions |
| Notes | Évaluations, saisie, moyennes |
| Financier | Paiements |
| Documents | Fichiers élèves |
| Statistiques | KPIs, effectifs, moyennes |
| Administration | Utilisateurs et rôles |

## Dépannage

- **API hors ligne (Desktop)** : vérifier l'URL dans `src/SchoolManagement.Desktop/appsettings.json` (`Api:BaseUrl`).
- **Certificat HTTPS** : le Desktop accepte les certificats auto-signés en développement.
- **Émulateur Android** : utiliser `https://10.0.2.2:7060` comme URL API.
