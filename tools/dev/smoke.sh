#!/usr/bin/env bash
# Test dymny działającego rozwiązania: SPA, antyforgery, logowanie, urlopy Anny, naliczenia, pracownicy, szkice korekt kadr
# oraz rozmowy demo z agentem (bez pytań do modelu, więc klucz Anthropic nie jest potrzebny).
# Użycie: tools/dev/smoke.sh [adres]   (domyślnie http://127.0.0.1:5080)
set -euo pipefail

BASE_URL="${1:-http://127.0.0.1:5080}"
BASE_URL="${BASE_URL%/}"

for tool in curl python3; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "BŁĄD: brak programu $tool." >&2
    exit 1
  fi
done

# Treść pytania z rozmowy demo Anny (DemoConversations); po niej smoke odnajduje identyfikator rozmowy.
ANNA_QUESTION='Ile dni urlopu mi zostało?'

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT
JAR="$WORK_DIR/cookies.txt"
BODY="$WORK_DIR/body"
CTYPE="$WORK_DIR/content-type"

ok() { echo "OK    $1"; }
fail() {
  echo "BŁĄD  $1: $2" >&2
  exit 1
}

# request METODA ŚCIEŻKA [dodatkowe argumenty curl...] -> wypisuje kod HTTP, ciało w $BODY
request() {
  local method="$1" path="$2"
  shift 2
  local result code
  # Przy błędzie połączenia curl wypisuje kod 000 i kończy się kodem ≠ 0; tu traktujemy to jak zwykły wynik.
  result="$(curl -sS -o "$BODY" -w '%{http_code} %{content_type}' -X "$method" -b "$JAR" -c "$JAR" --max-time 15 "$@" "$BASE_URL$path" 2>"$WORK_DIR/curl.err" || true)"
  code="${result%% *}"
  # Typ zawartości zapisujemy do pliku, bo funkcja działa w podpowłoce ($(request ...)).
  printf '%s' "${result#* }" >"$CTYPE"
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
step="skrypt main-*.js zwraca 200 jako javascript"
main_js="$(grep -oE 'main(-[A-Za-z0-9_]+)?\.js' "$BODY" | head -n 1 || true)"
[[ -n "$main_js" ]] || fail "$step" "w index.html nie ma odwołania do main-*.js"
code="$(request GET "/$main_js")"
[[ "$code" == "200" ]] || fail "$step" "/$main_js zwraca kod $code"
# Brakujący plik dostałby 200 z index.html (fallback SPA), więc sprawdzamy, że to nie jest strona HTML.
! grep -q '<app-root' "$BODY" || fail "$step" "/$main_js zwraca index.html zamiast skryptu"
grep -qi 'javascript' "$CTYPE" || fail "$step" "/$main_js ma typ zawartości '$(cat "$CTYPE")' zamiast javascript"
ok "$step ($main_js)"

# 3. Token antyforgery
step="GET /api/auth/antiforgery zwraca 204"
code="$(request GET /api/auth/antiforgery)"
[[ "$code" == "204" ]] || fail "$step" "kod HTTP $code"
token="$(xsrf_token)"
[[ -n "$token" ]] || fail "$step" "brak ciasteczka XSRF-TOKEN"
ok "$step"

login() {
  local user_name="$1" password="$2"
  request POST /api/auth/login -H 'Content-Type: application/json' -H "X-XSRF-TOKEN: $(xsrf_token)" \
    --data "{\"userName\":\"$user_name\",\"password\":\"$password\"}"
}

# 4. Złe hasło (przed poprawnym logowaniem, które zeruje licznik nieudanych prób)
step="logowanie ze złym hasłem zwraca 401"
code="$(login anna.nowak 'zlehaslo123')"
[[ "$code" == "401" ]] || fail "$step" "kod HTTP $code"
ok "$step"

# 5. Poprawne logowanie
step="POST /api/auth/login jako anna.nowak zwraca 200"
code="$(login anna.nowak 'annanowak123')"
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

# 8. Wyniki naliczenia za wrzesień (plan 7b: ekran Naliczenia)
step="GET /api/payroll/results?year=2026&month=9 jako anna.nowak zawiera gross"
code="$(request GET '/api/payroll/results?year=2026&month=9')"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code"
grep -q '"gross"' "$BODY" || fail "$step" "brak pola gross: $(head -c 300 "$BODY")"
ok "$step"

# 9. Drugi użytkownik (kadry) w osobnym pliku ciasteczek
JAR="$WORK_DIR/cookies-hr.txt"
step="GET /api/auth/antiforgery (piotr.zielinski) zwraca 204"
code="$(request GET /api/auth/antiforgery)"
[[ "$code" == "204" ]] || fail "$step" "kod HTTP $code"
ok "$step"

step="POST /api/auth/login jako piotr.zielinski zwraca 200"
code="$(login piotr.zielinski 'piotrzielinski123')"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code: $(head -c 300 "$BODY")"
ok "$step"

# 10. Kontrakt pracowników z userId (ADR-30)
step="GET /api/employees jako piotr.zielinski zawiera userId"
code="$(request GET /api/employees)"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code"
grep -q '"userId"' "$BODY" || fail "$step" "brak pola userId: $(head -c 300 "$BODY")"
ok "$step"

# 11. Szkice korekt ewidencji (UC-26)
step="GET /api/correction-drafts jako piotr.zielinski zawiera szkic ze statusem Pending"
code="$(request GET /api/correction-drafts)"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code"
tr -d ' \n' <"$BODY" | grep -q '^\[.*"status":"Pending"' || fail "$step" "brak szkicu Pending: $(head -c 300 "$BODY")"
ok "$step"

# 12. Rozmowy z agentem widziane przez kadry (UC-31): co najmniej rozmowy demo Anny i Tomasza
step="GET /api/conversations jako piotr.zielinski zwraca co najmniej dwie rozmowy"
code="$(request GET /api/conversations)"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code"
count="$(tr -d ' \n' <"$BODY" | { grep -o '"messageCount"' || true; } | wc -l)"
((count >= 2)) || fail "$step" "rozmów: $count: $(head -c 300 "$BODY")"
ok "$step ($count)"

# Identyfikator rozmowy Anny: obiekt z polem preview równym treści jej pytania.
anna_conversation_id() {
  python3 -c '
import json, sys
with open(sys.argv[1], encoding="utf-8") as f:
    items = json.load(f)
ids = [c["id"] for c in items if isinstance(c, dict) and c.get("preview") == sys.argv[2]]
print(ids[0] if ids else "")
' "$BODY" "$ANNA_QUESTION" 2>/dev/null || true
}
step="rozmowa z pytaniem „$ANNA_QUESTION” jest na liście kadr"
anna_id="$(anna_conversation_id)"
[[ -n "$anna_id" ]] || fail "$step" "brak rozmowy z takim podglądem: $(head -c 300 "$BODY")"
ok "$step ($anna_id)"

# 13. Odczyt cudzej rozmowy przez kadry zawiera wywołanie narzędzia
step="GET /api/conversations/<rozmowa Anny> jako piotr.zielinski zawiera wywołanie narzędzia"
code="$(request GET "/api/conversations/$anna_id")"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code"
tr -d ' \n' <"$BODY" | grep -q '"role":"ToolCall"' || fail "$step" "brak wiadomości ToolCall: $(head -c 300 "$BODY")"
ok "$step"

# 14. Odczyt rozmowy zapisany w audycie
step="GET /api/audit?entityType=Conversation jako piotr.zielinski zawiera ConversationRead"
code="$(request GET '/api/audit?entityType=Conversation')"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code"
grep -q 'ConversationRead' "$BODY" || fail "$step" "brak zdarzenia ConversationRead: $(head -c 300 "$BODY")"
ok "$step"

# 15. Pracownik (trzeci plik ciasteczek) nie widzi rozmowy Anny i ma tylko własną rozmowę
JAR="$WORK_DIR/cookies-employee.txt"
step="GET /api/auth/antiforgery (tomasz.lis) zwraca 204"
code="$(request GET /api/auth/antiforgery)"
[[ "$code" == "204" ]] || fail "$step" "kod HTTP $code"
ok "$step"

step="POST /api/auth/login jako tomasz.lis zwraca 200"
code="$(login tomasz.lis 'tomaszlis123')"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code: $(head -c 300 "$BODY")"
ok "$step"

step="GET /api/conversations/<rozmowa Anny> jako tomasz.lis zwraca 404"
code="$(request GET "/api/conversations/$anna_id")"
[[ "$code" == "404" ]] || fail "$step" "kod HTTP $code"
ok "$step"

step="GET /api/conversations jako tomasz.lis zwraca dokładnie jedną rozmowę"
code="$(request GET /api/conversations)"
[[ "$code" == "200" ]] || fail "$step" "kod HTTP $code"
count="$(tr -d ' \n' <"$BODY" | { grep -o '"messageCount"' || true; } | wc -l)"
((count == 1)) || fail "$step" "rozmów: $count: $(head -c 300 "$BODY")"
ok "$step"

echo "Test dymny zakończony powodzeniem."
