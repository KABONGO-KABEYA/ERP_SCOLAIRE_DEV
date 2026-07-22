# Documentation base de données

## Fichier source de vérité — Module Inscription

| Fichier | Rôle |
|---------|------|
| **[inscription-module.schema.yaml](inscription-module.schema.yaml)** | Schéma structuré (YAML) pour génération de documentation par IA |

### Comment l'utiliser avec Claude / Cursor

1. Joindre ou référencer `docs/database/inscription-module.schema.yaml` dans le prompt.
2. Demander par exemple :
   - « Génère une documentation PDF/Markdown du module Inscription à partir de ce fichier »
   - « Dessine un diagramme ER à partir de `relationships` et `tables_core` »
   - « Explique le flux `enrollment_wizard_complete` pour les utilisateurs métier »

### Maintenance

**À chaque modification** du module Inscription (entités, migrations, initializers, services) :

1. Mettre à jour `inscription-module.schema.yaml`
2. Incrémenter `meta.document_version` et `meta.last_updated`
3. Ajouter une entrée dans `changelog`

Fichiers à surveiller : voir section `source_of_truth` du YAML.
