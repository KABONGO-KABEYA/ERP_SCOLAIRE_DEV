# ERP Administration Scolaire RDC — Mobile

Application Flutter pour parents, enseignants et direction.

## Stack

- **Flutter** (Material 3)
- **Riverpod** (state management — choisi pour la compile-time safety et la testabilité)
- **go_router** (navigation multi-rôles)
- **dio** (client HTTP avec intercepteurs JWT)
- **flutter_secure_storage** (tokens)
- **Hive** (cache hors-ligne léger)

## Structure

```
lib/
├── core/           # API client, auth storage, config
├── features/
│   ├── auth/       # login
│   ├── parent/     # enfants, paiements, bulletins
│   ├── teacher/    # affectations, liste de classe, saisie notes
│   └── direction/  # tableau de bord KPIs et rapports
└── router/         # go_router
```

## Comptes démo

| Rôle | Identifiant | Mot de passe |
|------|-------------|--------------|
| Parent | `parent` | `Parent@2026` |
| Enseignant | `enseignant` | `Teacher@2026` |
| Direction | `direction` | `Direction@2026` |

La redirection après connexion dépend du rôle (`DIRECTION` → tableau de bord, `ENSEIGNANT` → cours, `PARENT` → enfants).

## URL API

Par défaut : `https://10.0.2.2:7060` (émulateur Android → localhost).

Pour changer :

```bash
flutter run --dart-define=API_BASE_URL=https://localhost:7060
```

## Prérequis

- Flutter SDK stable installé et dans le PATH
- Android Studio / Xcode selon la plateforme cible

## Démarrage

```bash
cd mobile/school_management_mobile
flutter pub get
flutter run
```

> **Note** : le projet a été scaffoldé manuellement. Exécuter `flutter create .` dans ce dossier si des fichiers plateforme manquent.

## Périmètre par rôle

Voir le prompt maître section 4 : Parent, Enseignant, Direction.
