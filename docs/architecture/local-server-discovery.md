# Local Server Discovery

## Objectif

Découverte automatique du serveur API sans configuration utilisateur.

## Priorité

1. mDNS (`_school-management._tcp` / `school-server.local`)
2. Dernière IP locale connue
3. Scan parallèle du sous-réseau privé (/24) sur le port **5096**
4. Serveur distant Cloud

## Health

`GET /api/health` (anonyme, sans DB)

```json
{
  "status": "ok",
  "server": "local",
  "school": "École",
  "version": "1.0.0",
  "time": "2026-07-30T10:15:00Z"
}
```

## Modules

| Plateforme | Emplacement |
|------------|-------------|
| .NET (Desktop + API advertise) | `src/SchoolManagement.LocalServerDiscovery/` |
| Flutter | `mobile/.../lib/core/local_server_discovery/` |

## Port local

L’API locale écoute **5096** (et conserve **5041** en secours en Development).
Docker / Cloud reste sur **1804**.
