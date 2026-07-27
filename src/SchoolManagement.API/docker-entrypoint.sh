#!/bin/sh
set -eu

# Coolify injecte souvent PORT ; défaut public = 1804
PORT="${PORT:-1804}"
export ASPNETCORE_URLS="http://0.0.0.0:${PORT}"

echo "Starting SchoolManagement.API on ${ASPNETCORE_URLS}"
echo "Deployment Role=${Deployment__Role:-unset} ReadOnly=${Deployment__ReadOnly:-unset}"
if [ -z "${SQL_CONNECTION_STRING:-}" ] && [ -z "${ConnectionStrings__Default:-}" ] && [ -z "${ConnectionStrings__DefaultConnection:-}" ]; then
  echo "WARNING: SQL_CONNECTION_STRING is empty — the API will exit if no ServeurDonnees.txt is present."
fi

exec dotnet SchoolManagement.API.dll
