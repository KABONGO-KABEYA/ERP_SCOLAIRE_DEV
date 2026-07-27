# Cartes élèves — Phase 1 (socle métier)

Module métier indépendant pour les cartes scolaires. Le QR et le numéro de carte sont des **identifiants universels** pour les futurs modules (paiements, présences, accès, etc.).

## Livré

### Phase 1 — socle
| Couche | Contenu |
|--------|---------|
| Domaine | `StudentCard`, `CardTemplate`, `CardSchoolSettings`, `StudentCardHistory`, `StudentCardPrintLog` + enums |
| SQL | `StudentCardSchemaInitializer` → tables `Carte*`, `CarteModele`, `CarteParametres`, `CarteHistorique`, `CarteImpression` |
| Application | `IStudentCardService` / `StudentCardService` |
| API | `api/v1/cards`, `api/v1/card-templates` |
| Permissions | `student-cards.*`, `card-templates.*` |
| Tests | `StudentCardServiceTests` |

### Phase 2 — Desktop (opérationnel)
| Élément | Contenu |
|---------|---------|
| Menu | Module top-level **Cartes élèves** |
| UI | KPI, filtres, liste paginée, fiche détail, création, actions métier |
| Client | `IStudentCardApiService` |
| Année | Filtre global via la barre du haut |

### Phase 3 — Concepteur + impression graphique
| Élément | Contenu |
|---------|---------|
| Layout JSON | `CardLayoutDocument` / éléments (texte, photo, logo, QR, formes) |
| Concepteur | `CardTemplateDesignerWindow` — drag & drop, grille 1 mm, recto/verso, propriétés |
| Impression | `IStudentCardPrintService` — aperçu, impression unitaire / lot via `PrintVisual` |
| Défaut | Modèle CR80 standard généré automatiquement |

## Règles clés

- Une seule carte **Active** par élève / année scolaire.
- Numéro unique indépendant du matricule : `{PREFIX}-{YEAR}-{SEQ}` (ex. `CSB-2026-000001`).
- QR = `ERP_CARD:{QrToken}` — **aucune** donnée personnelle.
- Perte / vol / désactivation → statut terminal ; impressions et mutations refusées.
- Renouvellement : crée une nouvelle carte ; conserve ou régénère le QR selon `KeepQrOnRenewal` (paramètre école, surchargeable).
- Réimpression : incrémente `PrintCount` + journal `CarteImpression` (pas forcément une nouvelle carte).

## Phases suivantes

4. **Mobile** : scan QR pour contrôle d’accès / présence.
5. Enrichissements designer : redimensionnement poignées, calques avancés, export PDF QuestPDF, hologramme / signature numérique.

## Endpoints principaux

- `GET /api/v1/cards`, `GET /api/v1/cards/{id}`, `POST /api/v1/cards`
- `POST /api/v1/cards/print`, `.../reprint`, `.../renew`, `.../lost`, `.../stolen`, `.../deactivate`
- `POST /api/v1/cards/resolve-qr` — résolution pour modules futurs
- `GET|POST|PUT|DELETE /api/v1/card-templates`, `POST .../preview`
