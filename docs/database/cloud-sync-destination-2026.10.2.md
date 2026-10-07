# Mise à jour et changement de destination cloud — 2026.10.2

La destination reste configurable dans `Api/ServeurDonneesCloud.txt`. Aucun serveur,
compte SQL, mot de passe ou nom de base client n'est intégré à cette correction.

## Installation chez le client

1. Sauvegarder sa base locale actuelle et ses fichiers de configuration.
2. Arrêter le service Windows `ErpScolaireApi` avant de changer la destination.
3. Dans le dossier d'installation existant, ouvrir `Api/ServeurDonneesCloud.txt`.
   Renseigner `BASE` avec la destination choisie. Si seul le nom de base change,
   conserver les autres paramètres, notamment `MOTDEPASSE` chiffré sur cette machine.
   `ACTIF=1` active la synchronisation. Ne pas copier un secret chiffré depuis un autre PC.
4. Exécuter `ERP_Scolaire_Update_2026.10.2.exe` en administrateur, puis l'assistant
   `ErpScolaire.Update.exe`. Sélectionner le dossier d'installation existant.
   Ne pas désinstaller l'application et ne pas exécuter l'installation initiale.
5. Vérifier la réussite de la mise à jour, le démarrage du service et le journal
   de synchronisation. Laisser le serveur allumé et connecté pendant le rattrapage.
6. Contrôler dans la nouvelle base les élèves, inscriptions, tarifs et paiements.
   Une file vide n'est une réussite que si aucune unité Failed/DeadLetter ne subsiste.

Si Windows affiche `CreateProcess a échoué ; code 740` après l'extraction,
ouvrir le dossier `Update` et lancer `ErpScolaire.Update.exe` par clic droit,
« Exécuter en tant qu'administrateur ». La mise à jour n'a pas encore été lancée.
L'enveloppe corrigée conserve les droits élevés de l'installateur pour cette étape
(`runascurrentuser` dans Inno Setup), sans changer les binaires applicatifs.

## Comportement

- La base cloud doit exister et être accessible au compte configuré. Si elle ne
  contient aucune table utilisateur, le moteur crée le schéma courant dans une
  transaction. Il ne purge ni ne recrée une base ayant déjà des tables.
- La copie cloud utilise des contraintes sans suppression physique en cascade.
  Les suppressions synchronisées restent logiques. Cela évite les chemins de
  cascade multiples refusés par SQL Server lors de la création du modèle complet.
- `SyncDestination`, table technique locale, mémorise un hachage serveur/base et
  la date de préparation. Elle ne stocke aucun mot de passe.
- À la première exécution de cette version, ou lors d'un changement de destination,
  l'application enfile toutes les entités du catalogue de synchronisation, sans
  la limite de 500 lignes du rattrapage habituel. Les lignes déjà Pending sont
  conservées ; les lignes autrefois Completed sont réenfilées.
- File et marqueur sont validés dans la même transaction locale. Le marqueur
  signifie « reprise enfilée », pas « transfert terminé ». Un redémarrage reprend
  l'outbox ; un retour à une précédente destination prépare une nouvelle reprise.
- Les données métier locales et leurs identifiants ne sont pas réécrits.
- La reprise automatique exige une source locale contenant exactement une école,
  afin de ne pas copier les enfants d'une autre école. Une source multi-école est
  bloquée avec un message explicite.
- La mise à jour conserve les fichiers de configuration installés. Le lanceur
  `ErpScolaire.Setup.exe` est exclu du paquet de mise à jour.

## Validation

Tests en mémoire : reprise malgré un ancien historique, 601 lignes, valeurs métier
préservées, changement de base, retour à une base précédente, absence de doublon
à destination identique et blocage d'une source multi-école.

Tests SQL : bases temporaires `Codex_SyncTest_*`, création réelle du schéma vide,
idempotence, annulation de la file et du marqueur lors d'une erreur forcée,
marqueur persistant après réouverture et transfert de l'établissement.
Aucune base métier n'est utilisée par ces tests.

Résultat : 63 tests de synchronisation et 3 tests de l'assistant de mise à jour
réussis. Le paquet contient les binaires 2026.10.2 et aucun fichier de configuration
machine. Son API contient le même binaire Infrastructure que la compilation courante.
