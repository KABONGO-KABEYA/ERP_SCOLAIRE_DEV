# Référence API — v1

Base URL (développement) : `http://localhost:5041/api/v1`

Authentification : `Authorization: Bearer {token}` (sauf `/auth/login`, `/auth/refresh` et `/health`).

## Auth

| Méthode | Route | Permission |
|---------|-------|------------|
| POST | `/auth/login` | — |
| POST | `/auth/refresh` | — |
| POST | `/auth/logout` | Authentifié |
| POST | `/auth/change-password` | Authentifié |
| GET | `/auth/me` | Authentifié |

## Écoles

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/schools/current` | `schools.read` |
| PUT | `/schools/current` | `schools.update` |
| GET | `/schools/current/regulation` | `schools.read` |
| PUT | `/schools/current/regulation` | `schools.update` |
| GET | `/schools/current/academic-years` | `schools.read` |
| POST | `/schools/current/academic-years` | `schools.update` |
| PUT | `/schools/current/academic-years/{id}/set-current` | `schools.update` |
| GET | `/schools/current/lookups` | `schools.read` |

## Structure pédagogique

| Méthode | Route | Permission |
|---------|-------|------------|
| POST | `/schools/current/pedagogical-structure/initialize` | `schools.update` |
| GET | `/schools/current/pedagogical-structure/summary` | `schools.read` |
| GET | `/schools/current/pedagogical-structure/classes` | `schools.read` |
| PUT | `/schools/current/pedagogical-structure/classes/{id}` | `schools.update` |
| PUT | `/schools/current/pedagogical-structure/classes` | `schools.update` |
| GET | `/schools/current/pedagogical-structure/classes/{id}/locals` | `schools.read` |
| POST | `/schools/current/pedagogical-structure/locals` | `schools.update` |
| PUT | `/schools/current/pedagogical-structure/locals/{id}` | `schools.update` |
| DELETE | `/schools/current/pedagogical-structure/locals/{id}` | `schools.update` |

## Élèves

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/students` | `students.read` |
| GET | `/students/{id}` | `students.read` |
| POST | `/students` | `students.create` |
| PUT | `/students/{id}` | `students.update` |
| DELETE | `/students/{id}` | `students.delete` |

## Académique

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/academic/sections` | `schools.read` |
| GET | `/academic/classrooms` | `schools.read` |
| POST | `/academic/classrooms` | `schools.update` |
| GET | `/academic/courses` | `schools.read` |
| POST | `/academic/courses` | `schools.update` |
| GET | `/academic/enrollments` | `schools.read` |
| POST | `/academic/enrollments` | `schools.update` |

## Notes

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/grades/evaluations` | `grades.read` |
| POST | `/grades/evaluations` | `grades.create` |
| GET | `/grades/evaluations/{id}/entries` | `grades.read` |
| POST | `/grades/entries` | `grades.update` |
| POST | `/grades/period-results/calculate` | `grades.update` |

## Paiements

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/payments` | `payments.read` |
| POST | `/payments` | `payments.create` |

## Documents

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/documents` | `students.read` |
| POST | `/documents` | `students.update` |
| GET | `/documents/{id}/download` | `students.read` |
| DELETE | `/documents/{id}` | `students.update` |

## Rapports

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/reports/dashboard` | `reports.read` |
| GET | `/reports/enrollment-by-class` | `reports.read` |
| GET | `/reports/class-averages` | `reports.read` |
| GET | `/reports/financial-summary` | `reports.read` |

## Parent (mobile)

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/parent/children` | `reports.read` |
| GET | `/parent/children/{id}/payments` | `payments.read` |
| GET | `/parent/children/{id}/bulletins` | `grades.read` |

## Enseignant (mobile)

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/teacher/assignments` | `grades.read` |
| GET | `/teacher/classes/{id}/students` | `grades.read` |
| GET | `/teacher/periods` | `grades.read` |

## Administration

| Méthode | Route | Permission |
|---------|-------|------------|
| GET | `/admin/users` | `admin.full` |
| GET | `/admin/roles` | `admin.full` |
| POST | `/admin/users` | `admin.full` |
| PUT | `/admin/users/{id}` | `admin.full` |
| PUT | `/admin/users/{id}/roles` | `admin.full` |

## Format de réponse

```json
{
  "success": true,
  "message": "…",
  "data": { },
  "errors": null
}
```
