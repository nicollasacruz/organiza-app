#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ ! -f .env ]]; then echo 'Copie .env.example para .env e configure DEV_DB_PASSWORD.' >&2; exit 1; fi
set -a
source .env
set +a
: "${DEV_DB_PASSWORD:?Configure DEV_DB_PASSWORD}"
export ConnectionStrings__Database="Host=localhost;Port=5432;Database=organiza_dev;Username=organiza;Password=$DEV_DB_PASSWORD"
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5080
export App__PublicOrigin=http://localhost:3000
export App__PasskeyDomain=localhost
export App__KeyPath="$PWD/.local/keys"
export Google__ClientId="${GOOGLE_CLIENT_ID:-}"
export Google__ClientSecret="${GOOGLE_CLIENT_SECRET:-}"
export OpenRouter__ApiKey="${OPENROUTER_API_KEY:-}"
export Google__RedirectUri=http://localhost:3000/api/integrations/google/callback
exec dotnet run --project backend/Organiza.Api -- "$@"
