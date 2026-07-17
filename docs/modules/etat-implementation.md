# État d'implémentation — Modules métier

> **ERP Administration Scolaire RDC** — Documentation de ce qui est déjà réalisé  
> **Date :** juillet 2026  
> **Périmètre :** Module Élève, Paramètres Établissement, Structure pédagogique, Années scolaires, Frais scolaires

---

## Table des matières

1. [Vue d'ensemble](#1-vue-densemble)
2. [Module Élève](#2-module-élève)
3. [Paramètres Établissement](#3-paramètres-établissement)
4. [Structure pédagogique](#4-structure-pédagogique)
5. [Années scolaires](#5-années-scolaires)
6. [Frais scolaires](#6-frais-scolaires)
7. [Synthèse complété / partiel](#7-synthèse-complété--partiel)
8. [Fichiers clés](#8-fichiers-clés)

---

## 1. Vue d'ensemble

| Module | Desktop WPF | API REST | Mobile Flutter | Maturité |
|--------|:-----------:|:--------:|:--------------:|:--------:|
| **Élève** | ✅ | ✅ | ⚠️ Inscription secrétaire | **Élevée** |
| **Paramètres Établissement** | ✅ | ✅ | ❌ | **Élevée** |
| **Structure pédagogique** | ✅ | ✅ | ⚠️ Consommation inscription | **Élevée** |
| **Années scolaires** | ✅ | ✅ | ⚠️ Consommation | **Moyenne** |
| **Frais scolaires** | ✅ | ✅ | ❌ | **Élevée** |

**Stack technique :** .NET 8 (Clean Architecture), WPF MVVM, ASP.NET Core API, SQL Server, Flutter (mobile).

**Principe :** le Desktop et le Mobile passent exclusivement par l'API (`/api/v1/*`). La configuration SQL et fichiers se fait via des fichiers locaux (`ServeurDonnees.txt`, `ServeurFichiers.txt`) contrôlés au démarrage.

---

## 2. Module Élève

### 2.1 Objectif

Gérer le cycle de vie des élèves : inscription, réinscription, modification de dossier, consultation, exclusion/abandon, documents et impression.

### 2.2 Écrans Desktop

| Écran | Fichier | Description |
|-------|---------|-------------|
| **Gestion des élèves** | `Views/StudentsView.xaml` | Hub principal : liste, filtres, actions |
| **Assistant inscription V2** | `Views/InscriptionEleveV2View.xaml` | Wizard 6 étapes (nouvelle inscription / réinscription) |
| **Modification dossier** | `Views/StudentDossierEditWindow.xaml` | Édition complète pour élève inscrit année courante |
| **Profil / historique** | `Views/StudentProfileWindow.xaml` | Identité + historique scolarité |
| **Fichiers dossier** | `Views/StudentDossierFilesWindow.xaml` | Explorateur fichiers sur disque |
| **Exclusion / abandon** | `Views/StudentWithdrawalWindow.xaml` | Dialogue avec raisons prédéfinies |
| **Impression liste** | `Services/StudentListPrintService.cs` | Export PDF liste filtrée |

**ViewModels associés :** `StudentsViewModel`, `EnrollmentWizardViewModel`, `StudentDossierEditViewModel`.

### 2.3 Fonctionnalités implémentées

#### Liste et recherche
- Recherche texte, filtres par année, section, classe pédagogique, local, option d'étude
- Filtres statut : inscrits, exclus, abandonnés
- Menu contextuel : profil, dossier fichiers, modifier, exclure/abandonner
- Impression de la liste filtrée

#### Assistant d'inscription (6 étapes)

**Nouvelle inscription :**
1. Identité (nom, postnom, prénom, genre, date/lieu de naissance, nationalité, adresse structurée)
2. Scolarité (section, local/classe, contrôle âge et capacité)
3. Responsables (père, mère, contacts ; recherche tuteurs existants)
4. Santé (groupe sanguin, allergies, conditions médicales)
5. Documents (upload : acte de naissance, photo, bulletin, certificat médical, etc.)
6. Validation + génération fiche d'inscription PDF

**Réinscription :** recherche élève existant → pré-remplissage → étapes 2 à 6.

**Modification dossier :** réservée aux inscrits de l'année courante ; changement de classe bloqué si notes/présences/résultats existent.

#### Documents et stockage
- Upload via `POST /api/v1/enrollment-wizard/store-file` (max 20 Mo)
- Stockage filesystem : `Dossier_Elève/{Nom_Prenom_Matricule}/{Année}/`
- Configuration racine via `ServeurFichiers.txt`

#### Exclusion / abandon
- Raisons codifiées (RDC)
- Mise à jour statut inscription + historique (`StudentStatusHistory`)

### 2.4 API

#### `StudentsController` — `api/v1/students`

| Méthode | Route | Description |
|---------|-------|-------------|
| GET | `/` | Recherche paginée |
| GET | `/{id}` | Détail élève |
| GET | `/{id}/profile` | Profil + historique inscriptions |
| POST | `/` | Création simple (CRUD basique) |
| PUT | `/{id}` | Mise à jour simple |
| POST | `/{id}/withdraw-current-year` | Exclusion ou abandon année courante |
| GET | `/withdrawal-reasons` | Listes de raisons |
| DELETE | `/{id}` | Archivage logique |
| GET | `/{id}/dossier-files` | Fichiers sur disque |

#### `EnrollmentWizardController` — `api/v1/enrollment-wizard`

| Méthode | Route | Description |
|---------|-------|-------------|
| GET | `/prerequisites` | Vérif année, structure, locaux |
| GET | `/registration-number` | Génération matricule auto |
| GET | `/search-students` | Recherche pour réinscription |
| GET | `/search-guardians` | Recherche tuteurs existants |
| POST | `/store-file` | Upload document |
| GET | `/structure-options` | Sections + locaux disponibles |
| GET | `/class-capacity` | Capacité classe |
| GET | `/fees` | Calcul frais (optionnel) |
| POST | `/validate` | Validation dossier |
| POST | `/complete` | Finalisation inscription |
| GET | `/fiche-inscription/{enrollmentId}` | Données fiche PDF |
| GET | `/student-dossier/{studentId}` | Chargement dossier édition |
| POST | `/student-dossier/{enrollmentId}/validate` | Validation modification |
| PUT | `/student-dossier/{enrollmentId}` | Mise à jour dossier |

**Permissions :** `students.read`, `students.create`, `students.update`, `students.delete`.

### 2.5 Entités domaine

| Entité | Rôle |
|--------|------|
| `Student` | Identité, adresse, photo, infos médicales, archivage |
| `Guardian` | Tuteur (identité, contacts, profession) |
| `StudentGuardian` | Lien élève–tuteur, relation, principal, autorisation récupération |
| `Enrollment` | Inscription année/classe/local, statut, dates |
| `StudentDocument` | Métadonnées document (type, chemin, taille) |
| `StudentStatusHistory` | Historique transitions de statut |

**Statuts inscription (`EnrollmentStatus`) :** PreInscription, Inscrit, Reinscrit, Transfere, Abandon, Exclusion, Archive.

### 2.6 Services métier

- **`StudentService`** — recherche, profil, retrait année courante, archivage, listage fichiers dossier
- **`EnrollmentWizardService`** — prérequis, validation, finalisation, modification dossier, génération matricule
- **`EnrollmentFormService`** — fiche d'inscription PDF brandée (logos, en-têtes, signatures)
- **`StudentDossierStorageService`** — persistance fichiers sur disque

### 2.7 Mobile (annexe)

- Wizard inscription 6 étapes (`enrollment_wizard_screen.dart`) — miroir Desktop pour secrétaire
- Pas de module élève complet côté parent (consultation paiements/bulletins seulement)

### 2.8 Non implémenté / partiel

| Élément | Statut |
|---------|--------|
| Frais à l'inscription dans le wizard Desktop | ✅ Soldes créés automatiquement à la validation (`StudentFeeBalance`) |
| Archivage élève | API + VM présents, pas de bouton UI |
| Brouillon wizard | Message local uniquement, pas de persistance serveur |
| `StudentEditWindow` | Legacy, remplacé par `StudentDossierEditWindow` |
| `DocumentsController` | API séparée, non intégrée au menu élève |

---

## 3. Paramètres Établissement

### 3.1 Objectif

Centraliser la configuration de l'établissement : profil, identité documentaire, règlement, structure, années, frais, géographie, utilisateurs.

### 3.2 Navigation Desktop

Accès : **Paramètres** (barre latérale Shell) → sous-sections via `SettingsNavCatalog`.

| Groupe | Rubrique | Statut |
|--------|----------|--------|
| **Référentiels** | Établissement | ✅ Complet (partiel champs) |
| | Structure pédagogique | ✅ Complet |
| | Années scolaires | ✅ Complet |
| | Matières | ✅ Complet |
| | Géographie | ✅ Complet |
| | Utilisateurs | ✅ Complet |
| | Enseignants | ✅ Complet |
| **Configuration financière** | Frais scolaires | ✅ Complet |
| **Administration scolaire** | Règlement intérieur | ✅ Complet |
| | Calendrier, Types d'évaluations, Coefficients | ⏳ Placeholder |
| **Administration système** | Sauvegarde, Journal, Paramètres système | ⏳ Placeholder |

### 3.3 Profil établissement

**Champs UI :** nom, raison sociale, ville, province, téléphone, email.

**Champs modèle non exposés UI :** adresse complète, n° enregistrement, site web, devise par défaut.

**API :** `GET/PUT api/v1/schools/current`

### 3.4 Identité documentaire (branding)

Gestion complète des éléments visuels pour documents imprimés :

- Logos (principal, actif/inactif)
- En-têtes par type de document
- Signatures, cachets
- Pieds de page

**Stockage :** `{racine_fichiers}/Documents/{Logos|Entetes|Signatures|Cachets}/{schoolId}/`

**API :** `api/v1/document-branding/*`

**Utilisation :** fiches d'inscription, bulletins, documents brandés via `DocumentPrintBrandingResolver`.

**Fichiers :** `Controls/DocumentBrandingControl.xaml`, `DocumentBrandingViewModel.cs`, `DocumentBrandingService.cs`.

### 3.5 Règlement intérieur

- Éditeur texte multiligne + date de dernière mise à jour
- Persisté en `AppConfiguration` (clé-valeur)
- **API :** `GET/PUT api/v1/schools/current/regulation`

### 3.6 Géographie

- CRUD pays / provinces / villes / communes
- Import Excel + modèle téléchargeable
- API lecture séparée pour formulaires (inscription, adresses)

**Fichiers :** `Controls/GeographyAdminControl.xaml`, `GeographyAdminController.cs`.

### 3.7 Utilisateurs et enseignants

- **Utilisateurs :** liste, création, rôles, activation, adresse structurée
- **Enseignants :** CRUD matricule, coordonnées, spécialisation, date d'embauche

**API :** `api/v1/admin/users`, `/roles`, `/teachers` (permission `admin.full`).

### 3.8 Configuration technique (hors écran Paramètres)

#### Base de données — `ServeurDonnees.txt`

| Élément | Description |
|---------|-------------|
| Format | Clé=valeur (serveur, port, base, auth SQL/Windows, mot de passe chiffré) |
| Desktop | `DatabaseStartupGate` bloque le démarrage ; fenêtre `DatabaseServerConfigWindow` |
| Login | Bouton « Changer d'établissement » rouvre la config SQL |
| API | Validation au boot ; arrêt fatal si échec |

#### Stockage fichiers — `ServeurFichiers.txt`

| Élément | Description |
|---------|-------------|
| Contenu | Racine dossier partagé (`Dossier_Elève`, sous-dossier `Documents/` pour branding) |
| Desktop | `FileStorageStartupGate` + `FileStorageConfigWindow` |
| API | Validation + test écriture au démarrage |

#### Login et authentification

- Fenêtre `LoginWindow` : identifiant/mot de passe, statut serveur, version
- JWT via `AuthController`
- Changement de mot de passe obligatoire si `MustChangePassword`
- Thème login premium (`LoginBrandPanel`, `LoginFormPanel`)

**Flux démarrage Desktop :**

```
Démarrage → DatabaseStartupGate → FileStorageStartupGate → LoginWindow
    → (MustChangePassword ? ChangePasswordWindow) → MainWindow
```

### 3.9 Non implémenté / partiel

- Champs profil école complets en UI
- Placeholders : calendrier, évaluations, coefficients, sauvegarde, journal
- Module Paramètres absent sur mobile
- Reset mot de passe : message « contacter l'administrateur »

---

## 4. Structure pédagogique

### 4.1 Objectif

Modéliser le système éducatif RDC : catalogue officiel, activation par établissement, locaux (salles) par année scolaire.

**Référence métier :** `Structure_Systeme_Educatif_RDC.md`

### 4.2 Catalogue RDC

**~167 classes templates** dans `RdcPedagogicalCatalog` :

| Programme | Contenu |
|-----------|---------|
| Maternelle | 3 classes |
| Primaire | 6 classes |
| CTEB | 2 classes |
| Humanités cycle long | ~120 (6 sections × options × 4 niveaux) |
| Humanités professionnelles | 8 filières × 3 niveaux = 24 |
| Filières spécialisées | 3 filières × 4 niveaux = 12 |

Chaque template : `TemplateCode`, `SchoolProgram`, `LevelOrder`, `DisplayName`, `HumanitiesSection`, `StudyOption`, âges (maternelle).

### 4.3 Sections institutionnelles

**6 codes DB** auto-provisionnés : MAT, PRI, CTEB, HUM, HPRO, FS.

**8 regroupements UI** (`StructureUiCatalog`) : Maternelle, Primaire, Secondaire général, Humanités techniques, Commerciale & Gestion, Technique rurale, Informatique, Arts.

### 4.4 Initialisation par école

Au premier accès (`GetSummary`, `GetClasses`) :

1. Crée les 6 sections DB
2. Insère toutes les `PedagogicalClass` du catalogue avec **`IsEnabled = false`**
3. Endpoint explicite : `POST .../pedagogical-structure/initialize`

### 4.5 Activation des classes et locaux

#### Classes pédagogiques
- Activer/désactiver une ou plusieurs classes
- Modifier `MinAge` / `MaxAge` (API ; UI partielle)
- **Garde-fou :** impossible de désactiver si élèves inscrits année courante

#### Locaux (`ClassRoom`)
- Liés à : `PedagogicalClassId`, `AcademicYearId`, `SectionId`, `StudyOptionId`
- Champs : nom (A, B…), code auto, capacité, observations, `IsActive`
- Unicité : `(PedagogicalClassId, AcademicYearId, Name)`
- Création interdite si classe pédagogique non activée

### 4.6 Interface Desktop — Wizard 3 colonnes

**Fichier :** `Views/PedagogicalStructureWizardControl.xaml`

| Colonne | Contenu |
|---------|---------|
| **Gauche** | 8 sections UI (Maternelle → Arts) |
| **Centre** | Options + liste classes avec toggle activation |
| **Droite** | Gestion locaux (CRUD, sélecteur année) |
| **Barre** | Recherche, filtres (Toutes/Activées/Inactives/Sans locaux), stats, Enregistrer |

**Commandes :** `SaveClassesCommand`, `AddLocalCommand`, `UpdateLocalCommand`, `DeleteLocalCommand`.

### 4.7 API REST

**Base :** `api/v1/schools/current/pedagogical-structure`

| Méthode | Route | Description |
|---------|-------|-------------|
| POST | `/initialize` | Seed sections + classes catalogue |
| GET | `/summary` | Total / activées / locaux |
| GET | `/classes` | Liste (search, program, enabledOnly, academicYearId) |
| PUT | `/classes/{id}` | Mise à jour unitaire |
| PUT | `/classes` | Mise à jour bulk |
| GET | `/classes/{id}/locals` | Locaux d'une classe |
| POST | `/locals` | Créer un local |
| PUT | `/locals/{id}` | Modifier un local |
| DELETE | `/locals/{id}` | Supprimer un local |

**Permissions :** `schools.read` / `schools.update`.

### 4.8 Entités

| Entité | Rôle |
|--------|------|
| `PedagogicalClass` | Classe officielle activée par école |
| `ClassRoom` | Local physique par année |
| `Section` | Section institutionnelle (6 codes) |
| `StudyOption` | Option/filière (humanités) |
| `Course` | Matière — liée au **local**, pas à la classe pédagogique |

### 4.9 Provisionnement inter-années

Lors création ou bascule d'année courante :

- **`AcademicYearClassRoomProvisioner`** — copie locaux actifs année N-1 → N
- **`ClassFeeScheduleProvisioner`** — copie tarifs par classe pédagogique

### 4.10 Intégrations

| Module | Usage |
|--------|-------|
| Inscription | Prérequis structure + locaux ; sélection `ClassRoom` ; contrôle âge/capacité |
| Frais scolaires | Tarification par `PedagogicalClassId` |
| Matières | Cours rattachés au local (`ClassRoomId`) |
| Dashboard | Rappel d'activer les classes |

### 4.11 Workflow inscription

```
Prérequis (année + classes activées + locaux)
    → GetStructureOptions (sections + locaux filtrés)
    → Contrôle capacité + compatibilité âge
    → Enregistrement avec ClassRoomId + PedagogicalClassId
```

### 4.12 Non implémenté / partiel

| Élément | Statut |
|---------|--------|
| Édition MinAge/MaxAge UI | API oui, champs UI absents |
| Admin structure web/mobile | Absent |
| Filtre par programme UI | Backend prêt, UI neutralisée |
| Matières au niveau classe pédagogique | Module séparé, lié au local |
| Tests automatisés | Non trouvés |

---

## 5. Années scolaires

### 5.1 Objectif

Gérer les années scolaires de l'établissement, définir l'année courante, provisionner automatiquement locaux et tarifs.

### 5.2 Fonctionnalités implémentées

| Fonction | Statut |
|----------|--------|
| Liste des années | ✅ |
| Création année | ✅ |
| Définir année courante | ✅ |
| Modification / suppression / clôture | ❌ |
| Gestion périodes (trimestres) | ⚠️ Entité + seed, pas d'API/UI |
| Provisionnement locaux + frais | ✅ Automatique |

### 5.3 Entités

**`AcademicYear`**
- `Label`, `StartDate`, `EndDate`, `IsCurrent`, `IsClosed`
- Index unique `(SchoolId, Label)`

**`AcademicPeriod`**
- Trimestres rattachés à une année (`Name`, `PeriodType`, `OrderIndex`, dates, `IsClosed`)
- Seed démo : 3 trimestres pour `2025-2026`

### 5.4 Règles métier

- **`EndDate > StartDate`** obligatoire à la création
- **Une seule année `IsCurrent`** par école
- **`SchoolConfigurationGuards`** — opérations sensibles (inscription) : année courante et non clôturée uniquement
- Bascule année courante → copie locaux + tarifs depuis année précédente

### 5.5 API

| Méthode | Route | Description |
|---------|-------|-------------|
| GET | `api/v1/schools/current/academic-years` | Liste triée par date décroissante |
| POST | `api/v1/schools/current/academic-years` | Créer (option `SetAsCurrent`) |
| PUT | `.../academic-years/{yearId}/set-current` | Définir année courante |
| GET | `api/v1/schools/current/lookups` | Lookups incluant années/périodes |

### 5.6 UI Desktop

**Paramètres → Années scolaires** (`SettingsView.xaml`)

- Grille : libellé, début, fin, courante, clôturée
- Bouton « Définir comme année courante »
- Formulaire création (libellé, dates, case « Définir comme année courante »)
- Notification globale via `AcademicYearRefreshBridge` (autres modules se rafraîchissent)

**Consommateurs :** inscription, notes, paiements, statistiques, frais scolaires, dashboard.

### 5.7 Services

- **`SchoolService`** — CRUD années, bascule courante, lookups
- **`AcademicYearClassRoomProvisioner`** — copie locaux inter-années
- **`ClassFeeScheduleProvisioner`** — copie tarifs inter-années

### 5.8 Non implémenté

- API/UI modification, suppression, clôture année
- CRUD périodes académiques (trimestres)
- Workflow `IsClosed` (champ existe, pas de flux exposé)
- Gestion années sur mobile

---

## 6. Frais scolaires

### 6.1 Objectif

Configurer le catalogue des frais (types, tranches), affecter les tranches aux types, définir les montants et échéances **par classe pédagogique et par année**.

### 6.2 Modèle de données

```
FeeType (catalogue école : Inscription, Minerval…)
    ↕ FeeTypeInstallment (affectation tranche → type + ordre)
FeeInstallment (tranches libres : Inscription, 1ère tranche…)
    ↓
ClassFeeAmount (montant + échéance par année × classe pédagogique × type × tranche)
```

**Important :** les tarifs sont au niveau **`PedagogicalClass`**, pas du local (`ClassRoom`).

### 6.3 Entités

| Entité | Champs clés |
|--------|-------------|
| `FeeType` | Code, nom, devise (CDF/USD), obligatoire, actif |
| `FeeInstallment` | Nom, `SortOrder`, actif |
| `FeeTypeInstallment` | Liaison type ↔ tranche avec ordre |
| `ClassFeeAmount` | Montant, `DueDate`, `SortOrder` |
| `StudentFeeBalance` | Solde dû/payé par élève × année × type (agrégé annuel) |

### 6.4 UI Desktop — Configuration des frais

**Accès :** Paramètres → Frais scolaires

**Fichiers :** `Controls/SchoolFeeConfigurationControl.xaml`, `ViewModels/SchoolFeeConfigurationViewModel.cs`, `Themes/SchoolFeeConfiguration.xaml`.

#### Section 1 — Configuration des tarifs

| Élément | Description |
|---------|-------------|
| Filtres | Année scolaire, section, type de frais |
| Panneau classes | Liste classes pédagogiques avec sélection multiple |
| Grille tranches | Ordre, tranche, montant, date limite, état, actions |
| Bouton « + Ajouter une tranche » | Popup tranches disponibles (catalogue) |
| Suppression | Icône par ligne (local jusqu'à Enregistrer) |
| Total annuel | Sous le tableau |
| Actions | Enregistrer, copier depuis année précédente, réinitialiser |

#### Section 2 — Types de frais
- CRUD catalogue (code auto-généré, devise, obligatoire, actif)

#### Section 3 — Tranches
- CRUD catalogue global

#### Section 4 — Affectation tranches ↔ type
- Par type sélectionné, ordre des tranches

### 6.5 Logique par classe (récente)

#### Principe
Chaque classe peut avoir sa propre grille tarifaire. Modifier une classe n'impacte pas les autres.

#### Signatures de configuration
Empreinte par classe pour détecter configurations identiques :

```
{FeeInstallmentId}|{SortOrder}|{Amount}|{DueDate}
```

concaténées, ordonnées par `SortOrder`.

#### Sélection multi-classe
- Seules les classes de **même signature** peuvent être sélectionnées ensemble
- « Tout sélectionner » ne prend que les classes compatibles
- Classes avec config différente : checkbox désactivée visuellement

#### Chargement / enregistrement
- **1 classe :** `GET/PUT schedule` (unitaire)
- **N classes compatibles :** `PUT schedule/bulk`
- Modifications locales (ajout/suppression tranche) jusqu'au clic **Enregistrer**
- À l'enregistrement : tranches utilisées auto-liées au type de frais si absentes

#### Affichage grille (`BuildClassScheduleLines`)
1. Si la classe a des `ClassFeeAmount` actifs → afficher **uniquement ses tranches**
2. Sinon → template depuis `FeeTypeInstallments` (montants à 0)
3. Soft-delete des lignes absentes de la requête à l'enregistrement

### 6.6 Règles métier

#### Édition (`AcademicYearFeeRules`)
- Modifiable si année **courante** OU **future** (`StartDate > aujourd'hui`)
- Années passées non courantes : **consultation seule**

#### Enregistrement
- Tranche doit être affectée au type (auto-liaison à la sauvegarde)
- Montant ≥ 0, `SortOrder > 0`
- Copie inter-années : ne copie que tranches **absentes** (pas d'écrasement)

#### Résolution montant inscription
- `ResolveAnnualAmountAsync` = **somme des montants** des tranches pour classe/type/année

### 6.7 API

**Base :** `api/v1/school-fees`

| Route | Description |
|-------|-------------|
| `GET catalog` | Types + tranches |
| `GET/POST/PUT/DELETE fee-types` | Gestion types |
| `GET/POST/PUT/DELETE installments` | Gestion tranches |
| `GET/PUT fee-types/{id}/installments` | Affectation tranches au type |
| `GET schedule` | Grille (année, classe, type) |
| `GET schedule/signatures` | Empreintes toutes classes |
| `PUT schedule` | Enregistrer une classe |
| `PUT schedule/bulk` | Enregistrer plusieurs classes |
| `POST schedule/copy-from-previous` | Copie unitaire |
| `POST schedule/copy-from-previous/bulk` | Copie bulk |

### 6.8 Provisionnement

**`ClassFeeScheduleProvisioner`** — déclenché à la création d'année courante ou bascule :

1. Identifie année source (précédente par date)
2. Copie tous les `ClassFeeAmount` actifs
3. Ignore combinaisons déjà présentes sur année cible

### 6.9 Intégration inscription

- **`CalculateFeesAsync`** — somme des tranches par type actif pour la classe (catégorie tarifaire `GENERAL` si présente, sinon 1re catégorie active)
- **`CompleteAsync`** — calcule les frais côté serveur si `FeeSummary` absente, puis crée/met à jour les `StudentFeeBalance` (dû = net, payé = 0)
- Desktop wizard : appelle `CalculateFees` avant validation ; le mobile et tout client sans `FeeSummary` restent couverts par le serveur
- Prérequis : grilles tarifaires configurées pour la classe et l'année courante (module Frais scolaires)

### 6.10 Données initiales (seed)

Types : `INSCR` (Frais d'inscription), `MINVAL` (Minerval) — CDF, obligatoires.

Tranches : Inscription, 1ère/2ème/3ème tranche.

Pas de montants par classe en seed (à configurer dans l'UI).

### 6.11 Contrôles UI récents

- **`ErpGridDatePicker`** — sélecteur date en grille avec contraintes chronologiques (`BlackoutDates`)

### 6.12 Non implémenté / partiel

| Élément | Statut |
|---------|--------|
| Frais wizard Desktop | Calcul API OK, non branché |
| Paiements par tranche | Paiement au niveau type de frais seulement |
| Configuration frais mobile | Absent |
| `FeeTypeCount` prérequis inscription | Toujours 0 (non alimenté) |

---

## 7. Synthèse complété / partiel

### ✅ Fonctionnel et utilisable en production (cœur métier)

| Domaine | Éléments |
|---------|----------|
| **Élève** | Liste/recherche, wizard inscription/réinscription, modification dossier, documents, exclusion, profil, PDF fiche |
| **Paramètres** | Profil école, branding documents, règlement, géographie, utilisateurs, enseignants, config SQL/fichiers, login |
| **Structure** | Catalogue RDC (~167 classes), activation, locaux par année, wizard desktop, intégration inscription |
| **Années** | Création, année courante, provisionnement locaux + frais |
| **Frais** | Types, tranches, grilles par classe, signatures, bulk, copie inter-années, règles édition |

### ⚠️ Partiel ou en attente

| Domaine | Lacune |
|---------|--------|
| **Élève** | Frais à l'inscription UI, archivage UI, brouillon wizard |
| **Paramètres** | Champs profil complets, placeholders admin (calendrier, sauvegarde…) |
| **Structure** | Édition âges UI, admin mobile/web |
| **Années** | Clôture, modification, gestion trimestres |
| **Frais** | Intégration wizard/paiements par tranche |

### ❌ Non implémenté

- Module Paramètres sur mobile
- Administration structure pédagogique mobile/web
- Tests automatisés dédiés à ces modules

---

## 8. Fichiers clés

### Module Élève

```
src/SchoolManagement.Desktop/Views/StudentsView.xaml
src/SchoolManagement.Desktop/Views/InscriptionEleveV2View.xaml
src/SchoolManagement.Desktop/ViewModels/StudentsViewModel.cs
src/SchoolManagement.Desktop/ViewModels/EnrollmentWizardViewModel.cs
src/SchoolManagement.API/Controllers/StudentsController.cs
src/SchoolManagement.API/Controllers/EnrollmentWizardController.cs
src/SchoolManagement.Application/Students/Services/StudentService.cs
src/SchoolManagement.Application/EnrollmentWizard/Services/EnrollmentWizardService.cs
src/SchoolManagement.Domain/Entities/Students/Student.cs
src/SchoolManagement.Infrastructure/Services/StudentDossierStorageService.cs
```

### Paramètres Établissement

```
src/SchoolManagement.Desktop/Views/SettingsView.xaml
src/SchoolManagement.Desktop/ViewModels/SettingsViewModel.cs
src/SchoolManagement.Desktop/UI/SettingsNavCatalog.cs
src/SchoolManagement.Desktop/Controls/DocumentBrandingControl.xaml
src/SchoolManagement.Desktop/Views/LoginWindow.xaml
src/SchoolManagement.Desktop/Services/DatabaseStartupGate.cs
src/SchoolManagement.Desktop/Services/FileStorageStartupGate.cs
src/SchoolManagement.API/Controllers/SchoolsController.cs
src/SchoolManagement.API/Controllers/DocumentBrandingController.cs
src/SchoolManagement.Application/DocumentBranding/Services/DocumentBrandingService.cs
src/SchoolManagement.Domain/Entities/Settings/School.cs
src/SchoolManagement.Domain/Entities/Settings/DocumentBranding.cs
```

### Structure pédagogique

```
Structure_Systeme_Educatif_RDC.md
src/SchoolManagement.Desktop/Views/PedagogicalStructureWizardControl.xaml
src/SchoolManagement.Desktop/UI/StructureUiCatalog.cs
src/SchoolManagement.API/Controllers/PedagogicalStructureController.cs
src/SchoolManagement.Application/Schools/Services/PedagogicalStructureService.cs
src/SchoolManagement.Application/Schools/Catalog/RdcPedagogicalCatalog.cs
src/SchoolManagement.Application/Schools/AcademicYearClassRoomProvisioner.cs
database/scripts/004_PedagogicalStructure.sql
```

### Années scolaires

```
src/SchoolManagement.Application/Schools/Services/SchoolService.cs
src/SchoolManagement.Application/Schools/SchoolConfigurationGuards.cs
src/SchoolManagement.Desktop/UI/AcademicYearRefreshBridge.cs
database/scripts/003_SeedData.sql
```

### Frais scolaires

```
src/SchoolManagement.Desktop/Controls/SchoolFeeConfigurationControl.xaml
src/SchoolManagement.Desktop/ViewModels/SchoolFeeConfigurationViewModel.cs
src/SchoolManagement.Desktop/Themes/SchoolFeeConfiguration.xaml
src/SchoolManagement.Desktop/Controls/ErpGridDatePicker.xaml
src/SchoolManagement.API/Controllers/SchoolFeeController.cs
src/SchoolManagement.Application/SchoolFees/Services/SchoolFeeService.cs
src/SchoolManagement.Application/SchoolFees/ClassFeeScheduleSignatureHelper.cs
src/SchoolManagement.Application/SchoolFees/AcademicYearFeeRules.cs
src/SchoolManagement.Application/SchoolFees/ClassFeeScheduleProvisioner.cs
src/SchoolManagement.Infrastructure/Persistence/SchoolFeeSchemaInitializer.cs
```

---

## Documents connexes

- [Architecture](../architecture.md)
- [Guide de démarrage](../guide-demarrage.md)
- [Référence API](../api-reference.md)
- [Structure système éducatif RDC](../../Structure_Systeme_Educatif_RDC.md)
