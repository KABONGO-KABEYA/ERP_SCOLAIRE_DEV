# Correctif de démarrage — 2026.10.3

Le journal client du 7 octobre à 13:42:42 signale une erreur SQL 547 sur
`FK_RegistrationNumberCounters_Schools_SchoolId`, dans
`RegistrationNumberCounterSchemaInitializer.EnsureCreatedAsync`.

La reconstruction des compteurs à partir de `Students` tentait de créer un
compteur pour un identifiant d'école absent de `Schools`. L'erreur fatale arrêtait
l'API ; le retour aux anciens binaires reproduisait la même initialisation.

Le correctif sélectionne uniquement les élèves dont l'école existe pour préparer
les compteurs. Un avertissement indique combien d'élèves ont une référence d'école
introuvable. Aucune suppression ni réaffectation d'élève n'est effectuée. Les
compteurs existants et les matricules sont conservés.

Ces références d'école incohérentes restent à examiner sur la base actuelle du
client avant de confirmer la synchronisation complète. Le correctif de démarrage
ne détermine pas à quelle école ces élèves doivent appartenir.

La version affichée par l'assistant provient désormais des métadonnées du binaire,
au lieu de l'ancien libellé constant `2026.9.0`. Le paquet conserve également le
correctif d'élévation et la reprise par destination de 2026.10.2.

## Installer

1. Sauvegarder la base actuelle du client et ses configurations.
2. Fermer l'application et l'ancien assistant.
3. Exécuter `ERP_Scolaire_Update_2026.10.3.exe` en administrateur, puis lancer
   l'assistant de mise à jour. Conserver le dossier d'installation existant.
4. Vérifier la réussite du redémarrage et les nouveaux messages du journal API.
   Ne pas désinstaller l'application et ne pas réinstaller la base depuis un backup.
5. Conserver la configuration cloud choisie sur le PC du client ; le paquet
   n'intègre aucun serveur, compte, mot de passe ou nom de base client.

## Validation

64 tests SQL / synchronisation réussis, dont une reproduction avec une école valide,
des élèves ayant une école absente, un identifiant vide et un ancien matricule.
Le test vérifie la création du bon compteur, la conservation d'un compteur existant,
l'idempotence et l'égalité des données élèves avant / après initialisation.
Tous les tests SQL utilisent uniquement des bases temporaires `Codex_SyncTest_*`.
