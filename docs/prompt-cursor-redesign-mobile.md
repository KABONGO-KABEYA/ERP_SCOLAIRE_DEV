# Prompt Cursor — Redesign UI/UX `school_management_mobile`

> À coller tel quel dans Cursor, sur le repo `mobile/school_management_mobile/`.
> Objectif : moderniser l'UI/UX de l'app Flutter existante **sans changer le périmètre fonctionnel** (routes, rôles, logique métier, offline/local-distant-cache).

---

## 0. Contexte à respecter absolument

- Ne pas toucher à la logique métier, aux routes de `app_router.dart`, aux modèles, ni au comportement de connexion Local/Distant/Cache.
- Ne pas inventer d'écrans ou de fonctionnalités absentes du code actuel.
- Source de vérité design actuelle : `lib/core/theme/erp_theme.dart`. `STYLE.md` à la racine est obsolète, l'ignorer.
- Tout changement visuel doit rester compatible Material 3 + Riverpod + go_router déjà en place.

---

## 1. Design system — à mettre à jour dans `erp_theme.dart`

### 1.1 Couleurs

Garder la base actuelle (cohérence avec le Desktop WPF) et ajouter un accent secondaire RDC :

```dart
// Marque (inchangé)
static const navy = Color(0xFF0B1F47);
static const primary = Color(0xFF1D4ED8);
static const primaryLegacy = Color(0xFF1E5EFF);
static const sidebar = Color(0xFF0F1F3D);

// Nouveau — accent secondaire RDC (à utiliser avec parcimonie : badges premium succès, highlights, jamais en accent principal)
static const accentGreen = Color(0xFF0E8A5F);
static const accentGold = Color(0xFFD9A441);

// Neutres (inchangé)
static const pageBackground = Color(0xFFF5F7FB);
static const cardBackground = Color(0xFFFFFFFF);
static const textPrimary = Color(0xFF1F2937);
static const textSecondary = Color(0xFF6B7280);
static const border = Color(0xFFE5E7EB);

// États connexion (inchangé)
static const modeLocal = Color(0xFF22C55E);
static const modeDistant = Color(0xFF1D4ED8); // aligné sur primary, pas de couleur dédiée
static const modeCache = Color(0xFFEF4444);

// Sémantique
static const success = Color(0xFF22C55E);
static const warning = Color(0xFFF59E0B);
static const danger = Color(0xFFEF4444);

// Dark mode (inchangé)
static const darkBackground = Color(0xFF111827);
static const darkCard = Color(0xFF1F2937);
```

**Règle d'usage** : `accentGreen`/`accentGold` ne remplacent jamais `primary`. Ils servent uniquement pour : badge "Premium" (gold), état de succès financier ou pédagogique (green), petites touches d'identité (icône app, écran de succès abonnement). Ne pas les utiliser comme couleur de CTA principal — le bleu `primary` reste l'accent d'action.

### 1.2 Typographie

- Conserver la police système actuelle (pas de police custom bundlée — trop de risque de régression offline/poids APK). Ne pas remplacer par une police "expressive".
- Garder l'échelle existante : 28/w600, 22/bold, 18/w600, 14/w500, 13/regular.
- Améliorer uniquement le `letter-spacing` des headlines (-0.2) pour un rendu plus premium sans changer la police.

### 1.3 Espacements, radius, composants

- Garder : padding page 20, padding carte 20, section 16, radius carte 16, radius bouton/input 10, hauteur bouton 42.
- `ErpCard` : ne pas ajouter d'ombre supplémentaire. Garder fond blanc + bordure légère + radius 16. Éviter l'empilement de shadows (biais IA à éviter listé dans le brief).
- Nouveau composant `ErpBanner` (voir §2) et `ErpLockChip` (voir §3.2) à créer.

---

## 2. Bannière de connexion — refonte visuelle (comportement inchangé)

Fichier concerné : `lib/app.dart` (et son widget de bannière).

- Remplacer la bannière plate actuelle par une pill compacte en haut de l'app (pas toute la largeur en bloc plein), avec icône + libellé court :
  - Local → fond `success.withOpacity(0.12)`, texte `success` foncé, icône `Icons.wifi`, texte "Mode local — [nom école]"
  - Distant → fond `primary.withOpacity(0.12)`, texte `primary` foncé, icône `Icons.cloud_outlined`, texte "Mode distant"
  - Cache → fond `danger.withOpacity(0.12)`, texte `danger` foncé, icône `Icons.cloud_off_outlined`, texte "Mode cache — hors ligne"
- Toujours visible, hauteur réduite (~32dp), ne doit jamais recouvrir le contenu ni l'AppBar.
- Icône + couleur + texte (jamais la couleur seule) pour l'accessibilité.

---

## 3. Navigation — proposition à implémenter

Fichier concerné : `lib/features/parent/parent_shell_screen.dart`, `lib/router/app_router.dart` (routes enfants à conserver, seul le regroupement visuel change).

### 3.1 Bottom nav parent : de 7 items visibles à 5

Actuellement 7 onglets en scroll horizontal. Remplacer par 5 destinations principales, en regroupant sous un hub "Scolarité" :

| # | Onglet affiché | Regroupe |
|---|---|---|
| 0 | Accueil | (inchangé) `/parent/home` |
| 1 | Paiements | (inchangé) `/parent/payments` |
| 2 | **Scolarité** (nouveau hub) | Notes + Bulletins + Présences (accès en cartes dans un écran intermédiaire) |
| 3 | **Messages** (renommé) | Communications + Notifications (badge de compteur si non lus) |
| 4 | Profil | (inchangé) `/parent/profile` |

- Les routes existantes (`/parent/notes`, `/parent/bulletins`, `/parent/attendance`, `/parent/communications`, `/parent/notifications`) restent telles quelles dans `go_router` — on ajoute simplement un écran hub `/parent/scolarite` et on redirige les items de nav vers ce hub au lieu de la route directe.
- Garder les cadenas premium visibles sur les items verrouillés à l'intérieur du hub (pas de masquage complet — contrainte du brief).
- Si vous préférez rester sur 7 items sans hub, implémenter en variante B : garder 7 items mais réduire la taille des icônes/labels et améliorer le scroll indicator. Je recommande la variante hub (A) pour la clarté mobile.

### 3.2 Chip verrouillage premium

Nouveau composant `ErpLockChip` : badge arrondi, fond `accentGold.withOpacity(0.15)`, texte `accentGold` foncé, icône cadenas 12px, texte "Premium". Remplace l'affichage actuel du verrou (à identifier dans le code parent — probablement dans `parent_models.dart` / les widgets de tab).

---

## 4. Écrans à retravailler visuellement (sans changer le contenu/data)

Pour chaque écran ci-dessous : appliquer le design system §1, garder les data bindings existants, ne pas changer les routes.

### Parent
- `/parent/home` : mettre en avant le solde du trimestre dans un bloc `navy` plein (au lieu d'une card blanche parmi d'autres), sélecteur enfants en pills horizontales scrollables, accès rapide Notes/Bulletins avec `ErpLockChip` si free, une seule bannière upsell Premium (pas une par carte).
- `/parent/payments` : liste des situations de frais en cards, montants dus/payés en couleurs sémantiques (danger si reste à payer, success si soldé), logos Airtel Money / M-Pesa / Orange Money conservés tels quels dans `assets/images/payments/`.
- Tunnel abonnement (`/parent/subscription/*`) : garder le flow existant, uniformiser chaque étape avec le nouveau design system (stepper visuel simple en haut, pas de nouvelle étape ajoutée).
- États free/empty/offline/erreur : créer des widgets réutilisables `ErpEmptyState`, `ErpOfflineState`, `ErpErrorState` avec icône + titre + description courte + action optionnelle.

### Enseignant
- Parcours linéaire classes → cours → évaluations → notes : garder la structure, uniformiser les listes en `ErpCard`, grille de saisie de notes avec inputs plus larges (zone tactile ≥ 44dp), sauvegarde batch avec feedback visuel clair (pas de calcul de moyenne côté UI — uniquement affichage de ce que l'API retourne).

### Secrétariat
- Hub `/secretary/home` : cartes d'action avec icônes, pas de texte long.
- Wizard inscription 6 étapes : stepper horizontal en haut (étape actuelle en `primary`, étapes suivantes en gris), boutons précédent/suivant fixes en bas d'écran.

### Promoteur / Direction
- Dashboard : cards KPI en grille 2 colonnes, valeurs en gros caractères (22/w500), variation période en petit texte sémantique (vert si positif, rouge si négatif). Graphiques `fl_chart` : garder la lib, styliser avec la palette du design system (primary pour la courbe principale, accentGreen pour une éventuelle comparaison).

---

## 5. États transversaux à créer

Widgets réutilisables dans `lib/core/widgets/` (ou dossier équivalent existant) :
- `ErpLoadingState` — skeleton ou spinner simple, cohérent avec le radius des cards.
- `ErpEmptyState` — icône + titre + description.
- `ErpOfflineState` — variante avec mention explicite "Données en cache" pour le mode Cache parent.
- `ErpErrorState` — message + bouton "Réessayer".

---

## 6. Contraintes non négociables (rappel)

1. Mobile-first Android, zones tactiles ≥ 44–48dp.
2. Ne jamais masquer complètement un onglet/fonctionnalité premium — toujours visible avec verrou.
3. Bannière de connexion toujours présente, jamais supprimée.
4. Pas de calcul métier dans l'UI (notes, moyennes, soldes viennent uniquement de l'API).
5. Pas de violet/indigo comme couleur dominante, pas de fond crème + serif terracotta, pas d'empilement de cards/pills/ombres, pas de hero marketing sur les écrans métier.
6. Aucune nouvelle dépendance lourde (pas de nouvelle police custom, pas de nouvelle lib d'icônes si évitable — utiliser `Icons` Material déjà disponibles).

---

## 7. Livrable attendu de Cursor

- Modifications de `erp_theme.dart` (tokens ci-dessus).
- Nouveaux widgets réutilisables (`ErpBanner`, `ErpLockChip`, `ErpEmptyState`, `ErpOfflineState`, `ErpErrorState`, `ErpLoadingState`).
- Refonte visuelle des écrans listés en §4, sans modification de la logique de data/état (Riverpod providers inchangés).
- Si la restructuration de nav (§3.1) est appliquée : nouvel écran hub `/parent/scolarite` + mise à jour de `parent_shell_screen.dart`, routes enfants existantes conservées dans `app_router.dart`.
- Aucune régression sur les comptes démo et le comportement Local/Distant/Cache existant.
