# Validation bout en bout — architecture connexion v2.0.1

**Étape 8** — checklist manuelle + tests automatisés de non-régression.

---

## 1. Tests automatisés (obligatoires avant release connexion)

### .NET — Fondations

```powershell
dotnet test tests/SchoolManagement.UnitTests --filter "Category=Foundations"
dotnet test tests/SchoolManagement.IntegrationTests --filter "Category=Foundations"
```

| Domaine | Couverture |
|---------|------------|
| ServerIdentity / ACL / clé AES prod | Unit Foundations |
| Health v2 discovery | Unit + intégration |
| Relay Bootstrap clé statique | `StaticSharedKeyBootstrapRelayRequestValidatorTests` |
| Routing token activation | `ActivationTokenRoutingReaderTests` |
| API parent SchoolId | `ParentApiSchoolContextTests` |

### Flutter — Fondations

```powershell
cd mobile\school_management_mobile
flutter pub get
flutter test test/foundations
```

| Domaine | Fichiers clés |
|---------|----------------|
| DeviceId | `device_identity_test.dart` |
| Health parse | `health_info_compat_test.dart` |
| SchoolBinding | `school_binding_*` |
| Discovery policy | `school_discovery_policy_test.dart` |
| Migration JWT | `jwt_binding_migration_test.dart` |
| Push scope | `parent_push_*_test.dart` |

---

## 2. Scénarios E2E manuels

### A. Activation (flux officiel)

| # | Action | Résultat attendu |
|---|--------|------------------|
| A1 | QR `erp-scolaire://activate?token=…` sans discovery préalable | Écran activation → Bootstrap |
| A2 | `start` + `complete` | `SchoolBinding` persisté |
| A3 | Health école = binding `schoolId` / `serverInstanceId` | OK |
| A4 | Login parent après activation | Accès espace parent |

### B. Discovery

| # | Contexte | Résultat |
|---|----------|----------|
| B1 | STRICT **off**, sans binding | Discovery legacy |
| B2 | STRICT **on**, avec binding | Candidats filtrés `schoolId` ; cloud = `binding.cloudBaseUrl` |
| B3 | Candidat autre école | Rejeté |

### C. Cache (étape 5)

| # | Action | Résultat |
|---|--------|----------|
| C1 | STRICT on, changement école binding | Purge scope ancienne école |
| C2 | Changement `serverInstanceId` | Purge, logout, redirect login |

### D. Notifications (étape 6)

| # | Action | Résultat |
|---|--------|----------|
| D1 | SignalR connecté, notif école courante | Affichée |
| D2 | Payload autre `schoolId` (strict) | Ignorée |
| D3 | Recovery instance | Transport push stoppé, reprise après login |

### E. Migration JWT (étape 7)

| # | Contexte | Résultat |
|---|----------|----------|
| E1 | Fenêtre ouverte, parent sans binding | Login → binding assisté |
| E2 | Post-échéance, sans binding | Redirect activation, pas de session |
| E3 | API parent | Données limitées au `SchoolId` JWT |

---

## 3. Critères d’acceptation globaux (rappel §8 architecture)

1. Zero discovery à l’activation avant binding enregistré.  
2. QR token seul ; métadonnées via Bootstrap.  
3. Flux start → session → complete → binding.  
4. Setup avec clés + fingerprint.  
5. `licenseId` dans contrat health/binding.  
6. Migration JWT auto-désactivée après date.  
7. Isolation post-binding (device, cache, push en mode strict).  
8. `protocolVersion` distinct de `version` / `apiVersion`.

---

## 4. Verdict étape 8

Cocher **GO production connexion** lorsque :

- [ ] Suites Foundations .NET **vertes**
- [ ] Suites Foundations Flutter **vertes** (CI ou release manager)
- [ ] Scénarios A + B validés sur au moins **1 école pilote**
- [ ] Registre Bootstrap renseigné + relay key prod
- [ ] Date migration communiquée et build mobile configuré

Verdict consigné dans [rapport-final-architecture-v2.md](rapport-final-architecture-v2.md).
