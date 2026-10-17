#!/usr/bin/env bash
# Uruchamia całe rozwiązanie do testów ręcznych: SQL Server w Dockerze, Angular zbudowany do wwwroot i Hr.Api w tle.
# Użycie: tools/dev/run.sh   (można uruchamiać wielokrotnie; poprzednie Hr.Api jest kończone)
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT_DIR="$ROOT_DIR/tools/dev"
cd "$ROOT_DIR"

DB_CONTAINER="hr-dev-db"
DB_VOLUME="hr-dev-db-data"
DB_IMAGE="mcr.microsoft.com/mssql/server:2022-latest"
DB_PORT=14330
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
API_URL="http://127.0.0.1:5080"
RUN_DIR="$ROOT_DIR/.run"
ENV_FILE="$ROOT_DIR/.env"

step() { echo; echo "==> $1"; }
die() {
  echo "BŁĄD: $1" >&2
  exit 1
}

# 1. Wymagane programy
step "Sprawdzam wymagane programy"
for tool in docker dotnet node npm curl ss setsid; do
  command -v "$tool" >/dev/null 2>&1 || die "brak programu '$tool'. Zainstaluj go i uruchom skrypt ponownie."
done
docker info >/dev/null 2>&1 || die "Docker nie odpowiada (czy usługa działa i czy masz uprawnienia do docker?)."
echo "docker, dotnet $(dotnet --version), node $(node --version): OK"

# 2. Hasło SA w .env (tworzone przy pierwszym uruchomieniu, nigdy nie wypisywane)
step "Wczytuję hasło bazy z .env"
generate_password() {
  local raw
  raw="$(head -c 48 /dev/urandom | base64 -w0)"
  raw="${raw//[^A-Za-z0-9]/}"
  # Stały sufiks gwarantuje wielką i małą literę, cyfrę oraz znaki specjalne (polityka SQL Server).
  printf 'Hr%sZa7-_' "${raw:0:20}"
}
if [[ ! -f "$ENV_FILE" ]] || ! grep -q '^HR_DEV_SA_PASSWORD=' "$ENV_FILE"; then
  # umask 077: plik powstaje od razu z uprawnieniami 600 (bez okna z domyślnymi uprawnieniami).
  (umask 077 && echo "HR_DEV_SA_PASSWORD=$(generate_password)" >>"$ENV_FILE")
  echo "Utworzono hasło SA w $ENV_FILE."
fi
chmod 600 "$ENV_FILE"
HR_DEV_SA_PASSWORD="$(grep '^HR_DEV_SA_PASSWORD=' "$ENV_FILE" | tail -n 1 | cut -d= -f2-)"
[[ -n "$HR_DEV_SA_PASSWORD" ]] || die "pusta wartość HR_DEV_SA_PASSWORD w $ENV_FILE."
# Przekazywane do kontenera przez środowisko (docker -e NAZWA), a nie w argumentach polecenia.
export MSSQL_SA_PASSWORD="$HR_DEV_SA_PASSWORD"
export SQLCMDPASSWORD="$HR_DEV_SA_PASSWORD"
echo "OK"

# 3. Kontener SQL Server
step "Uruchamiam bazę ($DB_CONTAINER na 127.0.0.1:$DB_PORT)"
since="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
state="$(docker container inspect -f '{{.State.Running}}' "$DB_CONTAINER" 2>/dev/null || echo "brak")"
case "$state" in
  true) echo "Kontener już działa." ;;
  false)
    echo "Wznawiam istniejący kontener."
    docker start "$DB_CONTAINER" >/dev/null
    ;;
  *)
    echo "Tworzę kontener (wolumen $DB_VOLUME)."
    docker run -d --name "$DB_CONTAINER" \
      -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD \
      -p "127.0.0.1:$DB_PORT:1433" \
      -v "$DB_VOLUME:/var/opt/mssql" \
      "$DB_IMAGE" >/dev/null
    ;;
esac

echo "Czekam na gotowość bazy (do 90 s)..."
ready=false
for _ in $(seq 1 45); do
  if out="$(docker exec -e SQLCMDPASSWORD "$DB_CONTAINER" "$SQLCMD" -C -S localhost -U sa -Q "SELECT 1" -b 2>&1)"; then
    ready=true
    break
  fi
  if [[ "$out" == *"Login failed"* ]] && docker logs --since "$since" "$DB_CONTAINER" 2>&1 | grep -q "Password did not match"; then
    die "hasło SA z .env nie pasuje do bazy w istniejącym wolumenie $DB_VOLUME (wolumen pochodzi z wcześniejszego uruchomienia z innym plikiem .env).
Przywróć poprzedni plik .env albo usuń bazę i załóż ją od nowa: tools/dev/stop.sh --reset && tools/dev/run.sh"
  fi
  if [[ "$(docker container inspect -f '{{.State.Running}}' "$DB_CONTAINER" 2>/dev/null)" != "true" ]]; then
    docker logs --tail 20 "$DB_CONTAINER" >&2 || true
    die "kontener $DB_CONTAINER zakończył działanie (log powyżej)."
  fi
  sleep 2
done
$ready || die "baza nie odpowiedziała w 90 s. Sprawdź: docker logs $DB_CONTAINER"
echo "Baza gotowa."

# 4. Frontend
step "Buduję Angulara (npm ci, build:dev, build:wwwroot)"
(
  cd "$ROOT_DIR/web"
  npm ci --no-audit --no-fund
  npm run build:dev
  npm run build:wwwroot
)

# 5. Hr.Api w tle
step "Uruchamiam Hr.Api na $API_URL"
"$SCRIPT_DIR/stop.sh" --api-only
# Bez węzłów MSBuild i serwera kompilatora w tle: po skrypcie zostaje tylko Hr.Api.
dotnet build "$ROOT_DIR/src/Hr.Api" -nologo -v quiet -nodeReuse:false -p:UseSharedCompilation=false || die "budowanie Hr.Api nie powiodło się."
mkdir -p "$RUN_DIR"
(
  export ASPNETCORE_URLS="$API_URL"
  export ASPNETCORE_ENVIRONMENT=Development
  export ConnectionStrings__Hr="Server=127.0.0.1,$DB_PORT;Database=hr_dev;User Id=sa;Password=$HR_DEV_SA_PASSWORD;TrustServerCertificate=True"
  export Hr__SeedDemoData=true
  unset MSSQL_SA_PASSWORD SQLCMDPASSWORD
  setsid nohup dotnet run --project "$ROOT_DIR/src/Hr.Api" --no-launch-profile --no-build \
    >"$RUN_DIR/api.log" 2>&1 </dev/null &
  echo $! >"$RUN_DIR/api.pid"
)
api_pid="$(cat "$RUN_DIR/api.pid")"
echo "PID $api_pid, log: .run/api.log"

# 6. Gotowość API
echo "Czekam na $API_URL/api/health (do 120 s; pierwszy start zakłada bazę i dane demo)..."
healthy=false
for _ in $(seq 1 120); do
  if curl -fsS --max-time 2 "$API_URL/api/health" >/dev/null 2>&1; then
    healthy=true
    break
  fi
  if ! kill -0 "$api_pid" 2>/dev/null; then
    tail -n 30 "$RUN_DIR/api.log" >&2 || true
    die "Hr.Api zakończyło działanie (ostatnie wiersze .run/api.log powyżej)."
  fi
  sleep 1
done
$healthy || die "Hr.Api nie odpowiedziało w 120 s. Sprawdź .run/api.log."
echo "Hr.Api działa."

# 7. Test dymny
step "Test dymny"
"$SCRIPT_DIR/smoke.sh" "$API_URL"

# 8. Podsumowanie
step "Gotowe"
cat <<INFO
Aplikacja: http://localhost:5080  (konta demo: docs/arc42/07-widok-wdrozenia.md, sekcja 7.2)
Z innego komputera otwórz tunel SSH i wejdź na http://localhost:5080:
  ssh -N -L 5080:127.0.0.1:5080 $(whoami)@$(hostname)
Log API: .run/api.log; zatrzymanie: tools/dev/stop.sh (z --reset usuwa też bazę).
INFO
