# Docker (Compose): lokalne uruchomienie całego systemu — projekt

**Data:** 2026-10-17
**Status:** do przeglądu
**Podstawa:** `docs/arc42` (sekcja 7.1 widok wdrożenia, 7.4 uruchomienie deweloperskie, ADR-32), plany 1–7d zmergowane.
**Poza zakresem:** wdrożenie na serwer, HTTPS i reverse proxy, CI, rejestr obrazów, testy E2E (osobny plan), zmiany backendu i kontraktu API.

## 1. Cel

Cały system (baza, API i Angular) startuje jedną komendą na tym hoście z Docker Engine i jest dostępny przez tunel SSH. Na hoście nie są potrzebne dotnet, node ani npm. Aplikacja działa także wtedy, gdy przeglądarka nie ma dostępu do internetu (fonty hostowane samodzielnie).

### Kryteria sukcesu
1. `tools/docker/up.sh` na czystym hoście (bez `.env`, bez wolumenu) buduje obraz, startuje bazę i aplikację, a `tools/dev/smoke.sh http://127.0.0.1:5080` przechodzi (kroki 1–15).
2. Port aplikacji nasłuchuje wyłącznie na `127.0.0.1` hosta; port bazy nie jest publikowany na hoście.
3. Obraz aplikacji nie zawiera `.env`, klucza Anthropic ani hasła SA; proces działa jako użytkownik inny niż root.
4. Strona nie ładuje żadnych zasobów spoza własnego hosta (brak `fonts.googleapis.com` i `fonts.gstatic.com`), a ikony i fonty renderują się poprawnie.
5. Dane przeżywają `docker compose down` (wolumen); `tools/docker/down.sh --reset` usuwa je.
6. `tools/dev/run.sh` i `tools/dev/stop.sh` działają jak dotąd; testy .NET i `npm run verify` przechodzą bez zmian asercji.

## 2. Decyzje

| Decyzja | Wybór | Uzasadnienie |
|---|---|---|
| Rola Dockera | Uruchomienie „jak produkt” lokalnie; `tools/dev/run.sh` zostaje jako szybka pętla deweloperska | Wybór właściciela (2026-10-17). |
| Środowisko aplikacji | `ASPNETCORE_ENVIRONMENT=Production`, seed demo włączony jawnie (`Hr__SeedDemoData=true`) | W `Production` nie ma `/openapi`; to instancja demonstracyjna z danymi syntetycznymi (ostrzeżenie R-03). |
| Dostęp | `127.0.0.1:5080` na hoście (kontener nasłuchuje na 8080), tunel SSH | Wymaganie stałe: tylko loopback. Ten sam adres co `run.sh`, więc ta sama komenda tunelu. |
| Konflikt z `run.sh` | Oba używają portu 5080, więc nie działają jednocześnie; `up.sh` odmawia startu z komunikatem, gdy port jest zajęty przez cokolwiek innego niż własny kontener | Prostota; osobne nazwy kontenerów, sieci i wolumenów (`hr-dev-*` kontra projekt Compose `hr`). |
| Fonty | Samodzielne hostowanie: pakiety npm `@fontsource/roboto` i `material-symbols` (tylko wariant Outlined, `woff2`), `@font-face` w stylach, usunięte `<link>` do Google Fonts | Wersje zamrożone w `package-lock.json`; brak ręcznie wklejanych binariów; zasoby w paczce Angulara. |
| Sekrety | Plik `.env` (poza repozytorium); hasło SA generuje `up.sh`; klucz Anthropic dopisuje właściciel | Wymaganie stałe: klucz tylko w `.env`, nigdy w argv, logach ani obrazie. |

## 3. Obraz `hr-app` (`Dockerfile` w katalogu głównym)

Trzy etapy:
1. **web** (`node:24`, wersja zgodna z `engines` w `web/package.json`): `npm ci`, `npm run build` (konfiguracja produkcyjna) w `web/`.
2. **publish** (`mcr.microsoft.com/dotnet/sdk:9.0`): kopiuje `Hr.sln`, `global.json`, `Directory.Build.props` i `src/`, odtwarza pakiety (warstwa cache), kopiuje wynik etapu `web` do `src/Hr.Api/wwwroot/` i wykonuje `dotnet publish src/Hr.Api -c Release`. `global.json` ma `rollForward: latestFeature`, więc działa z dowolnym SDK 9.0.x.
3. **runtime** (`mcr.microsoft.com/dotnet/aspnet:9.0`): kopiuje publikację, ustawia `USER app` (nie root), `EXPOSE 8080`, `ENTRYPOINT ["dotnet", "Hr.Api.dll"]`. Obraz zawiera `curl` albo healthcheck jest wykonywany przez Compose innym sposobem dostępnym w obrazie (do rozstrzygnięcia w planie, bez zmiany kontraktu: sprawdzenie `GET /api/health`).

`.dockerignore`: `.env`, `.run/`, `.git/`, `.superpowers/`, `**/bin/`, `**/obj/`, `web/node_modules/`, `web/dist/`, `web/.angular/`, `src/Hr.Api/wwwroot/*` (poza `.gitkeep`), `tests/`, `docs/`. Zasób `labour-code.json` jest osadzony w `Hr.Core` (EmbeddedResource), więc nie wymaga osobnego kopiowania.

## 4. `compose.yaml`

Nazwa projektu: `hr`. Sieć domyślna projektu; kontenery `hr-db` i `hr-app`; wolumen `hr-db-data`.

- **hr-db:** `mcr.microsoft.com/mssql/server:2022-latest`; `ACCEPT_EULA=Y`; `MSSQL_SA_PASSWORD` z `.env` (interpolacja `${HR_DB_PASSWORD:?...}`, bez wartości w pliku); brak sekcji `ports`; wolumen na `/var/opt/mssql`; healthcheck `sqlcmd ... SELECT 1` (hasło z `SQLCMDPASSWORD` w środowisku kontenera, nie w argumentach); `restart: unless-stopped`.
- **hr-app:** build z `Dockerfile`; `depends_on: hr-db: condition: service_healthy`; `ports: ["127.0.0.1:5080:8080"]`; środowisko: `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__Hr=Server=hr-db,1433;Database=hr;User Id=sa;Password=${HR_DB_PASSWORD};TrustServerCertificate=True`, `Hr__SeedDemoData=true`, `ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY:-}`; healthcheck na `/api/health`; `restart: unless-stopped`.
- Hasło trafia do kontenerów wyłącznie jako zmienna środowiskowa. Pusty `ANTHROPIC_API_KEY` oznacza brak klucza: czat zwraca 503, reszta działa.
- `compose.yaml` nie zawiera żadnych sekretów, więc jest commitowany.

## 5. Skrypty (`tools/docker/`)

- **`up.sh`:** sprawdza `docker` i `docker compose` oraz dostęp do demona; sprawdza, że port 5080 jest wolny albo zajęty przez kontener `hr-app`; tworzy `.env` (umask 077) z losowym `HR_DB_PASSWORD` według tej samej reguły co `run.sh` (polityka haseł SQL Server), jeśli go brak; nie wypisuje wartości sekretów; informuje, czy klucz Anthropic jest ustawiony (jak `run.sh`); `docker compose up -d --build --wait`; uruchamia `tools/dev/smoke.sh http://127.0.0.1:5080`; wypisuje adres i komendę tunelu SSH. Przy niezgodności hasła z istniejącym wolumenem wykrywa błąd logowania i podaje komendę `down.sh --reset`.
- **`down.sh`:** `docker compose down`; z `--reset` także `-v` (usuwa wolumen `hr-db-data`).
- Obie wersje działają z dowolnego katalogu, mają polskie komunikaty i kończą się kodem ≠ 0 przy błędzie. Skrypty `tools/dev/*` nie są zmieniane (wyjątek: ewentualnie wspólne helpery, jeśli plan to uzasadni).

## 6. Fonty bez CDN

- Zależności: `@fontsource/roboto` (wagi 300, 400, 500, podzbiory latin i latin-ext, bo aplikacja jest po polsku) i `material-symbols` (tylko `material-symbols-outlined.woff2`).
- `@font-face` i klasa `.material-symbols-outlined` w `web/src/styles/` (nowy plik `_fonts.scss`), pliki fontów w paczce aplikacji; `provideIconFont()` i ikony `fontIcon` bez zmian.
- `web/src/index.html`: usunięte `preconnect` i `link` do Google Fonts.
- Test: asercja (np. w skrypcie weryfikującym build albo teście specyfikacji), że zbudowany `index.html` i style nie zawierają `fonts.googleapis.com` ani `fonts.gstatic.com`. Wygląd po zmianie jest sprawdzany ręcznie przez właściciela (tunel SSH).

## 7. Testowanie

- **Smoke:** `tools/dev/smoke.sh` przeciwko kontenerom (kroki 1–15) jest głównym dowodem działania.
- **`tools/docker/verify.sh`** (test obrazu i konfiguracji, uruchamiany przez `up.sh` po smoke): proces `hr-app` nie działa jako root (`docker exec ... id -u`); w systemie plików obrazu nie ma `.env` (`docker exec ... test ! -e`); `docker compose port hr-db 1433` nie zwraca publikacji; `docker port hr-app` pokazuje wyłącznie `127.0.0.1:5080`; `index.html` i pliki `*.css` serwowane przez aplikację nie odwołują się do zasobów spoza hosta.
- **Regresja:** `npm run verify` w `web/` (fonty zmieniają style); `dotnet build` bez ostrzeżeń; `dotnet test` nie jest potrzebny, bo kod .NET się nie zmienia (potwierdzone `git diff` w przeglądzie końcowym).

## 8. Dokumentacja

- arc42 7.1: port `127.0.0.1:5080`, nazwa wolumenu i kontenerów zgodne z `compose.yaml`; 7.4 przemianowane na „Uruchomienie deweloperskie i w Dockerze”, z tabelą `tools/docker/*`, uwagą o wspólnym porcie 5080 i o tym, kiedy użyć którego.
- Nowe ADR-33: Docker Compose lokalnie, `Production` z seedem demo, samodzielne hostowanie fontów.
- `README.md` (status i szybki start), `web/README.md` (fonty), `docs/testy-reczne/docker.md` (kroki testu ręcznego z tunelem SSH, w tym wyłączenie internetu w przeglądarce opcjonalnie), `.env.example` rozszerzony o `HR_DB_PASSWORD` (bez wartości) z komentarzem.
- `.gitignore` bez zmian (`.env` już jest); `.dockerignore` nowy.

## 9. Ryzyka i uwagi

- Obraz SQL Server ma ok. 1,7 GB i wymaga min. 2 GB RAM; host ma 32 GB. Pierwszy start (pobranie obrazów, `npm ci`, publikacja) trwa kilka minut; kolejne korzystają z cache warstw.
- `Production` zmienia zachowanie względem `run.sh` (`Development`): brak `/openapi`, brak szczegółowych stron błędów. Smoke i aplikacja tego nie używają; plan sprawdza to uruchomieniem.
- Rozmiar `material-symbols` jest duży (wariant zmienny); plan ma zmierzyć rozmiar paczki i, jeśli przekracza rozsądny budżet, ograniczyć osie fontu albo użyć podzbioru używanych ikon.
- Hasło SA jest ustalane przy zakładaniu wolumenu; utrata `.env` przy istniejącym wolumenie wymaga `down.sh --reset` (komunikat w `up.sh`).
- Dwie instancje aplikacji na pustej bazie mogą się ścigać o seed (zastrzeżenie z 7.3); Compose uruchamia jedną instancję.
