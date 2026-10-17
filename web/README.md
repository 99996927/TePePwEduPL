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

## Uwagi

- `src/app/api` jest generowany; nie edytuj go ręcznie. Po zmianie kontraktu OpenAPI uruchom `npm run api:gen` i zatwierdź zmiany.
- Zawartość `src/Hr.Api/wwwroot/` (poza `.gitkeep`) nie jest w repozytorium; powstaje przez `npm run build` i `npm run build:wwwroot`.
- Budżet rozmiaru `initial` w `angular.json` podniesiony do `maximumWarning` 900kB i `maximumError` 1.5MB (Angular Material przekroczył domyślne 500kB: 504 kB po dodaniu ekranu urlopów).
- Ostrzeżenia npm 11 `allow-scripts` są nieszkodliwe dla budowania i testów.
