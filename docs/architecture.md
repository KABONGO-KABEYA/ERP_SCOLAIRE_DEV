# Architecture — ERP Administration Scolaire RDC

## Vue d'ensemble

```
┌─────────────────┐     ┌─────────────────┐
│  Desktop WPF    │     │  Flutter Mobile │
│  (MVVM)         │     │  (Riverpod)     │
└────────┬────────┘     └────────┬────────┘
         │         HTTPS/JWT     │
         └───────────┬───────────┘
                     ▼
         ┌───────────────────────┐
         │  ASP.NET Core API     │
         │  /api/v1/*            │
         └───────────┬───────────┘
                     ▼
         ┌───────────────────────┐
         │  SQL Server           │
         └───────────────────────┘
```

Le Desktop et le Mobile **ne contactent jamais** SQL Server directement.

## Clean Architecture (.NET)

| Couche | Projet | Responsabilité |
|--------|--------|----------------|
| Domain | `SchoolManagement.Domain` | Entités, enums, règles métier |
| Application | `SchoolManagement.Application` | Services, DTOs, interfaces |
| Infrastructure | `SchoolManagement.Infrastructure` | EF Core, JWT, fichiers, seed |
| API | `SchoolManagement.API` | Contrôleurs REST, Swagger |
| Desktop | `SchoolManagement.Desktop` | UI WPF consommant l'API |
| Shared | `SchoolManagement.Shared` | Constantes, `ApiResponse<T>` |

Dépendances : `API → Infrastructure → Application → Domain`

## Modules métier — statut

| Module | API | Desktop | Mobile |
|--------|-----|---------|--------|
| Authentification JWT | ✅ | ✅ | ✅ |
| Paramétrage école | ✅ | ✅ | — |
| Élèves | ✅ | ✅ | — |
| Académique | ✅ | ✅ | — |
| Notes | ✅ | ✅ | ✅ (enseignant) |
| Financier | ✅ | ✅ | ✅ (parent) |
| Documents | ✅ | ✅ | — |
| Statistiques / rapports | ✅ | ✅ | ✅ (direction) |
| Administration | ✅ | ✅ | — |

## Sécurité

- JWT access + refresh tokens
- BCrypt pour les mots de passe
- Permissions granulaires par endpoint (`students.read`, `grades.create`, `admin.full`, …)
- Politiques ASP.NET Core par permission
- Seed automatique des permissions et comptes démo

## Contexte RDC

- Multi-devises : CDF / USD
- Cycles : Primaire / Secondaire
- Français par défaut (`fr-FR`)
- Instance SQL locale typique : `localhost\HEROS_SQL19`

## Documentation complémentaire

- [Guide de démarrage](guide-demarrage.md)
- [Référence API](api-reference.md)
- [Modules métier](modules/README.md)
