# Plan 7b: Angular — naliczenia, zmiany płacowe, szkice korekt i audyt — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dokończyć interfejs kadrowo-płacowy: naliczenia miesięczne (podgląd i uruchamianie), zmiany płacowe z zatwierdzaniem przez drugą osobę, szkice korekt ewidencji i dziennik audytu (UC-16 do UC-20, część kadrowa UC-26, UC-28), z nazwiskami autorów dzięki `userId` w `EmployeeDto`, oraz uruchomić całość do ręcznych testów.

**Architecture:** Dwie małe, addytywne zmiany w backendzie (`UserId` w katalogu pracowników, jeden szkic korekty w danych demo), potem cztery ekrany Angulara na szkielecie z planu 7a (standalone, signals, generowany klient, wspólne dialogi i helpery). Nowe trasy są chronione guardami ról, nazwiska pochodzą z `EmployeeDirectory`.

**Tech Stack:** Jak w 7a: Angular 22.2.3, Angular Material 22.2.3 (w tym `MatPaginator`, bez nowych zależności), `ng-openapi-gen` 1.1.0, Vitest + jsdom; .NET 9, EF Core, xUnit, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-17-plan7b-angular-design.md` (zmergowany w #10) oraz spec 7a i arc42 (ADR-15, ADR-24, ADR-25, ADR-27–29).

## Global Constraints

- Obowiązują wszystkie Global Constraints planu 7a (`docs/superpowers/plans/2026-10-17-plan7a-angular.md`): dokładne wersje zależności bez nowych pakietów, standalone/signals/zoneless, nazwy plików bez sufiksu, zakaz `any`, interfejs po polsku (`pl-PL`), klient w `web/src/app/api/` generowany i niepoprawiany ręcznie (`api:check`), błędy `ProblemDetails` czytane przez `problemOf`/`messageOf`, czas wpisów pracy lokalny bez strefy (`toLocalDateTime`, nigdy `toISOString()`), build bez ostrzeżeń (budżet już podniesiony), commity po polsku w trybie rozkazującym bez emoji.
- **Dodatkowo w 7b:** brak stałych czekań w specach (wyłączone animacje `MATERIAL_ANIMATIONS` albo `vi.waitFor`); dialogi z żądaniem ustawiają `disableClose` na czas wysyłania i zdejmują po błędzie; subskrypcje sprzątane przy zniszczeniu komponentu (`DestroyRef`); żadnego `innerHTML` (JSON z audytu tylko jako tekst); `toISOString()` wolno użyć wyłącznie dla chwil w filtrze audytu; kwoty przez `formatMoney` (`pl-PL`, PLN), godziny przez `formatHours`; widoczność akcji wg ról i autorstwa jak w serwisach (`PayrollChangeService`: tworzy i wysyła `HR` będący autorem, decyduje `HRApprover` niebędący autorem; `CorrectionDraftService` i `AuditService`: `HR` i `HRApprover`; `PayrollService.RunMonthAsync`: `HR` i `HRApprover`).
- Menu per rola: Pracownik 4 pozycje (Pulpit, Urlopy, Czas pracy, Naliczenia), Kierownik 6 (+ Zatwierdzanie urlopów, Przegląd zespołu), `HR` i `HRApprover` 9 (+ Zmiany płacowe, Szkice korekt, Dziennik audytu). Wejście po URL bez uprawnień: przekierowanie na `/` z komunikatem `Brak dostępu do tej strony.`.
- Backend: `EmployeeSummary` zachowuje kompatybilność z istniejącymi wywołaniami (nowy parametr na końcu z wartością domyślną `null`); testy backendu `DOCKER_API_VERSION=1.43 dotnet test ...`; kotwica demo 2026-10-16, ziarno 20261016; dane demo deterministyczne (`DemoIds`, bez `Guid.NewGuid()`/zegara).
- Zmiany w `src/` wyłącznie w Task 1 i 2 (oraz skrypty w Task 10).

## Review Focus

1. **Autor lub decydent bez konta albo spoza katalogu** (`userId` nieznany): w tabelach „—”, własny `userId` jako „Ty”, bez wyjątku (Task 3, 6, 8).
2. **Uruchomienie naliczenia kończy się `422`/`403`** (np. nieprawidłowa ewidencja pracownika) albo jest klikane dwa razy: trwały komunikat na stronie, przycisk wraca do stanu aktywnego, drugie kliknięcie w trakcie nic nie wysyła (Task 5).
3. **Wartość w nowej zmianie płacowej** z przecinkiem i kropką, pusta, ujemna, zbyt duża, tekst: walidacja przy polu, do API nie trafia `NaN` (Task 7).
4. **JSON „przed/po” w audycie** pusty, niepoprawny albo zawierający znaczniki HTML (`<script>`): pokazany jako obojętny tekst, bez błędu parsowania (Task 9).
5. **Decyzja podjęta równolegle gdzie indziej** (zmiana płacowa lub szkic nie jest już oczekujący, `409`): lista odświeża się z komunikatem, okno dialogowe nie zostaje zablokowane (Task 6, 8).

---

## Pliki

| Plik / katalog | Odpowiedzialność |
|---|---|
| `src/Hr.Core/Services/EmployeeDirectoryService.cs`, `src/Hr.Api/Dtos/LeaveDtos.cs` | `UserId` w `EmployeeSummary` i `EmployeeDto`. |
| `src/Hr.Core/Seeding/Demo/DemoCorrectionDrafts.cs`, `DemoDataSet.cs`, `DemoDataGenerator.cs`, `src/Hr.Core/Seeding/DemoSeeder.cs` | Szkic korekty w danych demo. |
| `web/src/app/core/` (`employee-directory.ts`, `format.ts`, `labels.ts`, `date-format.ts`, `roles.ts`, `layout/shell.ts`, `app.routes.ts`) | Wspólne dodatki, trasy, menu. |
| `web/src/app/features/payroll/` | `payroll.ts`, `payroll-card.ts`, `payroll-summary.ts`. |
| `web/src/app/features/payroll-changes/` | `payroll-changes.ts`, `new-change-dialog.ts`, `reject-dialog.ts`. |
| `web/src/app/features/correction-drafts/` | `correction-drafts.ts`, `draft-decision-dialog.ts`. |
| `web/src/app/features/audit/` | `audit.ts`. |
| `tools/dev/smoke.sh`, `docs/testy-reczne/plan-7b.md` | Skrypt dymny i lista testów ręcznych. |

### Task 1: `userId` w katalogu pracowników i odświeżony klient

**Files:**
- Modify: `src/Hr.Core/Services/EmployeeDirectoryService.cs`, `src/Hr.Api/Dtos/LeaveDtos.cs`, `docs/openapi/hr-api.json`, `web/src/app/api/**` (regenerowane)
- Test: `tests/Hr.Core.IntegrationTests/Services/EmployeeDirectoryTests.cs`, `tests/Hr.Api.IntegrationTests/EmployeesApiTests.cs` (nowy), `OpenApiTests.cs`

**Interfaces:**
- Produces: `sealed record EmployeeSummary(Guid Id, string FirstName, string LastName, Guid TeamId, string TeamName, DateOnly HireDate, Guid? UserId = null)`; `sealed record EmployeeDto(Guid Id, string FirstName, string LastName, Guid TeamId, string TeamName, DateOnly HireDate, Guid? UserId)`; w kliencie `EmployeeDto.userId?: string | null`. `EmployeeDirectoryService.ListAsync` wyznacza `UserId` podzapytaniem po `ApplicationUser.EmployeeId` (pierwsze konto pracownika albo `null`), zakres danych (`VisibleEmployeeIds`) bez zmian.

- [ ] **Step 1: Testy (RED).** `EmployeeDirectoryTests`: `Directory_returns_user_id_for_employee_with_account_and_null_without` (dwóch pracowników w zakresie, jeden z kontem; asercje na `UserId`) oraz `Directory_user_ids_respect_data_scope` (pracownik widzi tylko siebie z własnym `UserId`). `EmployeesApiTests` (fabryka `Accounts`, jak w innych testach API): `Employees_include_userId_for_hr_for_all_and_for_employee_only_self` — `piotr.zielinski` widzi 6 pozycji z `userId` równym identyfikatorom kont z bazy, `tomasz.lis` jedną. `OpenApiTests` pada na starej migawce.
- [ ] **Step 2: Uruchom** `DOCKER_API_VERSION=1.43 dotnet test tests/Hr.Core.IntegrationTests --filter "FullyQualifiedName~EmployeeDirectory"` i `... tests/Hr.Api.IntegrationTests --filter "FullyQualifiedName~EmployeesApi|FullyQualifiedName~OpenApi"` — FAIL.
- [ ] **Step 3: Zaimplementuj** rekordy i zapytanie wg Interfaces (parametr z wartością domyślną, żeby `AgentToolbox` i inne miejsca tworzące `EmployeeSummary` kompilowały się bez zmian; sprawdź `grep -rn "new EmployeeSummary\|EmployeeSummary(" src tests`). Zaktualizuj migawkę `docs/openapi/hr-api.json` w sposób przewidziany przez `OpenApiTests` (przeczytaj test), następnie `cd web && npm run api:gen`; skopiowane zmiany klienta mają dotyczyć tylko `employee-dto.ts` (i ewentualnie metadanych).
- [ ] **Step 4: Weryfikacja:** testy z Step 2 przechodzą; `cd web && npm run verify` zielony (w tym `api:check`); `dotnet build Hr.sln` bez ostrzeżeń; istniejące testy `AgentToolbox` przechodzą.
- [ ] **Step 5: Commit** `Dodaj userId do katalogu pracowników i odśwież klienta API`.

### Task 2: Szkic korekty w danych demo

**Files:**
- Create: `src/Hr.Core/Seeding/Demo/DemoCorrectionDrafts.cs`
- Modify: `DemoDataSet.cs` (`CorrectionDrafts`), `DemoDataGenerator.cs`, `src/Hr.Core/Seeding/DemoSeeder.cs`
- Test: `tests/Hr.Core.Tests/Seeding/DemoCorrectionDraftsTests.cs`, `DemoDataInvariantsTests.cs`, `tests/Hr.Core.IntegrationTests/DemoDataSeedTests.cs`, `tests/Hr.Api.IntegrationTests/DemoDataApiTests.cs`

**Interfaces:**
- Consumes: `DemoOptions`, `DemoPerson`, `WorkEntryRecord`, `DemoScenarios.AnnaDailyRestTuesday`, `DemoIds.For`, `PolishTime.ToUtc`.
- Produces: `static class DemoCorrectionDrafts { static IReadOnlyList<CorrectionDraft> Build(DemoOptions o, IReadOnlyList<DemoPerson> people, IReadOnlyList<WorkEntryRecord> entries) }` zwracające **jeden** szkic: `EmployeeId` Anny, `WorkEntryId` wpisu Anny z dnia `AnnaDailyRestTuesday(anchor)` (14:00–22:30), `ProposedStart` 12:00, `ProposedEnd` 20:30, `ProposedBreak` 30 min (czas lokalny `Unspecified`), `Reason` po polsku (naruszenie odpoczynku dobowego, art. 132 § 1, proponowane przesunięcie zmiany), `Status` `Pending`, `CreatedByUserId` = `UserId` Piotra, `ConversationId` `null`, `CreatedAtUtc` = kotwica minus 8 dni, 10:00 lokalnie przeliczone `PolishTime.ToUtc`, `Id` = `DemoIds.For(seed, "draft:anna:1")`; `DemoDataSet.CorrectionDrafts` (domyślnie pusta lista, dla profilu `Accounts` pusta); `DemoSeeder` zapisuje szkice w transakcji danych i loguje ich liczbę.

- [ ] **Step 1: Testy (RED).** `DemoCorrectionDraftsTests`: dokładnie jeden szkic z wartościami wyżej dla kotwicy 2026-10-16 (data wpisu 2026-09-29), `WorkEntryId` istnieje w `entries` i należy do Anny; deterministyczny identyfikator; pusty wynik gdy brak wpisu. `DemoDataInvariantsTests` (dla każdej kotwicy i ziarna z `Cases()`): po podmianie wpisu z szkicu na proponowany `RestRules.Check` dla Anny **nie zawiera `KP-132-1`** i wpisy nie nakładają się; `CreatedAtUtc` szkicu (jako data lokalna) ≤ kotwica; identyfikatory unikalne w całym zbiorze. `DemoDataSeedTests`: po seedzie dokładnie jeden `CorrectionDraft` `Pending`, ponowny seed nie zmienia liczby (rozszerz rekord zliczeń o szkice). `DemoDataApiTests`: `piotr.zielinski` w profilu `Full` widzi w `GET /api/correction-drafts` jeden szkic `Pending` dla pracownika Anny.
- [ ] **Step 2: Uruchom** `dotnet test tests/Hr.Core.Tests --filter "FullyQualifiedName~Demo"` oraz testy integracyjne seeda i `DemoDataApiTests` — FAIL.
- [ ] **Step 3: Zaimplementuj** wg Interfaces (kolejność w generatorze: po wpisach czasu; dla profilu `Accounts` bez szkiców). Jeśli proponowane godziny przy innych kotwicach nie usuwają naruszenia (np. inny wpis scenariusza), dostosuj proponowaną zmianę tak, by niezmiennik przechodził dla wszystkich `Cases()`, zachowując wartości dla kotwicy domyślnej.
- [ ] **Step 4: Weryfikacja:** `dotnet test tests/Hr.Core.Tests`, `tests/Hr.Architecture.Tests`, `DOCKER_API_VERSION=1.43 dotnet test tests/Hr.Core.IntegrationTests --filter "FullyQualifiedName~DemoSeeder|FullyQualifiedName~DemoDataSeed"` oraz `tests/Hr.Api.IntegrationTests --filter DemoDataApiTests` zielone; `dotnet build Hr.sln` bez ostrzeżeń.
- [ ] **Step 5: Commit** `Dodaj szkic korekty wpisu Anny do danych demo`.

### Task 3: Wspólne dodatki frontendu, trasy i menu

**Files:**
- Create: `web/src/app/core/format.ts`, `web/src/app/core/labels.ts`, placeholdery stron: `features/payroll/payroll.ts`, `features/payroll-changes/payroll-changes.ts`, `features/correction-drafts/correction-drafts.ts`, `features/audit/audit.ts` (klasy `Payroll`, `PayrollChanges`, `CorrectionDrafts`, `Audit`, standalone z tytułem strony)
- Modify: `core/employee-directory.ts`, `core/date-format.ts`, `core/roles.ts`, `core/layout/shell.ts`, `app.routes.ts` oraz specy obok

**Interfaces:**
- Produces:
  - `EmployeeDirectory`: `nameOfUser(userId: string | null | undefined): string` (nazwisko z `EmployeeDto.userId`, nieznany lub `null` → `—`), `userLabel(userId: string | null | undefined): string` (własny `SessionService.user().userId` → `Ty`, w pozostałych przypadkach `nameOfUser`), `userIdOf(employeeId: string): string | null`.
  - `roles.ts`: `const HR_ROLES: readonly Role[] = [Roles.HR, Roles.HRApprover]`.
  - `format.ts`: `formatMoney(n: number): string` (`Intl.NumberFormat('pl-PL', {style:'currency', currency:'PLN'})`; testy porównują po zamianie ` ` na spację, np. `1 234,56 zł`).
  - `date-format.ts`: `formatInstantDateTime(iso: string): string` (czas lokalny `dd.MM.yyyy HH:mm`, z datą i godziną z lokalnych komponentów chwili).
  - `labels.ts`: `payrollChangeStatusLabel(s: string)` (`Draft` `Szkic`, `Pending` `Oczekuje na zatwierdzenie`, `Approved` `Zatwierdzona`, `Rejected` `Odrzucona`), `payrollComponentLabel(c: string)` (`BaseSalary` `Pensja zasadnicza`, `FixedAllowance` `Dodatek stały`), `draftStatusLabel(s: string)` (`Pending` `Oczekuje`, `Approved` `Zatwierdzony`, `Rejected` `Odrzucony`), `auditEventLabel(e: string)` (`Added` `Dodano`, `Modified` `Zmieniono`, `Deleted` `Usunięto`, `ConversationRead` `Odczyt rozmowy`, nieznane bez zmian), `auditEntityLabel(t: string)` (`Employee` `Pracownik`, `Team` `Zespół`, `LeaveRequest` `Wniosek urlopowy`, `WorkEntryRecord` `Wpis czasu`, `PayrollChange` `Zmiana płacowa`, `PayrollResultRecord` `Naliczenie`, `CorrectionDraft` `Szkic korekty`, `Conversation` `Rozmowa`, nieznane bez zmian), `const AUDIT_ENTITY_TYPES: readonly string[]` (klucze powyższych typów w tej kolejności).
  - Trasy (wszystkie `loadComponent`): `naliczenia` → `Payroll` (bez dodatkowego guarda), `zmiany-platowe` → `PayrollChanges`, `szkice-korekt` → `CorrectionDrafts`, `audyt` → `Audit` (te trzy z `roleGuard(...HR_ROLES)`); pozycje menu z flagą `hrOnly` (nowa obok `approversOnly`).

- [ ] **Step 1: Testy (RED).** `employee-directory.spec`: `nameOfUser` zwraca nazwisko dla znanego `userId`, `—` dla `null`, nieznanego i `undefined`; `userLabel` zwraca `Ty` dla własnego `userId`; `userIdOf` zwraca `userId` pracownika albo `null`. `format.spec`: `formatMoney(1234.5)` → `1 234,50 zł`, `formatMoney(0)` → `0,00 zł`. `date-format.spec`: `formatInstantDateTime` dla chwili zbudowanej z lokalnych komponentów (np. `new Date(2026, 9, 1, 9, 5).toISOString()`) → `01.10.2026 09:05` (niezależnie od strefy). `labels.spec`: wszystkie mapowania i nieznane wartości bez zmian, `AUDIT_ENTITY_TYPES` zawiera 8 typów. `shell.spec`: `Employee` 4 pozycje, `Manager` 6, `HR` i `HRApprover` 9 (`it.each` po rolach, asercje na etykietach). `routes.spec`: `Employee` i `Manager` na `/zmiany-platowe`, `/szkice-korekt`, `/audyt` kończą na `/` z komunikatem (UC-04); `HR` i `HRApprover` zostają; `/naliczenia` dostępne dla `Employee`.
- [ ] **Step 2: Uruchom** `cd web && npx ng test --watch=false` — FAIL.
- [ ] **Step 3: Zaimplementuj** wg Interfaces; placeholdery stron minimalne (tytuł), zastąpią je Task 4–9.
- [ ] **Step 4: Uruchom** `npm run verify` — PASS.
- [ ] **Step 5: Commit** `Dodaj wspólne helpery, etykiety, trasy i menu dla ekranów płacowych`.

### Task 4: Naliczenia: karta naliczenia i wybór pracownika

**Files:**
- Modify: `web/src/app/features/payroll/payroll.ts` (+`.html`, `.scss`); Create: `web/src/app/features/payroll/payroll-card.ts` (+`.html`, `.scss`), specy

**Interfaces:**
- Consumes: `PayrollService.payrollGet({employeeId?, year, month})`, `PayrollResultDto` (`employeeId`, `year`, `month`, `baseSalary`, `fixedAllowance`, `normHours`, `overtime50Hours`, `overtime100Hours`, `normalOvertimePay`, `allowance50`, `allowance100`, `gross`, `ruleIds`, `calculatedAtUtc`), `MonthSwitcher`, `EmployeePicker`, `EmployeeDirectory`, `SessionService`, `formatMoney`, `formatHours`, `formatInstantDateTime`, `problemOf`, `HR_ROLES`.
- Produces: `PayrollCard` (selector `app-payroll-card`; `input.required<PayrollResultDto>() result`, `input.required<string>() name`) pokazująca składniki: pensja zasadnicza, dodatek stały, norma czasu (h), nadgodziny 50% i 100% (h), wynagrodzenie za nadgodziny, dodatek 50%, dodatek 100%, **brutto**, reguły, `Obliczono: <data godzina>`; `Payroll` (strona): `MonthSwitcher` (domyślnie bieżący miesiąc), dla `HR_ROLES` `EmployeePicker` (domyślnie siebie), pobiera naliczenie wybranego pracownika (własne bez `employeeId`); `404` → informacja `Brak naliczenia za ten miesiąc.` (nie błąd); inne błędy z przyciskiem `Spróbuj ponownie`; stała uwaga `Naliczenie ma charakter wstępny i demonstracyjny.`; chroni przed nieaktualną odpowiedzią przy szybkiej zmianie miesiąca lub pracownika.

- [ ] **Step 1: Testy (RED).** `payroll-card.spec`: renderuje wszystkie składniki z poprawnym formatem (`formatMoney`, `formatHours`), numery reguł i datę obliczenia. `payroll.spec` (`HttpTestingController`, zegar `Date` stały): `Employee` pobiera `/api/payroll/results?year=2026&month=10` **bez** `employeeId` i pokazuje kartę; `404` pokazuje `Brak naliczenia za ten miesiąc.` (bez snackbara i bez przycisku ponowienia); `500` pokazuje błąd z `Spróbuj ponownie` i ponowienie działa; `Employee` nie widzi pickera, `HR` widzi i wybór innego pracownika pobiera z `employeeId=<id>`; zmiana miesiąca na wrzesień pobiera `month=9`; stara odpowiedź po zmianie wyboru jest ignorowana; stała uwaga widoczna.
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj.** **Step 4: Uruchom** `npm run verify` — PASS.
- [ ] **Step 5: Commit** `Dodaj ekran naliczeń z kartą naliczenia i wyborem pracownika`.

### Task 5: Naliczenia dla kadr: zestawienie miesiąca i uruchomienie

**Files:**
- Create: `web/src/app/features/payroll/payroll-summary.ts` (+`.html`, `.scss`), spec; Modify: `payroll.ts`/`payroll.html`, `payroll.spec.ts`

**Interfaces:**
- Consumes: `PayrollService.payrollGet`, `payrollRun({body: {year, month}})` → `Array<PayrollResultDto>`, `ConfirmDialog`, `Notifier`, `EmployeeDirectory.employees()`, `HR_ROLES`, `formatMoney`, `formatHours`.
- Produces: `PayrollSummary` (selector `app-payroll-summary`; `input.required<Date>() month`, `input.required<readonly EmployeeDto[]>() employees`, `input<PayrollResultDto[] | null>() runResult`, `output<string>() selected`) — tabela: pracownik, zespół, brutto, nadgodziny razem (50% + 100%); wynik pobierany osobno dla każdej osoby z limitem 4 jednoczesnych żądań (kolejka `mergeMap` z limitem), `404` jako `—`, błąd pojedynczej osoby jako wiersz z `Spróbuj ponownie` (nie wpływa na pozostałe); gdy `runResult` się zmieni, wiersze są zastępowane wynikami z odpowiedzi; kliknięcie wiersza i Enter emitują `selected` (przycisk ponowienia zatrzymuje propagację klawisza); sprzątanie subskrypcji. `Payroll` dla `HR_ROLES` pokazuje `PayrollSummary` oraz przycisk **„Uruchom naliczenie”**: `ConfirmDialog` (dla miesiąca bieżącego lub późniejszego treść `Miesiąc jeszcze trwa. Naliczenie będzie wstępne i można je powtórzyć.`), `POST /api/payroll/runs`; sukces: `Notifier.info('Naliczono N pracowników.')`, `runResult` ustawione, karta wybranego pracownika odświeżona; błąd: trwały komunikat na stronie (`role="alert"`, `messageOf`), przycisk wyłączony w trakcie.

- [ ] **Step 1: Testy (RED).** `payroll-summary.spec`: dla 7 osób wykonuje 7 żądań z nie więcej niż 4 w toku (licznik w mocku, spec pada przy limicie 5); `404` daje `—`; błąd jednej osoby nie psuje pozostałych i ponowienie wysyła jedno żądanie; kliknięcie wiersza emituje `selected` z `employeeId`, Enter na przycisku ponowienia nie emituje; po zniszczeniu komponentu oczekujące żądania są anulowane i nowe nie powstają; `runResult` zastępuje wiersze. `payroll.spec` (rozszerzenie): `HR` widzi zestawienie i przycisk, `Employee` nie; uruchomienie dla bieżącego miesiąca pokazuje ostrzeżenie w oknie, odmowa nic nie wysyła, potwierdzenie wysyła `{year:2026, month:10}` i po sukcesie pokazuje `Naliczono 31 pracowników.`; `422` z `detail` `Nieprawidłowa ewidencja pracownika Jan Kowalski: ...` pokazuje ten tekst w `role="alert"`, przycisk znów aktywny; podwójne kliknięcie w trakcie wysyła jedno żądanie.
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj** (wzorzec kolejki z `features/team/team-overview.ts`; limit współbieżności 4). **Step 4: Uruchom** `npm run verify` — PASS.
- [ ] **Step 5: Commit** `Dodaj zestawienie miesiąca i uruchamianie naliczenia dla kadr`.

### Task 6: Zmiany płacowe: lista, wysyłanie, zatwierdzanie i odrzucanie

**Files:**
- Modify: `web/src/app/features/payroll-changes/payroll-changes.ts` (+`.html`, `.scss`); Create: `web/src/app/features/payroll-changes/reject-dialog.ts` (+`.html`), specy

**Interfaces:**
- Consumes: `PayrollChangesService.payrollChangesList({employeeId?})`, `payrollChangesSubmit({id})`, `payrollChangesApprove({id})`, `payrollChangesReject({id, body: {reason}})`, `PayrollChangeDto` (`id`, `employeeId`, `component`, `value`, `effectiveFrom`, `reason`, `status`, `createdByUserId`, `decidedByUserId`, `decisionReason`, `createdAtUtc`), `EmployeeDirectory` (`nameOf`, `userLabel`), `SessionService` (`user().userId`, `hasRole`), `ConfirmDialog`, `EmployeePicker`, labele z `labels.ts`, `formatMoney`, `formatDate`, `formatInstantDate`.
- Produces: `PayrollChanges` — tabela: pracownik, składnik, wartość, obowiązuje od, status, autor (`userLabel`), decydent (`userLabel` albo `—`), uzasadnienie i decyzja; filtry: status (Wszystkie/Szkic/Oczekuje/Zatwierdzona/Odrzucona) i pracownik (`EmployeePicker` z pozycją `Wszyscy`, czyli `selectedId` równe `null`; obecny `EmployeePicker` nie ma takiej pozycji, więc rozszerz go opcjonalnym `input<string | null>() allLabel` (domyślnie `null` = bez pozycji, zachowanie dotychczasowe) wraz ze specem, bez zmiany użycia w czasie pracy i naliczeniach); sortowanie od najnowszych (`createdAtUtc`); stan pusty `Brak zmian płacowych`. Akcje: `Wyślij do zatwierdzenia` (rola `HR`, status `Draft`, `createdByUserId` = własny; `ConfirmDialog`), `Zatwierdź` (rola `HRApprover`, status `Pending`, autor ≠ własny; `ConfirmDialog`), `Odrzuć` (jak `Zatwierdź`; `RejectDialog`). Gdy status `Pending` i `createdByUserId` = własny, zamiast przycisków decyzji tekst `Autor nie może zatwierdzić własnej zmiany.` `RejectDialog` (dane `{change}`) sam wykonuje żądanie, uzasadnienie wymagane po `trim` (`Podaj uzasadnienie odrzucenia.`), `disableClose` w trakcie, wynik `{decided: PayrollChangeDto}` albo `{failure: ApiProblem}`; `422` zostaje w oknie, `403`/`404`/`409` zamykają okno z `failure`, a strona wywołuje `Notifier.error`, odświeża listę (`409`) i nie zostawia wiersza w stanie zablokowanym. Błędy `submit`/`approve` obsługiwane tak samo (snackbar, `409` odświeża).
- Przykładowe dane w specach odpowiadają danym demo: Anna `BaseSalary` 7800 od 2026-11-01 `Pending` autor Piotr; Jan `FixedAllowance` 400 od 2026-06-01 `Approved`; Tomasz `FixedAllowance` 300 od 2026-09-01 `Rejected`.

- [ ] **Step 1: Testy (RED).** Lista: wiersze z polskimi statusami i składnikami, kwotami przez `formatMoney`, autorem jako `Ty` (własny `userId`), nazwiskiem (znany) i `—` (nieznany `userId`); filtr statusu i pracownika (zapytanie z `employeeId`); kolejność od najnowszych. Akcje: jako `HR` (autor) `Szkic` ma `Wyślij do zatwierdzenia` i po potwierdzeniu `POST /api/payroll-changes/{id}/submit`; jako `HRApprover` cudze `Pending` ma `Zatwierdź` i `Odrzuć`, własne `Pending` pokazuje `Autor nie może zatwierdzić własnej zmiany.` bez przycisków; jako `HR` (nie autor, rola `HR`) brak przycisków decyzji; zatwierdzenie po potwierdzeniu wysyła `.../approve`; odrzucenie bez uzasadnienia nic nie wysyła, z uzasadnieniem wysyła `{reason:'…'}`; `422` w oknie, `disableClose` true w trakcie i false po `422`; `409` zamyka okno, pokazuje snackbar i ponownie pobiera listę (Review Focus 5); podwójne kliknięcie wysyła jedno żądanie.
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj** (wzorce z `features/leave/decision-dialog.ts` i `leave-approvals.ts`). **Step 4: Uruchom** `npm run verify` — PASS.
- [ ] **Step 5: Commit** `Dodaj listę zmian płacowych z wysyłaniem, zatwierdzaniem i odrzucaniem`.

### Task 7: Zmiany płacowe: nowa zmiana

**Files:**
- Create: `web/src/app/features/payroll-changes/new-change-dialog.ts` (+`.html`, `.scss`), spec; Modify: `payroll-changes.ts`/`.html`, `payroll-changes.spec.ts`

**Interfaces:**
- Consumes: `PayrollChangesService.payrollChangesCreate({body: {employeeId, component, value, effectiveFrom, reason}})` → `PayrollChangeDto`, `EmployeeDirectory.employees()`, `toDateOnly`, `payrollComponentLabel`, `problemOf`, `messageOf`, `Notifier`.
- Produces: `NewChangeDialog` — pola: pracownik (wybór z katalogu), składnik (`Pensja zasadnicza` → `BaseSalary`, `Dodatek stały` → `FixedAllowance`), wartość w zł (tekst z przecinkiem lub kropką dziesiętną, parsowany do liczby; wymagana, ≥ 0, skończona, do 2 miejsc po przecinku, górny limit 1 000 000), obowiązuje od (`MatDatepicker`, `toDateOnly`), uzasadnienie (wymagane po `trim`); `Zapisz` wyłączony w trakcie i do poprawnego formularza, `disableClose` w trakcie; błędy `422`/`409` jako `messageOf` w oknie; sukces zamyka okno z `PayrollChangeDto`, strona odświeża listę i pokazuje `Notifier.info('Zmiana zapisana jako szkic.')`. Przycisk **„Nowa zmiana”** w `PayrollChanges` tylko dla roli `HR`.

- [ ] **Step 1: Testy (RED).** Wysłane ciało dla `4500,50` i `4500.5`: `{employeeId:'e1', component:'BaseSalary', value:4500.5, effectiveFrom:'2026-11-01', reason:'…'}`; składnik `FixedAllowance` mapuje się poprawnie; wartości `''`, `-1`, `abc`, `1e9`, `12,345` blokują zapis z komunikatem przy polu i nie wysyłają żądania (Review Focus 3); brak uzasadnienia blokuje; `422` z API w oknie, `disableClose` w trakcie, podwójne kliknięcie wysyła jedno żądanie; sukces zamyka z wynikiem; przycisk `Nowa zmiana` widoczny tylko dla `HR` (nie dla `HRApprover`).
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj.** **Step 4: Uruchom** `npm run verify` — PASS.
- [ ] **Step 5: Commit** `Dodaj tworzenie zmian płacowych jako szkiców`.

### Task 8: Szkice korekt

**Files:**
- Modify: `web/src/app/features/correction-drafts/correction-drafts.ts` (+`.html`, `.scss`); Create: `web/src/app/features/correction-drafts/draft-decision-dialog.ts` (+`.html`), specy

**Interfaces:**
- Consumes: `CorrectionDraftsService.correctionDraftsList({status?})`, `correctionDraftsDecide({id, body: {approve, note}})`, `CorrectionDraftDto` (`id`, `employeeId`, `workEntryId`, `proposedStart`, `proposedEnd`, `proposedBreakMinutes`, `reason`, `status`, `createdByUserId`, `decidedByUserId`, `createdAtUtc`, `decidedAtUtc`, `decisionNote`), `TimesheetService.timesheetList({from, to})`, `EmployeeDirectory`, `monthRange`, `localDay`, `formatTime`, `formatDate`, `draftStatusLabel`, `ConfirmDialog` (nie), `Notifier`.
- Produces: `CorrectionDrafts` — filtr statusu (domyślnie `Oczekujące` → `status=Pending`; `Wszystkie` bez parametru; `Zatwierdzone`, `Odrzucone`); kolumny: pracownik, data wpisu (z `proposedStart`), **obecny wpis** (godziny z `timesheetList` dla miesięcy `proposedStart` wszystkich szkiców, dopasowane po `workEntryId`, a przy braku `—`), **proponowane godziny** (start–koniec, przerwa w minutach, `+1 dzień` gdy koniec w kolejnym dniu), powód, utworzył (`userLabel`), status, notatka decyzji; stan pusty `Brak szkiców korekt`; akcje `Zatwierdź`/`Odrzuć` dla `Pending`. `DraftDecisionDialog` (dane `{draft, approve}`) sam wykonuje żądanie: notatka opcjonalna przy zatwierdzeniu, wymagana po `trim` przy odrzuceniu (`Podaj uzasadnienie odrzucenia.`), `disableClose` w trakcie; `422`/`409` zostają w oknie jako `messageOf`, `403`/`404` zamykają okno (`failure`, snackbar), sukces zamyka z wynikiem i lista się odświeża; `409` dodatkowo odświeża listę.

- [ ] **Step 1: Testy (RED).** Domyślne zapytanie `GET /api/correction-drafts?status=Pending`; zmiana filtru na `Wszystkie` pobiera bez parametru; dla szkicu Anny (`proposedStart` 2026-09-29T12:00:00, `proposedEnd` 20:30, przerwa 30) tabela pokazuje `12:00–20:30`, a obecny wpis `14:00–22:30` z `timesheetList` za wrzesień (`from=2026-09-01&to=2026-09-30`), przy braku wpisu `—`; jedno żądanie listy wpisów na miesiąc nawet dla wielu szkiców; zatwierdzenie wysyła `{approve:true, note:null}` (z notatką — przyciętą), odrzucenie bez notatki nic nie wysyła, z notatką wysyła `{approve:false, note:'…'}`; `422` (np. nakładanie wpisów) w oknie; `403`/`404` snackbar; `409` odświeża listę z komunikatem (Review Focus 5); `disableClose` w trakcie; podwójne kliknięcie wysyła jedno żądanie; autor jako `Ty`/nazwisko/`—`.
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj.** **Step 4: Uruchom** `npm run verify` — PASS.
- [ ] **Step 5: Commit** `Dodaj ekran szkiców korekt ewidencji z decyzją kadr`.

### Task 9: Dziennik audytu

**Files:**
- Modify: `web/src/app/features/audit/audit.ts` (+`.html`, `.scss`), spec

**Interfaces:**
- Consumes: `AuditService.auditQuery({entityType?, entityId?, actorUserId?, from?, to?, skip?, take?})` → `AuditPageDto {total, items: AuditEntryDto[]}` (`id`, `actorUserId`, `atUtc`, `eventType`, `entityType`, `entityId`, `beforeJson`, `afterJson`), `EmployeeDirectory` (`employees()`, `userLabel`), `auditEventLabel`, `auditEntityLabel`, `AUDIT_ENTITY_TYPES`, `formatInstantDateTime`, `problemOf`, `messageOf`, `MatPaginator`.
- Produces: `Audit` — formularz filtrów: typ encji (`Wszystkie` + `AUDIT_ENTITY_TYPES` z polskimi nazwami), identyfikator encji (GUID; niepoprawny blokuje wyszukiwanie komunikatem `Podaj poprawny identyfikator (GUID).`), użytkownik (osoby z katalogu mające `userId`, wartość `userId`), zakres dat (`MatDateRangePicker`; `from` = początek pierwszego dnia czasu lokalnego, `to` = **koniec** ostatniego dnia czasu lokalnego 23:59:59.999, bo `AuditService` porównuje `AtUtc <= to`; oba przez `toISOString()` — jedyne dozwolone użycie), przyciski `Szukaj` i `Wyczyść`; tabela: czas (`formatInstantDateTime`), użytkownik (`actorUserId` `null` → `System (seed)`, inaczej `userLabel`), zdarzenie (`auditEventLabel`), typ encji (`auditEntityLabel`), skrócony identyfikator (8 znaków `…`, pełny w `title`); `MatPaginator` (25/50/100, domyślnie 25, `skip = pageIndex * pageSize`, `take = pageSize`, `length = total`); rozwijany wiersz `Szczegóły` z kolumnami **Przed** i **Po**: JSON sformatowany `JSON.stringify(JSON.parse(x), null, 2)`, a gdy parsowanie się nie uda lub wartość to `null`, surowy tekst albo `—`; wyłącznie interpolacja tekstu w `<pre>`; stan pusty `Brak wpisów dla wybranych filtrów`; błąd ładowania z `Spróbuj ponownie`; ochrona przed nieaktualną odpowiedzią (anulowanie poprzedniego żądania).

- [ ] **Step 1: Testy (RED).** Pierwsze ładowanie: `GET /api/audit?skip=0&take=25`; filtry dają parametry `entityType=PayrollChange`, `entityId=<guid>`, `actorUserId=<userId>`, `from` i `to` jako chwile ISO (dla zakresu 2026-10-01–2026-10-02 w lokalnej strefie testu: `from` = lokalna północ 1.10, `to` = lokalne 23:59:59.999 2.10; asercja przez konstrukcję `new Date(2026, 9, 1)` i `new Date(2026, 9, 2, 23, 59, 59, 999)`, niezależna od strefy); niepoprawny GUID blokuje wysłanie i pokazuje komunikat; zmiana strony na drugą wysyła `skip=25`, zmiana rozmiaru strony na 50 resetuje do pierwszej; `Wyczyść` przywraca zapytanie bez filtrów; wiersze: czas, `System (seed)` dla `actorUserId` `null`, `Ty` dla własnego, nazwisko dla znanego, `—` dla nieznanego; etykiety zdarzeń i typów (nieznane bez zmian); skrócony identyfikator z pełnym w `title`; rozwinięcie pokazuje sformatowany JSON przed i po, `null` jako `—`, niepoprawny JSON jako surowy tekst bez błędu, a wartość z `<script>alert(1)</script>` jako obojętny tekst (brak elementu `script` w DOM, Review Focus 4); stan pusty; błąd z ponowieniem; nieaktualna odpowiedź ignorowana.
- [ ] **Step 2: Uruchom** testy — FAIL.
- [ ] **Step 3: Zaimplementuj.** **Step 4: Uruchom** `npm run verify` — PASS.
- [ ] **Step 5: Commit** `Dodaj dziennik audytu z filtrami, stronicowaniem i podglądem zmian`.

### Task 10: Skrypt dymny, dokumentacja i pełna weryfikacja

**Files:**
- Modify: `tools/dev/smoke.sh`, `docs/arc42/07-widok-wdrozenia.md` (7.3 szkic w danych demo, 7.4 ponowne zasianie po aktualizacji), `docs/arc42/09-decyzje-architektoniczne.md` (ADR-30 + wiersz w tabeli), `docs/arc42/README.md` (status), `web/README.md`; Create: `docs/testy-reczne/plan-7b.md`

**Interfaces:**
- Produces: `smoke.sh` z dodatkowymi krokami 7b (każdy `OK`/`BŁĄD`, wyjście ≠ 0 przy pierwszym błędzie, osobny plik ciasteczek dla drugiego użytkownika): jako `anna.nowak` `GET /api/payroll/results?year=2026&month=9` zwraca 200 i zawiera `"gross"`; jako `piotr.zielinski` `GET /api/employees` zawiera `"userId"` i `GET /api/correction-drafts` zwraca tablicę z `"status":"Pending"`. ADR-30: decyzja (`userId` w `EmployeeDto`, UI buduje mapę nazwisk), alternatywy (osobny endpoint `GET /api/users`, bez zmian), konsekwencje (zmiana kontraktu widoczna dla każdego, kto widzi daną osobę; klient odświeżony; zakres danych bez zmian). `docs/testy-reczne/plan-7b.md`: tabela kroków per rola dla UC-16–UC-20, UC-26 (część kadrowa) i UC-28 na danych demo (Anna: naliczenie wrześniowe, zmiana pensji do 7800 zł od 1.11 oczekuje na Marię; Jan: dodatek 400 zł od 1.06 — różnica między majem a czerwcem w naliczeniu; Tomasz: odrzucony dodatek; szkic Anny z 29.09 do zatwierdzenia przez kadry, po zatwierdzeniu wpis w „Czas pracy” ma godziny 12:00–20:30; uruchomienie naliczenia za październik jako `piotr.zielinski`; `piotr.zielinski` tworzy zmianę i wysyła ją, `maria.kaminska` zatwierdza i odrzuca z uzasadnieniem, Piotr nie widzi przycisków decyzji; audyt: odczyt zdarzeń, filtr po zmianie płacowej i po użytkowniku; pracownik i kierownik nie mają pozycji kadrowych w menu). Zakres dokumentu bez obietnic ekranów 7c (agent, rozmowy).

- [ ] **Step 1: RED:** po dopisaniu kroków 7b `tools/dev/smoke.sh` bez działającej aplikacji kończy się `BŁĄD`, kod wyjścia ≠ 0.
- [ ] **Step 2: Zaimplementuj** kroki, dokumenty i listę testów ręcznych wg Interfaces (przeczytaj istniejący `smoke.sh` i `docs/testy-reczne/plan-7a.md`; trzymaj ten sam styl). Sprawdź `bash -n tools/dev/smoke.sh` i `shellcheck` (jeśli jest).
- [ ] **Step 3: Uruchom `tools/dev/stop.sh --reset` i `tools/dev/run.sh`** (nowy seed ze szkicem), potwierdź, że `smoke.sh` przechodzi, w logu API brak wyjątków, a w bazie jest dokładnie jeden szkic (`sqlcmd`: `SELECT COUNT(*) FROM CorrectionDrafts` = 1) oraz 31 pracowników; następnie `tools/dev/stop.sh`. Zostaw aplikację **zatrzymaną** (uruchomienie końcowe do ręcznych testów wykonuje kontroler).
- [ ] **Step 4: Pełna weryfikacja:** `cd web && npm run verify`; `dotnet build Hr.sln`; `DOCKER_API_VERSION=1.43 dotnet test Hr.sln` (cały zestaw; uruchom w tle, trwa kilkanaście minut) — wszystko zielone, bez ostrzeżeń; brak procesów `dotnet`/`node` po zakończeniu.
- [ ] **Step 5: Commit** (można w dwóch: skrypt i dokumentacja) `Rozszerz test dymny i opisz plan 7b w dokumentacji`.
