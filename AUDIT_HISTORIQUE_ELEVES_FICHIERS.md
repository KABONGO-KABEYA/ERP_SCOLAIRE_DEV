# Audit historique — Élèves et fichiers

**Date de l’audit :** 2026-08-12  
**Base :** `SchoolManagementRDC_Development` (`localhost\HEROS_SQL19`)  
**Racine fichiers :** `\\Desktop-ct9vndv\erp_scolaire`  
**Mode :** lecture seule uniquement (aucun `INSERT` / `UPDATE` / `DELETE` / déplacement / suppression / migration)

---

## 1. Objet de l'audit

Documenter l’état réel des élèves, inscriptions, relations parentales, matricules et fichiers (photos / documents / dossiers legacy) après les incidents connus (NDAYA / KABEYA / MASANGA) et le bug historique `FindExistingStudentFolderName` (réutilisation d’un dossier par suffixe de matricule).

Aucune correction n’a été appliquée. Ce rapport sert uniquement à décider d’éventuelles corrections ultérieures.

---

## 2. Méthodologie

1. Requêtes SQL `SELECT` uniquement sur `Students`, `Enrollments`, `StudentDocuments`, `StudentGuardians`, `Guardians`, `AcademicYears`, `StudentFeeBalances`, `AuditEntries`, `UserAccounts`.
2. Inventaire filesystem en lecture seule sous `2025-2026/` et `2026-2027/` (pas de `temp/`, pas de `students/{StudentId}/`).
3. Corrélation chemin DB ↔ existence fichier ↔ nom de dossier legacy (`NOM_PRENOM_MATRICULE`) ↔ identité Student.
4. Les cas NDAYA / KABEYA / MASANGA servent de point de départ ; l’ensemble de la base et du partage a été parcouru.
5. Quand le contenu binaire d’une photo ne peut pas être attribué avec certitude à une personne, la mention **« propriétaire impossible à déterminer avec les données disponibles »** est utilisée. Les coïncidences horodatage DB ↔ `LastWriteTime` fichier sont indiquées comme indices, pas comme preuve d’identité visuelle.

---

## 3. Cas connus

### 3.1 NDAYA MUSENGA MIRADIE

| Champ | Valeur constatée |
| ----- | ---------------- |
| StudentId | `631CDB63-51CD-4AF0-9908-C4B8BE7F4FD8` |
| Nom / prénom / postnom | NDAYA / MIRADIE / MUSENGA |
| Matricule | `ELV-2026-00005` |
| SchoolId | `71635F62-B975-479D-9E6E-FBACD05E4996` |
| CreatedAt | 2026-08-12 19:01:43 |
| CreatedBy | `A6BF7FAB-632B-4A34-A67E-4746DDEBE473` (`csb009` / ANGELLE OVI) |
| AddressId | NULL |
| Enrollment | **aucun** |
| StudentGuardians | **aucun** |
| StudentDocuments | **aucun** |
| StudentFeeBalances | **aucun** |
| PhotoPath | `2026-2027/KABEYA_GLORIA_ELV_2026_00005/PHOTO.jpg` |
| Fichier PhotoPath | **existe** (413 423 octets, mtime UTC ≈ 2026-08-12 19:01:42) |
| Audit `EnrollmentWizard.Complete` | **absent** |

**Incomplet par rapport à une inscription valide :** Enrollment, guardians, adresse, documents métadonnées, soldes frais, audit de finalisation.

**Anomalie fichier :** le `PhotoPath` pointe vers un dossier nommé **KABEYA_GLORIA**, pas NDAYA. Cohérence temporelle forte entre création Student et écriture du fichier 2026-2027 → indice que la photo a été écrite dans le dossier legacy réutilisé par suffixe `_ELV_2026_00005`.  
**Propriétaire certain du contenu binaire :** impossible à déterminer par lecture seule sans inspection visuelle humaine ; le *dossier* n’appartient pas nominalement à NDAYA.

### 3.2 KABEYA GLORIA

| Recherche | Résultat |
| --------- | -------- |
| Student en base nommé KABEYA / GLORIA | **aucun** |
| Enrollment lié | **aucun** |
| Référence `PhotoPath` vers son dossier | **oui** — Student NDAYA (`ELV-2026-00005`) |
| Référence `StudentDocument` | **aucune** |
| Dossier `2025-2026/KABEYA_GLORIA_ELV_2026_00005/` | existe — `ACTE_DE_NAISSANCE.png`, `FICHE_INSCRIPTION.html`, `PHOTO.jpg` (67 587 o, mtime 2026-07-13) |
| Dossier `2026-2027/KABEYA_GLORIA_ELV_2026_00005/` | existe — `PHOTO.jpg` (413 423 o, mtime 2026-08-12) |

**Interprétation :** dossier legacy historique sans Student actuel. Le dossier 2025-2026 semble être l’historique « Gloria » ; le dossier 2026-2027 a reçu une photo au moment de la tentative NDAYA (collision matricule `00005`).  
**Propriétaire des fichiers 2025-2026 :** impossible à rattacher à un Student actuel.  
**Propriétaire de `2026-2027/.../PHOTO.jpg` :** coincé entre identité dossier (KABEYA) et identité DB (NDAYA) — **décision humaine requise**.

### 3.3 MASANGA RUTH

| Champ | Valeur constatée |
| ----- | ---------------- |
| StudentId | `7263681A-C089-477C-AA17-CF0D5581A198` |
| Nom / prénom / postnom | MASANGA / RUTH / TSHIAKATUMBA |
| Matricule | `ELV-2026-00004` |
| Enrollment | 1 (année `2026-2027`, audit Complete 2026-08-12 13:16:31) |
| Guardians | 2 (père TSHIAKAS TSHIAKATUMBA, mère RACHEL TSHIABU) |
| Address | présent |
| Fee balances | 4 |
| PhotoPath | `2026-2027/NKULU_LINEE_ELV_2026_00004/PHOTO.jpg` |
| StudentDocument Photo | même chemin, CreatedAt **2026-08-12 19:31:37** |
| Fichier | **existe** (298 345 o, mtime ≈ 19:31:37) |

**Pourquoi le PhotoPath utilise un autre nom :** le dossier legacy `NKULU_LINEE_ELV_2026_00004` existait déjà (présent aussi en `2025-2026` avec une photo différente de 431 449 o, juillet 2026). L’ancien mécanisme a réutilisé le dossier par suffixe `_ELV_2026_00004` au lieu de créer `MASANGA_RUTH_ELV_2026_00004`.

L’inscription SQL de MASANGA est complète ; l’anomalie est **uniquement** le stockage fichier / chemin nommé NKULU.

---

## 4. Students orphelins

Critère métier retenu : un Student **créé via wizard d’inscription** sans Enrollment actif est un orphelin d’inscription. Un Student seed / démo sans Enrollment peut être un cas distinct.

| StudentId | Matricule | Nom | Prénom | SchoolId | Création | Créateur | #Enroll | Année(s) | Statut | Source probable | Gravité |
| --------- | --------- | --- | ------ | -------- | -------- | -------- | ------- | -------- | ------ | --------------- | ------- |
| `631CDB63-…4FD8` | ELV-2026-00005 | NDAYA | MIRADIE | `71635F62-…4996` | 2026-08-12 19:01:43 | csb009 | 0 | — | non archivé | Échec `complete` (P1 historique) après store-file | **ÉLEVÉE** |
| `67419519-…55F9` | ELV-2026-001 | Kabongo | Marie | idem | 2026-08-05 12:58:42 | NULL | 0 | — | non archivé | Seed / démo (format matricule atypique, guardian seed) | **FAIBLE** à **MOYENNE** (à trancher) |

Students avec Enrollment : MOTE JOSEPH (`00002`), TSHIBANGILA Nathalie (`00003`), MASANGA RUTH (`00004`) — **non orphelins**.

---

## 5. Relations Parent / Élève

| Contrôle | Résultat |
| -------- | -------- |
| StudentGuardians → Guardian inexistant | 0 |
| StudentGuardians → Student inexistant | 0 |
| Doublons (StudentId, GuardianId) | 0 |
| Students sans Guardian (actifs) | **NDAYA** (0) — anormal pour inscription complète ; **Kabongo Marie** a 1 guardian malgré 0 enrollment |
| Parent multi-enfants | Guardian `Jean Kabongo` lié à **Kabongo Marie** et **MOTE JOSEPH** — **légitime / non anomalie** (règle métier) |
| Parents MASANGA / TSHIBANGILA / MOTE (autres) | 2 guardians chacun — cohérent |

**Anomalie :** NDAYA sans aucun `StudentGuardian` (gravité ÉLEVÉE, liée à l’inscription incomplète).

---

## 6. Audit des matricules

### Doublons `(SchoolId, RegistrationNumber)`

Aucun doublon détecté parmi les 5 Students.

### Formats

| Matricule | Student | Observation |
| --------- | ------- | ----------- |
| `ELV-2026-00002` … `00005` | MOTE, TSHIBANGILA, MASANGA, NDAYA | Format actuel `ELV-YYYY-#####` (5 chiffres) — OK |
| `ELV-2026-001` | Kabongo Marie | Format **non conforme** au format P4 actuel (3 chiffres) — **documenté, non modifié** |

### Incohérences année / matricule

- Tous les matricules portent l’année calendaire **2026**.
- Année scolaire courante en base : **2026-2027** uniquement (`IsCurrent=1`).
- Dossiers filesystem nombreux sous **2025-2026** avec suffixes `ELV_2026_*` : l’année du dossier ne correspond pas à l’année « 2025 » du chemin ; probable réutilisation de dossiers d’essais. **Ne pas conclure automatiquement à une erreur métier** — documenté comme incohérence structurelle historique.

---

## 7. Audit PhotoPath

Students avec PhotoPath : **4 / 5** (Kabongo Marie : NULL).

| Student | Matricule | PhotoPath | Fichier existe ? | Format | Dossier nominal | Cohérence nom Student | Gravité |
| ------- | --------- | --------- | ---------------- | ------ | --------------- | --------------------- | ------- |
| MOTE JOSEPH | 00002 | `2026-2027/NTUMBA_ERICK_ELV_2026_00002/PHOTO.jpg` | Oui | Legacy | NTUMBA ERICK | **Non** | **CRITIQUE** |
| TSHIBANGILA Nathalie | 00003 | `2026-2027/MUTEMA_MON_COEUR_ELV_2026_00003/PHOTO.jpg` | Oui | Legacy | MUTEMA MON COEUR | **Non** | **CRITIQUE** |
| MASANGA RUTH | 00004 | `2026-2027/NKULU_LINEE_ELV_2026_00004/PHOTO.jpg` | Oui | Legacy | NKULU LINEE | **Non** | **CRITIQUE** |
| NDAYA MIRADIE | 00005 | `2026-2027/KABEYA_GLORIA_ELV_2026_00005/PHOTO.jpg` | Oui | Legacy | KABEYA GLORIA | **Non** | **CRITIQUE** |

**Aucun** PhotoPath au format nouveau `students/{StudentId}/`.

**Fait établi :** pour les 4 cas, le chemin DB pointe vers un dossier portant le **nom d’un autre élève** (identité legacy), tout en partageant le **même suffixe matricule** que le Student actuel. C’est le schéma exact de l’ancien `FindExistingStudentFolderName`.

Indice horodatage (non preuve visuelle) : les mtimes des photos 2026-2027 correspondent aux moments de création / document du Student actuel, tandis que des photos plus anciennes existent souvent sous `2025-2026/` pour le même nom de dossier.

---

## 8. Audit StudentDocument.StoragePath

Documents actifs : **4**.

| DocumentId | StudentId | Type | StoragePath | Existe | Format | Anomalie |
| ---------- | --------- | ---- | ----------- | ------ | ------ | -------- |
| `AD79A96D-…` | MOTE (`00002`) | Photo | `…/NTUMBA_ERICK_ELV_2026_00002/PHOTO.jpg` | Oui | Legacy | Mauvais dossier nominal — **CRITIQUE** |
| `CFDABD31-…` | MOTE (`00002`) | Acte de naissance | `…/NTUMBA_ERICK_ELV_2026_00002/ACTE_DE_NAISSANCE.pdf` | Oui | Legacy | Idem — **CRITIQUE** |
| `2DE61948-…` | TSHIBANGILA (`00003`) | Photo | `…/MUTEMA_MON_COEUR_ELV_2026_00003/PHOTO.jpg` | Oui | Legacy | Idem — **CRITIQUE** |
| `61DFFFFB-…` | MASANGA (`00004`) | Photo | `…/NKULU_LINEE_ELV_2026_00004/PHOTO.jpg` | Oui | Legacy | Idem — **CRITIQUE** |

NDAYA : aucun `StudentDocument` (PhotoPath seul).

---

## 9. Audit des dossiers legacy

### 9.1 Année `2026-2027` (4 dossiers)

| Dossier | Matricule extrait | Fichiers | Student DB même suffixe | Référencé PhotoPath/Docs | Classe |
| ------- | ----------------- | -------- | ----------------------- | ------------------------ | ------ |
| `NTUMBA_ERICK_ELV_2026_00002` | 00002 | PHOTO, ACTE, FICHE | MOTE JOSEPH | Oui (MOTE) | **C — suspect** |
| `MUTEMA_MON_COEUR_ELV_2026_00003` | 00003 | PHOTO, FICHE | TSHIBANGILA | Oui | **C — suspect** |
| `NKULU_LINEE_ELV_2026_00004` | 00004 | PHOTO, FICHE | MASANGA | Oui | **C — suspect** |
| `KABEYA_GLORIA_ELV_2026_00005` | 00005 | PHOTO | NDAYA | Oui (NDAYA PhotoPath) | **C — suspect** (+ historique KABEYA absent de la DB) |

### 9.2 Année `2025-2026` (24 dossiers)

| Classe | Dossiers | Commentaire |
| ------ | -------- | ----------- |
| **C / lié collision** | KABEYA…00005, MUTEMA…00003, NKULU…00004, NTUMBA…00002 | Suffixe = Student actuel, mais nom ≠ Student ; photos plus anciennes |
| **B — orphelin FS** | 20 autres (`ENGONGO`, `KABONGO_MEIRA`×2, `TENDE_PATRICIA`, `NDAYA_CHELAH`, etc.) | Aucun Student actuel avec ce matricule / ce nom |

Aucun dossier legacy **A — correctement référencé** au sens « nom dossier = nom Student + matricule cohérent ».

---

## 10. Audit des nouveaux dossiers

```text
{année}/students/{StudentId}/
```

- `2025-2026/students/` : **absent**
- `2026-2027/students/` : **absent**
- `temp/` : **absent**

**Nombre de dossiers nouveaux analysés : 0.**  
Aucune anomalie « StudentId FS orphelin » — la structure P3 n’a pas encore été utilisée en production sur ce partage.

---

## 11. Collisions historiques de matricules

Pattern détecté :

```text
même suffixe matricule ELV_2026_XXXXX
+
noms de dossier différents de LastName/FirstName du Student DB
+
PhotoPath / StoragePath pointant vers ce dossier
```

| Suffixe | Dossier(s) FS | Student DB actuel | Noms différents ? |
| ------- | ------------- | ----------------- | ----------------- |
| 00002 | NTUMBA_ERICK (2025+2026) | MOTE JOSEPH | Oui |
| 00003 | MUTEMA_MON_COEUR | TSHIBANGILA Nathalie | Oui |
| 00004 | NKULU_LINEE | MASANGA RUTH | Oui |
| 00005 | KABEYA_GLORIA | NDAYA MIRADIE | Oui |

**Conclusion :** le problème **n’était pas isolé** à NDAYA/KABEYA. **4 Students sur 4 ayant un PhotoPath** sont affectés par le même mécanisme. Les dossiers `2025-2026` montrent que les noms NTUMBA / MUTEMA / NKULU / KABEYA précèdent les Students actuels (juillet 2026 vs août 2026).

Nombre de collisions type FindExisting clairement observables en DB+FS : **4**.

---

## 12. Réconciliation Base ↔ Filesystem

| Catégorie | Occurrences | Exemples |
| --------- | ----------- | -------- |
| DB → fichier existe | 4 PhotoPath + 4 documents | Tous les chemins DB listés existent physiquement |
| DB → fichier absent | 0 | — |
| DB → mauvais dossier probable | **4 Students** | Voir §7–§8 |
| Fichier → aucune référence DB | **~20 dossiers 2025-2026** + fichiers annexes (fiches PDF/HTML) | Orphelins FS |
| Fichier → Student identifiable (par suffixe seulement) | 4 dossiers 2026-2027 | Identifiable par matricule, **pas** par le nom du dossier |
| Fichier → Student ambigu | Photos 2025-2026 des 4 dossiers collision + photo 2026-2027 KABEYA | Contenu potentiellement ancien élève **ou** nouvel élève — **décision humaine** |

---

## 13. Liste complète des anomalies

1. NDAYA orphelin SQL (Student sans Enrollment / guardians / adresse / docs / fees) + PhotoPath dans dossier KABEYA.
2. PhotoPath MOTE → dossier NTUMBA.
3. Documents MOTE → dossier NTUMBA.
4. PhotoPath TSHIBANGILA → dossier MUTEMA.
5. Document Photo TSHIBANGILA → dossier MUTEMA.
6. PhotoPath / Document MASANGA → dossier NKULU.
7. Dossier KABEYA sans Student KABEYA en base.
8. ~20 dossiers legacy 2025-2026 sans Student DB.
9. Matricule `ELV-2026-001` (Kabongo Marie) hors format P4.
10. Kabongo Marie sans Enrollment (seed probable).
11. Absence totale de structure `students/{StudentId}` alors que P3 est déployé côté code (état données, pas bug code).

---

## 14. Classification par gravité

### CRITIQUE (risque données d’un autre élève / mauvais dossier)

| # | Type | Student | Matricule | Problème |
| - | ---- | ------- | --------- | -------- |
| C1 | PhotoPath | MOTE JOSEPH | ELV-2026-00002 | Dossier `NTUMBA_ERICK_…` |
| C2 | StudentDocument | MOTE JOSEPH | ELV-2026-00002 | Acte + photo sous NTUMBA |
| C3 | PhotoPath | TSHIBANGILA Nathalie | ELV-2026-00003 | Dossier `MUTEMA_MON_COEUR_…` |
| C4 | StudentDocument | TSHIBANGILA | ELV-2026-00003 | Photo sous MUTEMA |
| C5 | PhotoPath + Document | MASANGA RUTH | ELV-2026-00004 | Dossier `NKULU_LINEE_…` |
| C6 | PhotoPath | NDAYA MIRADIE | ELV-2026-00005 | Dossier `KABEYA_GLORIA_…` |

### ÉLEVÉE

| # | Type | Student | Problème |
| - | ---- | ------- | -------- |
| E1 | Student orphelin | NDAYA | Pas d’Enrollment / guardians / adresse / fees |
| E2 | Relation | NDAYA | 0 StudentGuardian |

### MOYENNE

| # | Type | Cible | Problème |
| - | ---- | ----- | -------- |
| M1 | Matricule format | Kabongo Marie | `ELV-2026-001` |
| M2 | Student sans Enrollment | Kabongo Marie | Possible seed — à confirmer |

### FAIBLE

| # | Type | Cible | Problème |
| - | ---- | ----- | -------- |
| F1 | Dossiers FS orphelins | 20 dossiers 2025-2026 | Aucune référence DB actuelle |
| F2 | Dossier KABEYA 2025-2026 | historique | Pas de Student KABEYA |

---

## 15. Données nécessitant potentiellement une correction

*(recommandations uniquement — **aucune exécutée**)*

1. **NDAYA** : décider compléter l’inscription (Enrollment + guardians) **ou** archiver / supprimer le Student orphelin ; réaffecter la photo hors dossier KABEYA.
2. **Quatre PhotoPath / StoragePath collisionnés** : créer dossiers `students/{StudentId}/`, y placer les fichiers **après validation humaine** du contenu, mettre à jour les chemins DB.
3. **Dossiers legacy 2025-2026** : inventaire métier (archiver hors ligne vs conserver).
4. **Kabongo Marie / ELV-2026-001** : confirmer seed vs donnée réelle.
5. **Ne pas** fusionner automatiquement photos 2025-2026 et 2026-2027 des dossiers collision — risque d’écraser l’historique « ancien élève ».

---

## 16. Recommandations de nettoyage

Ordre conseillé (**à exécuter dans une phase séparée**, hors de cet audit) :

1. **Gel** : ne plus écrire via FindExisting (déjà traité par P3 pour les *nouvelles* créations).
2. **Décision humaine** dossier par dossier pour les 4 collisions (ouvrir les photos 2025-2026 vs 2026-2027).
3. **Plan NDAYA** : inscription à reprendre vs purge contrôlée.
4. **Script de migration fichiers** uniquement après validation signée : copie vers `students/{StudentId}/`, puis `UPDATE` PhotoPath/StoragePath, **sans** supprimer les legacy tant que non validé.
5. **Inventaire / archive** des 20 dossiers 2025-2026 non référencés.
6. **Pas de DELETE massif** avant sauvegarde complète du partage + backup SQL.

---

## 17. Points nécessitant une décision humaine

1. La photo `2026-2027/KABEYA_GLORIA_…/PHOTO.jpg` est-elle celle de **NDAYA** ou faut-il la traiter comme fichier compromis / à remplacer ?
2. Que faire des photos **2025-2026** des dossiers NTUMBA / MUTEMA / NKULU / KABEYA (anciens élèves absents de la DB) ?
3. Conserver ou supprimer le Student **NDAYA** orphelin ?
4. Statut de **Kabongo Marie** (`ELV-2026-001`) : seed à garder ou à retirer ?
5. Les 20 dossiers orphelins 2025-2026 : archive froide, destruction, ou réimport ?
6. Pour chaque collision : le contenu actuel 2026-2027 appartient-il bien au Student DB (MOTE / TSHIBANGILA / MASANGA / NDAYA) ?

---

## 15bis. Tableau de synthèse global

| Gravité | Type | Student | Matricule | Problème | Action potentielle |
| ------- | ---- | ------- | --------- | -------- | ------------------ |
| CRITIQUE | PhotoPath | MOTE JOSEPH | ELV-2026-00002 | Chemin sous `NTUMBA_ERICK_…` | Valider photo → migrer vers `students/{Id}/` + MAJ chemin |
| CRITIQUE | Documents | MOTE JOSEPH | ELV-2026-00002 | Acte + photo sous NTUMBA | Idem |
| CRITIQUE | PhotoPath | TSHIBANGILA Nathalie | ELV-2026-00003 | Chemin sous `MUTEMA_MON_COEUR_…` | Valider → migrer + MAJ |
| CRITIQUE | Document | TSHIBANGILA Nathalie | ELV-2026-00003 | Photo sous MUTEMA | Idem |
| CRITIQUE | PhotoPath + Document | MASANGA RUTH | ELV-2026-00004 | Chemin sous `NKULU_LINEE_…` | Valider → migrer + MAJ |
| CRITIQUE | PhotoPath | NDAYA MIRADIE | ELV-2026-00005 | Chemin sous `KABEYA_GLORIA_…` | Décider identité photo + corriger ou supprimer chemin |
| ÉLEVÉE | Orphelin SQL | NDAYA MIRADIE | ELV-2026-00005 | Student sans Enrollment / guardians | Compléter inscription ou purge contrôlée |
| ÉLEVÉE | Guardians | NDAYA MIRADIE | ELV-2026-00005 | 0 lien parent | Ajouter guardians si inscription reprise |
| MOYENNE | Format matricule | Kabongo Marie | ELV-2026-001 | Hors format P4 | Décider seed vs correction format |
| MOYENNE | Sans Enrollment | Kabongo Marie | ELV-2026-001 | Pas d’inscription | Confirmer légitimité |
| FAIBLE | FS orphelin | — | divers 2025-2026 | ~20 dossiers sans Student | Archive / inventaire hors ligne |
| FAIBLE | FS sans Student | KABEYA (historique) | ELV_2026_00005 | Pas de Student KABEYA | Conserver historique 2025-2026 séparément |

---

## 18. Conclusion

### Compteurs

| Indicateur | Nombre |
| ---------- | ------ |
| Students analysés | **5** |
| Enrollments analysés | **3** |
| PhotoPath analysés | **4** |
| StudentDocuments analysés | **4** |
| Dossiers legacy analysés | **28** (24 en 2025-2026 + 4 en 2026-2027) |
| Dossiers nouveaux `students/{Id}` analysés | **0** |
| Anomalies critiques | **6** (lignes C1–C6 ; 4 Students distincts affectés) |
| Anomalies élevées | **2** (NDAYA orphelin + sans guardian) |
| Anomalies moyennes | **2** (Marie format + sans enrollment) |
| Anomalies faibles | **2** catégories (orphelins FS 2025-2026 + KABEYA historique) |
| Cas décision humaine | **6** (§17) |

### Verdict

Le bug de réutilisation de dossier par matricule a touché **tous** les Students ayant une photo en base de développement (4/4), pas seulement NDAYA. Les dossiers legacy `2025-2026` montrent que les noms NTUMBA / MUTEMA / NKULU / KABEYA préexistaient. NDAYA cumule collision fichier **et** orphelin SQL (échec de finalisation).

### Recommandation générale (sans exécution)

Procéder en phase dédiée : **validation visuelle humaine des 4 collisions** → plan de migration vers `students/{StudentId}/` → mise à jour des chemins DB → traitement séparé de NDAYA (reprendre ou purger) → archivage des dossiers 2025-2026 non référencés. Toujours avec sauvegarde préalable. **Ne rien corriger automatiquement.**

---

*Fin du rapport — aucune donnée n’a été modifiée.*
