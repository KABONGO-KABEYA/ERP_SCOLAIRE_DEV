# Module Notes — Branche de travail collaborative

> **Branche Git :** `feature/notes`  
> **Base :** `main` (dernière version poussée)  
> **Objectif :** continuer / enrichir le module Notes sans bloquer le reste de l’ERP

---

## 1. Démarrage pour le collaborateur

```bash
git clone https://github.com/KABONGO-KABEYA/ERP_Administration_Scolaire_2026.git
cd ERP_Administration_Scolaire_2026
git fetch origin
git checkout feature/notes
git pull origin feature/notes
```

Ouvrir le dossier dans **Cursor** et travailler **uniquement** sur `feature/notes`.

Avant chaque journée :

```bash
git pull origin feature/notes
```

À la fin d’une session :

```bash
git add .
git commit -m "Notes: description courte de la modification"
git push origin feature/notes
```

Le propriétaire du repo récupère ensuite avec :

```bash
git fetch origin
git checkout main
git pull origin main
git merge origin/feature/notes
# ou via Pull Request GitHub : feature/notes → main
```

---

## 2. Ce qui existe déjà (point de départ)

| Couche | Emplacement | Rôle |
|--------|-------------|------|
| Domaine | `src/SchoolManagement.Domain/Entities/Grades/` | `Evaluation`, `GradeEntry`, résultats de période |
| Application | `src/SchoolManagement.Application/Grades/` | DTOs, `IGradeService`, `GradeService` (+ session cotation) |
| API | `src/SchoolManagement.API/Controllers/GradesController.cs` | CRUD + `POST cotation/session` + `GET cotation/periods` |
| Desktop | `src/SchoolManagement.Desktop/Views/GradesView.xaml` | Identification enseignant → classes/cours filtrés |
| Mobile enseignant | `TeacherController` + app Flutter | Affectations, classes, périodes |
| Mobile parent | `ParentController` bulletins | Consultation bulletins |
| Permissions | `grades.read`, `grades.create`, `grades.update` | Voir `Permissions.cs` |
| API doc | `docs/api-reference.md` § Notes | Routes `/api/v1/grades/...` |

### Flux Desktop actuel (RDC)

1. Panneau d'identification : année + enseignant (lié au compte connecté pour les enseignants).
2. Chargement automatique des affectations (`CourseAssignment`) selon la portée.
3. Paramètres : **Classe → période active (ouverte par l'admin) → Type d'évaluation → Cours**.
4. Anti-doublon : même année/classe/cours/période/type rouvre l'évaluation existante ; examen = 1 seule évaluation/cours.
5. Grille + stats + export Excel conservés.

**Moteur de périodes :** module Desktop « Périodes pédagogiques » (`AcademicMainPeriod` + `AcademicPeriod` enrichi). L'admin ouvre/clôture/verrouille ; Cotation n'offre plus le choix libre de période.

---

## 3. Pistes de travail suggérées

1. **Desktop** — fiabiliser `GradesView` (saisie notes, filtres, feedback erreurs).
2. **Bulletins** — génération / aperçu PDF aligné avec le branding établissement.
3. **Règles RDC** — coefficients, types d’évaluation, périodes (placeholders Paramètres).
4. **Mobile enseignant** — saisie de notes fluide hors ligne / sync si besoin.
5. **Tests** — scénarios API (création évaluation → saisie → calcul moyennes).

Respecter l’architecture : **Desktop / Mobile → API uniquement** (pas d’accès SQL direct depuis le client).

---

## 4. Règles de collaboration

- Toujours pousser sur **`feature/notes`**, jamais forcer sur `main` sans accord.
- Ne pas modifier hors périmètre Notes sauf nécessité (et le signaler dans le commit).
- Éviter les fichiers temporaires (`*_wpftmp.csproj`, `bin/`, `obj/`, `.dart_tool/`).
- Message de commit en français, style : `Notes: …` / `fix(notes): …`.

---

## 5. Accès GitHub

Le propriétaire doit ajouter le collaborateur dans :

**GitHub → Settings du repo → Collaborators → Add people** (rôle **Write**).

Sans cet accès, le clone est possible mais le `push` échouera.
