# Rapport — Étape 8 (architecture v2.0.1)

**Périmètre :** consolidation production, registre Bootstrap, documentation ops/déploiement/migration, inventaire flags, validation E2E, clôture architecture connexion. **Sans** modification des flux validés ni du métier.

**Date :** 2026-08-04

---

## 1. Livrables

| Document / artefact | Rôle |
|---------------------|------|
| [registre-ecoles-bootstrap.md](registre-ecoles-bootstrap.md) | Schéma registre + procédure ops |
| [inventaire-feature-flags-v2.md](inventaire-feature-flags-v2.md) | Flags + retrait post-migration |
| [deploiement-production-connexion-v2.md](../exploitation/deploiement-production-connexion-v2.md) | Checklist déploiement |
| [validation-bout-en-bout-v2.md](validation-bout-en-bout-v2.md) | E2E manuel + suites auto |
| [rapport-final-architecture-v2.md](rapport-final-architecture-v2.md) | **Rapport final officiel** |
| `SchoolRegistryEntryOptions` | Champs optionnels fingerprint / PEM (préparation auth forte) |

---

## 2. Registre Bootstrap

- Entrées existantes : `SchoolId`, `ActivationBaseUrl`, `CloudBaseUrl` (runtime inchangé).
- **Ajout documenté + modèle :** `PublicKeyFingerprint`, `KeyVersion`, `PublicKeyPem`, `ServerInstanceId` (non consommés en v2.0.1).
- Auth relay actuelle : clé statique ([bootstrap-relay-auth-evolution.md](bootstrap-relay-auth-evolution.md)).

---

## 3. Validation automatisée (passée)

| Suite | Résultat |
|-------|----------|
| `dotnet test --filter Category=Foundations` (unit) | **25/25 OK** |
| Idem (intégration) | **5/5 OK** |
| Flutter `test/foundations` | À exécuter en CI / release (`flutter` hors agent) |

Checklist manuelle : [validation-bout-en-bout-v2.md](validation-bout-en-bout-v2.md).

---

## 4. Contraintes respectées

- Protocoles Bootstrap / activation / discovery / cache / notifications : **non modifiés**.
- Extension registre = champs optionnels + documentation uniquement.

---

## 5. Suite

Architecture connexion **v2.0.1 clôturée** — reprise développement **fonctionnalités métier** ERP. Évolution hors gel : [rapport-final-architecture-v2.md](rapport-final-architecture-v2.md) § évolutions futures (signature mobile, JWT relay).
