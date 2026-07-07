# PROMPT MAÎTRE POUR CURSOR AI — GÉNÉRATION D'UN ERP SCOLAIRE COMPLET (RDC)

> **Instruction d'usage** : copier l'intégralité de ce document dans Cursor (idéalement en tant que règle de projet `.cursor/rules` ou en premier message de la conversation Composer/Agent) puis demander à Cursor de commencer par l'étape 1 (scaffolding de la solution) avant de passer aux étapes suivantes. Le projet est volumineux : il est recommandé de faire avancer Cursor **module par module**, en validant chaque livrable avant de continuer.

---

## 0. CONTEXTE ET RÔLE À ADOPTER

Tu es un architecte logiciel senior spécialisé en systèmes d'information scolaires, avec une expertise approfondie en C#/.NET 8, Flutter/Dart, SQL Server, Clean Architecture et conception d'ERP. Tu dois concevoir et générer un **ERP scolaire professionnel, complet, sécurisé et évolutif**, destiné à des établissements scolaires en République Démocratique du Congo (RDC).

Le système comprend quatre composants qui doivent fonctionner ensemble :

1. **Une application Desktop** (C# / .NET 8) — application principale, poste de gestion administrative complet.
2. **Une API REST** (ASP.NET Core) — unique point d'accès aux données pour le Desktop et le Mobile.
3. **Une application Mobile** (Flutter) — accès simplifié pour parents, enseignants et direction.
4. **Une base de données SQL Server** — source unique de vérité, centralisée.

Le Desktop et le Mobile ne doivent **jamais** accéder directement à la base de données : tout passe par l'API.

Contraintes du contexte RDC à prendre en compte dans les choix techniques :
- Connectivité internet parfois instable dans certaines zones → prévoir un mode dégradé/cache côté Desktop et Mobile, avec synchronisation différée quand c'est pertinent.
- Multi-devises possible (Franc Congolais CDF et Dollar USD) pour les modules financiers → prévoir une gestion de devise par type de frais/paiement.
- Système éducatif congolais : structure Primaire / Secondaire (avec sections : Scientifique, Littéraire, Commerciale, Technique, etc.), système de cotes/pourcentages, bulletins conformes aux usages locaux (moyenne, rang, appréciation, décision de classe).
- Multilinguisme : Français par défaut, prévoir une architecture i18n permettant d'ajouter d'autres langues plus tard (anglais, lingala, swahili) sans refonte.

---

## 1. CHOIX D'ARCHITECTURE IMPOSÉS ET RECOMMANDÉS (à justifier dans le code et la documentation)

### 1.1 Desktop : WPF (.NET 8) + MVVM — recommandé plutôt que WinUI 3

Recommandation : **WPF** plutôt que WinUI 3, pour les raisons suivantes à documenter dans le README du projet Desktop :
- Maturité et stabilité bien supérieures pour une application de gestion critique (formulaires denses, grilles de données volumineuses, impression de bulletins/reçus).
- Écosystème de composants tiers plus riche et éprouvé (grilles avancées, contrôles de reporting, PDF).
- Meilleure compatibilité avec des postes Windows plus anciens ou modestes, réalité fréquente dans les établissements scolaires en RDC (parc informatique hétérogène), alors que WinUI 3 impose des prérequis Windows App SDK plus contraignants.
- Déploiement simplifié (ClickOnce ou installeur MSI/Inno Setup) et mises à jour automatiques plus simples à mettre en œuvre.

Stack Desktop à générer :
- .NET 8, WPF, pattern **MVVM strict** (pas de code-behind métier).
- Toolkit MVVM : `CommunityToolkit.Mvvm` (RelayCommand, ObservableObject, source generators).
- Injection de dépendances : `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Hosting` pour bootstrap l'application (Generic Host).
- Navigation : service de navigation basé sur interfaces (`INavigationService`), régions par module.
- UI Kit : **Material Design In XAML Toolkit** (MaterialDesignThemes) ou **WPF-UI** (Fluent) — Cursor doit choisir l'un des deux et l'appliquer de façon cohérente sur tout le projet, avec un thème clair et un thème sombre commutables.
- Grilles de données : DataGrid avancé pour listes d'élèves, paiements, notes (tri, filtre, export Excel/PDF).
- Génération de documents (bulletins, reçus, certificats) : moteur de templates (ex. QuestPDF ou équivalent) permettant une mise en page professionnelle imprimable.
- Gestion hors-ligne partielle : cache local (SQLite ou fichier local chiffré) pour consultation en cas de coupure réseau, avec file d'attente de synchronisation vers l'API dès reconnexion.

### 1.2 Mobile : Flutter + Riverpod — recommandé plutôt que Provider

Recommandation : **Riverpod** plutôt que Provider, pour les raisons suivantes à documenter :
- Compile-time safety (erreurs détectées à la compilation plutôt qu'à l'exécution).
- Meilleure testabilité (providers indépendants du widget tree, faciles à mocker).
- Gestion native de l'asynchrone via `FutureProvider`/`AsyncNotifier`, pertinente pour une app fortement dépendante d'appels API.
- Évite les problèmes classiques de `BuildContext` de Provider dans une app à plusieurs rôles (parent/enseignant/direction) avec logique conditionnelle complexe.

Stack Mobile à générer :
- Flutter (dernière version stable), Dart avec null-safety strict.
- State management : **Riverpod** (`flutter_riverpod` + génération de code `riverpod_generator`).
- Architecture en couches côté Flutter : `presentation/ (pages, widgets)`, `application/ (providers, controllers)`, `domain/ (entities, use cases)`, `data/ (repositories, datasources, dto)`.
- Client HTTP : `dio` avec intercepteurs (ajout automatique du JWT, refresh token, logging, gestion centralisée des erreurs).
- Stockage sécurisé du token : `flutter_secure_storage`.
- Notifications push : Firebase Cloud Messaging pour notifications (paiements, absences, publication de bulletins).
- Gestion multi-rôles : un seul code base Flutter, avec un routage conditionnel selon le rôle de l'utilisateur connecté (parent / enseignant / direction), navigation via `go_router`.
- Mode hors-ligne léger : mise en cache des dernières données consultées (bulletins, horaires) via Hive ou stockage local, avec indicateur "dernière synchronisation".
- Design : Material 3, thème clair/sombre, composants adaptés au public (icônes claires, lisibilité pour utilisateurs peu familiers du numérique).

### 1.3 Backend : ASP.NET Core Web API + Clean Architecture

- .NET 8, ASP.NET Core Web API (minimal hosting model, controllers classiques pour la clarté des endpoints REST).
- Architecture en couches strictes, projets séparés dans la solution :
  - `SchoolManagement.Domain` : entités métier pures, value objects, enums, exceptions métier, aucune dépendance externe.
  - `SchoolManagement.Application` : use cases/services applicatifs, interfaces de repositories, DTO, validations (FluentValidation), interfaces de services (ex. `IPaymentService`, `IGradeCalculationService`), mapping (Mapster ou AutoMapper).
  - `SchoolManagement.Infrastructure` : implémentation EF Core, DbContext, repositories concrets, Unit of Work, migrations, services externes (envoi d'emails, notifications push, génération PDF), configuration Identity/JWT.
  - `SchoolManagement.API` : contrôleurs REST, middlewares, configuration Swagger/OpenAPI, versioning d'API, gestion des erreurs globales, CORS.
  - `SchoolManagement.Shared` : DTO partagés, constantes, enums communs pouvant être réutilisés (ou générés en équivalent Dart pour Flutter).
  - `SchoolManagement.Desktop` : application WPF consommant l'API.
  - (Flutter Mobile dans un dépôt/dossier séparé `mobile/`, car écosystème Dart indépendant de la solution .NET.)
- Pattern **Repository + Unit of Work** au-dessus d'EF Core (pas d'accès direct au DbContext depuis les services applicatifs).
- Respect strict de **SOLID** : interfaces pour chaque service/repository, injection de dépendances partout, responsabilité unique par classe.
- **DTO** systématiques en entrée/sortie d'API (jamais d'exposition directe des entités Domain).
- Authentification : **JWT** (access token courte durée + refresh token), avec claims incluant rôle, permissions, établissement (si architecture multi-écoles envisagée).
- Autorisation : policies ASP.NET Core basées sur permissions granulaires (pas seulement des rôles génériques), table de permissions en base.
- Versionning de l'API (`/api/v1/...`) pour permettre l'évolution sans casser le Mobile/Desktop déployés.
- Documentation API générée automatiquement (Swagger/OpenAPI) avec exemples de requêtes/réponses par module.
- Logging structuré (Serilog) + gestion centralisée des exceptions (middleware global renvoyant des réponses d'erreur normalisées).
- Tests unitaires (xUnit + Moq/NSubstitute + FluentAssertions) sur la couche Application au minimum, tests d'intégration sur les endpoints critiques (authentification, paiements, notes).

---

## 2. MODÈLE DE DONNÉES SQL SERVER

Demander à Cursor de produire un schéma SQL Server **normalisé en 3FN**, comprenant obligatoirement :

- Toutes les tables nécessaires aux modules listés en section 3, avec conventions de nommage cohérentes (PascalCase ou snake_case, à choisir et appliquer uniformément).
- Clés primaires (de préférence `GUID` ou `BIGINT IDENTITY` selon justification de performance/synchronisation), clés étrangères avec règles `ON DELETE`/`ON UPDATE` explicites et réfléchies (ex. `RESTRICT` sur les paiements, `CASCADE` maîtrisé sur les données de configuration).
- Contraintes `CHECK`, `UNIQUE`, `NOT NULL` cohérentes avec les règles métier (ex. un élève ne peut avoir deux inscriptions actives simultanées dans la même année scolaire).
- Champs d'audit standards sur toutes les tables métier : `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted` (soft delete) quand pertinent.
- Index adaptés aux requêtes fréquentes (recherche d'élève, listes de paiements par période, bulletins par classe).
- Vues SQL utiles pour les rapports (ex. `vw_SituationFinanciereEleve`, `vw_EffectifsParClasse`, `vw_MoyennesParClasse`).
- Procédures stockées pour les opérations sensibles ou complexes nécessitant transaction (ex. `sp_EnregistrerPaiement`, `sp_CalculerBulletin`, `sp_ClotureAnneeScolaire`).
- Fonctions SQL utilitaires (ex. calcul de moyenne pondérée, calcul de rang).
- Scripts de migration EF Core correspondants, générés et versionnés, avec script SQL brut équivalent fourni en annexe pour déploiement manuel si nécessaire.
- Un schéma de gestion des années scolaires permettant l'historisation complète (une inscription, des notes, des paiements rattachés à une année scolaire précise, sans jamais écraser les données des années précédentes).

Domaines de données à couvrir explicitement (non exhaustif, à compléter par Cursor selon les modules) :
- École, années scolaires, classes, sections, options, cours, périodes.
- Élèves, parents/tuteurs, documents, historique de scolarité (mutations, transferts, abandons, exclusions, archivage).
- Enseignants, attributions de cours, horaires, présences.
- Évaluations, notes, bulletins, décisions de classe.
- Types de frais, paiements, caisses, banques, journal de caisse, reçus.
- Utilisateurs, rôles, permissions, journal d'audit, journal de connexions.

---

## 3. MODULES FONCTIONNELS DÉTAILLÉS

Demander à Cursor de générer, pour **chaque module ci-dessous**, l'ensemble de la chaîne technique : entités Domain → interfaces Application → DTO → services → repositories → contrôleurs API → écrans Desktop (vue + ViewModel) → écrans Flutter le cas échéant → tests unitaires → documentation du module (fichier `README.md` par module expliquant les règles métier implémentées).

### 3.1 Paramétrage général
École (informations légales, logo, en-têtes de documents), années scolaires (avec activation d'une année courante), classes, sections, options, cours, périodes (trimestres/semestres selon configuration), types de frais, banques, caisses, utilisateurs, profils, permissions détaillées, configuration générale (devise par défaut, format des reçus, numérotation automatique des documents).

### 3.2 Gestion des élèves
Cycle de vie complet : pré-inscription, inscription, réinscription, mutation, transfert, abandon, exclusion, archivage. Gestion de la photo et des documents scannés (upload sécurisé, stockage organisé). Gestion des parents/tuteurs (lien multiple élève-tuteur, tuteur légal principal). Historique complet consultable par élève (toutes années confondues).

### 3.3 Gestion académique
Cours, enseignants, attribution des cours aux enseignants et classes, gestion des horaires (grille hebdomadaire), présences (élèves et enseignants), calendrier scolaire (jours fériés, périodes de vacances, examens), discipline (incidents, sanctions), récompenses/mentions.

### 3.4 Gestion des notes
Interrogations, examens, travaux pratiques, pondération configurable par type d'évaluation, calcul automatique des moyennes, classement (rang par classe), pourcentages, décision de conseil de classe, génération des bulletins (avec appréciation), impression, historique des résultats consultable sur plusieurs années.

### 3.5 Gestion financière
Types de frais configurables (frais d'inscription, minerval, frais annexes), paiements (complets, partiels, échelonnés), paiements multiples pour plusieurs enfants d'une même famille, annulation de paiement (avec justification et traçabilité), journal de caisse quotidien, gestion multi-caisses et multi-banques, rapports financiers (recettes par période, par type de frais, par classe), génération de reçus, historique des transactions, situation financière détaillée par élève, liste des débiteurs avec relances possibles.

### 3.6 Documents administratifs
Génération de certificats de scolarité, attestations, bulletins, cartes scolaires (avec code-barres/QR code pour vérification), reçus de paiement, palmarès de fin d'année, fiche individuelle de l'élève. Tous les documents doivent être générés en PDF, avec mise en page professionnelle et en-tête personnalisé de l'établissement.

### 3.7 Statistiques et tableaux de bord
Effectifs (par classe, section, sexe, évolution dans le temps), recettes (par période, comparaison entre années), taux de réussite, graphiques (courbes d'évolution, histogrammes), tableau de bord de direction avec indicateurs clés en temps réel.

### 3.8 Administration système
Gestion des utilisateurs et des rôles, permissions détaillées par module et par action (lecture/écriture/suppression/validation), journal d'audit complet (qui a fait quoi, quand), journal des connexions, sauvegarde et restauration de la base de données (interface dédiée dans le Desktop pour lancer une sauvegarde manuelle, en plus des sauvegardes planifiées côté serveur).

---

## 4. APPLICATION MOBILE FLUTTER — PÉRIMÈTRE FONCTIONNEL RÉDUIT ET CIBLÉ

Le Mobile ne doit **pas** reprendre l'intégralité des fonctionnalités du Desktop. Périmètre à générer, par rôle :

**Parent**
- Consultation des paiements et de la situation financière de chaque enfant.
- Consultation et téléchargement des reçus.
- Consultation des bulletins.
- Consultation des absences et retards.
- Consultation des sanctions/discipline.
- Consultation des communications de l'école (annonces, circulaires).
- Téléchargement des documents officiels (certificats, attestations).
- Réception de notifications push (nouveau paiement enregistré, nouveau bulletin disponible, absence signalée).

**Enseignant**
- Consultation de son horaire personnel.
- Consultation de la liste de ses classes et élèves.
- Encodage des présences en classe.
- Encodage des notes (interrogations, examens, travaux) selon les évaluations ouvertes par l'administration.
- Consultation de statistiques simples sur ses classes (moyenne de classe, taux de présence).

**Direction**
- Tableau de bord synthétique (effectifs, recettes du jour, taux de recouvrement).
- Recettes du jour en temps réel.
- Statistiques clés (effectifs, résultats).
- Validation mobile de certaines opérations sensibles (ex. validation d'une annulation de paiement initiée sur le Desktop).
- Réception de notifications importantes (anomalies, alertes de trésorerie, événements disciplinaires graves).

Le Mobile consomme exclusivement les endpoints de l'API sécurisée (JWT), avec gestion différenciée des permissions selon le rôle connecté.

---

## 5. SÉCURITÉ

- Authentification centralisée via JWT (émis par l'API), avec refresh token stocké de façon sécurisée côté Desktop et Mobile.
- Hachage des mots de passe avec un algorithme robuste (BCrypt ou Argon2), jamais de mot de passe en clair, jamais de hachage réversible.
- Gestion des rôles et permissions granulaires (table de permissions liée aux rôles, vérifiable côté API sur chaque endpoint sensible).
- Protection systématique contre les injections SQL (paramétrage EF Core, aucune concaténation de requêtes SQL brutes sans paramètres).
- Validation stricte des entrées (FluentValidation côté API, validation de formulaire côté Desktop et Flutter).
- Journal d'audit horodaté pour toute opération sensible (paiement, modification de note, suppression de dossier élève, changement de permission).
- Gestion des sessions avec expiration, révocation de token possible (déconnexion à distance en cas de compte compromis).
- Chiffrement des données sensibles au repos si applicable (ex. documents scannés stockés de façon sécurisée, accès restreint par permission).
- Limitation du taux de requêtes (rate limiting) sur les endpoints sensibles (authentification notamment) pour limiter les tentatives de force brute.

---

## 6. STRUCTURE DE LA SOLUTION À GÉNÉRER

```
SchoolManagement/
├── src/
│   ├── SchoolManagement.Domain/
│   ├── SchoolManagement.Application/
│   ├── SchoolManagement.Infrastructure/
│   ├── SchoolManagement.API/
│   ├── SchoolManagement.Desktop/
│   └── SchoolManagement.Shared/
├── mobile/
│   └── school_management_mobile/   (projet Flutter complet)
├── database/
│   ├── scripts/                    (scripts SQL bruts)
│   └── migrations/                 (migrations EF Core)
├── tests/
│   ├── SchoolManagement.UnitTests/
│   └── SchoolManagement.IntegrationTests/
└── docs/
    ├── architecture.md
    ├── modules/                    (un README par module métier)
    └── api/                        (documentation OpenAPI exportée)
```

---

## 7. UI/UX

- Design moderne inspiré des ERP professionnels existants (type ERP scolaires internationaux), pas d'interface "amateur" avec composants par défaut non stylés.
- Responsive côté Desktop (redimensionnement fluide des grilles et formulaires) et côté Mobile (adaptation aux différentes tailles d'écran de smartphones).
- Mode clair et mode sombre disponibles et basculables à chaud, sur Desktop comme sur Mobile.
- Navigation simple et intuitive : menu latéral par module sur le Desktop, navigation par onglets/bottom navigation sur le Mobile selon le rôle connecté.
- Iconographie moderne et cohérente (une seule bibliothèque d'icônes utilisée sur tout le projet).
- Attention particulière à la lisibilité et à la simplicité pour des utilisateurs finaux (personnel administratif, parents) pouvant avoir une familiarité limitée avec les outils numériques.

---

## 8. LIVRABLES ATTENDUS DE CURSOR

Demander explicitement à Cursor de produire, dans cet ordre :

1. L'architecture complète de la solution (arborescence de dossiers/projets telle que définie en section 6).
2. Le script complet de la base de données SQL Server (tables, clés, contraintes, index, vues, procédures stockées, fonctions), ainsi que les migrations EF Core correspondantes.
3. Les entités Domain et interfaces Application pour chaque module.
4. Les DTO, services applicatifs, validations.
5. Les repositories et l'Unit of Work (Infrastructure).
6. Les contrôleurs API REST documentés (Swagger), avec authentification JWT et gestion des permissions.
7. L'application Desktop WPF (vues XAML + ViewModels MVVM) pour chaque module.
8. L'application Flutter avec le périmètre fonctionnel défini en section 4.
9. Les tests unitaires (Application) et tests d'intégration (API) pour les modules critiques (authentification, paiements, notes, bulletins).
10. La documentation : un `README.md` global expliquant les choix d'architecture (WPF vs WinUI3, Riverpod vs Provider, Clean Architecture, Repository/Unit of Work), et un `README.md` par module métier expliquant les règles métier implémentées.

Le projet final doit constituer un ERP scolaire professionnel, robuste, sécurisé, conçu pour évoluer pendant plusieurs années sans nécessiter de refonte majeure de l'architecture.

---

## 9. MÉTHODE DE TRAVAIL RECOMMANDÉE POUR CURSOR

Pour éviter un contexte trop lourd en une seule génération, procéder par étapes successives, chaque étape devant être validée avant de passer à la suivante :

1. Scaffolding de la solution complète (dossiers, projets vides, fichiers de configuration).
2. Base de données complète (scripts SQL + migrations).
3. Couche Domain + Application (entités, interfaces, DTO) pour tous les modules.
4. Couche Infrastructure (repositories, Unit of Work, EF Core, JWT, services externes).
5. Couche API (contrôleurs, sécurité, Swagger).
6. Application Desktop, module par module dans l'ordre : Paramétrage → Élèves → Académique → Notes → Financier → Documents → Statistiques → Administration.
7. Application Flutter, rôle par rôle : Parent → Enseignant → Direction.
8. Tests unitaires et d'intégration.
9. Documentation finale.
