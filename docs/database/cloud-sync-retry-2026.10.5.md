# Mise à jour 2026.10.5 — préparation de la nouvelle destination cloud

Le journal api-20261007B.log montre un échec dans PrepareDestinationAsync : SqlServerRetryingExecutionStrategy refuse la transaction locale créée hors de son unité d'exécution. Cela bloque la préparation du transfert complet vers la nouvelle destination, même si un catch-up a déjà mis des modifications en file.

La préparation complète s'exécute désormais dans Database.CreateExecutionStrategy(). La transaction continue de garantir que la file et le marqueur de destination sont enregistrés ensemble. Lors d'une reprise, le suivi EF est vidé et les données sont relues ; un marqueur déjà validé empêche de préparer à nouveau la même destination. Les données métier ne sont pas réécrites par cette préparation.

Le test SQL utilise maintenant EnableRetryOnFailure(3), comme l'API de production. Il vérifie la préparation, l'annulation intégrale sur échec SQL volontaire, la reprise après correction et l'absence de nouvelle préparation au redémarrage.

## Installation et contrôle

1. Sauvegarder la base locale, fermer les postes puis installer ERP_Scolaire_Update_2026.10.5.exe en administrateur sur le serveur hébergeant l'API, sans désinstaller.
2. Conserver la base locale comme source et la nouvelle base cloud comme destination. Ne supprimer ni données ni marqueur manuellement.
3. Vérifier que l'API redémarre, puis lancer Synchroniser maintenant. Laisser le serveur et sa connexion Internet actifs.
4. Suivre la diminution de la file et les erreurs dans le journal. Confirmer également l'arrivée des données dans la base cloud configurée ; une préparation réussie ne prouve pas que tout le transfert est terminé.

Cette version inclut les corrections précédentes. Les éventuels problèmes de références d'école ou d'intégrité signalés ultérieurement nécessitent leur propre diagnostic.
