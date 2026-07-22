# ERP Administration Scolaire RDC — Mobile

Application Flutter pour parents, enseignants, direction, promoteur et secrétariat.

## Modes de connexion (automatiques)

L'utilisateur **ne choisit jamais** le serveur. Au démarrage (et périodiquement) :

1. Test de l'**API Locale** → **Mode Local** (lecture + écriture)
2. Sinon test de l'**API Cloud** → **Mode Cloud** (lecture seule ; notes enseignants autorisées)
3. Sinon → **Hors ligne**

| Indicateur | Signification |
|------------|---------------|
| 🟢 Mode Local | Serveur de l'établissement — toutes opérations selon rôles |
| 🔵 Mode Cloud | Copie synchronisée — consultation (+ notes si autorisé) |
| 🔴 Hors ligne / Serveur inaccessible | Aucun serveur joignable |

**Important :** la 4G ne remplace pas le Wi‑Fi de l'école. L'IP locale (`192.168.x.x`)
n'est pas accessible via Internet mobile. Pour consulter hors établissement, il faut
configurer `CLOUD_API_BASE_URL` (API Cloud publique).

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
