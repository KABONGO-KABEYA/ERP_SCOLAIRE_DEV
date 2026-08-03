# Brief design — Application mobile ERP Administration Scolaire RDC

> **Objectif de ce fichier**  
> Document de référence **complet** de l’application Flutter déjà en place, pour qu’un designer / Claude puisse proposer un **nouveau design UI/UX** sans explorer le code.  
> Ne pas inventer de modules absents : s’appuyer uniquement sur ce qui est décrit ici.

| | |
|---|---|
| **Produit** | ERP Administration Scolaire RDC |
| **App** | `school_management_mobile` (Flutter) |
| **Chemin** | `mobile/school_management_mobile/` |
| **Version** | 1.0.1+2 |
| **SDK** | Dart ≥ 3.5 |
| **Plateformes actuelles** | Android (principal), Web, Windows — pas de dossier iOS dédié |
| **Backend** | ASP.NET API (`/api/v1/...`) — local école + cloud |

---

## 1. Vision produit

Application mobile multi-rôles pour les établissements scolaires en **RDC** :

- Les **parents** consultent scolarité, paiements, notes, bulletins (freemium).
- Les **enseignants** saisissent les notes / cotations.
- Le **secrétariat / admin** inscrit et gère les dossiers élèves (en Wi‑Fi local).
- Le **promoteur / direction** suit les indicateurs financiers et pédagogiques.

Contrainte métier forte : **connexion intelligente Local / Distant / Cache** (l’utilisateur ne choisit jamais manuellement le serveur).

L’UI Desktop WPF (navy `#0B1F47`, bleu `#1D4ED8`) est la référence visuelle actuelle. Le mobile doit rester **cohérent** avec cette identité, tout en étant pensé **mobile-first** (pouce, lisibilité, offline).

---

## 2. Stack technique (à respecter dans le redesign)

| Couche | Choix |
|--------|--------|
| UI | Flutter Material 3 |
| État | Riverpod |
| Navigation | go_router |
| HTTP | Dio + JWT (refresh auto) |
| Sécurité session | flutter_secure_storage |
| Offline parent | Hive (`parent_offline_v1`) |
| Graphiques | fl_chart (promoteur) |
| Thème live | `lib/core/theme/erp_theme.dart` |

> ⚠️ `STYLE.md` à la racine du repo est **obsolète** (Gilroy, `#283073`, `#8DBF42`).  
> **Source de vérité design code** = `erp_theme.dart` ci-dessous.

---

## 3. Design system actuel (live)

### 3.1 Couleurs

| Token | Hex | Usage |
|-------|-----|--------|
| Navy / marque | `#0B1F47` | Brand, icône adaptive, accents forts |
| Primary | `#1D4ED8` | Boutons, sélection nav, focus |
| Primary legacy | `#1E5EFF` | Launcher / rétrocompat |
| Sidebar | `#0F1F3D` | Alignement desktop |
| Fond page | `#F5F7FB` | Scaffold clair |
| Carte | `#FFFFFF` | Surfaces |
| Texte | `#1F2937` | Titres / corps |
| Texte secondaire | `#6B7280` | Hints, meta |
| Bordure | `#E5E7EB` | Cards, inputs |
| Succès | `#22C55E` | Mode local, OK |
| Warning | `#F59E0B` | Alertes soft |
| Danger | `#EF4444` | Erreurs, mode cache |
| Fond dark | `#111827` | Mode sombre |
| Carte dark | `#1F2937` | Mode sombre |

**Indicateurs de connexion (bannière permanente en haut)**  

| Mode | Couleur indicative | Signification |
|------|--------------------|---------------|
| Local | Vert | Même Wi‑Fi que le serveur école — lecture + écriture |
| Distant | Bleu | Cloud / 4G — lecture (+ notes enseignant) |
| Cache | Rouge | Hors ligne — consultation cache parent |

### 3.2 Typographie & densités

- Police : **Segoe UI** (pas de font custom bundlée aujourd’hui)
- Headline large : 28 / w600  
- Headline medium : 22 / bold  
- Title large : 18 / w600  
- Body large : 14 / w500  
- Body medium : 13 / regular (secondaire)

### 3.3 Espacements & formes

| Token | Valeur |
|-------|--------|
| Padding page | 20 |
| Padding carte | 20 |
| Section | 16 |
| Radius carte | 16 |
| Radius bouton / input | 10 |
| Hauteur bouton | 42 |

Composant récurrent : **`ErpCard`** — fond blanc, bordure légère, ombre soft, coins 16.

### 3.4 Thème

- Light + Dark (`ThemeMode.system`)
- AppBar : fond carte, elevation 0, titre à gauche
- NavigationBar parent : indicateur primary translucide

---

## 4. Architecture de navigation

```
/login
   │
   ├─ Parent ────────── /parent/*     (shell + bottom nav 7 onglets)
   ├─ Enseignant ────── /teacher/*    (stack linéaire)
   ├─ Secrétariat/Admin /secretary/*  (hub cartes)
   ├─ Promoteur ─────── /promoteur/*  (dashboard + détails)
   └─ Direction ─────── home = /promoteur/dashboard
                        (écran /direction/dashboard existe mais n’est pas le home)
```

**Pas de Drawer.**  
Seul le **parent** a une bottom navigation (7 destinations, scroll horizontal, badges cadenas premium).

---

## 5. Rôles & parcours détaillés

### 5.1 Authentification — `/login`

- Formulaire identifiant / mot de passe
- Panel de marque responsive (logo / nom « ERP Scolaire RDC »)
- Après login → redirection selon rôle (`AuthStorage.homeRoute`)
- Logout depuis profil / compte

Comptes démo (doc) :

| Rôle | Identifiant | Mot de passe |
|------|-------------|--------------|
| Parent | `parent` | `Parent@2026` |
| Enseignant | `enseignant` | `Teacher@2026` |
| Direction | `direction` | `Direction@2026` |
| Admin | `admin` | `Admin@2026` |

---

### 5.2 Parent (persona la plus riche) — freemium

**Shell** : `ParentShellScreen` + `StatefulShellRoute`

| # | Onglet | Route | Accès Free | Accès Premium |
|---|--------|-------|------------|---------------|
| 0 | Accueil | `/parent/home` | ✅ | ✅ |
| 1 | Paiements | `/parent/payments` | ✅ | ✅ |
| 2 | Notes | `/parent/notes` | 🔒 | ✅ |
| 3 | Bulletins | `/parent/bulletins` | 🔒 | ✅ |
| 4 | Comms | `/parent/communications` | 🔒 | ✅ |
| 5 | Notifs | `/parent/notifications` | 🔒 | ✅ |
| 6 | Profil | `/parent/profile` | ✅ | ✅ |

**Écrans secondaires parent**

| Route | Contenu |
|-------|---------|
| `/parent/attendance` | Présences (premium) |
| `/parent/change-password` | Changement mot de passe |
| `/parent/subscription` | Offre Premium |
| `/parent/subscription/payment-method` | Choix Mobile Money |
| `/parent/subscription/phone` | Saisie numéro |
| `/parent/subscription/confirm` | Confirmation paiement |
| `/parent/subscription/status` | Statut paiement |
| `/parent/subscription/success` | Succès |
| `/parent/subscription/history` | Historique abonnements |

**Contenu fonctionnel parent**

- Sélecteur d’enfants (frère/sœur)
- Dashboard : résumé scolarité, alertes, CTA Premium si free
- Paiements : situations de frais, historique, reçus PDF (ZIP si premium)
- Notes : résultats par période / cours
- Bulletins : liste + ouverture PDF
- Communications : messages école ↔ parent
- Notifications : centre d’alertes
- Présences : suivi absences / retards
- Profil : infos compte, école, abonnement, déconnexion

**Assets paiement**  
`assets/images/payments/` → Airtel Money, M-Pesa, Orange Money

**Flags premium (`ParentFeatureFlags`)**  
Free = payments + profile ; Premium = notes, bulletins, communications, notifications, attendance.

---

### 5.3 Enseignant — cotation

Parcours linéaire :

1. `/teacher/assignments` — mes classes  
2. `/teacher/classes/:classRoomId/courses` — cours de la classe  
3. `.../evaluations` — évaluations du cours  
4. `/teacher/evaluations/:evaluationId/grades` — saisie des notes  

Autorisé en **Local et Distant** (écriture notes même cloud).

---

### 5.4 Secrétariat / Admin — inscriptions

| Route | Écran |
|-------|-------|
| `/secretary/home` | Hub (cartes d’actions) |
| `/secretary/students` | Recherche élèves |
| `/secretary/students/:studentId` | Dossier + documents |
| `/secretary/enrollment?mode=new\|re` | Wizard 6 étapes |
| `/secretary/account` | Compte |
| `/secretary/account/change-password` | Mot de passe |
| `/secretary/account/about` | À propos |

**Wizard inscription (6 étapes)**  
1. Identité élève  
2. Infos scolaires  
3. Responsables / tuteurs  
4. Santé  
5. Documents  
6. Validation  

**Règle d’écriture** : inscriptions / docs **uniquement en mode Local** (école). Cloud = lecture.

---

### 5.5 Promoteur — pilotage financier

Home : `/promoteur/dashboard`

KPI / détails :

- Encaissements  
- Recette mois / année  
- Dépenses  
- Débiteurs  
- Fonds (par destination)  
- Élèves inscrits  
- Graphiques (fl_chart)  
- Cache mémoire court (~20 s) sur l’overview  

---

### 5.6 Direction

- Home actuel = **même dashboard promoteur**  
- Écran alternatif `/direction/dashboard` : rapports (stats, financier, effectifs par classe, moyennes) — à considérer dans un redesign de navigation Direction

---

### 5.7 Non implémenté (ne pas designer comme existant)

- Rôle **élève**  
- Paramétrage école / frais / années scolaires (réservé Desktop)  
- Drawer global  
- Bottom nav pour autres rôles  

---

## 6. Modes de connexion (UX de 1er niveau)

L’utilisateur **ne choisit jamais** le serveur.

Ordre de détection :

1. API locale joignable (même Wi‑Fi) → **Mode Local**  
2. Sinon cloud joignable → **Mode Distant**  
3. Sinon → **Mode Cache** (Hive parent)

Découverte : mDNS `_school-management._tcp` → dernière IP → scan sous-réseau port **5096**.

Bannière de connexion **toujours visible** — le redesign doit la conserver (ou proposer un équivalent très lisible).

Politique d’écriture :

| Action | Local | Distant | Cache |
|--------|-------|---------|-------|
| Login | ✅ | ✅ | ❌ (besoin serveur) |
| Lecture parent | ✅ | ✅ | ✅ (cache) |
| Inscriptions / docs | ✅ | ❌ | ❌ |
| Saisie notes enseignant | ✅ | ✅ | ❌ |
| Paiement premium | selon API | selon API | ❌ |

---

## 7. Structure des écrans (inventaire pour wireframes)

### Auth
- [ ] Login (brand + formulaire + états erreur / chargement)

### Parent
- [ ] Shell + bottom nav (7 items, locks premium)
- [ ] Accueil / dashboard multi-enfants
- [ ] Paiements + détail frais + reçu
- [ ] Notes
- [ ] Bulletins + viewer PDF
- [ ] Communications
- [ ] Notifications
- [ ] Profil
- [ ] Présences
- [ ] Tunnel abonnement Premium (offre → moyen → téléphone → confirm → statut → succès → historique)
- [ ] Changer mot de passe
- [ ] États : free gate / empty / offline cache / erreur réseau

### Enseignant
- [ ] Liste classes
- [ ] Liste cours
- [ ] Liste évaluations (+ création)
- [ ] Grille saisie notes

### Secrétariat
- [ ] Hub
- [ ] Recherche élèves
- [ ] Dossier élève + documents
- [ ] Wizard 6 steps inscription / réinscription
- [ ] Compte / à propos / mot de passe

### Promoteur / Direction
- [ ] Dashboard financier (cartes KPI + graphiques)
- [ ] Écrans détail (encaissements, recettes, dépenses, débiteurs, fonds, élèves)
- [ ] (Option) Dashboard Direction pédagogique distinct

### Transversal
- [ ] Bannière mode connexion
- [ ] Dialog mise à jour OTA (APK)
- [ ] États loading / empty / error réutilisables

---

## 8. Contenu typique des écrans (données affichées)

### Dashboard parent
- Nom école, nom parent  
- Enfant sélectionné (photo / initiales, classe)  
- Résumé paiements (solde, statut)  
- Accès rapide notes / bulletins (si premium)  
- Bannière upgrade Premium si free  

### Paiements parent
- Liste situations de frais  
- Montants dus / payés / reste  
- Historique versements  
- Actions : voir reçu PDF  

### Notes / bulletins
- Périodes pédagogiques  
- Moyennes, pourcentages, mentions  
- Détail par cours  
- Téléchargement / ouverture bulletin PDF  

### Dashboard promoteur
- Chiffres clés du jour / mois / année  
- Listes (débiteurs, mouvements de fonds)  
- Graphiques d’évolution  

### Saisie notes enseignant
- Liste élèves de la classe  
- Note / absence / observation  
- Enregistrement batch  

---

## 9. Contraintes UX pour toute proposition de design

1. **Mobile-first Android** (écrans 5–7", zones pouce).  
2. **Cohérence multi-rôles** : même langage visuel, layouts adaptés au métier.  
3. **Freemium parent** : locks clairs + CTA Premium, sans cacher complètement les onglets.  
4. **Connexion Local/Distant/Cache** : statut toujours compréhensible.  
5. **Offline parent** : écrans consultables avec indication « données en cache ».  
6. **Accessibilité** : contrastes AA, tailles tactiles ≥ 44–48 dp.  
7. **Pas de calcul métier dans l’UI** : l’API / PeriodResult est source de vérité (notes, mentions, soldes).  
8. **Alignement marque** avec Desktop : navy + bleu primaire (sauf si proposition de rebrand assumée et documentée).  
9. **Bottom nav parent déjà chargée (7 items)** : proposer une IA plus claire (regroupement, FAB, onglets secondaires…) si pertinent.  
10. **Paiements Mobile Money RDC** : conserver les 3 opérateurs et leurs logos.

---

## 10. Brief pour Claude / designer — livrables attendus

Proposer un design moderne pour cette app **telle qu’elle existe**, en livrant :

1. **Direction artistique** (moodboard 1 page : tonalité, photo/illustration, UI chrome).  
2. **Design system mis à jour** (couleurs, type scale, radii, elevation, composants : boutons, cards, chips, nav, inputs, banners, locks premium).  
3. **Wireframes / mockups haute fidélité** par rôle :  
   - Parent (home, paiements, notes, premium paywall, profil)  
   - Enseignant (liste → saisie notes)  
   - Secrétariat (hub + 1 step wizard)  
   - Promoteur (dashboard)  
4. **Proposition de navigation** (surtout parent 7 onglets + direction vs promoteur).  
5. **États** : loading, empty, erreur, offline, free vs premium.  
6. **Spécifications** réutilisables en Flutter (tokens nommés compatibles avec `ErpColors` / `ErpSpacing`).

### Questions ouvertes (le design peut trancher)

- Regrouper Notes / Bulletins / Présences sous un hub « Scolarité » ?  
- Séparer clairement Direction et Promoteur dans la nav ?  
- Remplacer Segoe UI par une police expressive (tout en restant lisible) ?  
- Introduire un accent secondaire RDC (vert/or) **sans** retomber dans le violet générique AI ?  
- Mode sombre : renforcer ou simplifier ?

### À éviter (biais design AI)

- Thème violet / indigo par défaut  
- Fond crème + serif terracotta générique  
- Trop de cards / pills / ombres multicouches  
- Hero marketing sur les écrans métier  
- Calculs affichés « inventés » côté UI  

---

## 11. Fichiers techniques utiles

| Fichier | Rôle |
|---------|------|
| `mobile/school_management_mobile/lib/main.dart` | Entrée |
| `mobile/school_management_mobile/lib/app.dart` | MaterialApp + bannière connexion + updates |
| `mobile/school_management_mobile/lib/router/app_router.dart` | Toutes les routes |
| `mobile/school_management_mobile/lib/core/theme/erp_theme.dart` | Design system live |
| `mobile/school_management_mobile/lib/core/auth/auth_storage.dart` | Rôles → home |
| `mobile/school_management_mobile/lib/core/connection/` | Modes Local / Distant / Cache |
| `mobile/school_management_mobile/lib/features/parent/parent_shell_screen.dart` | Bottom nav parent |
| `mobile/school_management_mobile/lib/features/parent/models/parent_models.dart` | Freemium flags |
| `mobile/school_management_mobile/README.md` | Connexion & comptes démo |
| `docs/architecture.md` | Local ↔ Cloud |
| `docs/architecture/local-server-discovery.md` | mDNS / scan |

---

## 12. Résumé en une phrase

> Application Flutter multi-rôles (Parent freemium, Enseignant cotation, Secrétariat inscriptions locales, Promoteur/Direction finance) connectée automatiquement Local/Distant/Cache, déjà structurée autour d’un design system navy/bleu Material 3 — à redesignier pour plus de clarté, de hiérarchie et d’identité RDC, **sans changer le périmètre fonctionnel**.

---

*Document généré pour brief design — base code : `mobile/school_management_mobile`.*
