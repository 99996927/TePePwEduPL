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
| `tools/dev/smoke.sh [adres]` | Test dymny działającej aplikacji (`OK`/`BŁĄD` dla każdego kroku); obejmuje też naliczenia, `userId` w pracownikach, szkic korekty i rozmowy demo z agentem (konta `anna.nowak`, `piotr.zielinski` i `tomasz.lis`; bez pytań do modelu). |

Log API: `.run/api.log`. Listy kroków testów ręcznych: `docs/testy-reczne/plan-7a.md` (logowanie, urlopy, czas pracy, zespół), `docs/testy-reczne/plan-7b.md` (naliczenia, zmiany płacowe, szkice korekt, audyt) i `docs/testy-reczne/plan-7c.md` (agent). Do pracy nad samym frontendem można zamiast tego użyć `npm start` (proxy `/api` na działające `Hr.Api`).

## Ekran agenta (`src/app/features/agent/`)

Trasa `/agent` (nowa rozmowa) i `/agent/<id>` (otwarta rozmowa), jeden komponent `Agent` dla obu adresów (`UrlMatcher`, ADR-31):

- `ConversationList`: rozmowy po lewej (podgląd pytania, data, liczba wiadomości) i „Nowa rozmowa”; kadry (`HR`, `HRApprover`) widzą rozmowy wszystkich z autorem i przełącznik „Tylko moje”.
- `ConversationThread`: pytania i odpowiedzi jako tekst, zwinięte wiersze wywołań i wyników narzędzi, dymek „Agent pisze…”, ostrzeżenie o liczbach spoza wyników narzędzi (`ungroundedNumbers`).
- Pole pytania (limit 4000 znaków, Enter wysyła, Shift+Enter nowy wiersz) tylko we własnej lub nowej rozmowie; cudza rozmowa jest tylko do odczytu z banerem o audycie.
- Pytania wymagają klucza Anthropic: wiersz `ANTHROPIC_API_KEY=...` w `.env` w katalogu głównym repozytorium (poza gitem) i ponowne `tools/dev/run.sh`. Bez klucza API zwraca `503`, ekran pokazuje komunikat, a pytanie zostaje w polu.

## Wygląd i style (`src/styles/`, plan 7d)

- `src/styles/_tokens.scss`: zmienne CSS `--app-bg`, `--app-menu-bg`, `--app-card-shadow`, `--app-card-radius` i gradienty `--app-grad-primary|success|warning|danger|info`.
  Biały tekst wolno kłaść tylko na tych gradientach (kontrast ≥ 4,5:1).
- `src/styles/_components.scss`: klasy `page-card` (biała karta), `page-card__header` (gradientowy nagłówek; modyfikatory `--success|--warning|--danger|--info`; przyciski „filled”
  w nagłówku są białe), `page-card__body`, `page-card--tile` z `page-card__icon` (kafelek z pływającą ikoną), `data-table` (klasa na `<table mat-table>`), `status-chip` i style dialogów.
- Nowy ekran: `<div class="page-card"><div class="page-card__header"><h1>Tytuł</h1></div><div class="page-card__body">…</div></div>`; filtry i pola formularzy w ciele karty.
- Status jako chip: `<app-status-chip [status]="r.status" [label]="statusLabel(r.status)" />` (`shared/status-chip.ts`; ton z `statusTone`, nieznany status jest neutralny).
- Nowa pozycja menu: wpis w stałej `MENU` w `core/layout/shell.ts` z nazwą ikony Material Symbols (`icon`); tytuł ekranu w pasku bierze się z etykiety pozycji.
- Ikony: `<mat-icon fontIcon="nazwa" />` (zestaw `material-symbols-outlined` z klasą `mat-ligature-font` ustawia `provideIconFont()` z `core/icon-font.ts`); nie wpisuj nazwy ikony jako tekstu.
- Fonty są hostowane razem z aplikacją (plan Docker, brak CDN): Roboto (`@fontsource/roboto`, wagi 300/400/500, podzbiory `latin` i `latin-ext` dla polskich znaków) i Material Symbols Outlined (`@fontsource/material-symbols-outlined`, statyczny wariant 400, ok. 320 kB; pełny font `material-symbols` ma 4 MB, więc go nie używamy). `@font-face` i klasa `.material-symbols-outlined` są w `src/styles/_fonts.scss`. `npm run fonts:check` (część `npm run verify`) kończy się błędem, jeśli zbudowany `index.html` lub CSS odwołuje się do adresu spoza aplikacji.

## Uwagi

- Po aktualizacji do planu 7b istniejąca baza dev nie ma szkicu korekty w danych demo; wykonaj `tools/dev/stop.sh --reset` i `tools/dev/run.sh`.
- `src/app/api` jest generowany; nie edytuj go ręcznie. Po zmianie kontraktu OpenAPI uruchom `npm run api:gen` i zatwierdź zmiany.
- Zawartość `src/Hr.Api/wwwroot/` (poza `.gitkeep`) nie jest w repozytorium; powstaje przez `npm run build` i `npm run build:wwwroot`.
- Budżet rozmiaru `initial` w `angular.json` podniesiony do `maximumWarning` 900kB i `maximumError` 1.5MB (Angular Material przekroczył domyślne 500kB: 504 kB po dodaniu ekranu urlopów).
- Ostrzeżenia npm 11 `allow-scripts` są nieszkodliwe dla budowania i testów.
