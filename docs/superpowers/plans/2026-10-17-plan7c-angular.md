# Plan 7c: Angular — czat z agentem, historia i odczyt rozmów przez kadry — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dodać ekran agenta (UC-21–25, 27, 30, 31): czat z historią rozmów po lewej, odczyt cudzych rozmów przez kadry, ostrzeżenie o liczbach spoza narzędzi, oraz uruchomić całość z prawdziwym modelem (klucz z `.env`) do ręcznych testów.

**Architecture:** Bez zmian w backendzie i w kontrakcie API. Jedna trasa `agent` (z opcjonalnym `:id`, obsługiwana przez jeden `UrlMatcher`, żeby komponent nie był tworzony na nowo po pierwszej odpowiedzi) ładuje komponent `Agent`, który składa dwa komponenty prezentacyjne: `ConversationList` i `ConversationThread`. Stan w signals, klient generowany, nazwiska autorów z `EmployeeDirectory` (ADR-30).

**Tech Stack:** Jak w 7a i 7b: Angular 22.2.3, Angular Material 22.2.3 (bez nowych zależności), `ng-openapi-gen` 1.1.0, Vitest + jsdom; skrypty bash.

**Spec:** `docs/superpowers/specs/2026-10-17-plan7c-angular-design.md` (zmergowany w #12) oraz arc42 (ADR-11, ADR-14, ADR-26, ADR-30, sekcja 8.3a, R-10).

## Global Constraints

- Obowiązują Global Constraints planów 7a i 7b (`docs/superpowers/plans/2026-10-17-plan7a-angular.md`, `...plan7b-angular.md`): dokładne wersje zależności bez nowych pakietów, standalone/signals/zoneless, nazwy plików bez sufiksu, zakaz `any`, interfejs po polsku (`pl-PL`), klient w `web/src/app/api/` generowany i niepoprawiany ręcznie, błędy `ProblemDetails` czytane przez `problemOf`/`messageOf`, brak stałych czekań w specach (wyłączone animacje `MATERIAL_ANIMATIONS` albo `vi.waitFor`), subskrypcje sprzątane przy zniszczeniu komponentu, **żadnego `innerHTML`** (treść rozmów, parametry i wyniki narzędzi tylko jako tekst), build produkcyjny bez ostrzeżeń, commity po polsku w trybie rozkazującym bez emoji.
- **Dodatkowo w 7c:** kontrakt API i `docs/openapi/hr-api.json` się nie zmieniają (`npm run api:check` czysty bez regeneracji); zmiany w `src/` i w `tests/` są **zabronione** (rozmowy demo już istnieją: `DemoConversations` tworzy rozmowę Anny o saldzie urlopu z wywołaniem narzędzia `get_leave_balance` oraz rozmowę Tomasza z odmową, więc nie ma nowego seedu ani `stop.sh --reset`). Zakres: `web/`, `tools/dev/`, `docs/`.
- Role wiadomości w `MessageDto.role` to dokładnie `User`, `Assistant`, `ToolCall`, `ToolResult`. Dla `ToolCall` `content` to identyfikator wywołania, a `toolName` i `toolParams` (JSON jako tekst, może być `null`) opisują narzędzie; dla `ToolResult` `content` to JSON wyniku, a `toolName` nazwa narzędzia.
- Limit pytania: 4000 znaków (stała `MAX_QUESTION_LENGTH` po stronie UI odpowiada `AgentRunner.MaxQuestionLength`). Zakres widoczności rozmów egzekwuje backend: `HR` i `HRApprover` widzą wszystkie rozmowy, pozostali tylko własne; odczyt cudzej zapisuje `ConversationRead` w audycie (UI niczego dodatkowo nie zapisuje).
- Własna rozmowa to taka, której `userId` równa się `SessionService.user().userId`; każda inna jest tylko do odczytu.
- Klucz Anthropic: wyłącznie z `.env` (ignorowany przez git), nigdy w repo, w logach skryptów ani w argumentach poleceń.

## Review Focus

1. **Pytanie wysłane, ale agent niedostępny (`503`) albo rozmowa zmieniona równolegle (`409`):** tekst pytania nie ginie (zostaje w polu), rozmowa jest przeładowana z serwera (backend zapisał pytanie), a kolejne wysłanie działa (Task 4).
2. **Pierwsza odpowiedź nowej rozmowy:** adres zmienia się na `/agent/:id` bez ponownego tworzenia komponentu i bez drugiego pobrania rozmowy; lista rozmów się odświeża; ostrzeżenie `ungroundedNumbers` nie znika (Task 1, 4).
3. **Cudza rozmowa otwarta przez kadry:** brak pola pytania, baner z autorem i informacją o audycie, brak możliwości wysłania; wejście przez URL w cudzą rozmowę przez rolę bez dostępu daje `404`, snackbar i powrót do `/agent` (Task 3).
4. **Treść z HTML i długie wartości** (`<script>`, bardzo długi JSON parametrów, wiele linii): pokazane jako obojętny tekst, dymki się zawijają, bez błędów renderowania (Task 2).
5. **Szybkie przełączanie rozmów i podwójne wysłanie** (Enter dwa razy, zmiana rozmowy w trakcie ładowania lub wysyłania): odpowiedź starego żądania nie nadpisuje bieżącej rozmowy, w trakcie wysyłania pole jest zablokowane i drugi POST nie wychodzi (Task 3, 4).

---

## Pliki

| Plik / katalog | Odpowiedzialność |
|---|---|
| `web/src/app/core/api-problem.ts`, `labels.ts`, `app.routes.ts`, `layout/shell.ts`, `routes.spec.ts` | `conversationId` w `ApiProblem`, etykieta roli wiadomości, trasa i menu. |
| `web/src/app/features/agent/conversation-list.ts` (`.html`, `.scss`) | Panel listy rozmów (prezentacyjny). |
| `web/src/app/features/agent/conversation-thread.ts` (`.html`, `.scss`) | Dymki, wywołania narzędzi, wskaźnik oczekiwania, ostrzeżenie (prezentacyjny). |
| `web/src/app/features/agent/agent.ts` (`.html`, `.scss`) | Ekran: ładowanie, tryb tylko do odczytu, pole pytania, wysyłanie, błędy. |
| `tools/dev/run.sh`, `tools/dev/smoke.sh` | Klucz z `.env`, kroki dymne. |
| `docs/testy-reczne/plan-7c.md`, `docs/arc42/*`, `web/README.md` | Dokumentacja i ADR-31. |

### Task 1: Podstawy: `conversationId` w błędzie, etykiety, trasa i menu

**Files:**
- Create: `web/src/app/features/agent/agent.ts` (placeholder: klasa `Agent`, standalone, nagłówek `Agent`)
- Modify: `web/src/app/core/api-problem.ts`, `web/src/app/core/labels.ts`, `web/src/app/app.routes.ts`, `web/src/app/core/layout/shell.ts`, `web/src/app/routes.spec.ts`, `web/src/app/core/api-problem.spec.ts`, `web/src/app/core/labels.spec.ts`, `web/src/app/core/layout/shell.spec.ts` (jeśli istnieje, w przeciwnym razie test menu w `routes.spec.ts`)

**Interfaces:**
- Produces:
  - `ApiProblem` dostaje opcjonalne `conversationId?: string`; `problemOf` czyta je z surowego ciała (`ProblemDetails.Extensions["conversationId"]`) tylko gdy jest niepustym tekstem.
  - `labels.ts`: `messageRoleLabel(role: string): string` (`User` `Ty`, `Assistant` `Agent`, `ToolCall` `Wywołanie narzędzia`, `ToolResult` `Wynik narzędzia`, nieznane bez zmian; lookup przez `Object.hasOwn` jak w pozostałych etykietach).
  - Trasa `agent` obsługiwana jednym `UrlMatcher`: pasuje do `/agent` oraz `/agent/<id>` (dokładnie jeden segment po `agent`, parametr pozycyjny `id`), nie pasuje do dłuższych ścieżek; `loadComponent` → `Agent`; bez dodatkowego guarda (dostępna dla każdej zalogowanej roli). Ten sam wpis trasy obsługuje oba adresy, więc nawigacja `/agent` → `/agent/<id>` **nie tworzy komponentu na nowo** (reuse trasy).
  - Menu: pozycja `{ label: 'Agent', path: '/agent', exact: false, approversOnly: false, hrOnly: false }` na końcu bloku pozycji dla wszystkich (po „Naliczenia”, przed pozycjami `approversOnly`); liczby pozycji: Pracownik 5, Kierownik 7, `HR` i `HRApprover` 10.

- [ ] **Step 1: Testy (RED).** `api-problem.spec.ts`: `problemOf` z ciałem `{title, detail, conversationId: 'c1'}` zwraca `conversationId: 'c1'`; bez pola albo z wartością nie-tekstową pole nie występuje. `labels.spec.ts`: etykiety czterech ról wiadomości, nieznana bez zmian, `constructor`/`toString` bez zmian (`Object.hasOwn`). `routes.spec.ts`: `/agent` i `/agent/abc` otwierają komponent `Agent` dla `Employee`, `Manager` i `HR` (brak przekierowania, brak komunikatu o braku dostępu), `/agent/a/b` przekierowuje na `/`; nawigacja z `/agent` na `/agent/abc` zachowuje **tę samą instancję** komponentu (porównanie referencji do komponentu przed i po). Test menu: Pracownik 5 pozycji, Kierownik 7, `HR` i `HRApprover` 10, a „Agent” jest wśród nich dla każdej roli.
- [ ] **Step 2: Uruchom** `cd web && npx ng test --watch=false` — FAIL (nowe testy).
- [ ] **Step 3: Zaimplementuj** wg Interfaces (matcher jako stała w `app.routes.ts`; placeholder `Agent` minimalny, bez logiki).
- [ ] **Step 4: Weryfikacja:** `cd web && npm run verify` zielony (w tym `api:check` bez zmian klienta, build bez ostrzeżeń).
- [ ] **Step 5: Commit** `Dodaj trasę i menu agenta oraz identyfikator rozmowy w błędzie API`.

### Task 2: Komponenty prezentacyjne: lista rozmów i widok wiadomości

**Files:**
- Create: `web/src/app/features/agent/conversation-list.ts`, `conversation-list.html`, `conversation-list.scss`, `conversation-list.spec.ts`, `conversation-thread.ts`, `conversation-thread.html`, `conversation-thread.scss`, `conversation-thread.spec.ts`

**Interfaces:**
- Consumes: `ConversationSummaryDto` (`id`, `userId`, `startedAtUtc`, `preview: string | null`, `messageCount`), `MessageDto` (`seq`, `role`, `content`, `toolName`, `toolParams`, `createdAtUtc`), `EmployeeDirectory.userLabel`, `formatInstantDateTime`, `messageRoleLabel`.
- Produces:
  - `ConversationList` (selektor `app-conversation-list`): wejścia `conversations: ConversationSummaryDto[]`, `selectedId: string | null`, `showAuthor: boolean`, `loading: boolean`, `error: string | null`; model `onlyMine: boolean` (dwukierunkowy, przełącznik „Tylko moje” widoczny tylko gdy `showAuthor`); wyjścia `select: string` (id wybranej rozmowy), `create: void` (przycisk „Nowa rozmowa”), `retry: void` (przycisk „Spróbuj ponownie” przy `error`). Wiersz (`data-conversation="<id>"`): podgląd (`preview`, dla `null` tekst `(bez pytania)`), data przez `formatInstantDateTime`, liczba wiadomości, a gdy `showAuthor` także autor (`userLabel(userId)`; własny to `Ty`). Wybrany wiersz oznaczony `aria-current="true"`. Lista jest sortowana tak, jak przyszła (backend sortuje malejąco po czasie). Stany: ładowanie, pusta lista (`Brak rozmów.`), błąd. Filtrowanie „Tylko moje” robi komponent (porównanie `userId` z `SessionService.user().userId`).
  - `ConversationThread` (selektor `app-conversation-thread`): wejścia `messages: MessageDto[]`, `pending: boolean` (wskaźnik „Agent pisze…” jako dymek na końcu), `ungroundedNumbers: string[]`, `loading: boolean`, `emptyHint: string` (tekst dla pustej rozmowy). Dymki `User` (po prawej) i `Assistant` (po lewej) z treścią jako tekst (`white-space: pre-wrap`, zawijanie długich słów); wiadomości `ToolCall` i `ToolResult` jako zwijane `<details>` z podsumowaniem `Wywołanie narzędzia: <toolName>` i `Wynik narzędzia: <toolName>` oraz zawartością jako `<pre>` (parametry `toolParams` albo `content`; `null` parametrów pokazany jako `{}`); wiersze narzędzi są domyślnie zwinięte. Niepusta `ungroundedNumbers` pokazuje pod ostatnim dymkiem `Assistant` ostrzeżenie `Liczby nie pochodzą z danych systemu: <lista po przecinku>.` (`role="alert"`). Dymki mają `data-message="<seq>"`, role w `data-role`.

- [ ] **Step 1: Testy (RED).** `ConversationList`: renderuje wiersze z podglądem, datą (`formatInstantDateTime`), liczbą wiadomości; `preview: null` → `(bez pytania)`; przy `showAuthor` pokazuje autora (`Ty` dla własnego, nazwisko dla cudzego, `—` dla nieznanego); bez `showAuthor` brak autora i brak przełącznika „Tylko moje”; „Tylko moje” ukrywa cudze rozmowy; kliknięcie wiersza emituje `select` z id, przycisk „Nowa rozmowa” emituje `create`; wybrany wiersz ma `aria-current`; stan pusty, ładowania i błędu z przyciskiem `retry`; `preview` z `<script>` pokazany jako tekst (brak elementu `script`). `ConversationThread`: kolejność i klasy dymków dla ról; `ToolCall`/`ToolResult` jako zwinięte `<details>` z nazwą narzędzia i `<pre>` z JSON-em (w tym `toolParams: null` → `{}`); wiadomość z `<script>alert(1)</script>` i bardzo długim słowem nie tworzy elementu `script` (tekst równy treści); `pending` dodaje wskaźnik „Agent pisze…”; `ungroundedNumbers: ['12', '3,5']` pokazuje ostrzeżenie z listą, pusta nie pokazuje nic; `emptyHint` widoczny tylko dla pustej listy.
- [ ] **Step 2: Uruchom** `cd web && npx ng test --watch=false` — FAIL.
- [ ] **Step 3: Zaimplementuj** komponenty wg Interfaces; stylizacja Material (`MatButtonModule`, `MatSlideToggleModule` dla „Tylko moje”, `MatProgressBarModule` lub tekstowy wskaźnik); kolory dymków z motywu, bez `innerHTML`.
- [ ] **Step 4: Weryfikacja:** `cd web && npm run verify` zielony.
- [ ] **Step 5: Commit** `Dodaj listę rozmów i widok wiadomości agenta`.

### Task 3: Ekran agenta: ładowanie, otwieranie rozmów, tryb tylko do odczytu

**Files:**
- Modify: `web/src/app/features/agent/agent.ts` (zastąp placeholder), `agent.html`, `agent.scss`
- Create: `web/src/app/features/agent/agent.spec.ts`

**Interfaces:**
- Consumes: `ConversationsService.conversationsList()`, `conversationsGet({ id })`; `ConversationList`, `ConversationThread`; `EmployeeDirectory` (`load`, `userLabel`, `nameOfUser`); `SessionService` (`user`, `hasRole`); `HR_ROLES`; `Notifier`; `Router`, `ActivatedRoute` (parametr trasy `id`); `problemOf`, `messageOf`.
- Produces: komponent `Agent` (selektor `app-agent`) z układem dwukolumnowym (lista po lewej, rozmowa po prawej; poniżej szerokości tabletu kolumny jedna pod drugą). Zachowanie:
  - Start: `directory.load()` (błąd jako snackbar) oraz `conversationsList()`; `showAuthor` = `hasRole(...HR_ROLES)`; `onlyMine` domyślnie `false`.
  - Parametr `id` trasy wybiera rozmowę: `/agent` pokazuje pustą rozmowę z `emptyHint` „Zadaj pytanie agentowi, np. „Ile mam dni urlopu?”.”; `/agent/<id>` pobiera `conversationsGet({ id })`. Zmiana parametru przeładowuje rozmowę, chyba że identyfikator jest już załadowany lokalnie (po własnej pierwszej odpowiedzi, Task 4). Odpowiedź starego żądania (starszy numer sekwencji) jest ignorowana.
  - Wybór wiersza listy nawiguje na `/agent/<id>`, „Nowa rozmowa” na `/agent`; `404` przy `conversationsGet`: snackbar `Nie znaleziono rozmowy.` i nawigacja na `/agent`; inny błąd: komunikat w obszarze rozmowy z przyciskiem „Spróbuj ponownie”.
  - Tryb tylko do odczytu: załadowana rozmowa, której `userId` ≠ `session.user().userId`, pokazuje baner `Czytasz rozmowę: <nazwisko autora>. Odczyt jest zapisany w audycie.` (`role="status"`, `data-readonly-banner`) i **nie renderuje** pola pytania ani przycisku wysyłania. Własna rozmowa i nowa rozmowa pokazują stałą informację `Kadry mogą czytać rozmowy z agentem.` (R-10, `data-privacy-note`) i miejsce na pole pytania (dodane w Task 4: tu tylko kontener `data-composer-slot`, bez pola).
  - Zaznaczenie listy odpowiada otwartej rozmowie (`selectedId`).

- [ ] **Step 1: Testy (RED)** w `agent.spec.ts` (wzorzec specy ekranów z 7b: `provideHttpClientTesting`, `MATERIAL_ANIMATIONS` wyłączone, `SessionService` jako stub z `user` i `hasRole`, `RouterTestingHarness` lub `ActivatedRoute` stub): `Employee` — `/api/conversations` wołane raz, `showAuthor` fałsz (brak autora i przełącznika), wybór wiersza nawiguje na `/agent/<id>` i pobiera `GET /api/conversations/<id>`, `emptyHint` na `/agent`; `HR` — lista pokazuje autorów, przełącznik „Tylko moje” ukrywa cudze; otwarcie cudzej rozmowy pokazuje baner z nazwiskiem autora i brak pola pytania/przycisku wysyłania (brak `textarea` i `[data-send]`); otwarcie własnej pokazuje `data-privacy-note`; `404` przy otwarciu: `Notifier.error` z komunikatem i nawigacja na `/agent`; błąd listy: przycisk „Spróbuj ponownie” ponawia `GET /api/conversations`; szybka zmiana rozmowy (dwa kolejne `id` przed odpowiedzią pierwszego): odpowiedź pierwszego ignorowana, widoczna treść drugiego; zmiana `id` na już załadowane lokalnie id nie wywołuje drugiego `GET`.
- [ ] **Step 2: Uruchom** `cd web && npx ng test --watch=false` — FAIL.
- [ ] **Step 3: Zaimplementuj** `Agent` wg Interfaces: sygnały `conversations`, `loadingList`, `listError`, `current` (`{ id, userId, messages } | null`), `loadingConversation`, `conversationError`, `onlyMine`; numer sekwencji żądań jak w `CorrectionDrafts`; parametr trasy z `ActivatedRoute.paramMap` (zamiana na sygnał, `takeUntilDestroyed`). Metoda chroniona `openLocal(id: string, messages: MessageDto[], userId: string)` ustawia `current` bez żądania (używana w Task 4).
- [ ] **Step 4: Weryfikacja:** `cd web && npm run verify` zielony.
- [ ] **Step 5: Commit** `Dodaj ekran agenta z historią rozmów i odczytem rozmów przez kadry`.

### Task 4: Pole pytania i wysyłanie, obsługa błędów

**Files:**
- Modify: `web/src/app/features/agent/agent.ts`, `agent.html`, `agent.scss`, `agent.spec.ts`

**Interfaces:**
- Consumes: `ConversationsService.conversationsAsk({ body: { conversationId, text } })` → `AskResponse` (`conversationId`, `text`, `messages`, `ungroundedNumbers`), `AskRequest.conversationId: string | null`; `ApiProblem.conversationId` (Task 1); wszystko z Task 3.
- Produces: w `Agent` pole pytania w `data-composer-slot` (dla nowej i własnej rozmowy; `textarea` z etykietą „Pytanie do agenta”, licznik `<długość>/4000`, przycisk „Wyślij” `data-send`):
  - `const MAX_QUESTION_LENGTH = 4000` (eksport z `agent.ts`); Enter wysyła, Shift+Enter wstawia nową linię; pusty lub złożony z samych białych znaków tekst nie wysyła żądania; tekst dłuższy niż limit blokuje wysyłanie (licznik z błędem) i nie wysyła.
  - Wysłanie: tekst z `trim()`; dymek `User` pojawia się natychmiast (lokalna wiadomość, `seq` większy o 1 od ostatniej, `createdAtUtc` z chwili wysłania w ISO), `pending` = prawda (wskaźnik „Agent pisze…”), pole i przycisk zablokowane, drugi POST nie wychodzi. Po sukcesie: wiadomości rozmowy to dotychczasowe plus `messages` z odpowiedzi (zastępują lokalny dymek `User`: serwer zwraca nowe wiadomości łącznie z pytaniem), `ungroundedNumbers` z odpowiedzi trafiają do widoku (zostają do następnego wysłania lub zmiany rozmowy), pole jest czyszczone i odblokowane. Pierwsza odpowiedź nowej rozmowy: `current` dostaje `conversationId` z odpowiedzi i `userId` zalogowanego, adres zmienia się na `/agent/<conversationId>` (`router.navigate`, `replaceUrl: true`) **bez** ponownego pobrania rozmowy (`openLocal`), a lista rozmów jest odświeżana.
  - Błędy (`problemOf`): `422` komunikat `messageOf` pod polem, tekst zostaje, dymek lokalny usunięty; `409` i `503`: komunikat (`messageOf`) pod polem, tekst zostaje w polu, dymek lokalny usunięty, a gdy problem ma `conversationId` (albo rozmowa jest już otwarta) rozmowa jest przeładowana z serwera (`GET /api/conversations/<id>`; dla nowej rozmowy z `503` adres zmienia się na `/agent/<conversationId>` po przeładowaniu); `404`: snackbar `Nie znaleziono rozmowy.`, nawigacja na `/agent`; `401` obsługuje interceptor (bez zmian); `status` 0 (brak sieci): komunikat pod polem, tekst zostaje. Po każdym błędzie pole jest odblokowane, a `pending` fałszywe.
  - Zmiana rozmowy (nawigacja, „Nowa rozmowa”) w trakcie wysyłania: odpowiedź (sukces lub błąd) starego żądania jest ignorowana (numer sekwencji), pole nowej rozmowy odblokowane.

- [ ] **Step 1: Testy (RED)** w `agent.spec.ts`: nowa rozmowa: wpisanie pytania i kliknięcie „Wyślij” wysyła `POST /api/conversations/messages` z `{ conversationId: null, text }` (z `trim()`), ma natychmiast dymek `User` i wskaźnik „Agent pisze…”, pole zablokowane, drugie kliknięcie i drugi Enter nie wysyłają drugiego POST; po odpowiedzi widoczne dymki z `messages`, brak duplikatu pytania, pole puste i odblokowane, adres to `/agent/<id>` z odpowiedzi, lista rozmów odświeżona (`GET /api/conversations`), **brak** `GET /api/conversations/<id>`; dopytanie w istniejącej rozmowie wysyła `conversationId`; Enter wysyła, Shift+Enter nie; pusty i biały tekst nie wysyłają; 4001 znaków blokuje wysyłanie, 4000 przechodzi; `ungroundedNumbers: ['12']` pokazuje ostrzeżenie po odpowiedzi i znika przy kolejnym wysłaniu; `422` — komunikat pod polem, tekst zostaje, dymek lokalny usunięty; `409` — komunikat, tekst zostaje, rozmowa przeładowana (`GET /api/conversations/<id>`); `503` z `conversationId` w ciele przy nowej rozmowie — komunikat, tekst zostaje, rozmowa pobrana z serwera i adres `/agent/<id>`, kolejne wysłanie wychodzi z `conversationId`; `404` — snackbar i `/agent`; `status` 0 — komunikat, tekst zostaje; zmiana rozmowy w trakcie wysyłania ignoruje spóźnioną odpowiedź.
- [ ] **Step 2: Uruchom** `cd web && npx ng test --watch=false` — FAIL.
- [ ] **Step 3: Zaimplementuj** wg Interfaces: formularz reaktywny (`FormControl<string>`) albo sygnał tekstu; `MatFormFieldModule`, `MatInputModule` z `cdkTextareaAutosize`; metoda `send()` z numerem sekwencji wspólnym z ładowaniem rozmowy (Task 3); flaga `sending` blokuje ponowne wejście w `send()`.
- [ ] **Step 4: Weryfikacja:** `cd web && npm run verify` zielony (sprawdź liczbę testów i brak ostrzeżeń).
- [ ] **Step 5: Commit** `Dodaj wysyłanie pytań do agenta z obsługą błędów i ostrzeżeniem o liczbach`.

### Task 5: Skrypty uruchomieniowe, dokumentacja i testy ręczne

**Files:**
- Modify: `tools/dev/run.sh`, `tools/dev/smoke.sh`, `web/README.md`, `docs/arc42/05-widok-blokow.md`, `docs/arc42/07-widok-wdrozenia.md`, `docs/arc42/09-decyzje-architektoniczne.md`, `docs/arc42/10-jakosc.md`, `docs/arc42/README.md`
- Create: `docs/testy-reczne/plan-7c.md`

**Interfaces:**
- Consumes: istniejące `request`, `login`, `step`, `ok`, `fail` w `smoke.sh`; zmienna `ENV_FILE` i `RUN_DIR` w `run.sh`; konta demo (sekcja 7.2 arc42): `anna.nowak`, `piotr.zielinski`, `tomasz.lis`.
- Produces:
  - `run.sh`: jeżeli `.env` zawiera wiersz `ANTHROPIC_API_KEY=<niepusta wartość>`, wartość jest eksportowana procesowi API (`export ANTHROPIC_API_KEY=...` w podpowłoce uruchamiającej `dotnet run`); wartość nigdy nie jest wypisywana (ani w `echo`, ani w `set -x`, ani w komunikatach błędów). Bez klucza skrypt wypisuje jedną linię: `Brak ANTHROPIC_API_KEY w .env: czat agenta zwróci komunikat o niedostępności (503); pozostałe ekrany działają.`; po uruchomieniu klucza wypisuje `Klucz Anthropic wczytany z .env (czat korzysta z prawdziwego modelu).` Uprawnienia `.env` (`chmod 600`) bez zmian.
  - `smoke.sh` (kroki 12–15, po istniejących; skrypt ma dalej kończyć się komunikatem sukcesu; żadnych pytań do modelu): (12) `GET /api/conversations` jako `piotr.zielinski` zwraca 200 i co najmniej dwie rozmowy (`tr -d ' \n'` i dopasowanie dwóch wystąpień `"messageCount"`); identyfikator rozmowy zawierającej treść `Ile dni urlopu mi zostało?` ustala się z `GET /api/conversations` (pole `preview` równe tej treści, pole `id` w tym samym obiekcie; użyj `python3 -c` albo `grep -o` na skompaktowanym JSON-ie); (13) `GET /api/conversations/<id>` jako `piotr.zielinski` zwraca 200 i zawiera `"role":"ToolCall"`; (14) `GET /api/audit?entityType=Conversation` jako `piotr.zielinski` zwraca 200 i zawiera `ConversationRead`; (15) po zalogowaniu `tomasz.lis` (trzeci plik ciasteczek, wzorzec jak dla Piotra) `GET /api/conversations/<id rozmowy Anny>` zwraca 404, a `GET /api/conversations` zawiera dokładnie jedną rozmowę.
  - `docs/testy-reczne/plan-7c.md` (format jak `plan-7b.md`: przygotowanie, uwagi o zakresie, tabela kroków `#`, UC, konto, kroki, oczekiwany wynik) obejmuje co najmniej: `anna.nowak` widzi w „Agent” własną rozmowę o saldzie z wywołaniem narzędzia (zwinięty wiersz po kliknięciu pokazuje parametry i wynik); nowe pytanie z kluczem Anthropic (np. „Ile mam dni urlopu?”) z wskaźnikiem „Agent pisze…” i odpowiedzią zgodną z liczbą z ekranu „Urlopy” (UC-21); pytanie o nadgodziny w bieżącym miesiącu (UC-22); pytanie o odpoczynek dobowy z przepisem (UC-23); dopytanie w tej samej rozmowie (UC-30); `tomasz.lis` pytający o urlop Anny dostaje odmowę (UC-24); pytanie spoza zakresu (UC-27); `piotr.zielinski` widzi rozmowy wszystkich z autorami, filtr „Tylko moje”, otwiera rozmowę Anny w trybie tylko do odczytu z banerem i w „Audyt” znajduje zdarzenie „Odczyt rozmowy” (UC-31); `ewa.wisniewska` (kierownik, bez dostępu do cudzych rozmów) widzi tylko własne rozmowy (pustą listę) i dostaje komunikat „Nie znaleziono rozmowy.” przy wejściu przez URL w rozmowę Anny; opis zachowania bez klucza (komunikat o niedostępności i brak utraty pytania); opis kroku dodania klucza do `.env` (`ANTHROPIC_API_KEY=...`, plik poza gitem) i ponownego `tools/dev/run.sh`. Kroki z prawdziwym modelem mają kryteria dotyczące zachowania ekranu, nie treści odpowiedzi.
  - Dokumentacja: ADR-31 (ekran agenta: jedna trasa z `UrlMatcher`, odpowiedź w całości bez strumieniowania, ostrzeżenie `ungroundedNumbers` w UI, tryb tylko do odczytu dla cudzych rozmów, brak zmian kontraktu); arc42 05 (komponenty `features/agent`), 07 (klucz `ANTHROPIC_API_KEY` w `.env`, komunikat `run.sh`, kroki dymne, brak potrzeby resetu bazy w 7c), 10 (tabela testów: wiersz UC-21–27, 30, 31 wskazuje spec ekranu `agent.spec.ts` i smoke), README (status: plan 7c zrealizowany; pozostałe Docker i E2E); `web/README.md` (opis funkcji `agent`).

- [ ] **Step 1: Testy skryptów (RED).** Rozszerz `smoke.sh` o kroki (12)–(15); uruchomienie na działającej aplikacji ze starym stanem: kroki przechodzą od razu (dane demo już istnieją), więc „czerwony” test to sprawdzenie, że skrypt kończy się niepowodzeniem, gdy dopasowanie jest błędne: tymczasowo uruchom z oczekiwanym `Ile dni urlopu mi zostało?` zastąpionym nieistniejącym tekstem i potwierdź komunikat błędu, potem przywróć prawidłowy tekst. Sprawdź też `bash -n tools/dev/run.sh tools/dev/smoke.sh`.
- [ ] **Step 2: Zaimplementuj** zmiany `run.sh` (z `grep '^ANTHROPIC_API_KEY=' "$ENV_FILE" | tail -n 1 | cut -d= -f2-`; pusta wartość traktowana jak brak klucza; wartość nigdy w argumentach polecenia, tylko eksport w podpowłoce), `smoke.sh` i dokumentację.
- [ ] **Step 3: Zbuduj i uruchom całość:** `tools/dev/stop.sh` (bez `--reset`; dane demo nie zmieniły się), `tools/dev/run.sh`; oczekiwane: komunikat o kluczu zgodny ze stanem `.env`, `Test dymny zakończony powodzeniem.` z krokami 1–15. Sprawdź `git status`: `.env` i `.run/` nieśledzone, brak zmian w `src/` i `tests/`.
- [ ] **Step 4: Weryfikacja pełna:** `cd web && npm run verify`; `dotnet build Hr.sln` 0 ostrzeżeń; `DOCKER_API_VERSION=1.43 dotnet test Hr.sln` bez regresji (zmiany tylko poza `src/` i `tests/`, więc wynik taki jak po 7b: 563 zielone, 1 pominięty; zapisz faktyczny).
- [ ] **Step 5: Commit** `Dodaj klucz Anthropic z .env, kroki dymne agenta i dokumentację planu 7c`.
