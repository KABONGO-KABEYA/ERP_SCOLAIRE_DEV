# Rapport — Étape 2 (architecture v2.0.1)

**Périmètre :** modèles `SchoolBinding` / `ActivationSession`, persistance mobile, flags migration — **sans** activation, bootstrap, QR, ni changement discovery/login.

**Date :** 2026-08-04

---

## 1. Nouveaux fichiers

| Fichier | Rôle |
|---------|------|
| `mobile/.../lib/core/connection/connection_protocol_constants.dart` | `protocolVersion` / `apiVersion` alignés serveur |
| `mobile/.../lib/core/school_binding/activation_session.dart` | Modèle + enum `ActivationSessionStatus` |
| `mobile/.../lib/core/school_binding/school_binding.dart` | Modèle persistant post-activation |
| `mobile/.../lib/core/school_binding/school_binding_repository.dart` | Persistance secure `school_binding` |
| `mobile/.../lib/core/school_binding/activation_session_store.dart` | Mémoire + secure optionnel `activation_session` |
| `mobile/.../lib/core/school_binding/school_binding_activation_gate.dart` | Gate `isActivationFlowEnabled = false` (étape 3+) |
| `mobile/.../lib/core/config/binding_migration_config.dart` | `ALLOW_JWT_BINDING_MIGRATION`, `STRICT_SCHOOL_DISCOVERY`, dates |
| `mobile/.../test/foundations/school_binding_models_test.dart` | Round-trip JSON modèles |
| `mobile/.../test/foundations/school_binding_repository_test.dart` | save/load/clear repository |
| `mobile/.../test/foundations/binding_migration_config_test.dart` | Defaults migration |
| `docs/architecture/etape-2-rapport.md` | Ce rapport |

---

## 2. Fichiers modifiés

| Fichier | Modification |
|---------|----------------|
| `docs/architecture/identite-ecole-decouverte-v2.md` | Étape 2 marquée implémentée ; inventaire tests fondations étendu |

**Aucune modification** de : `main.dart`, `login_screen.dart`, `local_server_discovery.dart`, `connection_mode_notifier.dart`, `bootstrap_config.dart` (URL seule), API .NET, Desktop.

---

## 3. Structures ajoutées

### `ActivationSession` (§4.4)

`activationSessionId`, `activationTokenId`, `deviceId`, `schoolId`, `status` (`pending` \| `completed` \| `failed` \| `revoked`), `createdAt`, `expiresAt`, `clientHints?`. **Pas de `licenseId`** (v2.0.1).

### `SchoolBinding` (§4.6)

Champs contrat gelé + `extensions?` ; persistance JSON chiffrée clé `school_binding`.

### Migration (§4.11)

| Paramètre | Mécanisme |
|-----------|-----------|
| `allowJwtBindingMigration` | `--dart-define=ALLOW_JWT_BINDING_MIGRATION=` (défaut `true`) |
| `jwtBindingMigrationEndUtc` | `--dart-define=JWT_BINDING_MIGRATION_END_UTC=` |
| `jwtBindingMigrationDays` | `--dart-define=JWT_BINDING_MIGRATION_DAYS=` (défaut 30) |
| `strictSchoolDiscovery` | `--dart-define=STRICT_SCHOOL_DISCOVERY=` (défaut **false**) |

`BindingMigrationPolicy.effectiveAllowJwtBindingMigration` : calcul prêt, **non consommé** par le login.

### Inactivité garantie

- `SchoolBindingActivationGate.isActivationFlowEnabled == false`
- Aucun import des nouveaux modules dans discovery / auth / router
- Repository et store **non instanciés** au démarrage app

---

## 4. Tests réalisés

| Suite | Commande |
|-------|----------|
| Flutter fondations (étape 2) | `flutter test test/foundations/school_binding_models_test.dart test/foundations/school_binding_repository_test.dart test/foundations/binding_migration_config_test.dart` |
| Fondations existantes (étapes 1 + 1.1) | `dotnet test --filter Category=Foundations` + `flutter test test/foundations` (hors nouveaux fichiers) |

Les tests .NET **inchangés** ; pas de régression attendue côté serveur.

---

## 5. Impacts éventuels

| Zone | Impact |
|------|--------|
| **Discovery / login** | **Aucun** — flux identique |
| **Taille binaire mobile** | Légère (+ quelques classes Dart, tree-shaken si non référencées) |
| **Sécurité** | Clés secure storage définies mais vides tant qu’aucune activation |
| **CI** | Recommandé d’ajouter les 3 tests Flutter au job mobile |
| **Étape 3** | Brancher `SchoolBindingActivationGate`, client bootstrap, gates parent |

---

## 6. Prochaine étape (non démarrée)

Étape 3 — Bootstrap API, endpoints activation école, QR → session → binding, gate parent (hors migration JWT).

Validation utilisateur requise avant implémentation.
