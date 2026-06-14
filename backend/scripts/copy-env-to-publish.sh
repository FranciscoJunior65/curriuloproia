#!/usr/bin/env bash
# O publish ja inclui backend/.env automaticamente. Use publish-production ou dotnet publish.

set -euo pipefail
PUBLISH_DIR="${1:-$(dirname "$0")/../publish}"
BACKEND_DIR="$(dirname "$0")/.."
SOURCE="$BACKEND_DIR/.env"

if [[ ! -f "$SOURCE" ]]; then
  echo "Erro: crie backend/.env antes do deploy." >&2
  exit 1
fi

mkdir -p "$PUBLISH_DIR"
cp "$SOURCE" "$PUBLISH_DIR/.env"
echo "  $PUBLISH_DIR/.env"
