# Documentation OpenAPI / Swagger

Fichiers générés par `scripts/export-api-docs.ps1` :

| Fichier | Contenu |
|---------|---------|
| `../api-reference.md` | Catalogue lisible de **tous** les endpoints |
| `openapi.v1.json` | OpenAPI 3.0 (chemins + auth) — import Postman / Insomnia |
| `swagger.v1.json` | Spec Swagger runtime complète (si exportée depuis une API démarrée) |
| `endpoints.csv` | Liste brute des endpoints |

## Consulter en live

- Local : http://localhost:5041/swagger
- Cloud : http://169.58.93.203:1804/swagger

## Régénérer

```powershell
cd "d:\Mes Projet\ERP_Administration_Scolaire_2026"
.\scripts\export-api-docs.ps1
# Avec spec runtime (API démarrée) :
.\scripts\export-api-docs.ps1 -SwaggerUrl "http://localhost:5041/swagger/v1/swagger.json"
```
