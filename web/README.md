# Aplikacja Angular (hr-app)

Frontend systemu kadrowego: Angular 22 (standalone, zoneless, SCSS) i Angular Material.

## Wymagania

- Node `^24.15.0` (sprawdzone na 24.18), npm 11.
- Wszystkie zależności mają przypięte, dokładne wersje (`package-lock.json` jest w repozytorium).

## Polecenia

| Polecenie | Działanie |
|---|---|
| `npm install` | Instalacja zależności. |
| `npm start` | Serwer deweloperski z proxy `/api` na `http://127.0.0.1:5080` (`proxy.conf.json`). |
| `npm run build` | Build produkcyjny do `dist/hr-app/browser`. |
| `npm run build:dev` | Build `development` (widoczny panel kont demo). |
| `npm test` | Testy jednostkowe (Vitest + jsdom, jednorazowo). |
| `npm run api:gen` | Generuje klienta API do `src/app/api` z `../docs/openapi/hr-api.json`. |
| `npm run api:check` | Generuje klienta do katalogu tymczasowego i porównuje z `src/app/api`; kończy się błędem, gdy pliki się różnią (nie zależy od stanu gita). |
| `npm run build:wwwroot` | Kopiuje `dist/hr-app/browser/*` do `../src/Hr.Api/wwwroot/` (czyści go poza `.gitkeep`). |
| `npm run verify` | `api:check`, testy i build produkcyjny. |

## Uruchomienie całego rozwiązania (testy ręczne)

Skrypty w `tools/dev/` (z katalogu głównego repozytorium lub dowolnego innego; szczegóły w `docs/arc42/07-widok-wdrozenia.md`, sekcja 7.4):

| Polecenie | Działanie |
|---|---|
| `tools/dev/run.sh` | SQL Server w Dockerze (`hr-dev-db`, `127.0.0.1:14330`), `npm ci`, `npm run build:dev`, `npm run build:wwwroot`, `Hr.Api` w tle na `http://127.0.0.1:5080` z danymi demo, test dymny i komenda tunelu SSH. Można uruchamiać wielokrotnie. |
| `tools/dev/stop.sh` | Kończy `Hr.Api` i zatrzymuje bazę (dane zostają w wolumenie). |
| `tools/dev/stop.sh --reset` | Jak wyżej oraz usuwa kontener i wolumen bazy. |
| `tools/dev/smoke.sh [adres]` | Test dymny działającej aplikacji (`OK`/`BŁĄD` dla każdego kroku). |

Log API: `.run/api.log`. Lista kroków testu ręcznego: `docs/testy-reczne/plan-7a.md`. Do pracy nad samym frontendem można zamiast tego użyć `npm start` (proxy `/api` na działające `Hr.Api`).

## Uwagi

- `src/app/api` jest generowany; nie edytuj go ręcznie. Po zmianie kontraktu OpenAPI uruchom `npm run api:gen` i zatwierdź zmiany.
- Zawartość `src/Hr.Api/wwwroot/` (poza `.gitkeep`) nie jest w repozytorium; powstaje przez `npm run build` i `npm run build:wwwroot`.
- Budżet rozmiaru `initial` w `angular.json` podniesiony do `maximumWarning` 900kB i `maximumError` 1.5MB (Angular Material przekroczył domyślne 500kB: 504 kB po dodaniu ekranu urlopów).
- Ostrzeżenia npm 11 `allow-scripts` są nieszkodliwe dla budowania i testów.
