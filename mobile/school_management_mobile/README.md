# ERP Administration Scolaire RDC — Mobile

Application Flutter pour parents, enseignants, direction, promoteur et secrétariat.

## Modes de connexion (automatiques, sans câble USB)

L'utilisateur **ne choisit jamais** le serveur. Au démarrage, au changement Wi‑Fi/4G,
et périodiquement :

1. Même Wi‑Fi que le PC serveur (API locale joignable) → **Mode Local** (lecture + écriture)
2. Autre réseau (4G / autre Wi‑Fi) → **Mode Distant** (API publique, lecture seule + notes)
3. Aucune connexion / serveurs down → **Mode Cache** (données déjà téléchargées)

| Indicateur | Signification |
|------------|---------------|
| 🟢 Mode Local | Même Wi‑Fi que le serveur établissement |
| 🔵 Mode Distant | Autre réseau — VPS / Cloud public |
| 🔴 Mode Cache | Hors ligne — consultation du cache local |

**USB** sert uniquement à installer / debugger l'APK. La connexion API passe
par le Wi‑Fi (local) ou Internet (distant) — pas par `adb reverse`.

Par défaut : `CLOUD_API_BASE_URL=http://169.58.93.203:1804`.

La base locale reste la **Source of Truth**. Le Cloud ne reçoit que la sync **Local → Cloud**.

## Stack

- **Flutter** (Material 3)
- **Riverpod**
- **go_router**
- **dio** (JWT)
- **flutter_secure_storage**

## URL API

```bash
# Émulateur Android → API locale PC
flutter run

# Appareil physique (Wi‑Fi école) + cloud de secours
flutter run \
  --dart-define=LOCAL_API_BASE_URL=http://192.168.1.10:5041 \
  --dart-define=CLOUD_API_BASE_URL=https://api.votredomaine.com
```

Rétrocompatibilité : `API_BASE_URL` force l'URL locale.

## Instance API Cloud (.NET)

Sur le serveur cloud, dans `appsettings.json` (ou variables d'environnement) :

```json
"Deployment": {
  "Role": "Cloud",
  "ReadOnly": true
}
```

Le middleware refuse alors POST/PUT/PATCH/DELETE (sauf `/api/v1/auth`, `/api/v1/health`, `/api/v1/grades/entries`).

## Comptes démo

| Rôle | Identifiant | Mot de passe |
|------|-------------|--------------|
| Parent | `parent` | `Parent@2026` |
| Enseignant | `enseignant` | `Teacher@2026` |
| Direction | `direction` | `Direction@2026` |
| Admin | `admin` | `Admin@2026` |

## Démarrage

```bash
cd mobile/school_management_mobile
flutter pub get
flutter run
```
