#!/usr/bin/env bash
# Test dymny działającego rozwiązania: SPA, antyforgery, logowanie i urlopy Anny.
# Użycie: tools/dev/smoke.sh [adres]   (domyślnie http://127.0.0.1:5080)
set -euo pipefail

BASE_URL="${1:-http://127.0.0.1:5080}"
BASE_URL="${BASE_URL%/}"

if ! command -v curl >/dev/null 2>&1; then
  echo "BŁĄD: brak programu curl." >&2
  exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
JAR="$WORK_DIR/cookies.txt"
BODY="$WORK_DIR/body"

ok() { echo "OK    $1"; }
fail() {
  echo "BŁĄD  $1: $2" >&2
  exit 1
}

# request METODA ŚCIEŻKA [dodatkowe argumenty curl...] -> wypisuje kod HTTP, ciało w $BODY
request() {
  local method="$1" path="$2"
  shift 2
  local code
  # Przy błędzie połączenia curl wypisuje kod 000 i kończy się kodem ≠ 0; tu traktujemy to jak zwykły wynik.
  code="$(curl -sS -o "$BODY" -w '%{http_code}' -X "$method" -b "$JAR" -c "$JAR" --max-time 15 "$@" "$BASE_URL$path" 2>"$WORK_DIR/curl.err" || true)"
  echo "${code:-000}"
}

xsrf_token() {
  awk '$6 == "XSRF-TOKEN" { print $7 }' "$JAR" | tail -n 1
}

echo "Test dymny: $BASE_URL"

# 1. Strona główna SPA
step="GET / zawiera <app-root"
code="$(request GET /)"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code (czy aplikacja działa?)"
grep -q '<app-root' "$BODY" || fail "$step" "brak <app-root w odpowiedzi (czy Angular jest zbudowany do wwwroot?)"
ok "$step"

# 2. Skrypt main-*.js wskazany w index.html (build produkcyjny: main-HASH.js, build development: main.js)
step="skrypt main-*.js zwraca 200"
main_js="$(grep -oE 'main(-[A-Za-z0-9_]+)?\.js' "$BODY" | head -n 1 || true)"
[[ -n "$main_js" ]] || fail "$step" "w index.html nie ma odwołania do main-*.js"
code="$(request GET "/$main_js")"
[[ "$code" == "200" ]] || fail "$step" "/$main_js zwraca kod $code"
# Brakujący plik dostałby 200 z index.html (fallback SPA), więc sprawdzamy, że to nie jest strona HTML.
! grep -q '<app-root' "$BODY" || fail "$step" "/$main_js zwraca index.html zamiast skryptu"
ok "$step ($main_js)"

# 3. Token antyforgery
step="GET /api/auth/antiforgery zwraca 204"
code="$(request GET /api/auth/antiforgery)"
[[ "$code" == "204" ]] || fail "$step" "kod HTTP $code"
token="$(xsrf_token)"
[[ -n "$token" ]] || fail "$step" "brak ciasteczka XSRF-TOKEN"
ok "$step"

login() {
  local password="$1"
  request POST /api/auth/login -H 'Content-Type: application/json' -H "X-XSRF-TOKEN: $(xsrf_token)" \
    --data "{\"userName\":\"anna.nowak\",\"password\":\"$password\"}"
}

# 4. Złe hasło (przed poprawnym logowaniem, które zeruje licznik nieudanych prób)
step="logowanie ze złym hasłem zwraca 401"
code="$(login 'zlehaslo123')"
[[ "$code" == "401" ]] || fail "$step" "kod HTTP $code"
ok "$step"

# 5. Poprawne logowanie
step="POST /api/auth/login jako anna.nowak zwraca 200"
code="$(login 'annanowak123')"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code: $(head -c 300 "$BODY")"
ok "$step"

# 6. Saldo urlopu
step="GET /api/leave/balance ma entitlementDays 20"
code="$(request GET /api/leave/balance)"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code"
tr -d ' \n' <"$BODY" | grep -q '"entitlementDays":20' || fail "$step" "nieoczekiwane saldo: $(head -c 300 "$BODY")"
ok "$step"

# 7. Lista wniosków
step="GET /api/leave/requests zwraca 200"
code="$(request GET /api/leave/requests)"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code"
ok "$step"

echo "Test dymny zakończony powodzeniem."
