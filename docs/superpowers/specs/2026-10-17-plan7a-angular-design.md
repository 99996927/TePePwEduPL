# Plan 7a: Angular — szkielet, logowanie, urlopy i czas pracy — projekt

**Data:** 2026-10-17
**Status:** do przeglądu
**Podstawa:** `docs/arc42` (ADR-02, ADR-14, ADR-16, ADR-25; sekcje 5, 6, 7, 8; R-03, R-10), plany 1–6 zmergowane.
**Poza zakresem tego planu:** naliczenia, zmiany płacowe, szkice korekt i audyt (plan 7b), agent i historia rozmów (plan 7c), Docker Compose (plan Docker), testy E2E w Playwright (plan E2E).

## 1. Cel i podział prac

Frontend Angular dla asystenta kadrowo-płacowego: statyczny build serwowany przez `Hr.Api` z jednego originu (ADR-02). Całość UI (ok. 31 przypadków użycia) dzielimy na trzy plany, a każdy kończy się działającym, testowalnym kawałkiem:

| Plan | Zakres | UC |
|---|---|---|
| **7a** (ten dokument) | Projekt Angular, wygenerowany klient API, logowanie, układ i role, pulpit, urlopy, zatwierdzanie, czas pracy, przegląd zespołu, skrypty uruchomieniowe | 01–09, 10–15 |
| 7b | Naliczenia, zmiany płacowe z zatwierdzaniem, szkice korekt, audyt | 16–20, 26 (szkice), 28 |
| 7c | Agent: czat, historia rozmów, odczyt rozmów przez kadry, `ungroundedNumbers` | 21–27, 30, 31 |

**Wymóg właściciela:** po każdym planie całe rozwiązanie jest uruchomione (bez Docker Compose, skryptem), a właściciel testuje je ręcznie przez tunel SSH.

### Kryteria sukcesu planu 7a
1. `npm run verify` w `web/` przechodzi (sprawdzenie klienta API, testy, build produkcyjny).
2. `dotnet build Hr.sln` i testy `Hr.Api.IntegrationTests` (w tym migawka OpenAPI) przechodzą.
3. `tools/dev/run.sh` stawia aplikację na `http://127.0.0.1:5080`, a `tools/dev/smoke.sh` przechodzi.
4. Właściciel dostaje listę kroków do ręcznego testu per rola i UC oraz komendę tunelu SSH.

## 2. Decyzje

| Decyzja | Wybór | Uzasadnienie |
|---|---|---|
| Granularność | Trzy plany: 7a, 7b, 7c | Błąd w szkielecie nie odbija się na całości; każdy plan da się pokazać. |
| Dostęp ręczny | Tunel SSH do `127.0.0.1:5080` | Konta demo mają jawne hasła (R-03); nic nie jest wystawione na świat. |
| Biblioteka UI | Angular Material | Oficjalna, dostępna, gotowe date picker, tabele, dialogi, snackbar. |
| Klient API | `ng-openapi-gen` 1.1.0 | Serwisy na `HttpClient`, więc działają interceptory i obsługa XSRF z Angulara (ADR-16, ADR-25). |
| Architektura | Standalone + signals + trasy ładowane leniwie wg funkcji, bez NgRx | Stan to głównie odczyt z serwera; najmniej kodu. |
| Wersje | Angular CLI, `@angular/core`, `@angular/material` **22.2.3** (wymaga Node `^24.15`; w środowisku 24.18); wersje wpisane na stałe w `package.json`, `package-lock.json` w repo | Sprawdzone na npm 2026-10-17. |

## 3. Struktura i narzędzia

- **Lokalizacja:** `web/` w katalogu głównym repo, projekt `hr-app` z `ng new` (standalone, trasowanie, SCSS, bez SSR). Runner testów domyślny dla CLI 22 (do potwierdzenia przy tworzeniu projektu).
- **Zależności:** `@angular/material` (z CDK) i devDependency `ng-openapi-gen`; nic więcej bez uzasadnienia w planie.
- **Klient API:** `npm run api:gen` generuje do `web/src/app/api/` serwisy i modele z `docs/openapi/hr-api.json`; pliki są w repo. `npm run api:check` generuje ponownie i kończy się błędem, jeśli różni się od repo.
- **Build:** `npm run build` buduje do `web/dist/`, skrypt kopiuje wynik do `src/Hr.Api/wwwroot/`. `wwwroot` jest ignorowany w gicie (poza `.gitkeep`); placeholder `index.html` znika. Budowanie Angulara nie jest częścią `dotnet build` (w planie Docker zrobi to osobny etap obrazu).
- **Struktura kodu:** `core/` (sesja, interceptory, guardy, układ), `shared/` (błąd, wskaźnik ładowania, wybór pracownika), `features/` (`auth`, `dashboard`, `leave`, `timesheet`, `team`).
- **Język i format:** polski, `pl-PL` dla dat, liczb i godzin; bez i18n w tym etapie.
- **Sesja i CSRF (ADR-25):** logowanie najpierw woła `GET /api/auth/antiforgery`; interceptor domyślny Angulara dokłada `X-XSRF-TOKEN` z ciasteczka `XSRF-TOKEN` do żądań zapisu; ciasteczko sesyjne `hr.session` jest `HttpOnly`.

## 4. Ekrany i role

Sprawdzone w API: listy urlopów i wpisów czasu zwracają to, co użytkownik widzi wg zakresu danych (własne, zespół, wszyscy); saldo i analiza przyjmują opcjonalny `employeeId`.

**Szkielet:** górny pasek (nazwa, rola, „Wyloguj”), menu boczne zależne od roli, powiadomienia o błędach (snackbar). `401` czyści sesję i przekierowuje na logowanie. Pozycje menu niedostępne dla roli nie są pokazywane, a wejście po URL kończy się przekierowaniem na pulpit z komunikatem (UC-04).

| Ekran | Kto | Zawartość | UC |
|---|---|---|---|
| Logowanie `/logowanie` | wszyscy | Formularz login/hasło; błędy `401` i `423`. W buildzie deweloperskim zwijany panel „Konta demo” z loginami i hasłami. | 01, 02 |
| Pulpit `/` | wszyscy | Kafelki: saldo urlopu, bieżący miesiąc (przepracowane wobec normy, nadgodziny), alerty; dla kierownika i kadr „Do zatwierdzenia: N”. | 05, 12–14 |
| Urlopy `/urlopy` | wszyscy | Tabela wniosków z filtrem statusu; „Nowy wniosek” (zakres dat; po zapisie liczba dni z silnika; błąd braku salda `422` w formularzu); „Anuluj” własnego oczekującego. | 05–07, 09 |
| Zatwierdzanie `/urlopy/zatwierdzanie` | kierownik, kadry | Oczekujące wnioski zespołu; zatwierdź albo odrzuć z uzasadnieniem (wymagane przy odrzuceniu). | 08 |
| Czas pracy `/czas-pracy` | wszyscy | Wybór miesiąca; tabela wpisów; „Dodaj wpis” (czas lokalny bez strefy, ADR-17); „Koryguj” z powodem; panel analizy (norma, przepracowane, nadgodziny 50% i 100%, naruszenia odpoczynku z godzinami i numerem przepisu, średnia tygodniowa wobec 48 h, nadgodziny narastająco wobec 150 h); kierownik i kadry wybierają pracownika. | 10–14 |
| Przegląd zespołu `/zespol` | kierownik, kadry | Tabela osób dla wybranego miesiąca: przepracowane wobec normy, nadgodziny, znaczniki alertów; kliknięcie otwiera czas pracy osoby. | 15 |

**Zasady wspólne:** `422` i `409` pokazywane w formularzu komunikatem z `ProblemDetails`; `403` i `404` jako snackbar bez ujawniania szczegółów; stany puste i ładowania w tabelach; dostępność wg domyślnych zachowań Material (etykiety, klawiatura); układ od szerokości tabletu, telefon w miarę możliwości.

**Ograniczenia API wpływające na UI:** lista wpisów nie filtruje po pracowniku (ekran pobiera miesiąc dla widocznych osób i filtruje po stronie klienta, ok. 700 wpisów na miesiąc przy 31 osobach); przegląd zespołu woła analizę osobno dla każdej osoby (7 wywołań dla kierownika, 31 dla kadr) z ograniczoną współbieżnością.

## 5. Testy

**Aplikacja Angular** (runner domyślny CLI 22):
- **Guardy i sesja:** pracownik na `/zespol` i `/urlopy/zatwierdzanie` ląduje na pulpicie z komunikatem; niezalogowany na logowaniu (UC-04); `401` czyści sesję.
- **CSRF:** logowanie poprzedzone `GET /api/auth/antiforgery`; zapisy niosą `X-XSRF-TOKEN`.
- **Mapowanie błędów:** `401`, `423`, `422`, `409`, `403`, `404` dają właściwe komunikaty (`HttpTestingController`).
- **Komponenty:** logowanie, lista i nowy wniosek (błąd braku salda), zatwierdzanie z wymaganym uzasadnieniem, wpisy czasu (czas lokalny bez strefy), panel analizy z alertami, przegląd zespołu.
- **Kontrakt:** `npm run api:check`; **całość:** `npm run verify` (`api:check`, testy, build produkcyjny).
- **E2E w Playwright** to osobny plan; dla 7a powstaje lista kroków do ręcznego testu.

**Backend:** zmiany minimalne (`.gitkeep` zamiast placeholdera, wpisy w `.gitignore`). Weryfikacja: budowanie i testy `Hr.Api.IntegrationTests` z migawką OpenAPI; kontrakt API się nie zmienia.

## 6. Uruchomienie całego rozwiązania

`tools/dev/run.sh`:
1. sprawdza docker, dotnet i node;
2. startuje kontener SQL Server `hr-dev-db` (obraz `mcr.microsoft.com/mssql/server:2022-latest`) na `127.0.0.1:14330` z nazwanym wolumenem; hasło SA zapisuje w `.env` (ignorowany przez git);
3. buduje Angulara i kopiuje wynik do `src/Hr.Api/wwwroot/`;
4. uruchamia `Hr.Api` w tle na `http://127.0.0.1:5080` (`ConnectionStrings__Hr`, `Hr__SeedDemoData=true`, `ASPNETCORE_ENVIRONMENT=Development`); PID i log w `.run/` (ignorowane przez git);
5. czeka na `/api/health`, uruchamia `tools/dev/smoke.sh` (logowanie Anny, saldo, lista wniosków, serwowanie `index.html` i zasobów) i wypisuje komendę tunelu `ssh -N -L 5080:127.0.0.1:5080 <użytkownik>@<host>`.

`tools/dev/stop.sh` kończy API i zatrzymuje kontener bazy (dane zostają w wolumenie); `--reset` usuwa je razem z wolumenem. Dane demo są idempotentne.

**Zastrzeżenia:** proces w tle uruchomiony w sesji może zostać zakończony po jej zamknięciu (wystarczy ponownie `run.sh`); port bazy jest tylko na `127.0.0.1`; bez zbudowanego Angulara `/` zwraca 404 (skrypt zawsze buduje przed startem); agent (klucz Anthropic) nie jest potrzebny w 7a.

## 7. Dokumentacja do aktualizacji

- `web/README.md`: komendy (`npm ci`, `api:gen`, `api:check`, `build`, `test`, `verify`).
- arc42: sekcja 7 (uruchomienie deweloperskie), nowy ADR o stosie frontendu (Angular 22, Material, `ng-openapi-gen`, standalone i signals), status w README.

## 8. Ryzyka

- Generator może źle odwzorować część specyfikacji (np. `DateOnly`, `ProblemDetails`): łagodzi to przegląd wygenerowanego klienta i testy mapowania błędów.
- Czas lokalny bez strefy (ADR-17): błąd w serializacji dat w UI dałby `422`; łagodzi to test komponentu wpisów i jawne formatowanie.
- Tryb deweloperski z jawnymi hasłami: tylko `127.0.0.1` i tunel SSH, panel „Konta demo” wyłącznie w buildzie deweloperskim.
- Wydajność przeglądu zespołu dla kadr (31 wywołań analizy): ograniczona współbieżność; do rozważenia osobny endpoint zbiorczy, jeśli okaże się za wolno.
