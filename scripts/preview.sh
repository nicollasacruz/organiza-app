#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
(cd frontend && STATIC_EXPORT=1 npm run build)
mkdir -p backend/Organiza.Api/wwwroot
cp -R frontend/out/. backend/Organiza.Api/wwwroot/
exec ./scripts/api.sh
