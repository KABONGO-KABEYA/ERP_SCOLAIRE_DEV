# Installateur ERP Scolaire — Assistant 100 % automatisé

Un seul package pour **Serveur** et **Poste Client**. Après **Terminer**, le serveur est opérationnel : SQL, Cloud, fichiers, service Windows, sync — sans script PowerShell ni édition manuelle de fichiers.

## Générer le package

```powershell
cd "D:\Mes Projet\ERP_Administration_Scolaire_2026"
.\scripts\build-setup.ps1
# Optionnel (machines sans .NET 8 Runtime) :
.\scripts\build-setup.ps1 -SelfContained
```

Sortie par défaut : `C:\Temp\ERP_Scolaire_Setup\`

| Élément | Rôle |
|---------|------|
| `ErpScolaire.Setup.exe` | Assistant d'installation (Admin) |
| `payload\desktop\` | Application bureau |
| `payload\api\` | API locale + service |
| `payload\sql\010_Purge_Production_Virgin.sql` | Base Production vierge |

Runtime : sans `-SelfContained`, installer [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

## Installation Serveur (assistant)

1. Exécuter `ErpScolaire.Setup.exe` **en administrateur**.
2. **Étape 1** — Type : Installation Serveur.
3. **Étape 2** — SQL local (instance, base, auth) → **Tester** (obligatoire).
4. **Étape 3** — SQL Cloud (IP, port, base, login) → **Tester** → enregistre `ServeurDonneesCloud.txt` (ACTIF=1, DPAPI).
5. **Étape 4** — Dossier fichiers (ex. `D:\ERP_SCOLAIRE`) → créer, partager, permissions.
6. **Étape 5** — Récap → **Terminer** (vérifications finales bloquantes).

Le Setup réalise automatiquement :

- copie API + Desktop ;
- création / accès base SQL + login `NT AUTHORITY\SYSTEM` ;
- `ServeurDonnees.txt`, `ServeurFichiers.txt`, `appsettings` ;
- sync cloud activée ;
- purge métier (base vierge, permissions conservées) ;
- service Windows `ErpScolaireApi` + firewall + health check.

## Installation Poste Client

1. Type : Installation Poste Client.
2. URL API du serveur école.
3. Terminer → Desktop uniquement (`Api:ClientMode=true`, pas de SQL local à configurer).

## Premier démarrage Desktop

Si aucune école n'existe en base, un **assistant de configuration initiale** s'affiche avant la connexion :

1. Informations établissement (nom, adresse, téléphone, email, logo, devise)
2. Première année scolaire
3. Compte Administrateur
4. Types de frais / tranches / catégories tarifaires de base

Aucun module métier n'est accessible tant que cet assistant n'est pas terminé.

## Base Production vierge

Conservé : permissions, nomenclatures géo, paramètres techniques.  
Supprimé : école, élèves, parents, professeurs, paiements, frais, notifications, logs métier.

## Prérequis machine serveur

- Windows 10/11 ou Server, droits administrateur
- SQL Server accessible
- Accès réseau au SQL Cloud (si sync)
- Disque pour le dossier fichiers
- Smart App Control désactivé si le système bloque les binaires non signés
