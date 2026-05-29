#!/usr/bin/env bash
# Copia app.env ou .env para a pasta de publish (deploy IIS/Plesk).
# Uso: ./scripts/copy-env-to-publish.sh [pasta_publish]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_DIR="${1:-$BACKEND_DIR/publish}"

SOURCE=""
if [[ -f "$BACKEND_DIR/app.env" ]]; then
  SOURCE="$BACKEND_DIR/app.env"
elif [[ -f "$BACKEND_DIR/.env" ]]; then
  SOURCE="$BACKEND_DIR/.env"
else
  echo "Erro: crie backend/app.env ou backend/.env antes do deploy." >&2
  exit 1
fi

mkdir -p "$PUBLISH_DIR"
cp "$SOURCE" "$PUBLISH_DIR/app.env"
cp "$SOURCE" "$PUBLISH_DIR/.env"
echo "Copiado para:"
echo "  $PUBLISH_DIR/app.env"
echo "  $PUBLISH_DIR/.env"
