#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ ! -f .env ]]; then
  echo 'O .env privado deve estar nesta pasta antes do deploy.' >&2
  exit 1
fi
# The proxy network already belongs to the server; never create a replacement.
REVERSE_PROXY_CIDR="$(docker network inspect reverse-proxy --format '{{(index .IPAM.Config 0).Subnet}}')"
if [[ -z "$REVERSE_PROXY_CIDR" ]]; then
  echo 'A rede reverse-proxy não tem uma subnet configurada.' >&2
  exit 1
fi
export REVERSE_PROXY_CIDR
# Persist the detected subnet so later compose commands (bootstrap, logs, updates) work too.
umask 077
env_temp="$(mktemp .env.XXXXXX)"
trap 'rm -f "$env_temp"' EXIT
awk -v cidr="$REVERSE_PROXY_CIDR" '
  /^REVERSE_PROXY_CIDR=/ { print "REVERSE_PROXY_CIDR=" cidr; found=1; next }
  { print }
  END { if (!found) print "REVERSE_PROXY_CIDR=" cidr }
' .env > "$env_temp"
mv "$env_temp" .env
trap - EXIT
docker compose config --quiet
docker compose build
docker compose up -d db
docker compose run --rm app init-db
docker compose up -d
docker compose ps
