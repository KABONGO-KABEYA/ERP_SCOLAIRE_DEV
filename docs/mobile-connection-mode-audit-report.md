# Audit mobile — Mode Local hors réseau école + inscriptions

Date : 2026-08-03  
Périmètre : app Flutter parent/secrétariat (`mobile/school_management_mobile`)  
Objectif : vérifier le respect de la convention Local / Distant / Cache, après observation terrain :

> téléphone sur **un autre réseau** que celui de l’école → bannière **Mode Local** → **inscription possible**.

**Aucune correction métier dans ce document** — diagnostic + écarts vs convention.

---

## Convention (source de vérité)

D’après `docs/mobile-app-design-brief.md` §6 et `docs/architecture/local-server-discovery.md` :

| Règle | Attendu |
|-------|---------|
| Choix serveur | **Jamais manuel** — détection auto |
| Ordre | 1) API locale (même Wi‑Fi) → **Mode Local** → 2) Cloud → **Mode Distant** → 3) **Mode Cache** |
| Inscriptions / docs | **Local uniquement** |
| Notes enseignant | Local **et** Distant |
| Distant | Consultation (+ notes), **pas** d’inscription |

Découverte attendue : mDNS → dernière IP → scan `/24` port **5096** → cloud.

---

## Verdict

| Point | Statut | Verdict |
|-------|--------|---------|
| Bannière Mode Local / Distant / Cache | Implémentée | OK UX |
| `WritePolicy.canEnrollStudents` = Local seulement | Implémentée | **OK** |
| Boutons inscription désactivés en Distant | Implémentée | **OK** |
| Redirect router `/secretary/enrollment` si !canEnroll | Implémentée | **OK** |
| Blocage submit wizard si !canEnroll | Implémentée | **OK** |
| Cloud API `CloudReadOnlyMiddleware` refuse écritures | Implémentée côté serveur Cloud | **OK** (si Role=Cloud) |
| **Définition réelle de « Mode Local »** | = « health HTTP OK sur une IP candidate » | **NON CONFORME** |
| Vérification « même Wi‑Fi / même sous-réseau » | Absente | **NON CONFORME** |
| Utilisation de `health.server` (`local` \| `cloud`) | Ignorée pour le mode | **NON CONFORME** |
| Recheck silencieux (changement réseau) | Peut **retenir Local** si last IP encore joignable | **RISQUE FORT** |

**Conclusion :**  
Si l’inscription était possible, le mode était bien classé **Local** (la politique d’écriture a fonctionné).  
Le **composant fautif** n’est pas le wizard d’inscription : c’est **`LocalServerDiscovery`** (classification trop permissive de « local »).

---

## Chaîne réelle (preuve code)

```
Réseau téléphone
    ↓
LocalServerDiscovery._run / recheck
    ↓  (si GET /api/health OK sur lastKnown | mDNS | scan)
DiscoveryMode.local   ← toujours, sans vérifier le même sous-réseau
    ↓
ConnectionProbe → ConnectionMode.local
    ↓
WritePolicy.canEnrollStudents = true
    ↓
Secrétariat : boutons inscription ACTIVÉS + POST complete autorisé côté client
```

### Politique d’écriture (conforme)

```dart
// connection_mode.dart
bool get allowsWrites => this == ConnectionMode.local;

// write_policy.dart
bool get canEnrollStudents => mode.allowsWrites;
```

UI secrétariat :

- `secretary_home_screen.dart` : `enabled: canEnroll`
- `app_router.dart` : redirect si `!writePolicy.canEnrollStudents`
- `enrollment_wizard_screen.dart` : refuse submit si `!policy.canEnrollStudents`

Donc **inscription possible ⇔ Mode Local**. L’observation terrain confirme un **faux positif Local**.

---

## Écarts de découverte (où ça casse)

### 1. « Local » = joignable, pas « même Wi‑Fi »

Dans `local_server_discovery.dart`, dès qu’un probe `/api/health` réussit via :

- dernière IP connue (`lastKnown`)
- mDNS
- scan sous-réseau

→ `mode: DiscoveryMode.local` **sans** :

- comparer le préfixe `/24` du téléphone à celui de l’IP serveur ;
- comparer l’URL au cloud (`ApiConfig.effectiveCloudBaseUrl`) ;
- lire `health.server` (`"local"` / `"cloud"` renvoyé par l’API).

**Effet :** si le serveur école reste joignable depuis un autre réseau (port exposé, VPN, route inter-LAN, IP publique, etc.), l’app affiche **Mode Local** et **autorise les inscriptions**.

### 2. Priorité dangereuse : dernière IP en premier

`_run()` commence par la dernière IP connue. Si elle répond → Local immédiat, **avant** mDNS / scan / cloud.

Scénario typique :

1. Connexion à l’école → last URL = `http://192.168.x.y:5096` sauvegardée.
2. Passage sur un autre réseau.
3. Si cette IP répond encore (même brièvement) → **Mode Local** + écritures.

### 3. Recheck silencieux au changement de réseau

`ConnectionModeNotifier` :

- changement de connectivité → `refresh(silent: true)` ;
- silent → `probe(full: false)` → **`recheck()`**.

`recheck()` :

1. Probe `current.baseUrl` + lastKnown ;
2. Si OK → **`DiscoveryMode.local`** ;
3. Sinon seulement → cloud, puis rediscovery complète.

Donc un changement de réseau **ne force pas** une redécouverte complète tant que l’ancienne IP locale répond.

### 4. Champ `health.server` ignoré

L’API (`LocalDiscoveryHealthController`) renvoie :

```json
{ "status": "ok", "server": "local" | "cloud", "school": "...", ... }
```

Flutter parse `HealthInfo.server` mais **ne s’en sert jamais** pour choisir Local vs Distant.

### 5. `ApiConfig.localBaseUrlCandidates` hors découverte

Les `--dart-define=LOCAL_API_*` (script `run-on-phone.ps1`) ne pilotent **pas** `LocalServerDiscovery._run`.  
Ils servent surtout de fallback Dio (`app_providers` / auth) si `snap.baseUrl` est absent — autre surface de confusion possible, secondaire ici.

### 6. Serveur Cloud correctement bloqué (si Role=Cloud)

`CloudReadOnlyMiddleware` refuse les POST d’inscription sur instance Cloud.  
Cela **ne protège pas** si le client est en Mode Local contre une **API Role=Local** joignable hors Wi‑Fi école (cas le plus probable de l’incident).

---

## Matrice « symptôme → responsable »

| Observation | Composant | Cause |
|-------------|-----------|--------|
| Bannière « Mode Local » hors Wi‑Fi école | **Discovery** | IP locale encore joignable / lastKnown / recheck soft |
| Boutons inscription actifs | WritePolicy | Comportement attendu **si** mode = Local |
| Inscription aboutie | Client + API Local | API Locale (Role=Local) accepte les écritures |
| Pas de bascule Distant malgré 4G / autre Wi‑Fi | Recheck silent | Ne tombe sur le cloud que si last IP **échoue** |

Ce n’est **pas** un bug du wizard d’inscription isolé : c’est une **violation de la définition conventionnelle de Mode Local**.

---

## Ce qui respecte déjà la convention

1. Pas de sélecteur manuel de serveur dans l’UI.
2. Labels / sous-titres Mode Local / Distant / Cache.
3. Inscriptions gated côté mobile sur Local uniquement.
4. Notes autorisées aussi en Distant (`allowsGradeWrites`).
5. Middleware lecture seule sur déploiement Cloud.

---

## Correctifs appliqués (2026-08-03)

1. **Local uniquement si** même `/24` + URL ≠ cloud + `health.server ≠ cloud`.
2. **Changement de réseau** → `refresh` full (rediscovery), plus recheck soft seul.
3. **lastKnown hors sous-réseau** → clear + poursuite Distant/offline.
4. Probe sans JSON valide → **refus** (plus de fallback forcé `server=local`).

Voir implémentation : `local_server_discovery.dart`, `connection_mode_notifier.dart`.

## Correctifs recommandés (hors scope audit — à faire ensuite)

~~Ordre suggéré…~~ → **appliqués** (section ci-dessus).

Ancienne liste conservée pour historique de l’audit :

1. Classer Local seulement si même sous-réseau + pas cloud + health local.
2. Sur changement de connectivité : `rediscover()` full.
3. lastKnown hors sous-réseau → clear.
4. Logs `[Discovery]` détaillés.
---

## Procédure de reprouve (terrain)

1. Sur Wi‑Fi école : noter bannière + URL (tap bannière / logs `[Discovery]`).
2. Passer sur autre Wi‑Fi / 4G.
3. Attendre ≥ 3 s (debounce) + éventuel refresh manuel bannière.
4. Vérifier :
   - mode affiché ;
   - logs : lastKnown probe success ? ;
   - boutons Nouvelle inscription / Réinscription enabled ?
5. Corréler avec `adb logcat | findstr Discovery`.

---

## Synthèse une phrase

**La convention « Local = même Wi‑Fi école » n’est pas appliquée : le code assimile « serveur health joignable via last IP / mDNS / scan » à Local, donc hors réseau école l’app peut rester en Mode Local et ouvrir les inscriptions ; le garde-fou WritePolicy est correct mais se fie à une détection trompeuse.**
