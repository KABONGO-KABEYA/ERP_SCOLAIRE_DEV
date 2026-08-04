# Registre écoles — Bootstrap API (architecture v2.0.1)

**Référence :** [identite-ecole-decouverte-v2.md](identite-ecole-decouverte-v2.md) §4.1, §4.12  
**Statut :** schéma **production** (runtime actuel : routage activation + URLs cloud)

---

## 1. Rôle

Le registre Bootstrap (`Bootstrap:Schools`) est la **source de vérité opérationnelle** pour :

| Usage | Champ(s) |
|-------|----------|
| Routage `activation/start` → API école | `SchoolId`, `ActivationBaseUrl` |
| URL cloud dans `SchoolBinding` (complete) | `CloudBaseUrl` |
| **Futur** — confiance relay / pinning | `PublicKeyFingerprint`, `KeyVersion`, `PublicKeyPem` |
| **Futur** — audit réinstallation | `ServerInstanceId` (optionnel, ops) |

Le mobile **ne lit jamais** ce registre directement : il passe par Bootstrap (`start` / `complete`).

---

## 2. Schéma d’entrée

| Propriété | Obligatoire | Description |
|-----------|-------------|-------------|
| `SchoolId` | Oui | GUID école (JWT, health, binding) |
| `ActivationBaseUrl` | Oui | Base URL API école (LAN ou tunnel ops) pour relay activation |
| `CloudBaseUrl` | Oui | URL API distante parent (hors Wi‑Fi école) |
| `PublicKeyFingerprint` | Non | Empreinte clé publique RSA école (= health) |
| `KeyVersion` | Non | Version clé Setup |
| `PublicKeyPem` | Non | Clé publique PEM (stockage ops / future validation) |
| `ServerInstanceId` | Non | Dernier `serverInstanceId` connu (doc / alertes) |

Implémentation : `SchoolRegistryEntryOptions` (`SchoolManagement.Bootstrap.API`).

---

## 3. Exemple `appsettings.Production.json` (extrait)

```json
{
  "Bootstrap": {
    "RelayApiKey": "<secret relay — voir bootstrap-relay-auth-evolution.md>",
    "Schools": [
      {
        "SchoolId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        "ActivationBaseUrl": "https://ecole-a.internal:5041",
        "CloudBaseUrl": "https://cloud.erp-scolaire.example:1804",
        "PublicKeyFingerprint": "a1b2c3…",
        "KeyVersion": 1,
        "PublicKeyPem": null,
        "ServerInstanceId": "11111111-1111-1111-1111-111111111111"
      }
    ]
  }
}
```

**Synchronisation ops :** après Setup ou rotation de clés école, recopier `publicKeyFingerprint` / `keyVersion` depuis `ServerIdentity.json` (voir [server-identity-et-restauration.md](../exploitation/server-identity-et-restauration.md)).

---

## 4. Évolution auth Bootstrap ↔ école

| Phase | Mécanisme | Doc |
|-------|-----------|-----|
| **Actuel (v2.0.1)** | `X-Bootstrap-Relay-Key` (clé partagée) | [bootstrap-relay-auth-evolution.md](bootstrap-relay-auth-evolution.md) |
| **Cible** | JWT de service Bootstrap + JWKS | Même doc §3 |
| **Étape 9+ mobile** | Vérification `serverSignature` health | §4.12 architecture gelée |

Le registre **publicKey\*** prépare la validation côté Bootstrap (émetteur) et école (audience) **sans changer** les contrats REST mobile.

---

## 5. Procédure d’ajout d’une école

1. Installer l’ERP école (Setup) → `ServerIdentity` + health v2 OK.
2. Créer l’entrée registre Bootstrap (`SchoolId` = health `identity.schoolId`).
3. Renseigner `ActivationBaseUrl` (joignable depuis l’hôte Bootstrap) et `CloudBaseUrl` (parent distant).
4. (Recommandé) Copier fingerprint / keyVersion dans le registre.
5. Tester : QR parent → `start` → relay → `complete` → `SchoolBinding` mobile.

---

## 6. Non-objectifs v2.0.1

- Pas de base de données registre centralisée (config fichier / secrets manager).
- Pas de réplication automatique fingerprint depuis health (manuel ops).
- Pas de JWT relay implémenté (interfaces `IBootstrapRelayServiceTokenIssuer` réservées).
