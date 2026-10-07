# Mise à jour 2026.10.1 — reprise de la synchronisation des tarifs

## Problème corrigé

Des tarifs équivalents pouvaient avoir des GUID différents dans la base locale et le cloud.
L'insertion échouait sur `IX_ClassFeeAmounts_Year_Class_Category_FeeType_Installment`,
bloquant la préparation du référentiel financier et l'envoi des paiements.
Les colonnes d'audit de `EnrollmentPricingCategoryHistory` étaient également créées en
`nvarchar` alors que le modèle attend des GUID.

## Comportement après installation

- L'API migre automatiquement `CreatedBy`, `UpdatedBy` et `DeletedBy` en
  `uniqueidentifier NULL`. La migration est transactionnelle et rejouable. Une valeur
  invalide provoque une annulation explicite ; elle n'est jamais remplacée par NULL.
- Les tarifs actifs sont rapprochés par école, année, classe, catégorie, type de frais
  et échéance. Les identifiants locaux ne sont pas remplacés.
- Les correspondances sont conservées dans la table locale `SyncEntityIdentity`, par
  école et base cloud. Elles servent aux modifications et suppressions ultérieures.
  Les anciens tarifs supprimés sans correspondance connue ne sont pas rapprochés
  arbitrairement avec un tarif actif de remplacement.
- Les soldes élèves utilisent l'identifiant cloud du tarif et, si nécessaire, celui
  du solde déjà présent. Les montants dus figés restent ceux du solde local, sans
  recalcul à partir du tarif courant.
- La confirmation après envoi utilise les identifiants cloud correspondants.
- À la première boucle de synchronisation de chaque démarrage, les unités Failed ou
  DeadLetter de l'école locale portant l'erreur d'index de tarifs connue repassent en
  Pending. Les autres erreurs et les unités Completed ne sont pas réinitialisées.

## Installation sur le serveur de l'école

1. Faire une sauvegarde récente de la vraie base locale de l'école.
2. Fermer les postes clients et lancer `ERP_Scolaire_Update_2026.10.1.exe` en administrateur
   sur le serveur de l'école. Il s'agit du paquet de **mise à jour**.
3. Laisser l'assistant mettre à jour l'API et le Desktop et redémarrer le service.
   Il vérifie les colonnes d'audit et la table de correspondances avant d'annoncer la réussite.
4. Ouvrir **Paramètres → Administration système → Synchronisation cloud**.
   Vérifier que la synchronisation est active et que le serveur cloud est accessible.
5. Laisser la reprise automatique s'exécuter ; le bouton **Synchroniser maintenant**
   permet également de lancer la reprise. La file se vide par lots et peut nécessiter
   plusieurs cycles si le retard est important.
6. Contrôler la diminution des unités en attente, l'absence de la collision de tarifs
   dans le journal, puis la présence en ligne de quelques paiements récents de Saint Benoît.

Ne pas restaurer l'ancien backup sur la base cloud partagée. La version sera exécutée
sur la base actuelle de l'école ; le backup utilisé pour le diagnostic n'a pas été réparé.
Le schéma d'audit cloud a déjà été corrigé séparément avec autorisation.

## Validation réalisée

- Tests de rapprochement, isolation des écoles, références des soldes, réexécution
  sans doublon, changement de clé du tarif, suppression et reprise ciblée des erreurs.
- Tests SQL Server sur des bases temporaires dédiées : contraintes uniques et FK,
  migration des GUID avec conservation des valeurs, rollback sur valeur invalide,
  création rejouable de la table de correspondances et persistance entre contextes.
- Tests de l'assistant de mise à jour concernant la conservation de la configuration.

La remontée des données de production doit être confirmée après installation chez
l'école. Aucune synchronisation du vieux backup vers le cloud n'a été lancée.
