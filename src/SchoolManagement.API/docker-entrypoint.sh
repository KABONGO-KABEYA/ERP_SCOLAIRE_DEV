#!/bin/sh
set -eu

PORT="${PORT:-1804}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://0.0.0.0:${PORT}}"
export PORT

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
  echo "FATAL: SQL_CONNECTION_STRING is missing in Coolify Environment Variables."
  exit 1
fi

SQL_HOST_HINT=$(printf '%s' "$SQL_CONNECTION_STRING" | sed -n 's/.*[Ss]erver=\([^;]*\).*/\1/p' | head -n 1)
echo "SQL Server target: ${SQL_HOST_HINT:-unknown}"

if [ -z "${Jwt__SecretKey:-}" ]; then
  echo "FATAL: Jwt__SecretKey / JWT_SECRET_KEY is missing."
  exit 1
fi

set +e
dotnet SchoolManagement.API.dll
code=$?
echo "SchoolManagement.API exited with code ${code}"
exit "${code}"
