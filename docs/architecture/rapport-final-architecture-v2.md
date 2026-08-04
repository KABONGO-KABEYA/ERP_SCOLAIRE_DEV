# Rapport final — Architecture connexion v2.0.1

**Statut :** **TERMINÉE — prête pour déploiement production** (sous réserve des tests Flutter fondations et E2E pilote école).  
**Date de clôture :** 2026-08-04  
**Document de référence gelé :** [identite-ecole-decouverte-v2.md](identite-ecole-decouverte-v2.md)

---

## 1. Bilan des étapes réalisées

| Étape | Objectif | Rapport |
|-------|----------|---------|
| **0** | Gel architecture v2 | `identite-ecole-decouverte-v2.md` |
| **1** | Health v2, ServerIdentity, DeviceId, BootstrapConfig | Sprint doc §7 + tests Foundations |
| **1.1** | Durcissement identité / AES prod | [server-identity-et-restauration.md](../exploitation/server-identity-et-restauration.md) |
| **2** | `SchoolBinding`, `ActivationSession`, flags migration | [etape-2-rapport.md](etape-2-rapport.md) |
| **3** | Bootstrap API, activation 2 phases, relay clé statique | [etape-3-rapport.md](etape-3-rapport.md) |
| **4** | Discovery filtrée `STRICT_SCHOOL_DISCOVERY` | [etape-4-rapport.md](etape-4-rapport.md) |
| **5** | Cache partitionné, recovery `serverInstanceId` | [etape-5-rapport.md](etape-5-rapport.md) |
| **6** | Push / SignalR scopés école | [etape-6-rapport.md](etape-6-rapport.md) |
| **7** | Fin migration JWT, API parent `SchoolId` | [etape-7-rapport.md](etape-7-rapport.md) |
| **8** | Registre, doc prod, flags, validation E2E | [etape-8-rapport.md](etape-8-rapport.md) |

**Hors périmètre gelé (évolutions futures) :** étape 9 signature mobile, JWT relay production, FCM complet, `deviceId` sur `ParentDeviceTokens`.

---

## 2. Composants définitifs de l’architecture

### Serveur école

- `ServerIdentity` + health `/api/health` v2 (`identity`, `protocolVersion`, `serverSignature: null`)
- Activation parent relay `[BootstrapRelayOnly]` + `ParentActivationService`
- API parent / notifications scopées **`SchoolId`** JWT

### Bootstrap global

- `SchoolManagement.Bootstrap.API` — orchestration `start` / `complete`
- **Registre** `Bootstrap:Schools` ([registre-ecoles-bootstrap.md](registre-ecoles-bootstrap.md))
- Relay auth : **clé partagée** (extension JWT documentée)

### Mobile Flutter

- `DeviceIdentity`, `BootstrapApiClient`, `SchoolActivationService`
- `SchoolBinding` / `SchoolBindingRepository`
- `LocalServerDiscovery` + gates `SchoolBindingGate`
- Partition cache / auth / Hive / push (`STRICT_SCHOOL_DISCOVERY`)
- `JwtBindingMigrationService` + gates post-migration
- Flags : [inventaire-feature-flags-v2.md](inventaire-feature-flags-v2.md)

### Desktop / Setup

- QR token seul ; clés à l’install (I12)
- Pas de changement architecture connexion à l’étape 8

---

## 3. Dettes techniques restantes

| Id | Sujet | Mitigation actuelle | Cible |
|----|-------|---------------------|-------|
| **TD-RELAY-01** | Relay Bootstrap ↔ école clé statique | Secret fort, réseau restreint | JWT service ([bootstrap-relay-auth-evolution.md](bootstrap-relay-auth-evolution.md)) |
| **TD-SIG-01** | `serverSignature` health null | Fingerprint + binding + JWT | Étape 9 — vérif signature mobile |
| **TD-DEVID-01** | `deviceId` ERP non stocké sur tokens push | Scoping `SchoolId` serveur | Colonne BDD + enregistrement mobile |
| **TD-FCM-01** | Push FCM tué app | FG service + `/changes` | Intégration Firebase prod |
| **TD-FLAGS-01** | Legacy paths si STRICT off | Build prod progressive | Retrait code après métriques ([inventaire-feature-flags-v2.md](inventaire-feature-flags-v2.md)) |
| **TD-REG-01** | Registre Bootstrap fichier statique | Procédure ops manuelle | Secrets manager / DB registre (produit) |

Aucune dette ci-dessus **ne bloque** un pilote production contrôlé si checklist ops est suivie.

---

## 4. Recommandations déploiement production

1. **Ordre :** Setup école → health OK → entrée registre Bootstrap → relay keys prod → build mobile avec URLs + date migration.
2. **Piloter** activation QR + login sur 1 école ([validation-bout-en-bout-v2.md](validation-bout-en-bout-v2.md)).
3. **Communiquer** fin migration JWT ; surveiller bannières app.
4. **Activer** `STRICT_SCHOOL_DISCOVERY=true` sur build store une fois parents migrés.
5. **CI :** `Category=Foundations` .NET + `flutter test test/foundations` obligatoires sur toute PR touchant connexion.
6. **Ne pas** contourner Bootstrap pour l’activation en prod.

Guide détaillé : [deploiement-production-connexion-v2.md](../exploitation/deploiement-production-connexion-v2.md).

---

## 5. Recommandations évolutions futures

| Priorité | Évolution | Référence |
|----------|-----------|-----------|
| Haute | JWT relay Bootstrap (dual mode puis cutover) | TD-RELAY-01 |
| Haute | Signature health + pinning mobile | §4.12, étape 9 |
| Moyenne | Registre dynamique (DB / ops UI) | TD-REG-01 |
| Moyenne | Retrait flags migration legacy | inventaire flags §5 |
| Basse | `licenseId` produit (modules, quotas) | §4.13 |
| Basse | TD-DEVID-01, FCM natif | étape 6 rapport |

Toute modification du **gel** v2.0.1 requiert nouveau numéro de version + accord explicite (§10 architecture).

---

## 6. Verdict final

| Critère | État |
|---------|------|
| Flux activation / binding | Implémentés et documentés |
| Discovery / cache / notifications / migration | Implémentés (flags progressifs) |
| API parent isolées par école | Oui |
| Doc ops + registre + flags | Oui (étape 8) |
| Tests Foundations .NET | **30/30 OK** (2026-08-04) |
| Tests Foundations Flutter | Documentés — exécution release |

**L’architecture de connexion v2.0.1 est déclarée officiellement terminée.**  
L’équipe peut **reprendre le développement des fonctionnalités métier** de l’ERP en s’appuyant sur cette base gelée.

---

## 7. Index documentation connexion

- Architecture gelée : [identite-ecole-decouverte-v2.md](identite-ecole-decouverte-v2.md)
- Relay auth : [bootstrap-relay-auth-evolution.md](bootstrap-relay-auth-evolution.md)
- Identité serveur : [server-identity-et-restauration.md](../exploitation/server-identity-et-restauration.md)
- Déploiement : [deploiement-production-connexion-v2.md](../exploitation/deploiement-production-connexion-v2.md)
- Validation : [validation-bout-en-bout-v2.md](validation-bout-en-bout-v2.md)
- Flags : [inventaire-feature-flags-v2.md](inventaire-feature-flags-v2.md)
- Registre : [registre-ecoles-bootstrap.md](registre-ecoles-bootstrap.md)
