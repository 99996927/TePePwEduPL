# Plan 7a: Angular — szkielet, logowanie, urlopy i czas pracy — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zbudować frontend Angular (`web/`) z logowaniem, układem zależnym od roli, pulpitem, urlopami (lista, wniosek, anulowanie, zatwierdzanie), czasem pracy (wpisy, korekty, analiza) i przeglądem zespołu (UC-01 do UC-15), serwowany przez `Hr.Api`, oraz skrypty uruchamiające całe rozwiązanie do ręcznego testu.

**Architecture:** Aplikacja Angular 22 (standalone, signals, zoneless, trasy ładowane leniwie wg funkcji) z klientem API generowanym z `docs/openapi/hr-api.json` przez `ng-openapi-gen`. Stan to cienkie serwisy nad wygenerowanymi serwisami API; sesja w ciasteczku, CSRF przez domyślny mechanizm XSRF Angulara. Build trafia do `src/Hr.Api/wwwroot/`. `tools/dev/run.sh` stawia SQL Server w kontenerze, buduje Angulara i uruchamia `Hr.Api` z danymi demo.

**Tech Stack:** Angular 22.2.3 (CLI `@angular/build`), Angular Material 22.2.3, `ng-openapi-gen` 1.1.0, Vitest + jsdom (domyślny runner CLI 22: `ng test`), TypeScript, SCSS; skrypty bash; .NET 9 bez zmian kontraktu.

**Spec:** `docs/superpowers/specs/2026-10-17-plan7a-angular-design.md` (zmergowany w #8) oraz `docs/arc42` (ADR-02, ADR-14, ADR-16, ADR-17, ADR-25).

## Global Constraints

- Node `^24.15.0` (w środowisku 24.18). Wszystkie zależności w `web/package.json` z **dokładnymi wersjami** (bez `^` i `~`), `package-lock.json` w repo: `@angular/*` 22.2.x tak jak zainstalowane przez CLI (rdzeń 22.2.0, `@angular/build`/`cli` 22.2.3), `@angular/material` i `@angular/cdk` 22.2.3, `ng-openapi-gen` 1.1.0, `vitest` 5.0.5, `jsdom` 30.x, `typescript` 6.0.x, `rxjs` 7.8.x — wpisz faktycznie zainstalowane. `engines.node` = `^24.15.0`.
- Projekt: `web/` (nazwa `hr-app`), standalone, zoneless (domyślne w CLI 22, bez `zone.js`), SCSS, bez SSR. Nazwy plików komponentów bez sufiksu (`login.ts`, `login.html`, `login.scss`, `login.spec.ts`; klasa `Login`), tak jak generuje CLI 22. Szablon w osobnym pliku `.html`. Zakaz `any`; `strict` z `tsconfig`. Stan komponentów w `signal`/`computed`, formularze reaktywne.
- Tekst interfejsu po polsku; daty, liczby i godziny w `pl-PL`; kwoty i godziny z przecinkiem. Komunikaty błędów pochodzą z `title`/`detail` odpowiedzi `ProblemDetails` (pole `code` i `ruleId` są rozszerzeniami; wygenerowany model `ProblemDetails` ich nie ma, więc czytaj surowe ciało przez `problemOf`).
- Klient API: wygenerowany do `web/src/app/api/` (**nie edytuj ręcznie**, pliki w repo), konfiguracja `web/ng-openapi-gen.json`: `input` `../docs/openapi/hr-api.json`, `output` `src/app/api`, `services: true`, `skipJsonSuffix: true`, `promises: false`, `removeStaleFiles: true`. `provideApiConfiguration('')` (adresy względne, jeden origin, bez CORS).
- Sesja i CSRF (ADR-25): logowanie zaczyna się od `GET /api/auth/antiforgery`; Angular dokłada `X-XSRF-TOKEN` z ciasteczka `XSRF-TOKEN` do żądań zapisu (domyślne `withXsrfConfiguration`); ciasteczko sesji jest `HttpOnly`. Odpowiedź `401` czyści sesję i przekierowuje na `/logowanie` (z wyjątkiem żądania logowania).
- Czas wpisów pracy (ADR-17): zawsze lokalny, bez strefy, `yyyy-MM-ddTHH:mm:ss` (np. `2026-10-12T08:00:00`); nigdy `toISOString()`. Daty urlopów: `yyyy-MM-dd` z daty lokalnej.
- Role: `Employee`, `Manager`, `HR`, `HRApprover` (ciągi tekstowe z `CurrentUserDto.roles`). Zatwierdzanie urlopów, przegląd zespołu: `Manager`, `HR`, `HRApprover`. Wejście po URL na niedozwoloną trasę: przekierowanie na `/` z komunikatem `Brak dostępu do tej strony.`.
- Build produkcyjny bez ostrzeżeń (jeśli budżet `initial` ostrzega, podnieś `maximumWarning` do `900kB` i `maximumError` do `1.5MB` w `angular.json` z komentarzem w README). Panel „Konta demo” na logowaniu jest widoczny tylko gdy `isDevMode()` (build `development`); `tools/dev/run.sh` używa `npm run build:dev`.
- Komunikaty commitów po polsku, w formie rozkazującej, bez emoji. Testy backendu: `DOCKER_API_VERSION=1.43 dotnet test ...`. Zmiany w `src/` wyłącznie w Task 1 (`wwwroot`) i skryptach (Task 9).

## Review Focus

1. **Wygasła sesja w trakcie pracy** (dowolne wywołanie zwraca `401`): jedno przekierowanie na logowanie z komunikatem, bez pętli i bez kasowania wprowadzonych danych formularza logowania (Task 2).
2. **Brak połączenia lub `500`:** komunikat `Brak połączenia z serwerem` / ogólny błąd serwera, aplikacja nie zawiesza się na stanie ładowania (Task 2, 4).
3. **Czas lokalny:** wpis przechodzący przez północ, koniec przed początkiem, dzień zmiany czasu — poprawna wysyłka bez strefy i komunikat `422` z API w formularzu (Task 7).
4. **Zakres dat urlopu:** zakres przez granicę roku, zakres bez dni roboczych, brak salda — komunikat z API w formularzu, przycisk nie wysyła podwójnie (Task 5).
5. **Ręczne wejście po URL niedozwolonej roli i odświeżenie strony (F5) na głębokiej trasie:** sesja odtwarza się przed guardami, brak błyskania logowania (Task 2, 3).

---

## Pliki

| Plik / katalog | Odpowiedzialność |
|---|---|
| `web/` (`package.json`, `angular.json`, `ng-openapi-gen.json`, `proxy.conf.json`, `scripts/*.mjs`) | Projekt, skrypty `api:gen`, `api:check`, `build:wwwroot`, `verify`. |
| `web/src/app/api/` | Wygenerowany klient (nie edytować). |
| `web/src/app/core/` | `api-problem`, `roles`, `session`, `unauthorized.interceptor`, `notifier`, `guards`, `employee-directory`, `date-format`, `layout/shell`. |
| `web/src/app/shared/` | `month-switcher`, `employee-picker`, `confirm-dialog`. |
| `web/src/app/features/` | `auth/login`, `dashboard/dashboard`, `leave/*`, `timesheet/*`, `team/team-overview`. |
| `tools/dev/run.sh`, `stop.sh`, `smoke.sh` | Uruchomienie rozwiązania do testów ręcznych. |
| `docs/testy-reczne/plan-7a.md` | Lista kroków do ręcznego testu per rola i UC. |

### Task 1: Projekt Angular, klient API, skrypty i pliki ignorowane

**Files:**
- Create: `web/` (przez `ng new`), `web/ng-openapi-gen.json`, `web/proxy.conf.json`, `web/scripts/api-check.mjs`, `web/scripts/copy-to-wwwroot.mjs`, `web/README.md`, `src/Hr.Api/wwwroot/.gitkeep`
- Modify: `.gitignore` (`web/node_modules/`, `web/dist/`, `web/.angular/`, `src/Hr.Api/wwwroot/*` z wyjątkiem `.gitkeep`, `.run/`); delete: `src/Hr.Api/wwwroot/index.html` (placeholder)
- Test: `web/src/app/app.spec.ts` (zastąp domyślny)

**Interfaces:**
- Produces: skrypty npm w `web/package.json`: `start` (`ng serve --proxy-config proxy.conf.json`), `build` (produkcyjny), `build:dev` (`ng build --configuration development`), `test` (`ng test --watch=false`), `api:gen` (`ng-openapi-gen --config ng-openapi-gen.json`), `api:check` (`node scripts/api-check.mjs`), `build:wwwroot` (`node scripts/copy-to-wwwroot.mjs` kopiuje `dist/hr-app/browser/*` do `../src/Hr.Api/wwwroot/`, czyszcząc je poza `.gitkeep`), `verify` (`npm run api:check && npm test && npm run build`). Wygenerowane serwisy (`LeaveService`, `TimesheetService`, `AuthService`, `EmployeesService` itd.) z metodami nazwanymi jak operacje (`leaveBalance`, `leaveList`, `leaveSubmit`, `leaveCancel`, `leaveDecide`, `timesheetList`, `timesheetAdd`, `timesheetCorrect`, `timesheetAnalyze`, `employeesList`, `authLogin`, `authLogout`, `authMe`, `authAntiforgery`) — dokładne sygnatury weź z wygenerowanych plików.

- [ ] **Step 1: Test dymny (RED).** Zastąp `app.spec.ts` testem `App_creates_and_renders_router_outlet` (tworzy `App`, oczekuje elementu `router-outlet`) oraz dodaj `scripts/api-check` jako polecenie, które ma na razie nie istnieć (`npm run api:check` → FAIL: brak skryptu).
- [ ] **Step 2: Utwórz projekt** komendą `npx @angular/cli@22.2.3 new hr-app --directory web --ssr=false --style=scss --routing --skip-git --defaults` z katalogu głównego repo, potem `cd web && npx ng add @angular/material@22.2.3 --skip-confirmation --defaults` (motyw M3 `azure`/`blue`, typografia Roboto) i `npm install --save-dev --save-exact ng-openapi-gen@1.1.0`. Zamień wszystkie zakresy w `package.json` na dokładne zainstalowane wersje, dodaj `engines`, odśwież `package-lock.json` (`npm install`). Usuń z `app.html` szablon powitalny (zostaw `<router-outlet />`).
- [ ] **Step 3: Skonfiguruj generator** (`ng-openapi-gen.json` wg Global Constraints) i uruchom `npm run api:gen`; sprawdź, że powstały serwisy (`*.service.ts`) i modele, a nazwy metod odpowiadają liście z Interfaces; jeśli generator nie tworzy serwisów mimo `services: true`, zgłoś rozbieżność (Global Constraints: nie obchodź). Skrypt `api-check.mjs`: uruchamia generator i kończy się kodem ≠ 0, gdy `git status --porcelain -- src/app/api` jest niepusty (nowe/zmienione/usunięte pliki). Skrypt `copy-to-wwwroot.mjs` jak w Interfaces.
- [ ] **Step 4: Backend i pliki ignorowane:** usuń placeholder `index.html`, dodaj `src/Hr.Api/wwwroot/.gitkeep`, uzupełnij `.gitignore`. `web/proxy.conf.json` kieruje `/api` na `http://127.0.0.1:5080`. `web/README.md`: komendy i uwagi (Node, `verify`, `build:wwwroot`, budżet).
- [ ] **Step 5: Weryfikacja:** `cd web && npm run verify` — oczekiwane: `api:check` czysty (po commicie wygenerowanych plików), testy 1/1, build bez ostrzeżeń; `dotnet build Hr.sln` (0 błędów, 0 ostrzeżeń) oraz `DOCKER_API_VERSION=1.43 dotnet test tests/Hr.Api.IntegrationTests --filter "FullyQualifiedName~Skeleton|FullyQualifiedName~OpenApi"` przechodzą.
- [ ] **Step 6: Commit** `Dodaj projekt Angular z klientem API i skryptami budowania`.

### Task 2: Rdzeń: błędy, role, sesja, interceptory, guardy, daty

**Files:**
- Create: `web/src/app/core/api-problem.ts`, `roles.ts`, `session.ts`, `unauthorized.interceptor.ts`, `notifier.ts`, `guards.ts`, `employee-directory.ts`, `date-format.ts` oraz `*.spec.ts` obok; Modify: `web/src/app/app.config.ts`, `app.routes.ts`

**Interfaces:**
- Consumes: wygenerowane `AuthService` (`authAntiforgery`, `authLogin`, `authMe`, `authLogout`), `EmployeesService.employeesList`, modele `CurrentUserDto`, `EmployeeDto`.
- Produces:
  - `interface ApiProblem { status: number; title: string; detail?: string; code?: string; ruleId?: string }`; `function problemOf(error: unknown): ApiProblem` (z `HttpErrorResponse`: `status`, `title`, `detail`, `code`, `ruleId` z ciała; `status` 0 → `{status:0, title:'Brak połączenia z serwerem'}`; brak ciała → `{status, title:'Wystąpił błąd serwera.'}` dla ≥500 albo `'Żądanie nie powiodło się.'`); `function messageOf(p: ApiProblem): string` (`detail ?? title`).
  - `const Roles = { Employee: 'Employee', Manager: 'Manager', HR: 'HR', HRApprover: 'HRApprover' } as const`; `type Role`; `const APPROVER_ROLES: readonly Role[] = ['Manager','HR','HRApprover']`.
  - `@Injectable SessionService`: `readonly user: Signal<CurrentUserDto | null>`, `readonly loaded: Signal<boolean>`, `isAuthenticated: Signal<boolean>`, `hasRole(...roles: string[]): boolean`, `load(): Promise<void>` (wywołuje `authMe`; `401` → użytkownik `null`; ustawia `loaded`), `login(userName: string, password: string): Promise<void>` (najpierw `authAntiforgery`, potem `authLogin`; błąd propagowany jako `ApiProblem` przez `problemOf`), `logout(): Promise<void>`, `clear(): void`.
  - `const unauthorizedInterceptor: HttpInterceptorFn` (przy `401` z URL innego niż `/api/auth/login` i `/api/auth/me`: `session.clear()` i nawigacja na `/logowanie` z `queryParams {sesja: 'wygasla'}`, tylko raz na zdarzenie).
  - `@Injectable Notifier { error(p: ApiProblem | string): void; info(message: string): void }` (MatSnackBar, 5 s, `aria-live`).
  - `const authGuard: CanActivateFn`; `function roleGuard(...roles: Role[]): CanActivateFn` (brak roli → `Notifier.info('Brak dostępu do tej strony.')` i `UrlTree` do `/`).
  - `@Injectable EmployeeDirectory { load(): Promise<void>; nameOf(id: string): string; readonly employees: Signal<EmployeeDto[]> }` (cache jednego wywołania `employeesList`; nieznany id → `'—'`).
  - `date-format.ts`: `toDateOnly(d: Date): string` (`yyyy-MM-dd` z komponentów lokalnych), `toLocalDateTime(day: Date, time: string): string` (`time` `HH:mm` → `yyyy-MM-ddTHH:mm:00`), `monthRange(first: Date): { from: string; to: string }` (pierwszy i ostatni dzień miesiąca jako `DateOnly`), `formatDate(s: string): string` (`pl-PL`), `formatTime(s: string): string` (`HH:mm`), `formatHours(n: number): string` (przecinek, jedno miejsce po przecinku).
  - `app.config.ts`: `provideHttpClient(withInterceptors([unauthorizedInterceptor]), withXsrfConfiguration({cookieName:'XSRF-TOKEN', headerName:'X-XSRF-TOKEN'}))`, `provideApiConfiguration('')`, `provideAppInitializer(() => inject(SessionService).load())`, `provideNativeDateAdapter()`, `{provide: MAT_DATE_LOCALE, useValue: 'pl-PL'}`, `{provide: LOCALE_ID, useValue: 'pl-PL'}` z `registerLocaleData(localePl)`.

- [ ] **Step 1: Testy (RED).** `api-problem.spec`: `problemOf` z `HttpErrorResponse` 422 z ciałem `{title:'Walidacja', detail:'Niewystarczające saldo urlopu: brakuje 3 dni.', code:'Validation', ruleId:'KP-154-1'}` zwraca te pola; status 0 → `Brak połączenia z serwerem`; 500 bez ciała → `Wystąpił błąd serwera.`. `session.spec` (`HttpTestingController`): `load` przy `401` → `user()` null i `loaded()` true; `load` przy 200 ustawia użytkownika; `login` wysyła najpierw `GET /api/auth/antiforgery`, potem `POST /api/auth/login` z nagłówkiem `X-XSRF-TOKEN` równym wartości `document.cookie` `XSRF-TOKEN` (ustaw ciasteczko w teście); błąd `423` → odrzucenie z `ApiProblem {status:423, code:'LockedOut'}`; `logout` czyści użytkownika. `unauthorized.interceptor.spec`: `401` z `/api/leave/requests` czyści sesję i nawiguje raz do `/logowanie`; `401` z `/api/auth/login` nie nawiguje. `guards.spec`: niezalogowany → `/logowanie`; `Employee` na trasie `roleGuard('Manager','HR','HRApprover')` → `UrlTree('/')` i komunikat; `Manager` przechodzi. `date-format.spec`: `toLocalDateTime(new Date(2026,9,12), '08:05')` = `2026-10-12T08:05:00` (bez `Z`); `toDateOnly(new Date(2026,0,5))` = `2026-01-05`; `monthRange(new Date(2026,1,1))` = `{from:'2026-02-01', to:'2026-02-28'}`; `formatHours(8.5)` = `8,5`. `employee-directory.spec`: drugie `load` nie woła API; `nameOf` nieznany → `—`.
- [ ] **Step 2: Uruchom** `cd web && npx ng test --watch=false` — oczekiwane: błędy kompilacji, potem FAIL.
- [ ] **Step 3: Zaimplementuj** typy z Interfaces; `app.routes.ts` na razie z trasą `logowanie` (pusty komponent-zaślepka usuń w Task 3) i przekierowaniem `**` → `''`.
- [ ] **Step 4: Uruchom** testy — PASS; `npm run build` bez ostrzeżeń.
- [ ] **Step 5: Commit** `Dodaj rdzeń aplikacji: sesję, błędy API, guardy i formatowanie dat`.

### Task 3: Układ, trasy, logowanie

**Files:**
- Create: `web/src/app/core/dev-mode.ts`, `web/src/app/core/layout/shell.ts` (+`.html`, `.scss`, `.spec.ts`), `web/src/app/features/auth/login.ts` (+`.html`, `.scss`, `.spec.ts`), `web/src/app/shared/confirm-dialog.ts`; Modify: `app.routes.ts`, `app.html`, `styles.scss`

**Interfaces:**
- Consumes: `SessionService`, `Notifier`, `authGuard`, `roleGuard`, `problemOf`, `Roles`, `APPROVER_ROLES`.
- Produces: `web/src/app/core/dev-mode.ts` z `export const IS_DEV_MODE = new InjectionToken<boolean>('IS_DEV_MODE', { providedIn: 'root', factory: () => isDevMode() })`; trasy (wszystkie komponenty ładowane leniwie `loadComponent`): `logowanie` → `Login`; `''` (komponent `Shell`, `canActivate: [authGuard]`) z dziećmi: `''` → `Dashboard`, `urlopy` → `LeaveList`, `urlopy/zatwierdzanie` → `LeaveApprovals` (`roleGuard(...APPROVER_ROLES)`), `czas-pracy` → `Timesheet`, `zespol` → `TeamOverview` (`roleGuard(...APPROVER_ROLES)`); `**` → `''`. Komponenty `Dashboard`, `LeaveList`, `LeaveApprovals`, `Timesheet`, `TeamOverview` w tym zadaniu są pustymi zaślepkami z tytułem strony (zastąpią je Task 4–8). `Shell`: `MatToolbar` (tytuł `Asystent kadrowo-płacowy`, `DisplayName`, rola, przycisk `Wyloguj`), `MatSidenav` z pozycjami `Pulpit`, `Urlopy`, `Czas pracy` dla wszystkich oraz `Zatwierdzanie urlopów`, `Przegląd zespołu` tylko dla `APPROVER_ROLES`; `ConfirmDialog` (`MatDialog`, dane `{title, message, confirmLabel}`, wynik `boolean`).

- [ ] **Step 1: Testy (RED).** `login.spec`: sukces wywołuje `session.login` i nawiguje do `/` (lub `returnUrl`); błąd `401 InvalidCredentials` pokazuje `Login lub hasło są nieprawidłowe.` przy formularzu i zachowuje wpisany login; błąd `423` pokazuje komunikat o zablokowanym koncie; pusty login lub hasło blokuje wysłanie; po `?sesja=wygasla` widoczny komunikat `Sesja wygasła. Zaloguj się ponownie.`; panel „Konta demo” obecny tylko gdy `isDevMode()` zwraca `true` (wstrzyknij flagę tokenem `IS_DEV_MODE` o wartości domyślnej `isDevMode()`, w teście przełącz) i zawiera 6 loginów z hasłami `imięnazwisko123` z `docs/arc42/07-widok-wdrozenia.md` 7.2; kliknięcie wiersza wypełnia formularz. `shell.spec`: `Employee` widzi 3 pozycje menu (Pulpit, Urlopy, Czas pracy), `Manager` i `HR` widzą 5; `Wyloguj` woła `session.logout` i nawiguje do `/logowanie`. `routes.spec`: wejście `Employee` na `/zespol` i `/urlopy/zatwierdzanie` kończy się na `/` z komunikatem (UC-04); niezalogowany na `/urlopy` ląduje na `/logowanie`; odświeżenie na `/czas-pracy` z ważną sesją zostaje na tej trasie (inicjalizator sesji kończy się przed guardem).
- [ ] **Step 2: Uruchom** `npx ng test --watch=false` — FAIL.
- [ ] **Step 3: Zaimplementuj** komponenty i trasy; styl układu w `styles.scss` i `shell.scss` (treść w `mat-sidenav-content`, na wąskim ekranie `mat-sidenav` w trybie `over`).
- [ ] **Step 4: Uruchom** testy i `npm run build` — PASS, bez ostrzeżeń.
- [ ] **Step 5: Commit** `Dodaj układ aplikacji, trasy z guardami ról i logowanie`.

### Task 4: Pulpit

**Files:**
- Modify: `web/src/app/features/dashboard/dashboard.ts` (+`.html`, `.scss`); Test: `dashboard.spec.ts`; Create: `web/src/app/shared/month-switcher.ts` (+spec)

**Interfaces:**
- Consumes: `LeaveService.leaveBalance`, `TimesheetService.timesheetAnalyze`, `LeaveService.leaveList`, `SessionService`, `monthRange`, `formatHours`, `problemOf`, `Notifier`.
- Produces: `MonthSwitcher` (selector `app-month-switcher`; `input.required<Date>() month`, `output<Date>() monthChange`; przyciski `‹` i `›`, etykieta `październik 2026`, przycisk `›` dla bieżącego miesiąca wyłączony); `Dashboard` pokazujący kafelki: **Saldo urlopu** (wymiar, wykorzystane, oczekujące, pozostałe), **Bieżący miesiąc** (przepracowane wobec normy w godzinach, nadgodziny 50%/100%), **Alerty** (liczba naruszeń odpoczynku, przekroczenie średniej 48 h, przekroczenie limitu 150 h; `Brak alertów` gdy zero), a dla ról z `APPROVER_ROLES` kafelek **Do zatwierdzenia: N** (oczekujące wnioski innych osób; link do `/urlopy/zatwierdzanie`). Każdy kafelek ma stan ładowania i stan błędu z przyciskiem `Spróbuj ponownie`.

- [ ] **Step 1: Testy (RED).** `month-switcher.spec`: `›` emituje następny miesiąc, `‹` poprzedni, `›` wyłączony dla bieżącego miesiąca. `dashboard.spec` (`HttpTestingController`): dla `Anna` (`Employee`) wywołania `/api/leave/balance` i `/api/timesheet/analysis?from=<pierwszy dzień bieżącego miesiąca>&to=<dziś>` (zegar testu stały) renderują wartości `20`, `10`, `5`, `5`; alerty pokazują liczbę naruszeń z odpowiedzi; brak kafelka „Do zatwierdzenia” dla `Employee`; dla `Manager` kafelek zlicza tylko wnioski `Pending` o `employeeId` ≠ własnemu; błąd `500` jednego kafelka nie blokuje pozostałych i pokazuje `Spróbuj ponownie` (kliknięcie wywołuje ponownie).
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj** komponenty (wywołania równoległe, osobne sygnały stanu na kafelek).
- [ ] **Step 4: Uruchom** testy i build — PASS.
- [ ] **Step 5: Commit** `Dodaj pulpit z saldem urlopu, czasem pracy i alertami`.

### Task 5: Urlopy: lista, nowy wniosek, anulowanie

**Files:**
- Modify: `web/src/app/features/leave/leave-list.ts` (+`.html`, `.scss`); Create: `web/src/app/features/leave/new-leave-dialog.ts` (+`.html`, `.scss`), specs

**Interfaces:**
- Consumes: `LeaveService` (`leaveBalance`, `leaveList`, `leaveSubmit`, `leaveCancel`), `EmployeeDirectory`, `ConfirmDialog`, `Notifier`, `toDateOnly`, `formatDate`, `problemOf`, `messageOf`.
- Produces: `LeaveList` (nagłówek z saldem; tabela `MatTable`: pracownik (`EmployeeDirectory.nameOf`), od, do, dni, status w języku polskim — `Pending` `Oczekuje`, `Approved` `Zatwierdzony`, `Rejected` `Odrzucony`, `Cancelled` `Anulowany` — powód decyzji; filtr statusu `MatSelect`; sortowanie od najnowszych; przycisk `Nowy wniosek`; przycisk `Anuluj` tylko przy własnym wniosku `Pending` po potwierdzeniu `ConfirmDialog`; stan pusty `Brak wniosków`); `NewLeaveDialog` (`MatDateRangePicker`; `Zapisz` wyłączony do wyboru obu dat i w trakcie wysyłania; po sukcesie zamyka dialog zwracając `LeaveRequestDto`, lista się odświeża i `Notifier.info('Wniosek złożony: N dni roboczych.')`; błąd `422`/`409` wyświetla `messageOf(problem)` w dialogu, bez zamykania).

- [ ] **Step 1: Testy (RED).** `leave-list.spec`: renderuje wiersze z polskimi statusami i imionami; filtr `Oczekuje` ukrywa pozostałe; `Anuluj` widoczny tylko dla własnego `Pending` (inne statusy i cudze wnioski bez przycisku); potwierdzenie wywołuje `POST /api/leave/requests/{id}/cancel` i odświeża listę; odmowa w dialogu potwierdzenia nic nie wysyła; saldo w nagłówku. `new-leave-dialog.spec`: wybór zakresu `2026-11-02`–`2026-11-06` wysyła `POST /api/leave/requests` z `{from:'2026-11-02', to:'2026-11-06'}` (daty lokalne, bez przesunięcia strefy); `422` z `detail` `Niewystarczające saldo urlopu: brakuje 3 dni.` pokazuje ten tekst, dialog zostaje otwarty, a drugi klik w trakcie wysyłania nie wysyła drugiego żądania; sukces zamyka dialog z wynikiem; zakres przez granicę roku (`422` z API) pokazuje komunikat API.
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj** komponenty (`MatTable` z `MatSort` nie jest wymagane; sortowanie `computed`).
- [ ] **Step 4: Uruchom** testy i build — PASS.
- [ ] **Step 5: Commit** `Dodaj ekran urlopów: lista, nowy wniosek i anulowanie`.

### Task 6: Zatwierdzanie urlopów

**Files:**
- Modify: `web/src/app/features/leave/leave-approvals.ts` (+`.html`, `.scss`); Create: `web/src/app/features/leave/decision-dialog.ts` (+`.html`), specs

**Interfaces:**
- Consumes: `LeaveService.leaveList`, `LeaveService.leaveDecide` (`LeaveDecisionRequest {approve: boolean; reason?: string | null}`), `EmployeeDirectory`, `SessionService`, `Notifier`.
- Produces: `LeaveApprovals` (tabela oczekujących wniosków innych osób z widoczności użytkownika: pracownik, zespół jeśli dostępny z `EmployeeDirectory`, od, do, dni, data złożenia; przyciski `Zatwierdź` i `Odrzuć`); `DecisionDialog` (dane `{request, approve}`; przy odrzuceniu pole `Uzasadnienie` wymagane i niepuste po `trim`, przy zatwierdzeniu opcjonalne; wynik `{reason?: string}`).

- [ ] **Step 1: Testy (RED).** `leave-approvals.spec`: lista zawiera tylko `Pending` cudzych wniosków; `Zatwierdź` wysyła `POST /api/leave/requests/{id}/decision` z `{approve:true, reason:null}` i usuwa wiersz; `Odrzuć` bez uzasadnienia nie wysyła (komunikat `Podaj uzasadnienie odrzucenia.`); `Odrzuć` z uzasadnieniem wysyła `{approve:false, reason:'…'}`; błąd `403`/`404` pokazuje snackbar i nie usuwa wiersza; błąd `422` (np. brak salda przy zatwierdzaniu) pokazuje `messageOf` w dialogu; stan pusty `Brak wniosków do zatwierdzenia`.
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj.** **Step 4: Uruchom** testy i build — PASS.
- [ ] **Step 5: Commit** `Dodaj zatwierdzanie wniosków urlopowych przez kierownika i kadry`.

### Task 7: Czas pracy: wpisy, dodawanie i korekta

**Files:**
- Modify: `web/src/app/features/timesheet/timesheet.ts` (+`.html`, `.scss`); Create: `web/src/app/features/timesheet/entry-dialog.ts` (+`.html`), `web/src/app/shared/employee-picker.ts` (+spec), specs

**Interfaces:**
- Consumes: `TimesheetService` (`timesheetList`, `timesheetAdd`, `timesheetCorrect`), `EmployeeDirectory`, `SessionService`, `MonthSwitcher`, `toLocalDateTime`, `monthRange`, `formatDate`, `formatTime`, `formatHours`, `problemOf`, `messageOf`, `Notifier`.
- Produces: `EmployeePicker` (selector `app-employee-picker`; `input<EmployeeDto[]>() employees`, `model<string | null>() selectedId`; `MatSelect` z `Imię Nazwisko (zespół)`); `Timesheet` (nagłówek: `MonthSwitcher` i, dla `APPROVER_ROLES`, `EmployeePicker` z domyślnym wyborem siebie; pobiera `timesheetList` dla miesiąca i filtruje po stronie klienta do wybranego pracownika; tabela: dzień tygodnia i data, start, koniec, przerwa w minutach, przepracowane w godzinach; sortowanie rosnąco po dacie; wpis kończący się następnego dnia oznaczony `+1 dzień`; `Dodaj wpis` tylko dla własnego czasu pracy; `Koryguj` w wierszu dla właściciela wpisu oraz dla ról `HR` i `HRApprover` (tak jak w `TimesheetService.CorrectEntryAsync`); stan pusty `Brak wpisów w tym miesiącu`); `EntryDialog` (tryb `add` albo `correct`; pola data, start `HH:mm`, koniec `HH:mm`, przerwa w minutach, w trybie `correct` także `Powód` wymagany; jeśli `end <= start` w tej samej dacie, pole `Koniec` oznacza następny dzień i pokazuje podpowiedź `Wpis zakończy się następnego dnia`; wysyła `AddEntryRequest`/`CorrectEntryRequest` z `toLocalDateTime`; błędy `422`/`409` jako `messageOf` w dialogu). Analiza okresu dojdzie w Task 8 (miejsce w szablonie: `<app-analysis-panel>` dodawane tam).

- [ ] **Step 1: Testy (RED).** `employee-picker.spec`: zmiana wyboru ustawia `selectedId`. `timesheet.spec`: pobiera `GET /api/timesheet/entries?from=2026-10-01&to=2026-10-31` dla października 2026 i pokazuje tylko wpisy wybranego pracownika (wpisy innych osób odfiltrowane po `employeeId`); `Employee` nie widzi pickera; `Manager` widzi picker; wpis `22:00`–`06:00` następnego dnia ma znacznik `+1 dzień`; `Koryguj` nie jest pokazany dla cudzego wpisu roli `Manager`, ale jest dla `HR` i `HRApprover`; przejście `›` pobiera kolejny miesiąc. `entry-dialog.spec`: tryb `add`, data `2026-10-12`, `08:00`–`16:00`, przerwa `30` wysyła `POST /api/timesheet/entries` z `{start:'2026-10-12T08:00:00', end:'2026-10-12T16:00:00', breakMinutes:30}` (bez `Z`); `22:00`–`06:00` wysyła `end:'2026-10-13T06:00:00'`; `422` z API (np. `Wpis nie może kończyć się w przyszłości.`) pokazuje ten tekst, dialog zostaje; `409` (nakładanie) pokazuje komunikat; tryb `correct` bez powodu nie wysyła, z powodem wysyła `PUT /api/timesheet/entries/{id}` z `reason`; dzień zmiany czasu (`2026-10-25`) nadal daje wartości bez strefy.
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj.** **Step 4: Uruchom** testy i build — PASS.
- [ ] **Step 5: Commit** `Dodaj ekran czasu pracy: wpisy, dodawanie i korekty`.

### Task 8: Czas pracy: panel analizy, przegląd zespołu, porządki

**Files:**
- Create: `web/src/app/features/timesheet/analysis-panel.ts` (+`.html`, `.scss`), `web/src/app/features/team/team-overview.ts` (+`.html`, `.scss`), specs; Modify: `timesheet.html` (osadzenie panelu), `shell` (brak zmian poza ewentualną poprawką linku)

**Interfaces:**
- Consumes: `TimesheetService.timesheetAnalyze` (`employeeId?`, `from`, `to`), `TimesheetAnalysisDto` (`normHours`, `workedHours`, `overtime {hours50, hours100, total, ruleIds}`, `restViolations [{ruleId, from, to, restHours, requiredHours}]`, `weeklyAverage {averageWeeklyHours, exceedsLimit, ruleId}`, `overtimeYearToDate`, `annualOvertimeLimitExceeded`), `EmployeeDirectory`, `MonthSwitcher`, `monthRange`, `formatHours`, `formatDate`, `formatTime`.
- Produces: `AnalysisPanel` (selector `app-analysis-panel`; `input.required<string | null>() employeeId`, `input.required<Date>() month`; pobiera analizę dla miesiąca i pokazuje: norma i przepracowane (h), nadgodziny 50% i 100% (h), lista naruszeń odpoczynku (`Odpoczynek dobowy` dla `KP-132-1`, `Odpoczynek tygodniowy` dla `KP-133-1`; od–do, `X h` wobec wymaganych `Y h`, numer przepisu), średnia tygodniowa wobec 48 h z wyróżnieniem przy `exceedsLimit`, nadgodziny narastająco w roku wobec limitu 150 h z wyróżnieniem przy `annualOvertimeLimitExceeded`; `Brak naruszeń` gdy puste; stan ładowania i błędu); `TeamOverview` (`MonthSwitcher`; tabela osób z `EmployeeDirectory.employees` (dla kierownika jego zespół wynika z zakresu danych API, dla kadr wszyscy): imię i nazwisko, przepracowane/norma, nadgodziny razem, odznaki `Odpoczynek` (liczba naruszeń), `Średnia >48 h`; analiza pobierana osobno dla każdej osoby z ograniczoną współbieżnością 4; wiersz klikalny nawiguje do `/czas-pracy` z `queryParams {pracownik: <id>, miesiac: yyyy-MM}`, a `Timesheet` odczytuje te parametry jako stan początkowy).

- [ ] **Step 1: Testy (RED).** `analysis-panel.spec`: odpowiedź z jednym naruszeniem `KP-132-1` (`restHours` 9, `requiredHours` 11) pokazuje `Odpoczynek dobowy`, `9,0 h` wobec `11,0 h` i `KP-132-1`; `weeklyAverage.exceedsLimit` true oznacza wyróżnienie (klasa `exceeded`) i tekst `ponad limit 48 h`; `annualOvertimeLimitExceeded` true pokazuje ostrzeżenie o limicie 150 h; puste naruszenia → `Brak naruszeń`; zmiana `employeeId` pobiera analizę ponownie. `team-overview.spec`: dla 7 osób wykonuje 7 wywołań analizy z nie więcej niż 4 jednocześnie (licznik w mocku `HttpTestingController`); odznaki zgodne z odpowiedziami; błąd jednej osoby nie wyłącza pozostałych (wiersz z `Spróbuj ponownie`); kliknięcie wiersza nawiguje z parametrami; `timesheet.spec` rozszerz o odczyt `pracownik` i `miesiac` z adresu.
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj.** **Step 4: Uruchom** `npm run verify` — PASS (testy, `api:check`, build bez ostrzeżeń).
- [ ] **Step 5: Commit** `Dodaj panel analizy czasu pracy i przegląd zespołu`.

### Task 9: Skrypty uruchomieniowe, dokumentacja i lista testów ręcznych

**Files:**
- Create: `tools/dev/run.sh`, `tools/dev/stop.sh`, `tools/dev/smoke.sh`, `docs/testy-reczne/plan-7a.md`; Modify: `docs/arc42/07-widok-wdrozenia.md` (nowa sekcja 7.4 „Uruchomienie deweloperskie”), `docs/arc42/09-decyzje-architektoniczne.md` (nowy ADR o stosie frontendu o kolejnym wolnym numerze + wiersz w tabeli), `docs/arc42/README.md` (status), `web/README.md`

**Interfaces:**
- Produces: `tools/dev/run.sh` (bash, `set -euo pipefail`, uruchamialny z dowolnego katalogu, wykonywalny): (1) sprawdza `docker`, `dotnet`, `node` (komunikat po polsku przy braku); (2) wczytuje lub tworzy `.env` w katalogu głównym z `HR_DEV_SA_PASSWORD` (wygenerowane, spełnia politykę SQL Server: ≥ 12 znaków, wielkie/małe litery, cyfry, znak specjalny; plik `chmod 600`); (3) uruchamia kontener `hr-dev-db` (`mcr.microsoft.com/mssql/server:2022-latest`, `-p 127.0.0.1:14330:1433`, wolumen `hr-dev-db-data`, `ACCEPT_EULA=Y`, `MSSQL_SA_PASSWORD`) albo wznawia istniejący, i czeka na gotowość (zapytanie `SELECT 1` przez `sqlcmd` w kontenerze lub kontrola logu, do 90 s); (4) `cd web && npm ci && npm run build:dev && npm run build:wwwroot`; (5) uruchamia `Hr.Api` przez `setsid nohup dotnet run --project src/Hr.Api --no-launch-profile` z `ASPNETCORE_URLS=http://127.0.0.1:5080`, `ASPNETCORE_ENVIRONMENT=Development`, `ConnectionStrings__Hr` (Server `127.0.0.1,14330`, baza `hr_dev`, `User Id=sa`, `TrustServerCertificate=True`), `Hr__SeedDemoData=true`; PID w `.run/api.pid`, log w `.run/api.log`; jeśli proces już działa, kończy go i startuje na nowo; (6) czeka do 120 s na `GET /api/health`; (7) uruchamia `smoke.sh`; (8) wypisuje adres `http://localhost:5080` i komendę `ssh -N -L 5080:127.0.0.1:5080 <użytkownik>@<host>`. `tools/dev/stop.sh [--reset]`: kończy API z `.run/api.pid`, zatrzymuje `hr-dev-db`; `--reset` dodatkowo usuwa kontener i wolumen. `tools/dev/smoke.sh [adres]` (domyślnie `http://127.0.0.1:5080`, `curl` z plikiem ciasteczek): `GET /` zawiera `<app-root`, a wskazany w nim skrypt `main-*.js` zwraca 200; `GET /api/auth/antiforgery` 204; `POST /api/auth/login` jako `anna.nowak`/`annanowak123` z nagłówkiem `X-XSRF-TOKEN` 200; `GET /api/leave/balance` ma `"entitlementDays":20`; `GET /api/leave/requests` 200; logowanie ze złym hasłem 401; każdy krok wypisuje `OK`/`BŁĄD`, kod wyjścia ≠ 0 przy pierwszym błędzie.
- `docs/testy-reczne/plan-7a.md`: tabela kroków per rola (`anna.nowak`, `jan.kowalski`, `tomasz.lis`, `ewa.wisniewska`, `piotr.zielinski`, `maria.kaminska`) i UC-01–UC-15 z oczekiwanym wynikiem (m.in. Anna: saldo 20/10/5/5, naruszenia 29.09 i 12.09, wniosek oczekujący 2–6.11; Jan: średnia > 48 h w czerwcu–wrześniu, limit 150 h; Tomasz: brak alertów, brak dostępu do danych Anny; Ewa: zatwierdza wniosek zespołu, widzi Annę i Jana; Piotr i Maria: widzą wszystkich).

- [ ] **Step 1: Napisz `smoke.sh` i skrypty (RED/GREEN przez uruchomienie).** Najpierw `smoke.sh` bez działającej aplikacji kończy się błędem (`BŁĄD`, kod ≠ 0).
- [ ] **Step 2: Zaimplementuj** `run.sh` i `stop.sh` wg Interfaces; `chmod +x`. Sprawdź składnię `bash -n` oraz `shellcheck`, jeśli dostępny.
- [ ] **Step 3: Uruchom `tools/dev/run.sh`** i potwierdź: `smoke.sh` przechodzi, w logu API brak wyjątków, `curl -s http://127.0.0.1:5080/` zwraca stronę z `<app-root`. Następnie `tools/dev/stop.sh` i ponowne `run.sh` (dane z wolumenu, seed bez dublowania: `Employees` nadal 31 — sprawdź zapytaniem `sqlcmd`). Zostaw aplikację **zatrzymaną** (uruchomienie końcowe do ręcznych testów wykonuje kontroler, nie implementer).
- [ ] **Step 4: Dokumentacja:** sekcja 7.4 (komendy `run.sh`, `stop.sh`, `smoke.sh`, tunel SSH, wymagania, zastrzeżenia z speca §6), ADR (Angular 22, Material, `ng-openapi-gen`, standalone i signals, bez NgRx; konsekwencje: klient w repo, `api:check`), README status (`Zrealizowane: … plan 7a`; kolejne 7b, 7c, Docker, E2E), `web/README.md` (komendy skryptów dev).
- [ ] **Step 5: Pełna weryfikacja:** `cd web && npm run verify`; `dotnet build Hr.sln`; `DOCKER_API_VERSION=1.43 dotnet test tests/Hr.Api.IntegrationTests` (cały projekt API; uruchom w tle, może trwać kilka minut) — wszystkie zielone.
- [ ] **Step 6: Commit** (można w dwóch: skrypty i dokumentacja) `Dodaj skrypty uruchamiające rozwiązanie i opisz plan 7a w dokumentacji`.
