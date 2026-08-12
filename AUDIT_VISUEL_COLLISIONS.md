# Audit visuel — Collisions PhotoPath

**Date :** 2026-08-12  
**Racine source :** `\\Desktop-ct9vndv\erp_scolaire`  
**Dossier de travail :** `AUDIT_VISUEL_COLLISIONS/`  
**Base de données :** non modifiée  
**Filesystem source :** non modifié (copies uniquement)

```text
AUDIT VISUEL : TERMINÉ
DONNÉES SOURCE MODIFIÉES : NON
```

---

## 1. Objet

Préparer un **inventaire / copie de travail** permettant une **inspection visuelle humaine** des photos impliquées dans les 4 collisions critiques identifiées par l’audit historique, afin de décider ultérieurement à quel élève appartiennent réellement les images.

Cette étape **ne corrige pas** les `PhotoPath` / `StoragePath` et **ne migre aucun fichier** sur le partage source.

---

## 2. Règles de l'audit

- Aucun `UPDATE` / `DELETE` / `INSERT` SQL.
- Aucun déplacement, renommage ou suppression sur `\\Desktop-ct9vndv\erp_scolaire`.
- Copies uniquement vers `AUDIT_VISUEL_COLLISIONS/`.
- Vérification taille + SHA-256 source ↔ copie.
- Re-hash des sources après copie pour confirmer qu’elles sont inchangées.
- **Aucune identification automatique d’identité** à partir du contenu image.
- Les métadonnées EXIF éventuelles sont indicatives uniquement, **pas une preuve d’identité**.

---

## 3. MOTE JOSEPH

| Élément | Valeur |
| ------- | ------ |
| Student actuel | MOTE JOSEPH |
| Matricule | `ELV-2026-00002` |
| Dossier legacy | `NTUMBA_ERICK_ELV_2026_00002` |

### Photos

| Période | Source | Copie travail | Statut |
| ------- | ------ | ------------- | ------ |
| 2026-2027 | `\\Desktop-ct9vndv\erp_scolaire\2026-2027\NTUMBA_ERICK_ELV_2026_00002\PHOTO.jpg` | `AUDIT_VISUEL_COLLISIONS/01_MOTE_JOSEPH/photo_2026.jpg` | Copiée, hash OK |
| 2025-2026 | `...\2025-2026\NTUMBA_ERICK_ELV_2026_00002\PHOTO.jpg` | — | **ABSENTE** |

### Métadonnées photo 2026

| Champ | Valeur |
| ----- | ------ |
| Nom | `PHOTO.jpg` |
| Extension | `.jpg` |
| Taille | 59 760 octets |
| SHA-256 | `D631ECE37BDB0BACA3F19F17A75E8D2EFADEC1999B2DEC71BAD1215559487E42` |
| LastWriteTime (UTC) | 2026-08-05T14:49:35.9155008Z |
| EXIF | aucun des tags Make/Model/DateTimeOriginal détectés |

### Autres documents (inventaire, non copiés sauf info)

**2025-2026**

| Fichier | Taille | SHA-256 | LastWriteTime UTC |
| ------- | ------ | ------- | ----------------- |
| `FICHE_INSCRIPTION.html` | 3 021 | `6A57EAC3…A636` | 2026-07-13T13:21:52Z |

**2026-2027**

| Fichier | Taille | SHA-256 | LastWriteTime UTC |
| ------- | ------ | ------- | ----------------- |
| `ACTE_DE_NAISSANCE.pdf` | 77 263 | `05CB8C4F…B0C5` | 2026-08-05T14:49:35Z |
| `FICHE_INSCRIPTION.pdf` | 114 453 | `437CF15A…3EF7` | 2026-08-05T14:49:43Z |
| `PHOTO.jpg` | 59 760 | (voir ci-dessus) | 2026-08-05T14:49:35Z |

Fichier info : `01_MOTE_JOSEPH/autres_documents_info.txt`

---

## 4. TSHIBANGILA Nathalie

| Élément | Valeur |
| ------- | ------ |
| Student actuel | TSHIBANGILA Nathalie |
| Matricule | `ELV-2026-00003` |
| Dossier legacy | `MUTEMA_MON_COEUR_ELV_2026_00003` |

### Photos

| Période | Source | Copie travail | Statut |
| ------- | ------ | ------------- | ------ |
| 2026-2027 | `...\2026-2027\MUTEMA_MON_COEUR_ELV_2026_00003\PHOTO.jpg` | `02_TSHIBANGILA_NATHALIE/photo_2026.jpg` | Copiée, hash OK |
| 2025-2026 | `...\2025-2026\MUTEMA_MON_COEUR_ELV_2026_00003\PHOTO.jpg` | `02_TSHIBANGILA_NATHALIE/photo_2025.jpg` | Copiée, hash OK |

### Métadonnées

| Fichier | Taille | SHA-256 | LastWriteTime UTC | EXIF (indicatif) |
| ------- | ------ | ------- | ----------------- | ---------------- |
| photo_2026 | 406 290 | `0CCD75D65F81803AED7098221BE5AE2204239DE1128A143AC6BDA145CD9B0EB3` | 2026-08-05T20:52:48Z | TECNO CN5c ; DateTimeOriginal `2026:08:05 21:51:20` |
| photo_2025 | 68 029 | `A7B739D56F1ABE975AF9FBDD77D01E7AC0B8CA72A4A0886BEBB8A34CAA91B38D` | 2026-07-13T13:34:39Z | tags Make/Model/DateTime non détectés sur les IDs scannés |

Les deux SHA-256 sont **différents** → ce ne sont pas le même fichier.

### Autres documents (inventaire)

**2025-2026 :** `FICHE_INSCRIPTION.html` (3 218), `PHOTO.jpg`  
**2026-2027 :** `FICHE_INSCRIPTION.pdf` (126 876), `PHOTO.jpg`

---

## 5. MASANGA RUTH

| Élément | Valeur |
| ------- | ------ |
| Student actuel | MASANGA RUTH |
| Matricule | `ELV-2026-00004` |
| Dossier legacy | `NKULU_LINEE_ELV_2026_00004` |

### Photos

| Période | Source | Copie travail | Statut |
| ------- | ------ | ------------- | ------ |
| 2026-2027 | `...\2026-2027\NKULU_LINEE_ELV_2026_00004\PHOTO.jpg` | `03_MASANGA_RUTH/photo_2026.jpg` | Copiée, hash OK |
| 2025-2026 | `...\2025-2026\NKULU_LINEE_ELV_2026_00004\PHOTO.jpg` | `03_MASANGA_RUTH/photo_2025.jpg` | Copiée, hash OK |

### Métadonnées

| Fichier | Taille | SHA-256 | LastWriteTime UTC | EXIF (indicatif) |
| ------- | ------ | ------- | ----------------- | ---------------- |
| photo_2026 | 298 345 | `AA38822B4C2080D26269E4B52F0B3B058302BBF7D5E4DE9F33A7642E8DE1FCA1` | 2026-08-12T19:31:37Z | TECNO CN5c ; DateTimeOriginal `2026:08:12 20:31:33` |
| photo_2025 | 431 449 | `97F12E1EA40C63E9CF35F07C0D13613A63D0F4D0FC27B8DC0CD0D2287A74C348` | 2026-07-13T14:01:11Z | TECNO CN5c ; DateTimeOriginal `2026:07:13 14:59:47` |

SHA-256 distincts → fichiers différents.

### Autres documents (inventaire, non copiés)

**2025-2026**

| Fichier | Taille | Note |
| ------- | ------ | ---- |
| `ACTE_DE_NAISSANCE.pdf` | 2 735 824 | volumineux — non copié |
| `FICHE_INSCRIPTION.html` | 3 104 | |
| `PHOTO.jpg` | 431 449 | copiée en photo_2025 |

**2026-2027**

| Fichier | Taille |
| ------- | ------ |
| `FICHE_INSCRIPTION.pdf` | 103 983 |
| `PHOTO.jpg` | 298 345 |

---

## 6. NDAYA MIRADIE

| Élément | Valeur |
| ------- | ------ |
| Student actuel | NDAYA MIRADIE |
| Matricule | `ELV-2026-00005` |
| Dossier legacy | `KABEYA_GLORIA_ELV_2026_00005` |

### Comparaison requise (précise)

| Rôle | Source | Copie travail |
| ---- | ------ | ------------- |
| PHOTO KABEYA **historique** | `\\Desktop-ct9vndv\erp_scolaire\2025-2026\KABEYA_GLORIA_ELV_2026_00005\PHOTO.jpg` | `04_NDAYA_MIRADIE/photo_2025.jpg` |
| PHOTO actuellement référencée par **NDAYA** | `\\Desktop-ct9vndv\erp_scolaire\2026-2027\KABEYA_GLORIA_ELV_2026_00005\PHOTO.jpg` | `04_NDAYA_MIRADIE/photo_2026.jpg` |

Les deux originaux sont **inchangés** sur le partage.

### Métadonnées

| Fichier | Taille | SHA-256 | LastWriteTime UTC | EXIF (indicatif) |
| ------- | ------ | ------- | ----------------- | ---------------- |
| photo_2025 (historique) | 67 587 | `40945AD7DC75B3796EC82ABD7FEC619254CABA9B9014C1ECD0672B9B0FFB4DAC` | 2026-07-13T14:10:54Z | DateTimeOriginal `2026:07:07 16:12:52` |
| photo_2026 (référencée NDAYA) | 413 423 | `8FEAE4272E97243EEE1C4F9ACC9D45C55EFCE60FBFCC8409E705685F2894C0E2` | 2026-08-12T19:01:42Z | TECNO CN5c ; DateTimeOriginal `2026:08:12 20:00:02` |

SHA-256 distincts → **deux images différentes**.  
L’écriture de la photo 2026 coïncide temporellement avec la création du Student NDAYA (≈ 19:01 UTC le 2026-08-12) — indice technique uniquement, **pas une identification visuelle**.

### Autres documents (inventaire)

**2025-2026 :** `ACTE_DE_NAISSANCE.png` (56 395), `FICHE_INSCRIPTION.html` (3 234), `PHOTO.jpg`  
**2026-2027 :** `PHOTO.jpg` uniquement

---

## 7. Tableau comparatif

| Cas | Student actuel | Matricule | Dossier 2025-2026 | Photo 2025 | Dossier 2026-2027 | Photo 2026 | Taille 2026 | SHA-256 2026 (début…) | Observation |
| --- | -------------- | --------- | ----------------- | ---------- | ----------------- | ---------- | ----------- | --------------------- | ----------- |
| 1 | MOTE JOSEPH | ELV-2026-00002 | `NTUMBA_ERICK_…00002` | **ABSENTE** | `NTUMBA_ERICK_…00002` | Présente | 59 760 | `D631ECE3…7E42` | Seule photo 2026 à inspecter ; historique sans PHOTO |
| 2 | TSHIBANGILA Nathalie | ELV-2026-00003 | `MUTEMA_MON_COEUR_…00003` | Présente (68 029) | même nom | Présente (406 290) | 406 290 | `0CCD75D6…0EB3` | Deux photos distinctes à comparer |
| 3 | MASANGA RUTH | ELV-2026-00004 | `NKULU_LINEE_…00004` | Présente (431 449) | même nom | Présente (298 345) | 298 345 | `AA38822B…FCA1` | Deux photos distinctes ; EXIF TECNO les deux |
| 4 | NDAYA MIRADIE | ELV-2026-00005 | `KABEYA_GLORIA_…00005` | Présente (67 587) | même nom | Présente (413 423) | 413 423 | `8FEAE427…C0E2` | Historique KABEYA vs photo incident NDAYA |

### Chemins source complets (photos)

1. 2026 : `\\Desktop-ct9vndv\erp_scolaire\2026-2027\NTUMBA_ERICK_ELV_2026_00002\PHOTO.jpg`  
2. 2025 : `\\Desktop-ct9vndv\erp_scolaire\2025-2026\MUTEMA_MON_COEUR_ELV_2026_00003\PHOTO.jpg`  
   2026 : `\\Desktop-ct9vndv\erp_scolaire\2026-2027\MUTEMA_MON_COEUR_ELV_2026_00003\PHOTO.jpg`  
3. 2025 : `\\Desktop-ct9vndv\erp_scolaire\2025-2026\NKULU_LINEE_ELV_2026_00004\PHOTO.jpg`  
   2026 : `\\Desktop-ct9vndv\erp_scolaire\2026-2027\NKULU_LINEE_ELV_2026_00004\PHOTO.jpg`  
4. 2025 : `\\Desktop-ct9vndv\erp_scolaire\2025-2026\KABEYA_GLORIA_ELV_2026_00005\PHOTO.jpg`  
   2026 : `\\Desktop-ct9vndv\erp_scolaire\2026-2027\KABEYA_GLORIA_ELV_2026_00005\PHOTO.jpg`

---

## 8. Hash et intégrité

| # | Fichier source | Copie | Tailles égales | SHA-256 égaux | Source intacte après copie |
| - | -------------- | ----- | -------------- | ------------- | -------------------------- |
| 1 | PHOTO 2026 NTUMBA | `01_…/photo_2026.jpg` | Oui | Oui | Oui |
| 2a | PHOTO 2026 MUTEMA | `02_…/photo_2026.jpg` | Oui | Oui | Oui |
| 2b | PHOTO 2025 MUTEMA | `02_…/photo_2025.jpg` | Oui | Oui | Oui |
| 3a | PHOTO 2026 NKULU | `03_…/photo_2026.jpg` | Oui | Oui | Oui |
| 3b | PHOTO 2025 NKULU | `03_…/photo_2025.jpg` | Oui | Oui | Oui |
| 4a | PHOTO 2026 KABEYA | `04_…/photo_2026.jpg` | Oui | Oui | Oui |
| 4b | PHOTO 2025 KABEYA | `04_…/photo_2025.jpg` | Oui | Oui | Oui |

**Inventaire attendu :** 7 photos (cas 1 sans 2025) + 4 fichiers `autres_documents_info.txt`  
**Inventaire obtenu :** 7 photos + 4 infos (+ `_inventory.json` technique)  
**Re-vérification finale source ↔ copie :** `ALL_MATCH=True`

Aucun fichier source n’a été supprimé, déplacé, renommé ou modifié.

---

## 9. Observations techniques

- Structure de travail créée sous le dépôt : `AUDIT_VISUEL_COLLISIONS/01_…` à `04_…`.
- Cas MOTE : **pas de photo 2025-2026** dans le dossier `NTUMBA_ERICK` (seulement une fiche HTML).
- Pour les cas 2–4, les paires 2025/2026 ont des **SHA-256 différents** : comparaison visuelle utile.
- EXIF présents sur plusieurs images (souvent téléphone TECNO) — **ne constituent pas une preuve d’identité**.
- Documents volumineux (ex. acte NKULU 2025 ≈ 2,7 Mo) listés mais **non copiés** (non nécessaires à l’identification visuelle des photos).
- Aucune analyse faciale / conclusion automatique d’appartenance n’a été effectuée.

### Arborescence de travail

```text
AUDIT_VISUEL_COLLISIONS/
├── _inventory.json
├── 01_MOTE_JOSEPH/
│   ├── photo_2026.jpg
│   └── autres_documents_info.txt
├── 02_TSHIBANGILA_NATHALIE/
│   ├── photo_2026.jpg
│   ├── photo_2025.jpg
│   └── autres_documents_info.txt
├── 03_MASANGA_RUTH/
│   ├── photo_2026.jpg
│   ├── photo_2025.jpg
│   └── autres_documents_info.txt
└── 04_NDAYA_MIRADIE/
    ├── photo_2026.jpg
    ├── photo_2025.jpg
    └── autres_documents_info.txt
```

---

## 10. Décisions restant à prendre

Aucune décision n’est prise ici. Pour chaque cas, après inspection visuelle humaine :

### Cas 1 — MOTE JOSEPH

```text
Photo 2026 probablement à conserver : à confirmer visuellement
Photo historique : absente (rien à archiver côté PHOTO 2025)
Photo ambiguë : décision humaine requise (appartenance MOTE vs historique NTUMBA)
```

### Cas 2 — TSHIBANGILA Nathalie

```text
Photo 2026 probablement à conserver : à confirmer visuellement
Photo historique : à conserver / archiver selon décision
Photo ambiguë : décision humaine requise
```

### Cas 3 — MASANGA RUTH

```text
Photo 2026 probablement à conserver : à confirmer visuellement
Photo historique : à conserver / archiver selon décision
Photo ambiguë : décision humaine requise
```

### Cas 4 — NDAYA MIRADIE / KABEYA

```text
Photo 2026 probablement à conserver : à confirmer visuellement
Photo historique : à conserver / archiver selon décision
Photo ambiguë : décision humaine requise
```

Ouvrir les fichiers dans `AUDIT_VISUEL_COLLISIONS/` (explorateur / visionneuse) pour trancher avant toute correction future des chemins.

---

```text
AUDIT VISUEL : TERMINÉ
DONNÉES SOURCE MODIFIÉES : NON
```
