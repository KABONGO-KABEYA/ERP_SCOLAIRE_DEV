# Build context = racine du dépôt (Coolify / docker compose).
# Port public API Cloud = 1804 (surchargeable via PORT / ASPNETCORE_URLS).
# SQL distant = SQL_CONNECTION_STRING (souvent port 1433) — obligatoire sur Coolify.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/SchoolManagement.Shared/SchoolManagement.Shared.csproj src/SchoolManagement.Shared/
COPY src/SchoolManagement.Domain/SchoolManagement.Domain.csproj src/SchoolManagement.Domain/
COPY src/SchoolManagement.Application/SchoolManagement.Application.csproj src/SchoolManagement.Application/
COPY src/SchoolManagement.Infrastructure/SchoolManagement.Infrastructure.csproj src/SchoolManagement.Infrastructure/
COPY src/SchoolManagement.LocalServerDiscovery/SchoolManagement.LocalServerDiscovery.csproj src/SchoolManagement.LocalServerDiscovery/
COPY src/SchoolManagement.API/SchoolManagement.API.csproj src/SchoolManagement.API/

RUN dotnet restore src/SchoolManagement.API/SchoolManagement.API.csproj

COPY src/SchoolManagement.Shared/ src/SchoolManagement.Shared/
COPY src/SchoolManagement.Domain/ src/SchoolManagement.Domain/
COPY src/SchoolManagement.Application/ src/SchoolManagement.Application/
COPY src/SchoolManagement.Infrastructure/ src/SchoolManagement.Infrastructure/
COPY src/SchoolManagement.LocalServerDiscovery/ src/SchoolManagement.LocalServerDiscovery/
COPY src/SchoolManagement.API/ src/SchoolManagement.API/

RUN dotnet publish src/SchoolManagement.API/SchoolManagement.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
 && cp src/SchoolManagement.API/docker-entrypoint.sh /app/publish/docker-entrypoint.sh \
 && chmod +x /app/publish/docker-entrypoint.sh

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data/files /app/logs \
    && chmod -R 777 /app/data /app/logs

ENV PORT=1804 \
    ASPNETCORE_URLS=http://0.0.0.0:1804 \
    ASPNETCORE_ENVIRONMENT=Production \
    FILE_STORAGE_ROOT=/app/data/files \
    SERVER_IDENTITY_DIR=/app/data/files/server-identity \
    Deployment__Role=Cloud \
    Deployment__ReadOnly=true

EXPOSE 1804

COPY --from=build /app/publish .

# Shell form = expansion de $PORT (Coolify peut injecter PORT)
HEALTHCHECK --interval=20s --timeout=8s --start-period=180s --retries=6 \
  CMD curl -fsS "http://127.0.0.1:${PORT:-1804}/api/v1/health" || exit 1

ENTRYPOINT ["./docker-entrypoint.sh"]
