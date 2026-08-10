# Spécification technique — QR établissement, registre Bootstrap SQL, SchoolBinding multi-écoles

**Statut :** SPÉCIFICATION — validation produit requise avant toute implémentation  
**Date :** 2026-08-10  
**Bootstrap mobile labo :** `https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io`  
**Références :** analyse QR établissement validée ; architecture v2.0.1 (SchoolBinding, DeviceId, I13)

---

## 0. Règles produit gelées (rappel)

1. QR établissement obligatoire pour **tout** utilisateur mobile (parent, enseignant, direction, secrétaire, etc.).
2. QR = liaison **ÉTABLISSEMENT ↔ TÉLÉPHONE** uniquement.
3. Login **après** `SchoolBinding`.
4. Multi-établissements : nouveau scan = **ajout**, jamais purge des autres.
5. Modèle mobile conservé : `RegisteredSchoolsStore` → `ActiveSchoolId` → `SchoolBinding` actif.
6. `ParentActivationToken` ≠ QR établissement ; **interdit** pour établir le binding d’établissement.
7. Création école → SchoolId + credential + registre Bootstrap + QR + affichage/impression.
8. QR stable par défaut ; régénération = révocation ancien + nouveau credential.
9. Registre = **SQL dédiée Bootstrap** (pas d’env Coolify `Schools__N__*`).
10. Mobile reste sur l’URL Bootstrap Coolify actuelle (sslip.io `gopvetrs…`).

---

## A. Modèle de données

### A.1 Base SQL dédiée Bootstrap

Nom proposé : **`SchoolManagementBootstrap`** (instance SQL Coolify distincte de `SchoolManagementRDC_Production`).

Variables Coolify Bootstrap (uniquement infra) :

```env
PORT=1805
ASPNETCORE_URLS=http://0.0.0.0:1805
ASPNETCORE_ENVIRONMENT=Production
Bootstrap__RelayApiKey=...
Bootstrap__ConnectionString=Server=...;Database=SchoolManagementBootstrap;...
# Plus AUCUN Bootstrap__Schools__*
```

### A.2 Table `BootstrapSchoolRegistry`

Registre opérationnel (routage + métadonnées publiques pour binding).

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | Surrogate |
| `SchoolId` | `uniqueidentifier` | **UQ** NOT NULL | GUID métier école (SQL école) |
| `SchoolName` | `nvarchar(200)` | NOT NULL | Affichage |
| `ActivationBaseUrl` | `nvarchar(500)` | NOT NULL | Base URL API école (relay / validation établissement) |
| `CloudBaseUrl` | `nvarchar(500)` | NOT NULL | URL cloud pour `SchoolBinding` |
| `PublicKeyFingerprint` | `nvarchar(128)` | NULL | Aligné health |
| `KeyVersion` | `int` | NULL | Aligné health |
| `ServerInstanceId` | `uniqueidentifier` | NULL | Dernier connu (ops) |
| `LicenseId` | `uniqueidentifier` | NULL | Optionnel |
| `IsActive` | `bit` | NOT NULL DEFAULT 1 | École autorisée sur Bootstrap |
| `RegisteredAtUtc` | `datetime2` | NOT NULL | Première inscription |
| `UpdatedAtUtc` | `datetime2` | NOT NULL | Dernière MAJ |
| `RowVersion` | `rowversion` | | Concurrence |

**Index :**

- `UX_BootstrapSchoolRegistry_SchoolId` UNIQUE (`SchoolId`)
- `IX_BootstrapSchoolRegistry_IsActive` (`IsActive`) INCLUDE (`SchoolId`, `ActivationBaseUrl`)

### A.3 Table `BootstrapSchoolEstablishmentCredential`

Credential d’établissement (matériel de confiance du QR). **Jamais** stocker le secret en clair.

| Colonne | Type | Contraintes | Description |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | = `jti` / credential id exposé dans le JWT |
| `SchoolId` | `uniqueidentifier` | NOT NULL FK logique → Registry.SchoolId | |
| `CredentialVersion` | `int` | NOT NULL | 1, 2, 3… (rotation) |
| `TokenType` | `nvarchar(64)` | NOT NULL | constante `school_establishment` |
| `SecretHash` | `nvarchar(128)` | NOT NULL | SHA-256 (hex) du secret ou du JWT signing material selon mode |
| `Status` | `nvarchar(32)` | NOT NULL | `Active` \| `Revoked` |
| `CreatedAtUtc` | `datetime2` | NOT NULL | |
| `RevokedAtUtc` | `datetime2` | NULL | |
| `RevokedReason` | `nvarchar(500)` | NULL | |
| `CreatedBy` | `nvarchar(128)` | NULL | ops / schoolId / user |

**Contraintes / index :**

- `UX_EstablishmentCredential_Active` : **au plus un** `Status=Active` par `SchoolId`  
  (filtered unique index SQL Server : `WHERE Status = 'Active'`)
- `IX_EstablishmentCredential_SchoolId_Version` UNIQUE (`SchoolId`, `CredentialVersion`)
- `IX_EstablishmentCredential_Id` = PK (lookup JWT `jti`)

### A.4 Table `BootstrapEstablishmentSession` (éphémère)

Sessions corrélation start→complete (équivalent sessions actuelles, **sans** stocker le token JWT brut).

| Colonne | Type | Description |
|---------|------|-------------|
| `Id` | `uniqueidentifier` PK | Session renvoyée au mobile |
| `SchoolId` | `uniqueidentifier` | |
| `CredentialId` | `uniqueidentifier` | Credential utilisé |
| `DeviceId` | `nvarchar(128)` | |
| `Status` | `nvarchar(32)` | `Pending` / `Completed` / `Expired` |
| `CreatedAtUtc` | `datetime2` | |
| `ExpiresAtUtc` | `datetime2` | TTL court (ex. 15 min) |
| `CompletedAtUtc` | `datetime2` NULL | |

**Index :** PK ; `IX_Session_Device_Status` (`DeviceId`, `Status`).

### A.5 Côté API école (SQL école — tables à ajouter)

| Table | Rôle |
|-------|------|
| `SchoolEstablishmentCredentials` | Miroir local du credential actif (hash + version) pour affichage QR / rotation offline-capable |
| (optionnel) `SchoolEstablishmentAudit` | Qui a régénéré / imprimé |

**Ne pas modifier** le schéma `ParentActivationTokens` / `ParentActivationSessions` pour le QR établissement.

### A.6 Entités domaine (noms proposés)

**Bootstrap :**

- `BootstrapSchoolRegistryEntry`
- `BootstrapSchoolEstablishmentCredential`
- `BootstrapEstablishmentSession`

**École (Application/Domain) :**

- `SchoolEstablishmentCredential` (entité locale)
- DTOs : `IssueSchoolEstablishmentQrResponse`, `RegisterSchoolWithBootstrapRequest`, etc.

**Mobile :**

- `SchoolBinding` **conservé** (champs existants) ; sémantique :
  - `activationTokenId` → id du **credential établissement** (ou renommer plus tard en `establishmentCredentialId` via `extensions` / champ dédié en v2 contrat — **recommandation spec :** ajouter `establishmentCredentialId` et garder `activationTokenId` alias pour compat lecture)
  - `activationSessionId` → id session établissement Bootstrap
  - `suggestedUserName` reste nullable (souvent null pour QR établissement)

### A.7 Relations (vue)

```text
BootstrapSchoolRegistry (1) ──< (N) BootstrapSchoolEstablishmentCredential
BootstrapSchoolRegistry (1) ──< (N) BootstrapEstablishmentSession
Credential (1) ──< (N) Sessions (historiques)
```

---

## B. Contrat du QR établissement

### B.1 Format

- **Payload QR** = deep link **ou** JWT compact (comme aujourd’hui).
- Deep link recommandé :

```text
erp-scolaire://establish?token=<JWT>
```

- QR encode soit le deep link entier, soit le JWT seul (mobile accepte les deux, comme activation parent).

### B.2 JWT — claims obligatoires

| Claim | Valeur |
|-------|--------|
| `token_type` | **`school_establishment`** (constante stricte) |
| `school_id` | GUID école (`D`) |
| `jti` | = `Credential.Id` |
| `ver` | `CredentialVersion` (int) |
| `iss` | `https://bootstrap.erp-scolaire.com` **ou** issuer école documenté (fixifié : `school:{schoolId}`) |
| `aud` | `erp-scolaire-mobile-establish` |
| `iat` | émission |
| `exp` | voir B.3 |

Signature : HMAC ou asymétrique — **recommandation labo :** HMAC avec secret dérivé du credential material stocké hashé côté Bootstrap + école (détail implémentation en phase code). Alternative : JWT signé par clé école + vérification via fingerprint registre.

### B.3 Durée de validité

| Aspect | Règle |
|--------|--------|
| Credential **stable** | Reste `Active` jusqu’à révocation admin |
| JWT dans le QR | Peut être **longue durée** (ex. 10 ans) **ou** « sans exp métier » tant que `jti` est Active en BD — **recommandation :** `exp` long (ex. 3650 jours) + **révocation BD** fait foi |
| Session Bootstrap start→complete | Courte (10–15 min) |

Différence claire avec ParentActivationToken : TTL parent typiquement **≤ 120 min**, consommable une fois.

### B.4 Différences QR établissement vs ParentActivationToken

| | QR établissement | ParentActivationToken |
|--|------------------|------------------------|
| `token_type` | `school_establishment` | `parent_activation` (existant) |
| But | SchoolBinding téléphone↔école | Invitation / suggestion compte parent |
| Stabilité | Stable + rotation admin | Éphémère |
| Consommation | Non « one-shot » pour lier un device (N devices OK tant que credential Active) | One-shot / session parent |
| Établit SchoolBinding ? | **Oui (seul autorisé)** | **Non** |
| Qui émet | Setup + admin établissement | Staff `ParentActivationManage` |

### B.5 Interdit dans le QR

- Mot de passe / hash utilisateur  
- Connection string / secrets SQL  
- `Bootstrap__RelayApiKey`  
- Liste d’élèves / données métier  
- URL admin Coolify  
- Clé privée serveur  
- Métadonnées marketing inutiles (garder token minimal)

---

## C. API Bootstrap

Base URL labo : `https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io`

### C.1 Endpoints établissement (nouveaux)

| Méthode | Route | Auth | Description |
|---------|-------|------|-------------|
| `POST` | `/establishment/start` | Anonyme + rate limit | Démarre liaison téléphone↔école |
| `POST` | `/establishment/complete` | Anonyme + session | Finalise → `SchoolBinding` |
| `POST` | `/registry/schools/upsert` | **Service** (`X-Bootstrap-Relay-Key` ou futur JWT service) | Enregistre / met à jour école + credential |
| `POST` | `/registry/schools/{schoolId}/credentials/rotate` | Service | Révoque actif + crée nouveau |
| `GET` | `/health` | Public | Inchangé |

Les routes parent actuelles `/activation/start|complete` **restent** pour ParentActivation **uniquement** (si conservées) et **doivent refuser** `token_type=school_establishment` pour le binding (et inversement).

### C.2 `POST /establishment/start`

**Request :**

```json
{
  "token": "<JWT school_establishment>",
  "deviceId": "<DeviceId installation>",
  "clientHints": { }
}
```

**Validation Bootstrap :**

1. JWT lisible ; `token_type == school_establishment`.
2. `school_id` + `jti` + `ver` présents.
3. Lookup `BootstrapSchoolRegistry` par `SchoolId` ; `IsActive=true`.
4. Lookup credential `jti` ; `Status=Active` ; `CredentialVersion == ver`.
5. Vérifier signature / hash selon politique.
6. Créer `BootstrapEstablishmentSession` Pending.

**Response 200 :**

```json
{
  "establishmentSessionId": "...",
  "schoolId": "...",
  "deviceId": "...",
  "status": "pending",
  "expiresAt": "..."
}
```

**Erreurs 400/401/403/404 (corps `{ "error": "..." }`) :**

| Cas | Message type |
|-----|----------------|
| Mauvais `token_type` | `Token non valide pour l'établissement (type incorrect).` |
| École inconnue | `École introuvable dans le registre Bootstrap.` |
| Credential révoqué | `QR établissement révoqué. Demandez un nouveau QR à l'école.` |
| Version mismatch | `Version de credential invalide.` |
| Signature | `Token établissement invalide.` |

### C.3 `POST /establishment/complete`

**Request :**

```json
{
  "establishmentSessionId": "...",
  "deviceId": "..."
}
```

**Validation :** session Pending, même `deviceId`, non expirée ; registry active.

**Response 200 — `SchoolBinding` :** (aligné DTO actuel)

```json
{
  "schoolId": "...",
  "schoolName": "...",
  "cloudBaseUrl": "...",
  "serverInstanceId": "...",
  "licenseId": null,
  "activationDate": "...",
  "activationTokenId": "<credentialId>",
  "activationSessionId": "<establishmentSessionId>",
  "deviceId": "...",
  "protocolVersion": 2,
  "suggestedUserName": null,
  "expiresAt": null,
  "extensions": {
    "bindingKind": "school_establishment",
    "establishmentCredentialVersion": 1
  }
}
```

`cloudBaseUrl` / `schoolName` / `serverInstanceId` / `licenseId` : issus du **registre** (+ optionnellement refresh health école si joignable).

### C.4 Authentification école → Bootstrap (registre)

Header existant : `X-Bootstrap-Relay-Key: <Bootstrap__RelayApiKey>`  
(évolutif JWT service — hors scope immédiat, interfaces déjà prévues TD-RELAY).

**Upsert body (exemple) :**

```json
{
  "schoolId": "...",
  "schoolName": "...",
  "activationBaseUrl": "https://...",
  "cloudBaseUrl": "http://169.58.93.203:1804",
  "publicKeyFingerprint": "sha256:...",
  "keyVersion": 1,
  "serverInstanceId": "...",
  "licenseId": null,
  "credential": {
    "credentialId": "...",
    "credentialVersion": 1,
    "secretHash": "...",
    "tokenType": "school_establishment"
  }
}
```

### C.5 Comment Bootstrap retrouve l’établissement

```text
JWT.school_id  →  BootstrapSchoolRegistry.SchoolId
JWT.jti        →  BootstrapSchoolEstablishmentCredential.Id (Active)
```

Pas de liste env ; pas de scan réseau.

---

## D. Flux de création d’une école

```text
Setup / InitialSetupService.CompleteAsync
  → INSERT School (SchoolId = Guid.NewGuid())
  → Générer credentialId + secret + CredentialVersion=1
  → Persister hash local (SchoolEstablishmentCredentials)
  → POST Bootstrap /registry/schools/upsert  (RelayApiKey)
       → INSERT/UPDATE Registry
       → INSERT Credential Active
  → Émettre JWT school_establishment + deep link
  → Desktop / Setup : écran « QR établissement » (afficher / imprimer / copier)
  → Refresh ServerIdentity (schoolId health)
```

**Échec upsert Bootstrap :** Setup doit signaler clairement (école créée localement mais non joignable mobile tant que registre KO) — politique : **bloquant** recommandé pour prod ; **retry** admin.

**URLs à publier :**

- `ActivationBaseUrl` : URL joignable depuis Bootstrap pour ops futures (health) — labo peut être Cloud ou tunnel.
- `CloudBaseUrl` : URL parent distant (ex. `http://169.58.93.203:1804`).

---

## E. Flux mobile

### E.1 Première installation

```text
Install app
  → RegisteredSchoolsStore vide
  → Gate : shouldRequireEstablishmentQr() == true
  → Écran « Rejoindre un établissement » (remplace sémantique ParentActivationScreen pour ce gate)
  → Scan QR / colle token
  → POST Bootstrap /establishment/start
  → POST Bootstrap /establishment/complete
  → SchoolBindingRepository.addSchool(binding, setAsActive: true)
       → RegisteredSchoolsStore écrit registry + ActiveSchoolId
  → Navigation /login
  → Utilisateur s’authentifie (parent OU enseignant OU direction…)
  → Droits selon JWT métier
```

**Interdit :** atteindre un home métier sans binding.

### E.2 Ajouter une autre école

```text
Menu « Mes établissements » → Ajouter
  → Même écran establish
  → Scan nouveau QR (autre SchoolId)
  → addSchool(...)
  → Si SchoolId déjà présent : SchoolAlreadyRegisteredException (comportement actuel à conserver)
  → Sinon : AJOUT dans registry ; setAsActive selon UX (recommandé : proposer bascule)
  → Aucune suppression des autres entrées
```

### E.3 Bootstrap URL mobile

Build / run avec :

`--dart-define=BOOTSTRAP_API_BASE_URL=https://gopvetrs5vjo1v6z0fdh57ty.169.58.93.203.sslip.io`

Ne pas utiliser `bootstrap.169.58.93.203.sslip.io`.

---

## F. Flux de changement d’école (ActiveSchoolId)

```text
RegisteredSchoolsScreen
  → setActive(schoolId)
  → ActiveSchoolId mis à jour
  → SchoolBinding actif = registry[schoolId]
  → onActiveSchoolSwitched (push/cache) SANS purge des autres bindings
  → Re-discovery / session scoped à la nouvelle école
```

Logout utilisateur ≠ remove school.  
Remove school = action explicite (purge scoped données de **cette** école seulement).

---

## G. Flux de régénération QR

```text
Admin Desktop → « Régénérer QR établissement »
  → POST école interne rotate
  → POST Bootstrap /registry/schools/{id}/credentials/rotate
       → Status ancien = Revoked
       → Nouveau CredentialVersion = N+1, Status=Active
  → Nouveau JWT + QR affiché / imprimable
```

| Acteur | Comportement |
|--------|----------------|
| Téléphones **déjà liés** | Conservent leur `SchoolBinding` local ; **pas** d’invalidation forcée du binding (liaison déjà établie). Option future : force re-establish via flag registre — **hors v1 spec** (documenter comme non-fait). |
| **Nouvelles** installs / ajout école | Doivent scanner le **nouveau** QR ; ancien JWT → erreur « révoqué ». |
| Ancien QR imprimé | Inutilisable après rotation. |

---

## H. ParentActivationToken — séparation

### H.1 Ce qui reste

- Tables `ParentActivationTokens` / Sessions  
- `ParentActivationService` issue/start/complete  
- Desktop « Invitation parent » (permission existante)  
- Routes école `/api/v1/parent/activation/issue` et `/api/v1/activation/*` **si** encore utilisées pour parcours parent  
- Bootstrap `/activation/start|complete` **uniquement** pour `token_type=parent_activation`  
- Deep link `erp-scolaire://activate?token=...` (parent)

### H.2 Ce qui change

- **Ne plus** utiliser ce flux pour premier lancement / gate établissement  
- Mobile : gate global → `/establish` (nouveau) pas `/parent/activate`  
- Bootstrap : rejet croisé des token_types  
- Docs / UI : libellés distincts (« QR établissement » vs « Invitation parent »)

### H.3 Ordre parent type

```text
QR établissement → SchoolBinding → Login parent
(optionnel) Invitation parent → suggestion username / onboarding — jamais le binding école
```

---

## I. Migration Coolify / ECOLE TEST

### I.1 Étapes ops

1. Créer DB `SchoolManagementBootstrap` + appliquer migrations EF Bootstrap.  
2. Seed / script one-shot : lire anciens env `Bootstrap__Schools__0__*` + credential généré pour ECOLE TEST (`SchoolId=71635f62-b975-479d-9e6e-fbacd05e4996`).  
3. Upsert registry + credential Active.  
4. Générer QR établissement Desktop pour ECOLE TEST ; valider `/establishment/start` sur sslip.io actuel.  
5. Retirer de Coolify : `Bootstrap__Schools__0__SchoolId`, `ActivationBaseUrl`, `CloudBaseUrl`.  
6. Redeploy Bootstrap (code + connection string uniquement).  
7. Mobile : rebuild avec même URL `gopvetrs…` + nouveaux endpoints.  
8. Appareils déjà liés via **ancien** ParentActivation binding :  
   - **Stratégie recommandée labo :** conserver bindings existants (même forme JSON) ; nouveaux ajouts via establish.  
   - Si binding absent / cassé : rescan QR établissement.

### I.2 Compatibilité temporaire (si besoin)

Feature flag Bootstrap : `Bootstrap:AllowLegacyEnvSchoolRegistry=true` **une release** — lecture env si BD vide. Puis suppression.  
**Recommandation :** éviter si possible ; migration one-shot + cutover.

---

## J. Sécurité

| Sujet | Règle |
|-------|--------|
| Stockage secret | **Hash only** en BD Bootstrap et école ; JWT signé ; pas de secret en logs |
| Rotation | Filtered unique Active ; ancien Revoked immédiat |
| Révocation | Status + message mobile clair |
| Autre école | Binding filtré par `schoolId` ; discovery STRICT compare health |
| Mélange bindings | `RegisteredSchoolsStore` clé = `schoolId` ; switch ne purge pas |
| DeviceId | Conservé (I9) : envoyé start/complete ; stocké dans binding |
| Relay registre | `X-Bootstrap-Relay-Key` ; rate limit establish anonyme |
| Type confusion | Refuse parent token sur `/establishment/*` et inverse |

---

## K. Compatibilité — modules impactés

| Module | Impact |
|--------|--------|
| **Setup** | Après create school : credential + upsert Bootstrap + QR |
| **API école** | Nouveaux endpoints establishment credential / rotate / QR payload ; config `Bootstrap:RegistryBaseUrl` |
| **Bootstrap** | EF + SQL ; SchoolRegistry depuis BD ; endpoints `/establishment/*` + `/registry/*` ; déprécier options Schools[] |
| **Desktop** | Écran QR établissement (print) ; garder invitation parent séparée |
| **Mobile** | Gate tous rôles ; écran establish ; `addSchool` inchangé sémantiquement ; URL Bootstrap gopvetrs |
| **SchoolBinding** | Conservé ; `extensions.bindingKind` |
| **RegisteredSchoolsStore** | Inchangé (déjà multi) |
| **ParentActivation** | Recentré ; plus de gate premier lancement |
| **Coolify** | ConnectionString Bootstrap DB ; supprimer Schools__* ; garder RelayApiKey |

---

# Livrables de clôture (demandés)

## 1. Schéma final d’architecture

```text
[Setup/Desktop] --create school--> [SQL École]
        |                              |
        | credential+URLs              | ParentActivation* (optionnel, séparé)
        v                              v
[Bootstrap API + SQL Bootstrap Registry]
        ^
        | establish start/complete
[Mobile] --QR établissement--> SchoolBinding --> RegisteredSchoolsStore
        |
        v
     /login (tous rôles) --> permissions
```

## 2. Liste exacte des tables

**Bootstrap DB :**

1. `BootstrapSchoolRegistry`  
2. `BootstrapSchoolEstablishmentCredentials`  
3. `BootstrapEstablishmentSessions`  

**École DB (ajout) :**

4. `SchoolEstablishmentCredentials`  
5. (optionnel) `SchoolEstablishmentAudits`  

**Inchangé :** `ParentActivationTokens`, `ParentActivationSessions`, `Schools`, …

## 3. Liste exacte des endpoints

**Bootstrap :**

- `POST /establishment/start`  
- `POST /establishment/complete`  
- `POST /registry/schools/upsert`  
- `POST /registry/schools/{schoolId}/credentials/rotate`  
- `GET /health`  

**API école (nouveaux) :**

- `POST /api/v1/school/establishment/qr` (get/affiche courant — auth admin)  
- `POST /api/v1/school/establishment/rotate` (auth admin)  
- (interne) appel client vers Bootstrap upsert/rotate  

**Conservés (parent, séparés) :**

- `POST /api/v1/parent/activation/issue`  
- `POST /api/v1/activation/start|complete` (parent token only)  
- Bootstrap `POST /activation/start|complete` (parent token only)

## 4. Fichiers / zones à modifier (indicatif)

- `src/SchoolManagement.Bootstrap.API/**` (Program, Options, SchoolRegistry, nouveaux controllers/services, EF)  
- `src/SchoolManagement.Infrastructure/Setup/InitialSetupService.cs`  
- `src/SchoolManagement.Infrastructure/ParentActivation/**` (garde-fous type)  
- Nouveaux services `SchoolEstablishment/**`  
- `src/SchoolManagement.Desktop/**` (QR établissement UI)  
- `mobile/.../school_binding_gate.dart`, `app_router.dart`, nouvel écran establish, `bootstrap_api_client.dart`  
- `coolify.bootstrap.env.example`, docs exploitation  
- Tests unitaires / foundations mobile

## 5. Migrations nécessaires

1. Migration EF **Bootstrap** : create 3 tables.  
2. Migration EF **école** : `SchoolEstablishmentCredentials` (+ audit optionnel).  
3. Script ops : seed ECOLE TEST depuis ancien env.  
4. Coolify : ajouter `Bootstrap__ConnectionString` ; supprimer `Bootstrap__Schools__*`.

## 6. Risques de régression

| Risque | Mitigation |
|--------|------------|
| Mobiles encore sur flux parent pour premier lancement | Gate + nouvel écran ; communication rebuild |
| Bindings labo créés via ParentActivation | Accepter legacy bindingKind ; nouveaux = establishment |
| Bootstrap down | Pas de fallback discovery pour establish (I10) |
| Upsert Setup échoue | Erreur visible + retry admin |
| Confusion QR imprimés | Libellés Desktop distincts ; rotation invalide anciens |
| Multi-école add écrase | Réutiliser `addSchool` existant + tests registry |
| JWT secret / relay | Ne pas mélanger avec ParentActivation |

## 7. Ordre recommandé d’implémentation

1. **Bootstrap DB + EF + Registry repository** (lecture/écriture)  
2. **Endpoints `/registry/schools/upsert` + rotate** + tests  
3. **Endpoints `/establishment/start|complete`** + tests  
4. **API école : credential local + client upsert** branché Setup  
5. **Desktop : écran QR établissement**  
6. **Mobile : gate universel + écran establish + client API** (URL gopvetrs)  
7. **Séparer / verrouiller ParentActivation** (token_type)  
8. **Migration ECOLE TEST + retrait env Schools__***  
9. **Docs + tests multi-école add/switch**  
10. **Validation bout-en-bout** (install neuve → QR → login enseignant/parent)

---

**Fin de spécification.**  
**Aucune implémentation tant que cette spec n’est pas validée explicitement.**
