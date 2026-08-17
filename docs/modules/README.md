# Modules métier

| Module | Documentation | Statut |
|--------|---------------|--------|
| **État d'implémentation (5 modules)** | [etat-implementation.md](etat-implementation.md) | ✅ Documenté |
| **Inscription (schéma BDD)** | [inscription-module.schema.yaml](../database/inscription-module.schema.yaml) | ✅ Source de vérité YAML |
| Authentification | — | ✅ Complet |
| Paramétrage | [etat-implementation.md §3](etat-implementation.md#3-paramètres-établissement) | ✅ Desktop + API |
| Élèves | [etat-implementation.md §2](etat-implementation.md#2-module-élève) | ✅ Desktop + API |
| Structure pédagogique | [etat-implementation.md §4](etat-implementation.md#4-structure-pédagogique) | ✅ Desktop + API |
| Années scolaires | [etat-implementation.md §5](etat-implementation.md#5-années-scolaires) | ✅ Desktop + API |
| Frais scolaires | [etat-implementation.md §6](etat-implementation.md#6-frais-scolaires) | ✅ Desktop + API |
| Académique | — | ✅ Desktop + API |
| Notes | [notes-handoff.md](notes-handoff.md) | ✅ Base existante — branche `feature/notes` |
| Présence | [attendances-handoff.md](attendances-handoff.md) | 🚧 Schéma BDD — branche `feature/attendances` |
| Financier | — | ✅ Desktop + API + Mobile parent |
| Documents | — | ✅ Desktop + API |
| Statistiques | — | ✅ Desktop + API + Mobile direction |
| Administration | — | ✅ Desktop + API |

Voir aussi :

- [Guide de démarrage](../guide-demarrage.md)
- [Référence API](../api-reference.md)
- [Architecture](../architecture.md)
- [Schéma Inscription (YAML)](../database/inscription-module.schema.yaml)
- [Sync cloud local → distant](../database/cloud-sync.md)
