#!/usr/bin/env bash
# Uruchamia cały system w Docker Compose: SQL Server + Hr.Api z Angularem na http://127.0.0.1:5080.
# Użycie: tools/docker/up.sh   (można uruchamiać wielokrotnie; dane zostają w wolumenie hr-db-data)
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT_DIR="$ROOT_DIR/tools/docker"
cd "$ROOT_DIR"

API_PORT=5080
API_URL="http://127.0.0.1:$API_PORT"
ENV_FILE="$ROOT_DIR/.env"

step() { echo; echo "==> $1"; }
die() {
  echo "BŁĄD: $1" >&2
  exit 1
}

# 1. Wymagane programy
step "Sprawdzam wymagane programy"
for tool in docker curl ss python3; do
  command -v "$tool" >/dev/null 2>&1 || die "brak programu '$tool'. Zainstaluj go i uruchom skrypt ponownie."
done
docker compose version >/dev/null 2>&1 || die "brak wtyczki Docker Compose v2 ('docker compose')."
docker info >/dev/null 2>&1 || die "Docker nie odpowiada (czy usługa działa i czy masz uprawnienia do docker?)."
echo "docker $(docker version -f '{{.Server.Version}}'), compose $(docker compose version --short): OK"

# 2. Port aplikacji: wolny albo zajęty przez własny kontener hr-app
step "Sprawdzam port $API_PORT"
own_port_published="false"
if docker port hr-app 2>/dev/null | grep -q "127.0.0.1:$API_PORT"; then
  own_port_published="true"
fi
if [[ "$own_port_published" != "true" ]] && ss -ltnH "sport = :$API_PORT" 2>/dev/null | grep -q .; then
  die "port $API_PORT jest zajęty przez inny program. Jeśli to wcześniejsze tools/dev/run.sh, zatrzymaj je: tools/dev/stop.sh
Sprawdź, co nasłuchuje: ss -ltnp 'sport = :$API_PORT'"
fi
echo "OK"

# 3. Sekrety w .env (hasło bazy tworzone przy pierwszym uruchomieniu, nigdy nie wypisywane)
step "Wczytuję konfigurację z .env"
generate_password() {
  local raw
  raw="$(head -c 48 /dev/urandom | base64 -w0)"
  raw="${raw//[^A-Za-z0-9]/}"
  # Stały sufiks gwarantuje wielką i małą literę, cyfrę oraz znaki specjalne (polityka SQL Server).
  printf 'Hr%sZa7-_' "${raw:0:20}"
}
trim_spaces() {
  local v="$1"
  v="${v#"${v%%[![:space:]]*}"}"
  printf '%s' "${v%"${v##*[![:space:]]}"}"
}
env_value() {
  local v
  v="$(grep "^$1=" "$ENV_FILE" 2>/dev/null | tail -n 1 | cut -d= -f2- || true)"
  trim_spaces "${v%$'\r'}"
}
if [[ ! -f "$ENV_FILE" ]]; then
  (umask 077 && : >"$ENV_FILE")
fi
if [[ -z "$(env_value HR_DB_PASSWORD)" ]]; then
  # Pusta wartość (np. skopiowana z .env.example) oznacza brak hasła: usuwamy ten wiersz i dopisujemy wygenerowane.
  if grep -q '^HR_DB_PASSWORD=' "$ENV_FILE"; then
    sed -i '/^HR_DB_PASSWORD=/d' "$ENV_FILE"
  fi
  (umask 077 && echo "HR_DB_PASSWORD=$(generate_password)" >>"$ENV_FILE")
  echo "Utworzono hasło bazy (HR_DB_PASSWORD) w $ENV_FILE."
fi
chmod 600 "$ENV_FILE"

anthropic_key="$(env_value ANTHROPIC_API_KEY)"
if [[ ${#anthropic_key} -ge 2 ]]; then
  case "$anthropic_key" in
    \"*\" | \'*\') anthropic_key="$(trim_spaces "${anthropic_key:1:${#anthropic_key}-2}")" ;;
  esac
fi
if [[ -n "$anthropic_key" ]]; then
  echo "Klucz Anthropic znaleziony w .env (czat korzysta z prawdziwego modelu)."
else
  echo "Brak ANTHROPIC_API_KEY w .env: czat agenta zwróci komunikat o niedostępności (503); pozostałe ekrany działają."
fi
unset anthropic_key

# 4. Budowanie i start (Compose czyta wartości z .env; skrypt nie przekazuje ich w argumentach)
step "Buduję obraz i uruchamiam system (pierwszy raz kilka minut)"
if ! docker compose up -d --build --wait; then
  recent_logs="$(docker compose logs --tail 200 hr-app hr-db 2>&1 || true)"
  printf '%s\n' "$recent_logs" | tail -n 30 >&2
  # Do zmiennej, a nie potokiem: grep -q kończy się wcześniej, a pipefail uznałby to za błąd.
  if grep -qF "Login failed for user 'sa'" <<<"$recent_logs"; then
    die "hasło bazy z .env nie pasuje do bazy w istniejącym wolumenie hr-db-data (wolumen pochodzi z wcześniejszego uruchomienia z innym plikiem .env).
Przywróć poprzedni plik .env albo usuń bazę i załóż ją od nowa: tools/docker/down.sh --reset && tools/docker/up.sh"
  fi
  die "uruchomienie nie powiodło się (logi powyżej). Dalsze logi: docker compose logs hr-app hr-db"
fi
echo "Kontenery działają."

# 5. Testy
step "Test dymny"
"$ROOT_DIR/tools/dev/smoke.sh" "$API_URL"
step "Kontrole obrazu i konfiguracji"
"$SCRIPT_DIR/verify.sh"

# 6. Podsumowanie
step "Gotowe"
cat <<INFO
Aplikacja: http://localhost:$API_PORT  (konta demo: README.md oraz docs/arc42/07-widok-wdrozenia.md, sekcja 7.2)
Z innego komputera otwórz tunel SSH i wejdź na http://localhost:$API_PORT:
  ssh -N -L $API_PORT:127.0.0.1:$API_PORT $(whoami)@$(hostname)
Logi: docker compose logs -f hr-app; zatrzymanie: tools/docker/down.sh (z --reset usuwa też bazę).
INFO
