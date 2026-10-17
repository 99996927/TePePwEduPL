# Plan 7d: nowy wygląd (styl „Material Dashboard”) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cała aplikacja Angular dostaje nowy wygląd: jasnoszare tło, białe karty z cieniem, fioletowy gradientowy akcent, menu z ikonami, chipy statusów, dymki w czacie. Bez zmian logiki.

**Architecture:** Motyw Material 3 z nadpisanymi tokenami plus jeden zestaw wspólnych stylów (`web/src/styles/`) i mały komponent `app-status-chip`. Szablony ekranów dostają klasy `page-card*` i chipy; teksty, `data-*` i role ARIA zostają, więc istniejące testy przechodzą bez zmian asercji.

**Tech Stack:** Angular 22.2.3, Angular Material (M3, `mat.theme`), SCSS, Vitest+jsdom.

**Spec:** `docs/superpowers/specs/2026-10-17-plan7d-wyglad-design.md`

## Global Constraints

- Zmieniamy tylko `web/`, `docs/` i (opcjonalnie) `tools/dev/smoke.sh`; backend, `tests/`, `docs/openapi` i `web/src/app/api/` (wygenerowany klient) nie są ruszane.
- Istniejące asercje testów nie są modyfikowane; wolno dodawać nowe testy i importy komponentów. Jeśli istniejący test nie przechodzi, poprawiamy implementację, nie test (wyjątek: test zależny od struktury DOM, którą spec jawnie zmienia, np. selektor paska; zmianę zgłosić w raporcie).
- Zachowujemy: wszystkie atrybuty `data-*`, role ARIA, `aria-label`, polskie teksty, tagi `h1` z tytułami ekranów, klasę `logout` i tekst „Wyloguj”, liczby pozycji menu 5/7/10.
- Statusy: kolor to dodatek, tekst etykiety zostaje jedynym nośnikiem informacji; chip renderuje wyłącznie tekst etykiety (bez dodatkowego tekstu w DOM).
- Kontrast tekstu co najmniej WCAG AA (4,5:1; 3:1 dla tekstu ≥ 18,66 px pogrubionego). Tekst biały tylko na ciemnych odcieniach gradientu; chipy to ciemny tekst na jasnym tle tonu.
- Tokeny kolorów: tło strony `#eee`; menu `#f4f5f7`; cień karty `0 2px 8px rgba(0,0,0,.12)`; promień karty 8 px; akcent gradient 60°: fiolet `#9c27b0 → #7b1fa2`, zielony `#43a047 → #2e7d32`, pomarańczowy `#fb8c00 → #e65100`, czerwony `#e53935 → #b71c1c`, turkus `#00acc1 → #00838f`.
- Fonty i ikony bez zmian (Roboto, Material Symbols z Google Fonts). Bez trybu ciemnego.
- Weryfikacja końcowa: `cd web && npm run verify` (baza: 350 testów, bez ostrzeżeń).

## Review Focus

Wejścia i warunki, które spec implikuje, a które mogą zepsuć użytkownikowi widok (każdy ma test w zadaniu-właścicielu):
1. Nieznany status (spoza słownika) → chip neutralny z surowym kluczem, bez wyjątku (Task 1).
2. Długi tytuł ekranu i długa rola („Zatwierdzający kadr”) → przycinanie z wielokropkiem, bez rozjechania paska i menu (Task 2, test: brak zawijania ustawiony klasą; sprawdzenie ręczne).
3. Wąski ekran (≤ 800 px): menu `over`, zamyka się po wyborze pozycji, przycisk menu ma `aria-label="Menu"` (Task 2).
4. Fokus klawiaturą na pozycjach menu i przyciskach w gradientowym nagłówku jest widoczny (Task 2, Task 4: styl `:focus-visible`; ręcznie).
5. Stany ładowania, błędu (`role="alert"`) i pusty wewnątrz kart nadal są widoczne i mają kontrast (Task 3 i 4: istniejące testy ekranów muszą przechodzić).

---

### Task 1: Motyw, wspólne style i `app-status-chip`

**Files:**
- Create: `web/src/styles/_tokens.scss`, `web/src/styles/_components.scss`
- Create: `web/src/app/shared/status-chip.ts`, `web/src/app/shared/status-chip.spec.ts`
- Modify: `web/src/styles.scss`

**Interfaces:**
- Consumes: istniejące `mat.theme` w `styles.scss`.
- Produces: 
  - Zmienne CSS na `:root` z tokenów (nazwy: `--app-bg`, `--app-menu-bg`, `--app-card-shadow`, `--app-card-radius`, `--app-grad-primary`, `--app-grad-success`, `--app-grad-warning`, `--app-grad-danger`, `--app-grad-info`).
  - Klasy globalne: `.page-card` (biała karta), `.page-card__header` (gradientowy nagłówek wystający nad treść; wariant `--primary` domyślny, modyfikatory `--success|--warning|--danger|--info`), `.page-card__body`, `.page-card--tile` (karta z pływającą ikoną; element `.page-card__icon` z modyfikatorami kolorów), `.data-table` (klasa na `<table mat-table>`: nagłówek, paski, odstępy).
  - `export type StatusTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral'`.
  - `export function statusTone(status: string): StatusTone` w `shared/status-chip.ts`: `Approved`→success, `Pending`→warning, `Rejected`→danger, `Cancelled`→neutral, `Draft`→neutral, inne (także puste)→neutral; wyszukiwanie przez `Object.hasOwn`.
  - Komponent `StatusChip` (`selector: 'app-status-chip'`): `label = input.required<string>()`, `status = input.required<string>()`; szablon to jeden `<span class="status-chip status-chip--{{tone}}" [attr.data-tone]="tone()">{{ label() }}</span>` (tylko tekst etykiety w DOM).

- [ ] **Step 1: Write the failing tests** w `status-chip.spec.ts`: `statusTone` zwraca success/warning/danger/neutral dla `Approved`/`Pending`/`Rejected`/`Cancelled`; `statusTone('Zażółć')`, `statusTone('')` i `statusTone('toString')` zwracają `neutral` (nieznany status, `Object.hasOwn`); komponent z `label='Zatwierdzony'`, `status='Approved'` renderuje `textContent.trim() === 'Zatwierdzony'` i `data-tone="success"`; z `status='Nieznany'` ma `data-tone="neutral"` i tekst równy etykiecie.
- [ ] **Step 2: Run** `cd web && npx ng test --watch=false` — Expected: FAIL (brak komponentu).
- [ ] **Step 3: Implement** `shared/status-chip.ts` (standalone, `ChangeDetectionStrategy.OnPush`, style chipów w `_components.scss`: ciemny tekst na jasnym tle tonu, np. success `#1b5e20` na `#e8f5e9`, warning `#8a4b00` na `#fff3e0`, danger `#b71c1c` na `#ffebee`, info `#006064` na `#e0f7fa`, neutral `#37474f` na `#eceff1`; zaokrąglenie 12 px, padding 2px 10px, `font: var(--mat-sys-label-medium)`).
- [ ] **Step 4: Implement** `_tokens.scss` (zmienne z Global Constraints) i `_components.scss` (klasy z bloku Produces; `.page-card__header` ma biały tekst na gradiencie, margines ujemny/transformację tak, by wystawał ~16 px nad treść, `:focus-visible` z outline 2 px białym dla kontrolek w nagłówku; `.data-table` z paskami `tr:nth-child(even)` w `#fafafa`, nagłówek `th` pogrubiony w `#555`). W `styles.scss`: `@use` plików `styles/`, paleta Material na fiolet (`mat.$violet-palette` albo najbliższa dostępna w Material 22, potwierdzić w `node_modules/@angular/material`), `body` tło `var(--app-bg)`, zachować `.error-snackbar`.
- [ ] **Step 5: Verify contrast** jednorazowym skryptem Node (nie commitować) liczącym WCAG dla: białego tekstu na początkowych stopach każdego gradientu i par chipów; wpisać ilorazy do raportu; każdy ≥ 4,5. Jeśli nie, przyciemnić odcień (i poprawić Global Constraints w tym planie).
- [ ] **Step 6: Run** `cd web && npm run verify` — Expected: wszystkie testy zielone (350 + nowe), build bez ostrzeżeń.
- [ ] **Step 7: Commit** `git add web && git commit -m "Dodaj motyw, wspólne style i chip statusu"`.

---

### Task 2: Szkielet — menu z ikonami, pasek górny z tytułem

**Files:**
- Modify: `web/src/app/core/layout/shell.ts`, `shell.html`, `shell.scss`, `shell.spec.ts` (tylko dodane testy)

**Interfaces:**
- Consumes: klasy i tokeny z Task 1; `MENU`, `SessionService`, `Router`.
- Produces: `MenuItem.icon: string` (nazwa Material Symbols; mapowanie w stałej `MENU` w `shell.ts`: Pulpit `dashboard`, Urlopy `beach_access`, Czas pracy `schedule`, Naliczenia `payments`, Agent `smart_toy`, Zatwierdzanie urlopów `task_alt`, Przegląd zespołu `groups`, Zmiany płacowe `request_quote`, Szkice korekt `edit_note`, Audyt `fact_check`); `protected readonly title = computed(...)`: etykieta pozycji menu o najdłuższej ścieżce będącej prefiksem aktualnego URL (z `router.url` jako sygnał przez `toSignal` ze zdarzeń `NavigationEnd`, wartość początkowa z `router.url`), a gdy brak dopasowania — `'Asystent kadrowo-płacowy'`.

- [ ] **Step 1: Write the failing tests** (dodane do `shell.spec.ts`, w stylu istniejących): dla każdej roli liczba pozycji menu to nadal 5/7/10 i każda pozycja zawiera `mat-icon` z oczekiwaną nazwą ikony; tytuł dla `/urlopy/zatwierdzanie` to „Zatwierdzanie urlopów” (najdłuższy prefiks, nie „Urlopy”), dla `/` to „Pulpit”, dla `/nieznana` to „Asystent kadrowo-płacowy”; w menu jest nazwa i rola zalogowanego (`data-user-name`, `data-user-role`); po kliknięciu pozycji na wąskim ekranie menu się zamyka (istniejące zachowanie, test pozostaje).
- [ ] **Step 2: Run** `cd web && npx ng test --watch=false` — Expected: nowe testy FAIL.
- [ ] **Step 3: Implement** w `shell.ts`/`shell.html`: menu `#f4f5f7`, szerokość 260 px, nagłówek z nazwą aplikacji, pozycje `mat-list-item` z `matListItemIcon`, aktywna z gradientem `--app-grad-primary`, białym tekstem i cieniem; stopka menu z nazwą i rolą (`data-user-name`, `data-user-role`; tekst z przycinaniem `text-overflow: ellipsis`); pasek górny biały (`mat-toolbar` bez `color="primary"`), z tytułem `title()` (przycinany), nazwa użytkownika i rola ukryte poniżej 800 px jak dotąd, przycisk „Wyloguj” (`class="logout"`, ikona `logout` plus tekst; tekst zostaje w DOM). Ruling: zamiast rozwijanego menu użytkownika zostaje widoczny przycisk „Wyloguj” (rozwijane menu ukryłoby przycisk przed istniejącym testem wylogowania; koszt, gdyby zły: jeden `mat-menu` i poprawka testu).
- [ ] **Step 4: Run** `cd web && npm run verify` — Expected: zielono.
- [ ] **Step 5: Commit** `git commit -m "Dodaj menu z ikonami i pasek górny z tytułem ekranu"`.

---

### Task 3: Logowanie i pulpit

**Files:**
- Modify: `web/src/app/features/auth/login.{html,scss}`, `web/src/app/features/dashboard/dashboard.{html,scss,ts}`

**Interfaces:**
- Consumes: `.page-card--tile`, `.page-card__icon` i modyfikatory kolorów, `.page-card__header` z Task 1.
- Produces: brak nowych interfejsów publicznych.

- [ ] **Step 1: Write the failing tests** (dodane do `dashboard.spec.ts` i `login.spec.ts`, w ich stylu): każdy kafelek `[data-tile]` zawiera element `.page-card__icon` z `mat-icon` (balance `beach_access`, month `schedule`, alerts `warning`, approvals `task_alt`); kafelek `alerts` bez alertów ma modyfikator zielony, z alertami czerwony (po stanie danych); logowanie ma nagłówek `.page-card__header` z tekstem „Asystent kadrowo-płacowy”.
- [ ] **Step 2: Run** testy — Expected: nowe FAIL.
- [ ] **Step 3: Implement** kafelki w stylu `page-card--tile` (zachować `data-tile`, `data-value`, `mat-progress-bar`, przyciski „Spróbuj ponownie”, `role="alert"`); logowanie jako karta na szarym tle z gradientowym nagłówkiem; panel „Konta demo” bez zmian funkcjonalnych.
- [ ] **Step 4: Run** `cd web && npm run verify` — Expected: zielono.
- [ ] **Step 5: Commit** `git commit -m "Zmień wygląd logowania i pulpitu"`.

---

### Task 4: Ekrany z tabelami, naliczenia i chipy statusów

**Files:**
- Modify: szablony i style w `features/leave/` (`leave-list`, `leave-approvals`), `features/timesheet/` (`timesheet`, `analysis-panel`), `features/team/team-overview`, `features/payroll/` (`payroll`, `payroll-card`, `payroll-summary`), `features/payroll-changes/payroll-changes`, `features/correction-drafts/correction-drafts`, `features/audit/audit`
- Modify: `leave-list.ts`, `payroll-changes.ts`, `correction-drafts.ts` (import `StatusChip`)
- Test: dodane testy w `leave-list.spec.ts`, `payroll-changes.spec.ts`, `correction-drafts.spec.ts`, `analysis-panel.spec.ts`

**Interfaces:**
- Consumes: `StatusChip`, `statusTone` z Task 1; klasy `page-card`, `page-card__header`, `data-table`.
- Produces: brak.

- [ ] **Step 1: Write the failing tests:** w trzech listach (urlopy, zmiany płacowe, szkice) komórka statusu zawiera `app-status-chip`, a jej `textContent.trim()` równa się polskiej etykiecie statusu (jak w istniejących asercjach), `data-tone` zgodny z `statusTone`; w `analysis-panel.spec.ts` naruszenia odpoczynku mają element w tonie `danger`, a brak alertów `success`.
- [ ] **Step 2: Run** testy — Expected: nowe FAIL.
- [ ] **Step 3: Implement** na każdym ekranie: `h1` z tytułem trafia do `.page-card__header` (tag `h1` zostaje; filtry i przyciski akcji w nagłówku, z `:focus-visible` i kontrastem na gradiencie), treść w `.page-card__body`, tabele z klasą `data-table`; w komórkach statusu `<app-status-chip [status]="r.status" [label]="statusLabel(r.status)" />`; kwoty w tabelach naliczeń wyrównane do prawej; stany ładowania, błędu (`role="alert"`) i pusty wewnątrz karty w obecnych elementach; panel analizy jako osobna karta z wyróżnionymi wartościami i alertami w kolorach tonów. Selektory `data-*` i teksty bez zmian.
- [ ] **Step 4: Run** `cd web && npm run verify` — Expected: zielono (wszystkie istniejące testy tych ekranów bez zmian asercji).
- [ ] **Step 5: Commit** `git commit -m "Zmień wygląd ekranów z tabelami i dodaj chipy statusów"`.

---

### Task 5: Agent (lista, dymki) i dialogi

**Files:**
- Modify: `features/agent/` (`agent`, `conversation-list`, `conversation-thread`: html i scss), wszystkie `*-dialog.scss` i `shared/confirm-dialog.ts` (styl), `web/src/styles/_components.scss` (wspólne style dialogów)
- Test: dodane testy w `conversation-thread.spec.ts`

**Interfaces:**
- Consumes: tokeny i klasy z Task 1.
- Produces: brak.

- [ ] **Step 1: Write the failing test:** w `conversation-thread.spec.ts` wiadomość User ma `data-role="User"` i klasę `bubble--user`, a Assistant klasę `bubble--assistant` (dymki wg roli; ToolCall i ToolResult pozostają zwijanymi `<details>`).
- [ ] **Step 2: Run** testy — Expected: FAIL.
- [ ] **Step 3: Implement:** lista rozmów jako `.page-card`; dymki: User po prawej w gradiencie akcentu z białym tekstem, Assistant po lewej na białym z cieniem, tool call/result stonowane (`#f4f5f7`); ostrzeżenie o liczbach pomarańczowe z ciemnym tekstem; treść nadal tylko jako tekst (bez `innerHTML`); dialogi: zaokrąglenie 12 px, cień, tytuł z lewą kreską akcentu, bez zmian w polach i przyciskach.
- [ ] **Step 4: Run** `cd web && npm run verify` — Expected: zielono.
- [ ] **Step 5: Commit** `git commit -m "Zmień wygląd czatu agenta i dialogów"`.

---

### Task 6: Dokumentacja, instrukcja testu ręcznego, uruchomienie

**Files:**
- Create: `docs/testy-reczne/plan-7d.md`
- Modify: `web/README.md`, `docs/arc42/05-*.md` (sekcja o układzie i stylach), `docs/arc42/09-*.md` (ADR-32), `docs/arc42/README.md` (status planu 7d)

**Interfaces:** Consumes: wyniki Task 1–5. Produces: dokumentacja.

- [ ] **Step 1: Write** `docs/testy-reczne/plan-7d.md`: tunel SSH i adres (`ssh -N -L 5080:127.0.0.1:5080 azureuser@tepecompute`, http://localhost:5080), lista ekranów do obejrzenia per rola (konta z `docs/arc42/07-widok-wdrozenia.md` sekcja 7.2), w tym: menu i tytuł w pasku, kafelki pulpitu, chipy statusów, czat (dymki), dialogi, widok wąski (okno < 800 px), fokus klawiaturą (Tab) na menu i w nagłówkach kart; uwaga, że ocena wizualna należy do właściciela.
- [ ] **Step 2: Update** `web/README.md` (struktura `styles/`, klasy `page-card*`, `data-table`, `app-status-chip` i jak dodać kafelek lub chip), arc42 sekcja 5 oraz ADR-32 „Motyw: Material 3 z nadpisaniem tokenów i wspólne klasy stylów” (decyzja, alternatywy z projektu: własny CSS, kupiony szablon; konsekwencje), wiersz w tabeli ADR i status w README.
- [ ] **Step 3: Verify** `cd web && npm run verify`, `dotnet build Hr.sln` (0 ostrzeżeń); przy zielonych restart: `tools/dev/run.sh` (kończy się smoke, kroki 1–15 zielone). Backend i `dotnet test` nie są ruszane, więc ich bieg nie jest wymagany.
- [ ] **Step 4: Commit** `git add docs web/README.md && git commit -m "Dodaj dokumentację i instrukcję testu ręcznego planu 7d"`.
