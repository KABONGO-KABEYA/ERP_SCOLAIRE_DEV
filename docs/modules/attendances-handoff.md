# Module Présence — Branche de travail collaborative

> **Branche Git :** `feature/attendances`  
> **Base :** `main`  
> **Objectif :** développer le module Présence des élèves sans mélanger avec Notes ou le reste de l’ERP

---

## 1. Démarrage pour le collaborateur

```bash
git clone https://github.com/KABONGO-KABEYA/ERP_Administration_Scolaire_2026.git
cd ERP_Administration_Scolaire_2026
git fetch origin
git checkout feature/attendances
git pull origin feature/attendances
```

Ouvrir le dossier dans **Cursor** et travailler **uniquement** sur `feature/attendances`.

Avant chaque journée :

```bash
git pull origin feature/attendances
```

À la fin d’une session :

```bash
git add .
git commit -m "Présence: description courte de la modification"
git push origin feature/attendances
```

Le propriétaire du repo récupère ensuite avec :

```bash
git fetch origin
git checkout main
git pull origin main
git merge origin/feature/attendances
# ou via Pull Request GitHub : feature/attendances → main
```

---

## 2. Ce qui existe déjà (point de départ)

| Couche | Emplacement | Rôle |
|--------|-------------|------|
| Domaine | `StudentAttendance`, `TeacherAttendance` dans `Entities/Academic/Academic.cs` | Présence élève / enseignant |
| Enum | `StudentAttendancePresence` dans `DomainEnums.cs` | Absent, Present, Late, Excused |
| Lien inscription | `EnrollmentId` sur `StudentAttendance` | Une présence est liée à une **inscription** (`Enrollment`) |
| EF | `AcademicConfigurations.cs` | FK, index `(EnrollmentId, AttendanceDate, CourseAssignmentId)` |
| Schéma BDD | `AttendanceSchemaInitializer.cs` | Migration idempotente au démarrage API |
| Script SQL | `database/scripts/009_StudentAttendanceEnrollment.sql` | Alignement manuel local / cloud |
| Consommation | `ParentService`, `PromoterDashboardService` | Lecture présences (parent / dashboard) |

**Ne pas recréer** la table : étendre avec l’**écran desktop**, l’**API de saisie** et les règles métier.

---

## 3. Pistes de travail suggérées

1. **API** — endpoints CRUD présences par classe / date / inscription.
2. **Desktop** — écran « Présence des élèves » (liste, saisie rapide, export).
3. **Règles** — une ligne par `(EnrollmentId, AttendanceDate, CourseAssignmentId?)`.
4. **Sync cloud** — vérifier `CloudSyncCatalog` après évolutions du modèle.
5. **Tests** — scénarios création inscription → saisie présence → stats dashboard.

Respecter l’architecture : **Desktop / Mobile → API uniquement** (pas d’accès SQL direct depuis le client).

---

## 4. Règles de collaboration

- Toujours pousser sur **`feature/attendances`**, jamais forcer sur `main` sans accord.
- Ne pas modifier le module **Notes** (`feature/notes`) depuis cette branche.
- Éviter les fichiers temporaires (`bin/`, `obj/`, `*_wpftmp.csproj`).
- Messages de commit en français : `Présence: …` / `feat(attendances): …`.

---

## 5. Accès GitHub

Le propriétaire doit ajouter le collaborateur :

**GitHub → Settings du repo → Collaborators → Add people** (rôle **Write**).
