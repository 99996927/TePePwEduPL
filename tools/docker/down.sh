#!/usr/bin/env bash
# Zatrzymuje system uruchomiony przez tools/docker/up.sh.
# Użycie: tools/docker/down.sh [--reset]
#   (bez opcji)  zatrzymuje i usuwa kontenery; dane zostają w wolumenie hr-db-data
#   --reset      dodatkowo usuwa wolumen hr-db-data (baza zostanie założona od nowa); plik .env zostaje
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

case "${1:-}" in
  "") ;;
  --reset) ;;
  -h | --help)
    sed -n '2,6p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
  *)
    echo "Nieznana opcja: $1 (dozwolone: --reset)" >&2
    exit 2
    ;;
esac

command -v docker >/dev/null 2>&1 || {
  echo "BŁĄD: brak programu docker." >&2
  exit 1
}

# Compose wymaga HR_DB_PASSWORD przy interpolacji, a do zatrzymania wartość nie jest potrzebna.
export HR_DB_PASSWORD="${HR_DB_PASSWORD:-nieuzywane}"

if [[ "${1:-}" == "--reset" ]]; then
  echo "Zatrzymuję kontenery i usuwam wolumen hr-db-data (dane bazy)..."
  docker compose down -v
else
  echo "Zatrzymuję kontenery (dane zostają w wolumenie hr-db-data)..."
  docker compose down
fi
echo "Zatrzymano."
