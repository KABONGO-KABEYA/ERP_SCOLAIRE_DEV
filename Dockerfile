# Build context = racine du dépôt (Coolify / docker compose).
# Port public distant de l'API Cloud = 1804 (conteneur + hôte).
# La base SQL distante reste sur son propre port (souvent 1433), via SQL_CONNECTION_STRING.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/SchoolManagement.Shared/SchoolManagement.Shared.csproj src/SchoolManagement.Shared/
COPY src/SchoolManagement.Domain/SchoolManagement.Domain.csproj src/SchoolManagement.Domain/
COPY src/SchoolManagement.Application/SchoolManagement.Application.csproj src/SchoolManagement.Application/
COPY src/SchoolManagement.Infrastructure/SchoolManagement.Infrastructure.csproj src/SchoolManagement.Infrastructure/
COPY src/SchoolManagement.API/SchoolManagement.API.csproj src/SchoolManagement.API/

RUN dotnet restore src/SchoolManagement.API/SchoolManagement.API.csproj

COPY src/SchoolManagement.Shared/ src/SchoolManagement.Shared/
COPY src/SchoolManagement.Domain/ src/SchoolManagement.Domain/
COPY src/SchoolManagement.Application/ src/SchoolManagement.Application/
COPY src/SchoolManagement.Infrastructure/ src/SchoolManagement.Infrastructure/
COPY src/SchoolManagement.API/ src/SchoolManagement.API/

RUN dotnet publish src/SchoolManagement.API/SchoolManagement.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data/files /app/logs \
    && chmod -R 777 /app/data /app/logs

ENV ASPNETCORE_URLS=http://0.0.0.0:1804 \
    ASPNETCORE_ENVIRONMENT=Production \
    FILE_STORAGE_ROOT=/app/data/files \
    Deployment__Role=Cloud \
    Deployment__ReadOnly=true

EXPOSE 1804

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=45s --retries=3 \
  CMD curl -fsS http://127.0.0.1:1804/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "SchoolManagement.API.dll"]
