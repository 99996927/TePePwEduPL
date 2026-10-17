#!/usr/bin/env bash
# Kontrole obrazu i konfiguracji uruchomionego systemu Docker (po tools/docker/up.sh).
# Użycie: tools/docker/verify.sh   (kod 0 = wszystkie kontrole OK)
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

BASE_URL="http://127.0.0.1:5080"

ok() { echo "OK    $1"; }
fail() {
  echo "BŁĄD  $1: $2" >&2
  exit 1
}

running="$(docker container inspect -f '{{.State.Running}}' hr-app 2>/dev/null || echo "brak")"
[[ "$running" == "true" ]] || fail "kontener hr-app" "nie działa (uruchom tools/docker/up.sh)"

uid="$(docker exec hr-app id -u)"
[[ "$uid" != "0" ]] || fail "użytkownik procesu" "aplikacja działa jako root"
ok "aplikacja działa jako użytkownik nie-root (uid $uid)"

if docker exec hr-app test -e /app/.env; then
  fail "plik .env w obrazie" "/app/.env istnieje"
fi
ok "w obrazie nie ma pliku .env"

db_ports="$(docker port hr-db 2>/dev/null || true)"
[[ -z "$db_ports" ]] || fail "port bazy" "hr-db publikuje porty na hoście ($db_ports)"
ok "baza nie publikuje portu na hoście"

app_ports="$(docker port hr-app)"
[[ -n "$app_ports" ]] || fail "port aplikacji" "brak publikacji portu"
while IFS= read -r line; do
  [[ "$line" == *"-> 127.0.0.1:5080" ]] || fail "port aplikacji" "publikacja inna niż 127.0.0.1:5080 ($line)"
done <<<"$app_ports"
ok "aplikacja nasłuchuje na hoście tylko na 127.0.0.1:5080"

index="$(curl -fsS --max-time 10 "$BASE_URL/")" || fail "strona główna" "brak odpowiedzi z $BASE_URL/"
bad="$(printf '%s' "$index" | grep -oE 'https?://[^[:space:]"'"'"'<>)]+' | grep -vE '^https?://www\.w3\.org' || true)"
[[ -z "$bad" ]] || fail "index.html" "odwołania do zasobów zewnętrznych: $bad"
while IFS= read -r css; do
  [[ -n "$css" ]] || continue
  css_body="$(curl -fsS --max-time 10 "$BASE_URL/${css#/}")" || fail "styl $css" "nie można pobrać"
  bad="$(printf '%s' "$css_body" | grep -oE 'https?://[^[:space:]"'"'"'<>)]+' | grep -vE '^https?://www\.w3\.org' || true)"
  [[ -z "$bad" ]] || fail "styl $css" "odwołania do zasobów zewnętrznych: $bad"
done < <(printf '%s' "$index" | grep -oE 'href="[^"]+\.css"' | sed -E 's/^href="(.*)"$/\1/')
ok "strona i style nie odwołują się do zasobów spoza hosta"

curl -fsS --max-time 5 "$BASE_URL/api/health" >/dev/null || fail "/api/health" "brak odpowiedzi 200"
ok "/api/health odpowiada"
echo "Wszystkie kontrole przeszły."
