# LocalServerDiscovery

Module unique de découverte du serveur API (Desktop .NET).

## Ordre

1. mDNS (`_school-management._tcp` / `school-server.local`)
2. Dernière IP locale connue (`%LocalAppData%/ERP_Scolaire/Discovery`)
3. Scan parallèle du sous-réseau privé (/24) sur le port `5096`
4. Serveur distant (Cloud)

## Health

`GET /api/health` — anonyme, sans base de données.

## Intégration Desktop

```csharp
services.AddLocalServerDiscovery(o =>
{
    o.RemoteBaseUrl = "http://169.58.93.203:1804";
});
services.AddTransient<DiscoveryBaseAddressHandler>();
services.AddHttpClient("SchoolApi", c => c.BaseAddress = new Uri(DiscoveryConstants.PlaceholderBaseUrl))
    .AddHttpMessageHandler<DiscoveryBaseAddressHandler>();
```

Le miroir Flutter vit dans `mobile/.../lib/core/local_server_discovery/`.
