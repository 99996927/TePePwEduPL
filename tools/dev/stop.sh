#!/usr/bin/env bash
# Zatrzymuje rozwiązanie uruchomione przez tools/dev/run.sh.
# Użycie: tools/dev/stop.sh [--reset]
#   (bez opcji)  kończy Hr.Api i zatrzymuje kontener bazy; dane zostają w wolumenie hr-dev-db-data
#   --reset      dodatkowo usuwa kontener hr-dev-db i wolumen hr-dev-db-data (baza zostanie założona od nowa)
#   --api-only   kończy tylko Hr.Api (używane przez run.sh przed ponownym startem)
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

DB_CONTAINER="hr-dev-db"
DB_VOLUME="hr-dev-db-data"
API_PORT=5080
PID_FILE="$ROOT_DIR/.run/api.pid"

MODE="stop"
case "${1:-}" in
  "") ;;
  --reset) MODE="reset" ;;
  --api-only) MODE="api" ;;
  -h | --help)
    sed -n '2,7p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 0
    ;;
  *)
    echo "Nieznana opcja: $1 (dozwolone: --reset)" >&2
    exit 2
    ;;
esac

# Kończy grupę procesów (setsid w run.sh: PID = identyfikator grupy), czeka do 15 s, potem SIGKILL.
kill_tree() {
  local pid="$1"
  kill -TERM -- "-$pid" 2>/dev/null || kill -TERM "$pid" 2>/dev/null || true
  for _ in $(seq 1 30); do
    kill -0 "$pid" 2>/dev/null || return 0
    sleep 0.5
  done
  echo "Proces $pid nie zakończył się w 15 s, wymuszam zakończenie."
  kill -KILL -- "-$pid" 2>/dev/null || kill -KILL "$pid" 2>/dev/null || true
}

# PID-y procesów nasłuchujących na porcie API.
port_pids() {
  ss -ltnpH "sport = :$API_PORT" 2>/dev/null | grep -o 'pid=[0-9]*' | cut -d= -f2 | sort -u || true
}

stop_api() {
  if [[ -f "$PID_FILE" ]]; then
    local pid
    pid="$(cat "$PID_FILE")"
    # Po restarcie maszyny PID z pliku może należeć do innego procesu, więc sprawdzamy jego polecenie.
    if [[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null \
      && ps -o args= -p "$pid" 2>/dev/null | grep -q 'src/Hr.Api'; then
      echo "Kończę Hr.Api (PID $pid)..."
      kill_tree "$pid"
    elif [[ -n "$pid" ]]; then
      echo "Plik .run/api.pid jest nieaktualny (proces $pid nie jest Hr.Api); usuwam go."
    fi
    rm -f "$PID_FILE"
  fi

  # Pozostałość po wcześniejszym uruchomieniu (np. usunięty plik PID): proces Hr.Api/dotnet na porcie API.
  local pid comm
  for pid in $(port_pids); do
    comm="$(ps -o comm= -p "$pid" 2>/dev/null || true)"
    if [[ "$comm" == "Hr.Api" || "$comm" == "dotnet" ]]; then
      echo "Kończę proces $comm (PID $pid) nasłuchujący na porcie $API_PORT..."
      kill_tree "$pid"
    else
      echo "Port $API_PORT zajmuje inny program ($comm, PID $pid); nie zatrzymuję go." >&2
      return 1
    fi
  done
}

stop_api
[[ "$MODE" == "api" ]] && exit 0

if command -v docker >/dev/null 2>&1; then
  if [[ "$MODE" == "reset" ]]; then
    if docker container inspect "$DB_CONTAINER" >/dev/null 2>&1; then
      echo "Usuwam kontener $DB_CONTAINER..."
      docker rm -f "$DB_CONTAINER" >/dev/null
    fi
    if docker volume inspect "$DB_VOLUME" >/dev/null 2>&1; then
      echo "Usuwam wolumen $DB_VOLUME (dane bazy)..."
      docker volume rm "$DB_VOLUME" >/dev/null
    fi
  elif [[ "$(docker container inspect -f '{{.State.Running}}' "$DB_CONTAINER" 2>/dev/null || true)" == "true" ]]; then
    echo "Zatrzymuję kontener $DB_CONTAINER (dane zostają w wolumenie $DB_VOLUME)..."
    # Obraz SQL Server nie przekazuje SIGTERM do serwera (docker stop kończy się SIGKILL, kod 137),
    # dlatego najpierw zamykamy bazę poleceniem SHUTDOWN; przy błędzie i tak zatrzymujemy kontener.
    if [[ -f "$ROOT_DIR/.env" ]]; then
      SQLCMDPASSWORD="$(grep '^HR_DEV_SA_PASSWORD=' "$ROOT_DIR/.env" | tail -n 1 | cut -d= -f2- || true)"
      export SQLCMDPASSWORD
      docker exec -e SQLCMDPASSWORD "$DB_CONTAINER" /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -Q "SHUTDOWN" -b >/dev/null 2>&1 || true
    fi
    docker stop -t 30 "$DB_CONTAINER" >/dev/null
  fi
else
  echo "Brak programu docker: pomijam zatrzymanie bazy." >&2
fi

echo "Zatrzymano."
