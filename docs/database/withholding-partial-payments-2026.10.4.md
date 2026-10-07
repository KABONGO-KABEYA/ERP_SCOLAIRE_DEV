# Mise à jour 2026.10.4 — retenues sur acomptes

Le complément d'un acompte pouvait recalculer une retenue fixe déjà appliquée. La détection du premier paiement utilisait un solde lu avant l'enregistrement de ses modifications, provoquant une collision sur IX_FinRetenueApplication_Unique.

La correction consulte les applications de retenues existantes, les anciennes allocations et les paiements terminés. Elle tient aussi compte des applications en attente d'enregistrement dans le même encaissement. La modification d'un paiement conserve les retenues fixes qui lui appartiennent. Les retenues en pourcentage gardent leur fonctionnement par paiement.

L'index unique reste actif. La mise à jour ne réactive aucune configuration et ne réécrit aucun ancien paiement. Les tests couvrent notamment un acompte de 20 puis un complément de 80 avec une retenue fixe de 5 : total net 95 et une seule application de retenue. Ce scénario et la modification du premier paiement sont vérifiés sur une base SQL temporaire.

## Installation

1. Sauvegarder la base et fermer les postes de l'application.
2. Installer ERP_Scolaire_Update_2026.10.4.exe en administrateur sur l'installation existante, sans désinstallation préalable. Cette version inclut les corrections précédentes de synchronisation, de démarrage et d'élévation.
3. Après une mise à jour réussie, réactiver dans le module de configuration uniquement les retenues désactivées pour contourner cette erreur.
4. Ne pas ressaisir un complément déjà encaissé avec la retenue désactivée. Vérifier séparément les encaissements effectués pendant cette désactivation si une régularisation est nécessaire.

Les références d'école manquantes signalées par la version 2026.10.3 nécessitent toujours un examen avant de conclure que toutes les données peuvent se synchroniser.
