# Guide style et design — ERP Administration Scolaire (Mobile)

Ce document décrit les règles de style UI à suivre dans l'application Flutter mobile afin de garder une interface cohérente.

> Source technique dans le projet : `mobile/school_management_mobile/lib/core/theme/erp_theme.dart` (`ErpColors`, `AppGaps`, `ErpTheme`, `AppText` / `AppTextLarge`).

## 1) Identite visuelle

### Palette principale

Source: `mobile/school_management_mobile/lib/core/theme/erp_theme.dart`

- `primaryColor`: `#283073` (bleu principal, marque)
- `secondaryColor`: `#8DBF42` (vert accent, actions positives)
- `tertiaryColor`: `#B5CCD8` (bleu clair support)
- `alertColor`: `#F21D52` (erreurs, alertes)
- `backgroundColor`: `#F6F6F6` (fond global des ecrans)

### Couleurs de texte

- `primaryFontColor`: `#2C3E50` (titres, texte prioritaire)
- `secondaryFontColor`: `#34495E` (texte secondaire, hints)

### Couleurs utilitaires

- `buttonPressedColor`: `#8DBF42`
- `facebookBackgroundColor`: `#283073`
- `borderColor`: `secondaryFontColor` avec opacite 20%

## 2) Typographie

Source: `erp_theme.dart` (`ErpTheme`, `AppText`, `AppTextLarge`)

- Police par defaut: `Gilroy`
- `TextTheme.labelLarge`: 16, semi-bold (`w600`)
- `TextTheme.bodyLarge`: 24, `primaryFontColor`, semi-bold (`w600`)
- `TextTheme.bodyMedium`: 16, `primaryFontColor`, regular (`w400`)

Composants texte utilitaires:

- `AppText`: texte normal, `Gilroy`, poids normal
- `AppTextLarge`: texte fort, `Gilroy`, poids `w900` (titres impactants)

Regle: reutiliser les styles du `ThemeData` et les widgets texte internes avant de creer de nouveaux styles.

## 3) Espacements et layout

Source: `erp_theme.dart` (`AppGaps`)

- Utiliser les gaps standards (`AppGaps.hGap*` et `AppGaps.wGap*`) pour les espacements repetitifs
- Padding horizontal d'ecran standard: `AppGaps.screenPaddingValue` (= 10)
- Padding minimum bas: `AppGaps.minimumBottomPaddingValue` (= 5)
- Bottom nav padding standard: `AppGaps.bottomNavBarPadding`

Regle: eviter les `SizedBox(height: x)` ou `EdgeInsets` "hardcodes" quand une constante `AppGaps` existe deja.

## 4) Theme global Flutter

Source: `erp_theme.dart`, `lib/app.dart`

- Theme applique globalement via `AppThemeData.appThemeData` dans `MaterialApp`
- `brightness`: clair
- `scaffoldBackgroundColor`: `AppColors.backgroundColor`
- `primarySwatch`: derive de `AppColors.primaryColor` via `generateMaterialColor`

### AppBar

- Fond transparent
- Elevation a 0
- Titre centre
- Style titre: `Gilroy`, 24, `primaryFontColor`, `w600`

### Inputs

- Style dense/collapse
- Hint en `secondaryFontColor`
- Bordure underline avec `secondaryFontColor` opacite 30%

## 5) Composants et etats UI

Regles recommandees:

- Actions principales: utiliser `primaryColor`
- Actions secondaires/validation: utiliser `secondaryColor`
- Etats d'erreur et messages critiques: utiliser `alertColor`
- Arriere-plans de pages: conserver `backgroundColor`
- Separateurs: utiliser la logique de `dividerColor` du theme (opacite legere)

## 6) Cohabitation avec l'existant

Pour toute nouvelle page ou composant:

1. Partir du `ThemeData` existant
2. Reutiliser `AppColors`, `AppGaps`, `AppText`/`AppTextLarge`
3. Eviter de dupliquer des styles inline deja disponibles
4. Ajouter une constante dans `AppColors`/`AppGaps` si un nouveau besoin devient recurrent

## 7) Rappel pratique (checklist rapide)

- [ ] Police `Gilroy` conservee
- [ ] Couleurs prises depuis `AppColors`
- [ ] Espacements pris depuis `AppGaps`
- [ ] AppBar et champs alignes sur le theme global
- [ ] Etats erreur/succes conformes (`alertColor`/`secondaryColor`)

## 8) Design produit (UX/UI)

Cette section complete la partie "style" avec des regles de design d'interface pour la coherence visuelle et l'experience utilisateur.

### Principes directeurs

- Simplicite medicale: information claire, priorite a la lisibilite
- Confiance: interfaces stables, peu de surprises, vocabulary sobre
- Guidance: chaque ecran doit guider vers une action evidente
- Continuite: memes composants, memes comportements, memes espacements

### Hierarchie visuelle

- Titre d'ecran visible et unique en haut de page
- Une action principale par ecran (CTA prioritaire)
- Grouper les informations par cartes/sections courtes
- Eviter de melanger plusieurs niveaux d'importance dans un meme bloc

### Composants (usage recommande)

- Bouton primaire: fond `primaryColor`, texte blanc, reserve au CTA principal
- Bouton secondaire: style plus discret, pour alternatives non critiques
- Champs de formulaire: labels clairs + messages d'erreur contextualises
- Cartes d'information: fond clair, contenu aerien, separation nette
- Listes: separateurs legers, actions rapides explicites

### Etats et feedback utilisateur

- Chargement: indicateur visible (skeleton/spinner) sans bloquer inutilement
- Succes: confirmation courte et rassurante
- Erreur: message actionnable ("quoi faire ensuite"), couleur `alertColor`
- Vide: expliquer la raison et proposer l'action de sortie
- Desactive: contraste suffisant, raison implicite ou explicite

### Iconographie et imagerie

- Icons simples, lineaires, coherentes en taille/epaisseur
- Eviter les melanges de styles d'icones sur le meme ecran
- Images medecins/patients: cadrage uniforme et propre
- Illustrations secondaires: ne jamais concurrencer le CTA principal

### Accessibilite (minimum requis)

- Contraste texte/fond suffisant sur tous les ecrans
- Taille de texte lisible (eviter < 14 pour le texte courant)
- Zones tactiles confortables (au moins 44x44)
- Informations importantes jamais transmises uniquement par la couleur
- Messages d'erreur comprehensibles par lecture vocale

### Micro-interactions

- Animations courtes et utiles (150 a 300 ms)
- Transitions fluides mais discretes
- Etats pressed/hover/focus visuellement distincts
- Ne pas sur-animer les ecrans cliniques (focus sur l'information)

### Ecrans cles (orientation design)

- Authentification: parcours court, rassurant, messages de securite explicites
- Prise de rendez-vous: etapes visibles, recap avant validation
- Chat/IA: differencier clairement systeme, medecin et patient
- Profil patient: sections claires, edition progressive, confirmations visibles

## 9) Workflow de creation UI

Pour chaque nouvelle fonctionnalite:

1. Definir l'objectif utilisateur principal de l'ecran
2. Maquetter la hierarchie (titre, contenu, CTA)
3. Appliquer tokens existants (`AppColors`, `AppGaps`, typo)
4. Verifier etats (loading, vide, erreur, succes)
5. Verifier accessibilite et coherence avec les ecrans voisins

