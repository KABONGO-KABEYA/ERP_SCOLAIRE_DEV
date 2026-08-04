# Rapport — Étape 7 (architecture v2.0.1)

**Périmètre :** fin migration JWT → `SchoolBinding`, gates mobile, durcissement API parent `SchoolId`, préparation `STRICT_SCHOOL_DISCOVERY`. **Hors scope :** Bootstrap, activation, discovery (logique filtrée), cache, notifications (étapes 4–6), étape 8 registre clés.

**Date :** 2026-08-04

---

## 1. Migration JWT → SchoolBinding (§4.11)

### Fenêtre migration (mobile)

| Comportement | Condition |
|--------------|-----------|
| Login parent **sans** binding autorisé | `BindingMigrationPolicy.effectiveAllowJwtBindingMigration == true` |
| Après login réussi | `JwtBindingMigrationService.tryMigrateAfterParentLogin` : health + JWT → `SchoolBinding` (`extensions.migratedFromJwt`) |
| Fin automatique migration | Date `JWT_BINDING_MIGRATION_END_UTC` **ou** durée `JWT_BINDING_MIGRATION_DAYS` depuis `migrationEpochUtc` ; si dépassé → `effectiveAllowJwtBindingMigration = false` même si compile-time `true` |
| Post-migration parent sans binding | Session refusée → `/parent/activate?reason=binding_required` |
| Staff (non parent) | Login sans binding inchangé |

### Composants mobile

| Fichier | Rôle |
|---------|------|
| `jwt_binding_migration_service.dart` | Construction binding assistée |
| `jwt_binding_migration_constants.dart` | Marqueurs `jwt-migration` |
| `school_binding_gate.dart` | Gates login / activation / legacy |
| `strict_discovery_rollout_policy.dart` | Hint déploiement STRICT |
| `parent_migration_banner.dart` | Bannière J-7 + incitation QR |
| `binding_migration_config.dart` | `isPostMigrationPhase`, `daysUntilMigrationEndUtc`, `isMigrationEndingSoon` |

---

## 2. API parent — contexte `SchoolId`

### Serveur

- **`ParentApiSchoolContext`** : `RequireSchoolId`, `EnsureResourceSchool`
- **`ParentController`** : `SchoolId` JWT sur chaque appel ; vérif `UserAccount.SchoolId`
- **`ParentService`** : filtre enfants par école ; `EnsureChildAccessAsync` vérifie `Student.SchoolId`
- **`NotificationService`** : inbox / changes / unread / read / delivered filtrés ou validés par `SchoolId`
- **`ParentNotificationsController`** + hub SignalR ACK : `SchoolId` obligatoire

Les parents multi-écoles côté serveur (compte unique, JWT une école) ne voient plus les données d’une autre école via les API parent.

---

## 3. Feature flags avant production

| Flag (dart-define) | Défaut | Rôle |
|--------------------|--------|------|
| `ALLOW_JWT_BINDING_MIGRATION` | `true` | Fenêtre login legacy parent |
| `JWT_BINDING_MIGRATION_END_UTC` | `''` | Fin explicite (prioritaire) |
| `JWT_BINDING_MIGRATION_DAYS` | `30` | Fin relative à `migrationEpochUtc` |
| `STRICT_SCHOOL_DISCOVERY` | **`false`** | Partition cache/discovery/push — activer progressivement en prod parent |

**Rollout recommandé :**

1. Communique date fin migration → builds avec `JWT_BINDING_MIGRATION_END_UTC`
2. Après échéance : parents sans QR bloqués (gate app)
3. Build prod parent : `STRICT_SCHOOL_DISCOVERY=true` quand la base utilisateurs est migrée
4. Optionnel : `ALLOW_JWT_BINDING_MIGRATION=false` au compile-time pour durcir le binaire

**Non modifié :** protocole Bootstrap / activation ; code discovery (filtrage reste opt-in STRICT).

---

## 4. Mécanismes legacy encore présents

| Mécanisme | Statut |
|-----------|--------|
| Discovery non filtrée | Tant que `STRICT_SCHOOL_DISCOVERY=false` |
| Login staff sans `SchoolBinding` | Autorisé |
| Migration JWT assistée | Tant que fenêtre ouverte |
| Binding `jwt-migration` (non QR officiel) | Valide jusqu’à scan QR ultérieur |
| `shouldUseBootstrapActivationFlow` | Lié à `SchoolBindingActivationGate` (activation QR déjà activée étape 3) |

**Retiré / durci :**

- `SchoolBindingGate.shouldBlockLoginForMissingBinding` stub → **`shouldBlockParentSessionWithoutBinding`** effectif post-migration
- API parent sans filtre école → **filtrage systématique**

---

## 5. Tests réalisés

| Test | Résultat |
|------|----------|
| `dotnet build` SchoolManagement.API | **OK** |
| `ParentApiSchoolContextTests` (Foundations) | **OK** (après fix Xunit) |
| `jwt_binding_migration_test.dart` | Non exécuté ici (`flutter` absent PATH agent) |
| `binding_migration_config_test.dart` | Inchangé, compatible |

**Local :**

```bash
dotnet test tests/SchoolManagement.UnitTests --filter "Category=Foundations&FullyQualifiedName~ParentApiSchoolContext"
cd mobile/school_management_mobile && flutter test test/foundations/jwt_binding_migration_test.dart
```

---

## 6. Impacts

| Zone | Impact |
|------|--------|
| **Clients mobile anciens** | Aucun changement API breaking ; gates migration dans nouvelles versions |
| **API parent JSON** | Même contrat ; données filtrées par JWT `SchoolId` |
| **Parents post-migration sans QR** | Doivent activer via `/parent/activate` |
| **Enseignants / secrétariat** | Pas de binding requis |
| **Étape 8** | Non démarrée |

---

## 7. Prochaine étape

**Étape 8** — registre clés bootstrap + doc ops (après validation explicite de l’étape 7).
