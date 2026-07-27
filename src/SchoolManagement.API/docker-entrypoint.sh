#!/bin/sh
set -eu

# Coolify injecte souvent PORT ; défaut public = 1804
PORT="${PORT:-1804}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:${PORT}}"
export PORT

# Alias fréquents Coolify / .env → noms ASP.NET
if [ -z "${SQL_CONNECTION_STRING:-}" ] && [ -n "${ConnectionStrings__DefaultConnection:-}" ]; then
  export SQL_CONNECTION_STRING="$ConnectionStrings__DefaultConnection"
fi
if [ -z "${SQL_CONNECTION_STRING:-}" ] && [ -n "${ConnectionStrings__Default:-}" ]; then
  export SQL_CONNECTION_STRING="$ConnectionStrings__Default"
fi
if [ -z "${Jwt__SecretKey:-}" ] && [ -n "${JWT_SECRET_KEY:-}" ]; then
  export Jwt__SecretKey="$JWT_SECRET_KEY"
fi
if [ -z "${Jwt__Issuer:-}" ] && [ -n "${JWT_ISSUER:-}" ]; then
  export Jwt__Issuer="$JWT_ISSUER"
fi
if [ -z "${Jwt__Audience:-}" ] && [ -n "${JWT_AUDIENCE:-}" ]; then
  export Jwt__Audience="$JWT_AUDIENCE"
fi

echo "Starting SchoolManagement.API on ${ASPNETCORE_URLS}"
echo "Deployment Role=${Deployment__Role:-unset} ReadOnly=${Deployment__ReadOnly:-unset}"

if [ -z "${SQL_CONNECTION_STRING:-}" ]; then
  echo "================================================================"
  echo "FATAL: SQL_CONNECTION_STRING is missing in Coolify environment."
  echo "Add this runtime variable (Environment Variables), then Redeploy:"
  echo ""
  echo "  SQL_CONNECTION_STRING=Server=IP_SQL,1433;Database=SchoolManagementRDC;User Id=sa;Password=***;TrustServerCertificate=True;Encrypt=True"
  echo "  Jwt__SecretKey=... (min 32 chars)   OR   JWT_SECRET_KEY=..."
  echo "  PORT=1804"
  echo "  Deployment__Role=Cloud"
  echo "  Deployment__ReadOnly=true"
  echo "  FILE_STORAGE_ROOT=/app/data/files"
  echo "================================================================"
  exit 1
fi

if [ -z "${Jwt__SecretKey:-}" ]; then
  echo "================================================================"
  echo "FATAL: Jwt__SecretKey / JWT_SECRET_KEY is missing."
  echo "Add Jwt__SecretKey (min 32 characters) in Coolify Environment Variables."
  echo "================================================================"
  exit 1
fi

exec dotnet SchoolManagement.API.dll
