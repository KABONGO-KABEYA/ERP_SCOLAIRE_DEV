# Inventaire Feature Flags — connexion v2.0.1

**Date :** 2026-08-04 · **Étape 8**

Flags listés = **pilotage déploiement / migration** uniquement (pas de flags métier ERP).

---

## 1. Mobile (compile-time `--dart-define`)

| Flag | Défaut | Fichier | Rôle |
|------|--------|---------|------|
| `STRICT_SCHOOL_DISCOVERY` | `false` | `binding_migration_config.dart` | Discovery filtrée par `SchoolBinding`, cloud = `binding.cloudBaseUrl`, partition cache/push |
| `ALLOW_JWT_BINDING_MIGRATION` | `true` | idem | Autorise login parent sans binding (fenêtre migration) |
| `JWT_BINDING_MIGRATION_END_UTC` | `''` | idem | Fin migration (ISO8601 UTC, prioritaire) |
| `JWT_BINDING_MIGRATION_DAYS` | `30` | idem | Fin relative à `migrationEpochUtc` |
| `BOOTSTRAP_API_BASE_URL` | (vide) | `bootstrap_config.dart` | URL Bootstrap globale |
| `LOCAL_API_BASE_URL` / `CLOUD_API_BASE_URL` | dev | `api_config.dart` | URLs API école (discovery / login) |
| `LOCAL_API_CANDIDATES` | `''` | idem | Liste LAN |

**Politique runtime :** `BindingMigrationPolicy.effectiveAllowJwtBindingMigration` désactive la migration après échéance **même si** `ALLOW_JWT_BINDING_MIGRATION=true`.

---

## 2. Serveur école / cloud

| Config | Environnement | Rôle |
|--------|---------------|------|
| `ERP_CONFIG_ENCRYPTION_KEY` | Production cloud **obligatoire** | Chiffrement config + garde démarrage |
| `Activation:BootstrapRelayKey` | Production | Validation relay Bootstrap (clé partagée) |
| `JWT_*` | Tous | Auth API (hors scope migration binding) |
| `ASPNETCORE_ENVIRONMENT` | Tous | Dev vs Production (garde clé AES) |

---

## 3. Bootstrap API

| Config | Rôle |
|--------|------|
| `Bootstrap:RelayApiKey` | Secret sortant relay → école |
| `Bootstrap:Schools[]` | Registre écoles ([registre-ecoles-bootstrap.md](registre-ecoles-bootstrap.md)) |

---

## 4. Matrice déploiement production (recommandée)

| Phase | Mobile parent | Serveur | Bootstrap |
|-------|---------------|---------|-----------|
| **Pilote** | Defaults (STRICT off, migration on) | Relay key fort | Registre 1 école |
| **Migration** | Annoncer `JWT_BINDING_MIGRATION_END_UTC` | — | — |
| **Post-migration** | `ALLOW_JWT_BINDING_MIGRATION=false` (option) | — | — |
| **Durcissement** | `STRICT_SCHOOL_DISCOVERY=true` | Relay key + registre fingerprints | JWKS futur |
| **Steady-state** | STRICT on, migration off | JWT relay (futur) | Registre complet |

---

## 5. Flags supprimables après migration complète

| Flag / mécanisme | Condition de retrait | Remplacement |
|------------------|----------------------|--------------|
| `ALLOW_JWT_BINDING_MIGRATION` | 100 % parents avec binding QR (ou jwt-migration accepté) + date dépassée | Gate permanent « binding requis » |
| `JWT_BINDING_MIGRATION_*` | Idem | — (code gate `isPostMigrationPhase` seul) |
| `JwtBindingMigrationService` | Plus aucun parent sans binding en base support | Activation QR seule |
| Legacy prefs push/cache **non scopées** | `STRICT_SCHOOL_DISCOVERY=true` partout | Clés `school.{id}.*` uniquement |
| Discovery non filtrée (code path) | STRICT on par défaut au build | Retirer branche legacy **uniquement** après métriques prod |

**À conserver long terme :**

- `BOOTSTRAP_API_BASE_URL`, URLs API (infra).
- `Bootstrap:Schools` (registre).
- `STRICT_SCHOOL_DISCOVERY` peut devenir **défaut `true`** au compile-time sans supprimer le flag (rollback d’urgence).

**Hors retrait v2 :** `serverSignature` (null jusqu’étape 9), relay clé statique jusqu’à JWT relay.

---

## 6. Vérification CI

```powershell
dotnet test --filter "Category=Foundations"
cd mobile\school_management_mobile
flutter test test/foundations
```

Toute modification de flag doit mettre à jour ce document et [deploiement-production-connexion-v2.md](../exploitation/deploiement-production-connexion-v2.md).
