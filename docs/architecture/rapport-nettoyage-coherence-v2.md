# Rapport de nettoyage et cohérence — architecture connexion v2.0.1

**Date :** 2026-08-04  
**Statut chantier :** architecture connexion **terminée** ([rapport-final-architecture-v2.md](rapport-final-architecture-v2.md))  
**Périmètre :** connexion / identité école / discovery / binding / activation Bootstrap / migration JWT / push scopé — **hors** fonctionnalités métier ERP (notes, finances, desktop bulletins métier, etc.)  
**Méthode :** revue statique du dépôt (grep, lecture ciblée) — **aucune modification de code**  
**Références :** [identite-ecole-decouverte-v2.md](identite-ecole-decouverte-v2.md), [inventaire-feature-flags-v2.md](inventaire-feature-flags-v2.md)

---

## 1. Synthèse exécutive

| Catégorie | Volume indicatif | Action |
|-----------|------------------|--------|
| Code mort / API inutilisée | **faible** (3 symboles mobile + branches mortes) | Nettoyage sûr **sans** impact prod |
| Chemins « legacy » migration | **modéré** (discovery, cache, push, login) | Retrait **après** migration parents + `STRICT_SCHOOL_DISCOVERY=true` |
| Dettes `TD-*` documentées | **6** | Conserver jusqu’à évolutions cibles (relay JWT, étape 9, FCM, etc.) |
| `TODO` / `FIXME` dans `.dart` / `.cs` connexion | **0** | — |
| Commentaires / docs **historiques** obsolètes | **plusieurs** | Corriger à la plume ou archiver explicitement |
| Artefacts repo (`_run/…`) | **1 dossier** | Gitignore / hors dépôt — pas lié à v2 |

**Recommandation de clôture :** le chantier peut être **gelé tel quel** pour la prod ; planifier **une PR de nettoyage** post-pilote (code mort + commentaires) puis **une PR de retrait migration** une fois métriques OK.

---

## 2. Légende des verdicts

| Verdict | Signification |
|---------|----------------|
| **Supprimer maintenant** | Aucun appelant ou branche impossible ; retrait sans changement de comportement prod (builds actuels). |
| **Supprimer après migration complète** | Encore requis tant que parents sans binding QR / `STRICT_SCHOOL_DISCOVERY=false` / fenêtre JWT migration ouverte. |
| **Conserver définitivement** | Pièce maîtresse v2.0.1 ou extension future documentée (interfaces sans impl.). |

**« Migration complète »** = aligné sur [inventaire-feature-flags-v2.md](inventaire-feature-flags-v2.md) §5 : 100 % parents avec `SchoolBinding` (QR ou `jwt-migration` accepté), échéance `JWT_BINDING_MIGRATION_*` dépassée, builds store avec `STRICT_SCHOOL_DISCOVERY=true`, plus de dépendance aux clés prefs/cache push non scopées.

---

## 3. Mobile Flutter

### 3.1 Peut être supprimé maintenant

| Élément | Emplacement | Justification |
|---------|-------------|---------------|
| Getter `allowsLegacyParentLoginWithoutBinding` | `lib/core/school_binding/school_binding_gate.dart` | **Aucun appelant** dans le dépôt ; le login utilise `shouldBlockParentSessionWithoutBinding` + `JwtBindingMigrationService` + `BindingMigrationPolicy` directement. |
| Méthode `shouldUseBootstrapActivationFlow` | `lib/core/school_binding/school_binding_gate.dart` | **Aucun appelant** (seule mention : doc étape 7). |
| Classe entière `SchoolBindingActivationGate` (option) | `lib/core/school_binding/school_binding_activation_gate.dart` | `isActivationFlowEnabled` est **`const true`** ; seuls usages : garde dans `parent_activation_screen.dart` (branche morte) et délégation depuis `shouldUseBootstrapActivationFlow` (morte). **Alternative :** supprimer la classe et la garde UI, pas seulement la gate. |
| Branche UI « Activation non disponible » | `lib/features/parent/activation/parent_activation_screen.dart` (L41–44) | Condition toujours fausse avec le code actuel. |
| Constante top-level `apiBaseUrl` | `lib/core/config/api_config.dart` (L97–101) | `@Deprecated` ; **aucune référence** au symbole global — seul `ApiConfig._legacyBaseUrl` consomme `API_BASE_URL`. |
| Commentaires de fichier obsolètes | `binding_migration_config.dart` L2–3 (« non appliqués au login »), L33–34 (« inactif tant qu'aucun gate ») | Faux post-étapes 4–7 ; **documentation inline** à corriger lors du nettoyage. |
| Commentaire obsolète | `activation_session.dart` L1 (« étape 2 : modèle uniquement ») | Modèle **utilisé** par `BootstrapApiClient`, `SchoolActivationService`, `ActivationSessionStore`. |

### 3.2 Supprimer après migration complète

| Élément | Emplacement | Remplacement / note |
|---------|-------------|---------------------|
| `BindingMigrationConfig.allowJwtBindingMigration`, `jwtBindingMigrationEndUtc`, `jwtBindingMigrationDays` | `binding_migration_config.dart` | Gate permanent « binding requis » ; `isPostMigrationPhase` peut devenir constant `true` au build ou être supprimé. |
| `BindingMigrationPolicy.effectiveAllowJwtBindingMigration`, `daysUntilMigrationEndUtc`, `isMigrationEndingSoon` | idem | Idem |
| `JwtBindingMigrationService` + `jwt_binding_migration_constants.dart` | `lib/core/school_binding/` | Activation QR seule ; extension `migratedFromJwt` / token `jwt-migration` optionnelle en lecture seule pour audit. |
| Appel migration post-login | `login_screen.dart` | — |
| Widget `ParentMigrationBanner` (ou contenu migration) | `lib/features/parent/widgets/parent_migration_banner.dart` | Bannières J-7 / post-migration STRICT inutiles. |
| `StrictDiscoveryRolloutPolicy` | `lib/core/config/strict_discovery_rollout_policy.dart` | Messages rollout migration. |
| Fallback discovery « legacy » si STRICT sans binding | `local_server_discovery.dart` L535–539 | En prod steady-state, STRICT + pas de binding → échec explicite plutôt que repli non filtré. |
| Chemins prefs **non scopés** | `school_scoped_preferences.dart`, `cache_partition_policy.dart`, `parent_push_preferences.dart` | Clés `school.{id}.*` uniquement ; tests `*_legacy*` dans `test/foundations/`. |
| Garde push legacy (STRICT off) | `parent_push_school_guard.dart` | Comportement strict par défaut. |
| Flag compile-time `STRICT_SCHOOL_DISCOVERY` (optionnel) | `binding_migration_config.dart` | Peut rester comme **rollback d’urgence** avec défaut `true` ([inventaire-feature-flags-v2.md](inventaire-feature-flags-v2.md) §5). |

### 3.3 Conserver définitivement

| Élément | Emplacement | Rôle |
|---------|-------------|------|
| `SchoolBinding`, `SchoolBindingRepository`, `SchoolBindingGate` (méthodes actives) | `lib/core/school_binding/` | Cœur binding |
| `shouldBlockParentSessionWithoutBinding`, `shouldFilterDiscoveryByBinding`, `shouldPreferActivationEntryForParent` | `school_binding_gate.dart` | Gates prod (post-migration pour la 3e) |
| `SchoolActivationService`, `BootstrapApiClient`, `ActivationSession*`, stores | idem | Activation Bootstrap |
| `ServerInstanceRecoveryService`, `ServerInstanceBindingSync` | idem | Recovery `serverInstanceId` |
| `LocalServerDiscovery`, `DiscoveryConstants`, modèles health | `lib/core/local_server_discovery/` | Discovery v2 |
| `DeviceIdentity`, `ApiConfig` (sans `apiBaseUrl` global) | `lib/core/config/`, `device_identity.dart` | Infra URL / device |
| `connection_protocol_constants.dart` | `lib/core/connection/` | Alignement `protocolVersion` |
| `CachePartitionPolicy`, purge cache | `lib/core/cache/` | Partition école |
| Push scopé | `lib/features/parent/notifications/*` | SignalR / FG / guards |
| Suite `test/foundations/*.dart` | `mobile/.../test/foundations/` | CI connexion |

---

## 4. Serveur école (`SchoolManagement.API` + Application)

### 4.1 Peut être supprimé maintenant

| Élément | Verdict | Justification |
|---------|---------|---------------|
| — | — | **Aucun** type connexion v2 clairement mort identifié côté API (hors métier ERP). |

### 4.2 Supprimer après migration complète

| Élément | Emplacement | Note |
|---------|-------------|------|
| Logique métier liée aux parents **sans** `SchoolId` JWT (si résidus) | Contrôleurs parent | Étape 7 a centralisé `ParentApiSchoolContext` — **vérifier** toute PR future sur `ParentController` / `ParentService`. |
| Comportements permissifs **uniquement** pour migration binding | À confirmer au retrait mobile | Pas de flag serveur dédié JWT-binding ; le retrait est surtout **mobile + politique login**. |

### 4.3 Conserver définitivement

| Élément | Emplacement |
|---------|-------------|
| `LocalDiscoveryHealthController`, health v2 (`serverSignature: null`) | API |
| `ServerIdentity`, chiffrement prod, guards AES | Application / API |
| `ParentApiSchoolContext` | Application |
| Scoping `SchoolId` | `ParentController`, `ParentNotificationsController`, `ParentNotificationsHub`, `ParentService`, `NotificationService` |
| Activation relay `[BootstrapRelayOnly]`, `ParentActivationService`, DTOs | Application |
| `StaticSharedKeyBootstrapRelayRequestValidator` | API — **jusqu’à TD-RELAY-01 résolu** (≠ migration JWT parent) |
| `NullNotificationRealtimePublisher` + override `SignalRNotificationRealtimePublisher` dans `Program.cs` | Pattern DI : Application par défaut null ; API host SignalR — **les deux** |
| Tests `[Trait("Category", "Foundations")]` | `tests/` |

---

## 5. Bootstrap API (`SchoolManagement.Bootstrap.API`)

| Élément | Verdict | Commentaire |
|---------|---------|-------------|
| `BootstrapOrchestrator`, `SchoolRegistry`, `BootstrapOptions.Schools` | **Conserver définitivement** | Registre écoles |
| Champs optionnels registre `PublicKeyFingerprint`, `KeyVersion`, `PublicKeyPem`, `ServerInstanceId` | **Conserver définitivement** | Non lus en runtime v2.0.1 ([registre-ecoles-bootstrap.md](registre-ecoles-bootstrap.md)) — **préparation** étape 9 / relay JWT |
| `StaticSharedKeyBootstrapRelayOutboundAuth` | **Conserver** → remplacer après **TD-RELAY-01** | Pas « migration JWT parent » |
| `RelayApiKey` | **Conserver** (ops) | Secret prod |

---

## 6. Interfaces et dettes techniques (`TD-*`)

Aucun `TODO`/`FIXME` dans le code connexion ; dettes **nommées** dans docs / commentaires XML.

| Id | Éléments code concernés | Verdict |
|----|-------------------------|---------|
| **TD-RELAY-01** | `StaticSharedKeyBootstrapRelay*`, `X-Bootstrap-Relay-Key`, `BootstrapRelayAuthConstants.LegacySharedKeyHeaderName` | **Conserver** jusqu’à impl. JWT relay ; puis supprimer impl. statique, **garder** interfaces |
| **TD-SIG-01** | `serverSignature` null health, champs registre PEM/fingerprint | **Conserver définitivement** (placeholder étape 9) |
| **TD-DEVID-01** | Pas de colonne `deviceId` tokens push | **Conserver** comportement actuel ; évolution BDD future |
| **TD-FCM-01** | FG service + polling | **Conserver** |
| **TD-FLAGS-01** | Branches STRICT off / migration JWT | **Supprimer après migration complète** (§3.2) |
| **TD-REG-01** | Registre fichier `Bootstrap:Schools` | **Conserver** ; remplacement produit = chantier séparé |
| **TD-FLAGS-01** (doc) | [inventaire-feature-flags-v2.md](inventaire-feature-flags-v2.md) | Mettre à jour lors du retrait code |

| Interface sans implémentation | Fichier | Verdict |
|-------------------------------|---------|---------|
| `IBootstrapRelayServiceTokenIssuer` | Application | **Conserver définitivement** (contrat [bootstrap-relay-auth-evolution.md](bootstrap-relay-auth-evolution.md)) |
| `IBootstrapRelayRequestValidator` / `IBootstrapRelayOutboundAuth` | Application | **Conserver** — impl. actuelle statique, future JWT |

---

## 7. Feature flags et configuration

### 7.1 Supprimer après migration complète (mobile)

| Flag / mécanisme | Fichier |
|------------------|---------|
| `ALLOW_JWT_BINDING_MIGRATION` | `binding_migration_config.dart` |
| `JWT_BINDING_MIGRATION_END_UTC`, `JWT_BINDING_MIGRATION_DAYS` | idem |
| Code + tests dédiés migration JWT binding | `jwt_binding_migration_*`, `jwt_binding_migration_test.dart` |
| Chemins legacy discovery/cache/push | voir §3.2 |

### 7.2 Conserver définitivement (ou long terme)

| Flag / config | Rôle |
|---------------|------|
| `BOOTSTRAP_API_BASE_URL` | URL Bootstrap |
| `LOCAL_API_BASE_URL`, `CLOUD_API_BASE_URL`, `LOCAL_API_CANDIDATES` | Infra / dev |
| `STRICT_SCHOOL_DISCOVERY` | Recommandé : défaut `true` en prod ; flag **conservé** pour rollback ([inventaire-feature-flags-v2.md](inventaire-feature-flags-v2.md)) |
| `Activation:BootstrapRelayKey` | Validation relay entrant (école) |
| `Bootstrap:RelayApiKey` | Relay sortant Bootstrap |
| `ERP_CONFIG_ENCRYPTION_KEY`, `JWT_*` (auth API) | Prod / sécurité |

### 7.3 Incohérence de nommage (config relay — **documentée**, pas bug)

| Côté | Nom propriété | Section config |
|------|---------------|----------------|
| Bootstrap API | `RelayApiKey` | `Bootstrap:RelayApiKey` |
| API école | `BootstrapRelayKey` | `Activation:BootstrapRelayKey` |

**Verdict :** **Conserver** ; renommage unifié = changement breaking ops + doc — hors scope nettoyage v2.

---

## 8. Duplications et cohérence

| Sujet | Détail | Verdict |
|-------|--------|---------|
| Deux « gates » binding | `SchoolBindingGate` vs `SchoolBindingActivationGate` | **Fusionner / supprimer** activation gate (§3.1) |
| `ConnectionProtocolConstants` | C# (`Application/ServerIdentity/`) vs Dart (`connection_protocol_constants.dart`) | **Conserver** — duplication **intentionnelle** cross-stack ; synchroniser manuellement si version change |
| `RequireSchoolId()` privé | `ParentController` + `ParentNotificationsController` | **Conserver** (DRY faible, acceptable) ou extraire helper API plus tard |
| Modèle session | Dart `ActivationSession` vs C# `ActivationSessionDto` | **Conserver** — nommage cohérent par couche |
| Tests health discovery | `HealthDiscoveryEndpointTests` + `FoundationsIntegrationTests` | **Conserver** — recouvrement partiel acceptable CI |
| `ApiConfig._legacyBaseUrl` vs `API_BASE_URL` | Deux mécanismes pour ancienne variable d’env | **Conserver** `_legacyBaseUrl` ; supprimer seulement `const apiBaseUrl` export (§3.1) |

---

## 9. Documentation

| Document | Verdict | Commentaire |
|----------|---------|-------------|
| `identite-ecole-decouverte-v2.md` | **Conserver définitivement** | Gel v2.0.1 |
| `rapport-final-architecture-v2.md`, étapes 3–8, exploitation, validation E2E | **Conserver définitivement** | Clôture chantier |
| `etape-2-rapport.md` | **Conserver** (archive) | **Snapshot historique** : indique `isActivationFlowEnabled == false`, migration « non consommée » — **ne plus utiliser** comme état actuel |
| `inventaire-feature-flags-v2.md`, `deploiement-production-connexion-v2.md` | **Conserver** ; mettre à jour au retrait flags | |
| `bootstrap-relay-auth-evolution.md`, `registre-ecoles-bootstrap.md` | **Conserver définitivement** | Roadmap relay / registre |

**Suggestion (sans suppression) :** ajouter en tête de `etape-2-rapport.md` une ligne « État au 2026-08-04 : voir rapport final — ce document décrit l’étape 2 uniquement ».

---

## 10. Fichiers / artefacts hors code source connexion

| Élément | Verdict | Commentaire |
|---------|---------|-------------|
| `_run/desktop-bulletins/` (binaires, DLL, fonts) | **Ne pas versionner** | Build local ; ajouter à `.gitignore` ou supprimer du working tree — **sans lien** architecture connexion v2 |
| `.env.example` (commentaires flags mobile) | **Conserver définitivement** | Onboarding dev / CI |

---

## 11. Ordre de nettoyage recommandé (post-clôture, sans urgence prod)

1. **PR A — hygiène immédiate (faible risque)**  
   Supprimer symboles morts §3.1, corriger commentaires inline, optionnellement retirer `SchoolBindingActivationGate` + garde UI morte.  
   Mettre à jour tests si des helpers morts étaient exportés (aucun test sur `allowsLegacy*` / `shouldUseBootstrap*` aujourd’hui).

2. **PR B — après pilote école + date migration**  
   Retrait `JwtBindingMigrationService`, bannières, flags `JWT_BINDING_MIGRATION_*`, simplification `BindingMigrationPolicy`.

3. **PR C — après `STRICT_SCHOOL_DISCOVERY=true` store + purge prefs legacy**  
   Retrait branches legacy cache/push/discovery ; ajuster fondations Flutter.

4. **Chantier séparé (non bloquant clôture v2)**  
   TD-RELAY-01 (JWT relay), étape 9 signature, TD-REG-01, TD-DEVID-01, TD-FCM-01.

---

## 12. Validation

Pour toute PR de nettoyage connexion :

```powershell
dotnet test --filter "Category=Foundations"
cd mobile\school_management_mobile
flutter test test/foundations
```

---

**Fin du rapport** — prêt pour clôture du chantier architecture connexion v2.0.1 et reprise du développement métier ERP sur base gelée.
