# Plan 3: serwisy aplikacyjne `Hr.Core` — urlopy, czas pracy, zmiany płacowe, naliczenia — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zbudować w `Hr.Core` serwisy aplikacyjne realizujące przypadki użycia UC-05 do UC-20 (saldo i wnioski urlopowe, ewidencja i analiza czasu pracy, zmiany płacowe z dwuosobowym zatwierdzaniem, miesięczne naliczenia), które egzekwują role i zakres danych, a wszystkie liczby biorą wyłącznie z silnika `Hr.Rules`.

**Architecture:** Cienkie serwisy (`LeaveService`, `TimesheetService`, `PayrollChangeService`, `PayrollService`) przyjmują `HrDbContext`, `ICurrentUser`, `IClock` i `WorkCalendar`. Zwracają `ServiceResult<T>` z kodem błędu mapowalnym później na HTTP (NotFound, Forbidden, Validation, Conflict). Reguły i liczby pochodzą z `Hr.Rules`, zakres danych z `DataScope`, a zapisy audytuje istniejący interceptor.

**Tech Stack:** .NET 9, EF Core 9 (SQL Server), xUnit, Testcontainers.MsSql (jak w planie 2).

**Spec:** `docs/arc42/README.md` (sekcje 3.5, 5.2, 6, 8.1, 9: ADR-08, 15, 17, 18, 22). Poprzednie plany (zmergowane): plan 1 (silnik), plan 2 (warstwa danych). Kolejne: Api + OpenAPI (plan 4), Agent (plan 5), Angular (plan 6), generator danych demo, import Kodeksu pracy, Docker i E2E (plan 7).

## Global Constraints

- `Hr.Core` nie liczy godzin, nadgodzin, wymiaru czasu pracy, urlopu ani kwot samodzielnie: wywołuje `Hr.Rules` (`LeaveRequestRules`, `LeaveEntitlement`, `WorkingTimeNorm`, `OvertimeCalculator`, `RestRules`, `WeeklyAverageRules`, `OvertimeLimit`, `PayrollCalculator`).
- Błędy: obiekt niewidoczny dla użytkownika (poza `DataScope`) lub nieistniejący daje `NotFound` (bez ujawniania istnienia); obiekt widoczny, ale operacja niedozwolona dla roli, daje `Forbidden`; naruszenie reguły biznesowej daje `Validation` (z `RuleId`, gdy jest); konflikt stanu lub nakładania daje `Conflict`.
- Czas wyłącznie z `IClock`; data „dziś” to `DateOnly.FromDateTime(clock.UtcNow)`.
- Każda operacja zapisu kończy się jednym `SaveChangesAsync`; audyt dopisuje interceptor.
- Wymiar urlopu liczony na rok kalendarzowy; wniosek urlopowy musi mieścić się w jednym roku kalendarzowym.
- Role (ADR-08, 15): zmianę płacową tworzy i wysyła `HR`, zatwierdza lub odrzuca `HRApprover` (nigdy autor), naliczenia uruchamia `HR` lub `HRApprover`.
- Wszystkie komunikaty błędów po polsku; komunikaty commitów po polsku, w formie rozkazującej, bez emoji.
- Testy integracyjne na SQL Server w kontenerze (fixture z planu 2); nazwy testów zaczynają się od `UCxx_`.

## Review Focus

1. **Samozatwierdzanie i zakres decydenta:** kierownik nie zatwierdza własnego wniosku ani wniosku spoza zespołu; zwykły pracownik nie zatwierdza niczyjego (Task 3).
2. **Saldo urlopu:** liczą się dni zatwierdzone i oczekujące, wniosek nakładający się na istniejący jest odrzucany, wniosek na sam weekend (0 dni) i przez granicę roku jest odrzucany, ponowna decyzja nie obniża salda drugi raz (Task 2, 3).
3. **Cztery oczu w serwisie:** autor nie zatwierdza własnej zmiany, `HR` nie zatwierdza, szkic (`Draft`) nie może być zatwierdzony bez wysłania (Task 6).
4. **Daty obowiązywania:** stawka z zatwierdzonej zmiany obowiązuje od `EffectiveFrom` włącznie, niezatwierdzona nie obowiązuje wcale (Task 6, 8).
5. **Naliczenie:** ponowne uruchomienie za ten sam miesiąc nie duplikuje wierszy, pracownik i kierownik nie uruchamiają naliczeń, pracownik widzi tylko własne (Task 8).

---

## Struktura plików

| Ścieżka | Odpowiedzialność |
|---|---|
| `src/Hr.Core/Services/ServiceResult.cs` | `ServiceResult<T>`, `ServiceError`, `ServiceErrorCode` |
| `src/Hr.Core/Services/LeaveService.cs` | Saldo, składanie, anulowanie, decyzje, lista wniosków |
| `src/Hr.Core/Services/TimesheetService.cs` | Wpisy czasu pracy, korekty, analiza (`TimesheetAnalysis`) |
| `src/Hr.Core/Services/PayrollChangeService.cs`, `SalaryResolver.cs` | Zmiany płacowe i wyznaczanie obowiązującej stawki |
| `src/Hr.Core/Services/PayrollService.cs` | Miesięczne naliczenia |
| `src/Hr.Core/Domain/PayrollResultRecord.cs`, migracja `AddPayrollResults` | Zapisane wyniki naliczeń |
| `src/Hr.Rules/Payroll/PayrollCalculator.cs` | Rozszerzenie o stały dodatek |
| `tests/Hr.Core.IntegrationTests/Services/` | Testy serwisów |

---

### Task 1: `ServiceResult` i saldo urlopowe (UC-05)

**Files:**
- Create: `src/Hr.Core/Services/ServiceResult.cs`, `src/Hr.Core/Services/LeaveService.cs`
- Modify: `tests/Hr.Core.IntegrationTests/TestOrg.cs` (parametr `withRequests`)
- Test: `tests/Hr.Core.IntegrationTests/Services/LeaveBalanceTests.cs`

**Interfaces:**
- Consumes: `HrDbContext`, `ICurrentUser`, `IClock`, `DataScope.VisibleEmployeeIds`, `Hr.Rules.Leave.LeaveEntitlement`, `Hr.Rules.Calendar.WorkCalendar`.
- Produces:
  - `public enum ServiceErrorCode { NotFound, Forbidden, Validation, Conflict }`
  - `public sealed record ServiceError(ServiceErrorCode Code, string Message, string? RuleId = null);`
  - `public sealed class ServiceResult<T> { public bool IsSuccess { get; } public T? Value { get; } public ServiceError? Error { get; } public static ServiceResult<T> Ok(T value); public static ServiceResult<T> Fail(ServiceErrorCode code, string message, string? ruleId = null); }`
  - `public sealed record LeaveBalance(int EntitlementDays, int ApprovedDays, int PendingDays, int RemainingDays);` gdzie `RemainingDays = EntitlementDays − ApprovedDays − PendingDays`.
  - `public sealed class LeaveService { public LeaveService(HrDbContext db, ICurrentUser user, IClock clock, WorkCalendar calendar); public Task<ServiceResult<LeaveBalance>> GetBalanceAsync(Guid employeeId, CancellationToken ct = default); }`
  - `TestOrg.SeedAsync(HrDbContext db, bool withRequests = true)` — przy `false` nie tworzy wniosków ani wpisów czasu (dotychczasowe testy bez zmian).

Liczone są wnioski o `From.Year` równym rokowi z `clock`, staż liczony na dzień z `clock`.

- [ ] **Step 1: Napisz testy, które mają się nie udać** (zegar `FakeClock` na 2026-10-16, `TestOrg.SeedAsync(db, withRequests: false)`).
  - `UC05_Balance_of_employee_hired_2019_is_20_days_all_remaining`: Anna dla siebie → `Entitlement 20, Approved 0, Pending 0, Remaining 20`.
  - `UC05_Balance_after_hire_date_change_gives_26_days`: Annie ustawiona data zatrudnienia 2014-05-01 → `Entitlement 26`.
  - `UC05_Balance_counts_approved_and_pending_of_current_year`: wnioski Anny: zatwierdzony 5 dni w 2026, oczekujący 3 dni w 2026, zatwierdzony 4 dni w 2025 → `Approved 5, Pending 3, Remaining 12`.
  - `UC05_Balance_of_other_team_is_not_found`: Tomasz pyta o saldo Anny → `NotFound`.
  - `UC05_Manager_can_read_team_member_balance`: Ewa pyta o saldo Anny → sukces.
  - `UC05_User_without_employee_gets_not_found`: `TestUser(userId, null, Roles.HR)` → `NotFound`.
- [ ] **Step 2: Uruchom — FAIL** (`dotnet test tests/Hr.Core.IntegrationTests --filter LeaveBalanceTests`), oczekiwany błąd kompilacji.
- [ ] **Step 3: Zaimplementuj `ServiceResult`, `LeaveService.GetBalanceAsync` i parametr `withRequests` w `TestOrg`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj ServiceResult i saldo urlopowe"`

---

### Task 2: Składanie wniosku urlopowego (UC-06, UC-07)

**Files:**
- Modify: `src/Hr.Core/Services/LeaveService.cs`
- Test: `tests/Hr.Core.IntegrationTests/Services/LeaveSubmitTests.cs`

**Interfaces:**
- Consumes: `LeaveService`, `ServiceResult<T>` (Task 1), `Hr.Rules.Leave.LeaveRequestRules.Evaluate`.
- Produces: `public Task<ServiceResult<LeaveRequest>> SubmitAsync(DateOnly from, DateOnly to, CancellationToken ct = default);` — wniosek dla siebie (`user.EmployeeId`), status `Pending`, `Days` z silnika, `CreatedAtUtc` z zegara. Błędy: brak `EmployeeId` lub roli → `Forbidden`; `to < from`, różne lata kalendarzowe lub 0 dni roboczych → `Validation`; saldo niewystarczające (z uwzględnieniem dni zatwierdzonych i oczekujących) → `Validation` z `RuleId = "KP-154-1"`; nakładanie na wniosek `Pending` lub `Approved` tego pracownika → `Conflict`.

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `UC06_Submit_creates_pending_request_with_working_days`: Anna, 12–16.10.2026 → `Days 5`, `Status Pending`, `EmployeeId` Anny, `CreatedAtUtc` równe zegarowi; zapisany w bazie.
  - `UC06_Submit_skips_holiday`: 9–13.11.2026 → `Days 4`.
  - `UC07_Submit_over_balance_is_validation_error_with_rule`: Anna ma zatwierdzone 18 dni, wniosek na 5 dni → `Validation`, `RuleId "KP-154-1"`, w bazie brak nowego wniosku.
  - `UC07_Pending_days_reduce_available_balance`: oczekujące 17 dni + wniosek na 5 dni → `Validation`.
  - `UC06_Submit_weekend_only_is_validation_error`: 17–18.10.2026 (sobota–niedziela) → `Validation`.
  - `UC06_Submit_across_years_is_validation_error`: 30.12.2026–5.01.2027 → `Validation`.
  - `UC06_Submit_reversed_dates_is_validation_error`.
  - `UC06_Submit_overlapping_existing_request_is_conflict`: istniejący `Pending` 12–16.10, nowy 14–20.10 → `Conflict`; wniosek `Cancelled` lub `Rejected` w tym terminie nie blokuje.
  - `UC06_Submit_without_employee_link_is_forbidden`: `TestUser(id, null, Roles.Employee)` → `Forbidden`.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `SubmitAsync`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj składanie wniosków urlopowych"`

---

### Task 3: Anulowanie, decyzje i lista wniosków (UC-08, UC-09)

**Files:**
- Modify: `src/Hr.Core/Services/LeaveService.cs`
- Test: `tests/Hr.Core.IntegrationTests/Services/LeaveDecisionTests.cs`

**Interfaces:**
- Consumes: `LeaveService` (Task 1, 2), `DataScope.Visible`.
- Produces:
  - `Task<ServiceResult<LeaveRequest>> CancelAsync(Guid requestId, CancellationToken ct = default);` — tylko autor, tylko status `Pending` → `Cancelled`; cudzy lub niewidoczny → `NotFound`; status inny niż `Pending` → `Conflict`.
  - `Task<ServiceResult<LeaveRequest>> DecideAsync(Guid requestId, bool approve, string? reason, CancellationToken ct = default);` — decydent to `Manager` zespołu pracownika albo `HR`/`HRApprover`; własny wniosek → `Forbidden`; rola `Employee` → `Forbidden`; niewidoczny → `NotFound`; status inny niż `Pending` → `Conflict`; odrzucenie bez uzasadnienia → `Validation`; zatwierdzenie ponownie sprawdza saldo (`Validation` z `KP-154-1`); ustawia `DecidedByEmployeeId` i `DecisionReason`.
  - `Task<IReadOnlyList<LeaveRequest>> ListAsync(CancellationToken ct = default);` — widoczne wnioski, od najnowszych (`From` malejąco).

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `UC09_Author_cancels_pending_request`: Anna anuluje swój → `Cancelled`.
  - `UC09_Cannot_cancel_approved_request`: → `Conflict`.
  - `UC09_Other_employee_cannot_cancel`: Tomasz anuluje wniosek Anny → `NotFound`.
  - `UC08_Manager_approves_team_member_request_and_balance_drops`: Ewa zatwierdza wniosek Anny na 5 dni → `Approved`, `DecidedByEmployeeId == Ewa`, saldo Anny (`GetBalanceAsync`) `Approved 5`.
  - `UC08_Manager_rejects_with_reason`: odrzucenie z uzasadnieniem → `Rejected`, `DecisionReason` zapisane; bez uzasadnienia → `Validation`, status bez zmian.
  - `UC08_Manager_cannot_decide_other_team_request`: Ewa wobec wniosku Tomasza → `NotFound`.
  - `UC08_Manager_cannot_decide_own_request`: Ewa wobec własnego → `Forbidden`.
  - `UC08_Employee_cannot_decide`: Jan wobec wniosku Anny → `NotFound` (nie widzi); Anna wobec własnego → `Forbidden`.
  - `UC08_HR_can_decide_any_request_but_not_own`: Piotr zatwierdza wniosek Tomasza, a własny → `Forbidden`.
  - `UC08_Decision_on_already_decided_request_is_conflict_and_does_not_change_balance`: drugie zatwierdzenie → `Conflict`, saldo bez zmiany.
  - `UC08_Approve_rechecks_balance`: między złożeniem a decyzją zatwierdzony zostaje inny wniosek zużywający saldo → zatwierdzenie `Validation` z `KP-154-1`.
  - `UC03_List_returns_only_visible_requests_newest_first`: Anna widzi swoje, Ewa zespołu, HR wszystkie, w kolejności `From` malejąco.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `CancelAsync`, `DecideAsync`, `ListAsync`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj anulowanie, decyzje i listę wniosków urlopowych"`

---

### Task 4: Wpisy czasu pracy i korekty (UC-10, UC-11)

**Files:**
- Create: `src/Hr.Core/Services/TimesheetService.cs`
- Test: `tests/Hr.Core.IntegrationTests/Services/TimesheetEntryTests.cs`

**Interfaces:**
- Consumes: `HrDbContext`, `ICurrentUser`, `IClock`, `WorkCalendar`, `Hr.Rules.Time.WorkEntry`, `DataScope.Visible`.
- Produces: `public sealed class TimesheetService { public TimesheetService(HrDbContext db, ICurrentUser user, IClock clock, WorkCalendar calendar); }` z metodami:
  - `Task<ServiceResult<WorkEntryRecord>> AddEntryAsync(DateTime start, DateTime end, TimeSpan breakTime, CancellationToken ct = default);` — wpis dla siebie; niepoprawny przedział lub przerwa (konstruktor `WorkEntry`) → `Validation`; koniec w przyszłości względem zegara → `Validation`; nakładanie na istniejący wpis → `Conflict`; brak `EmployeeId` → `Forbidden`.
  - `Task<ServiceResult<WorkEntryRecord>> CorrectEntryAsync(Guid entryId, DateTime start, DateTime end, TimeSpan breakTime, string reason, CancellationToken ct = default);` — autor lub `HR`/`HRApprover`; `Manager` i inni → `Forbidden` (gdy wpis widoczny) albo `NotFound`; brak uzasadnienia → `Validation`; reszta walidacji jak wyżej (nakładanie z wyłączeniem korygowanego wpisu).
  - `Task<IReadOnlyList<WorkEntryRecord>> ListAsync(DateOnly from, DateOnly toInclusive, CancellationToken ct = default);` — widoczne wpisy o dacie rozpoczęcia w przedziale, rosnąco po `Start`.

- [ ] **Step 1: Napisz testy, które mają się nie udać** (zegar na 2026-10-16 12:00 UTC, `TestOrg` bez wniosków i wpisów).
  - `UC10_Add_entry_for_self_is_stored`: Anna 12.10 08:00–17:00 przerwa 1 h → zapisany z `EmployeeId` Anny, `ToRulesEntry().WorkedHours == 8m`.
  - `UC10_Add_entry_with_end_before_start_is_validation_error`.
  - `UC10_Add_entry_with_break_longer_than_entry_is_validation_error`.
  - `UC10_Add_entry_ending_in_the_future_is_validation_error`: koniec 17.10.
  - `UC10_Overlapping_entry_is_conflict`: istniejący 08–16, nowy 12–20 tego samego pracownika → `Conflict`; stykający (16:00–20:00) dozwolony; cudzy wpis w tym czasie nie blokuje.
  - `UC10_Add_without_employee_link_is_forbidden`.
  - `UC11_Author_corrects_entry_with_reason_and_audit_records_modification`: korekta końca z 16:00 na 17:00 z uzasadnieniem → zapisana, w `AuditLog` wpis `Modified` dla `WorkEntryRecord`.
  - `UC11_Correction_without_reason_is_validation_error`.
  - `UC11_HR_can_correct_any_entry`: Piotr koryguje wpis Anny → sukces.
  - `UC11_Manager_cannot_correct_team_members_entry`: Ewa koryguje wpis Anny → `Forbidden`.
  - `UC11_Other_employee_gets_not_found`: Tomasz koryguje wpis Anny → `NotFound`.
  - `UC11_Correction_overlapping_other_entry_is_conflict`.
  - `UC03_List_filters_by_range_and_scope`: Anna widzi tylko swoje w przedziale, Ewa zespołu, kolejność rosnąca.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `TimesheetService` (`AddEntryAsync`, `CorrectEntryAsync`, `ListAsync`).**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj wpisy czasu pracy i korekty"`

---

### Task 5: Analiza czasu pracy (UC-12, UC-13, UC-14, UC-15)

**Files:**
- Modify: `src/Hr.Core/Services/TimesheetService.cs`
- Test: `tests/Hr.Core.IntegrationTests/Services/TimesheetAnalysisTests.cs`

**Interfaces:**
- Consumes: `TimesheetService` (Task 4), `Hr.Rules` (`WorkingTimeNorm`, `OvertimeCalculator`, `NightWindow.Default`, `RestRules`, `WeeklyAverageRules`, `OvertimeLimit`).
- Produces:
  - `public sealed record TimesheetAnalysis(decimal NormHours, decimal WorkedHours, OvertimeResult Overtime, IReadOnlyList<RestViolation> RestViolations, AverageHoursResult WeeklyAverage, decimal OvertimeYearToDate, bool AnnualOvertimeLimitExceeded);`
  - `Task<ServiceResult<TimesheetAnalysis>> AnalyzeAsync(Guid employeeId, DateOnly from, DateOnly toInclusive, CancellationToken ct = default);`
  - `NormHours` z `WorkingTimeNorm.HoursForPeriod` pomniejszonego o godziny nieobecności (8 h × dni robocze zatwierdzonych wniosków urlopowych w przedziale); wpisy do reguł to wpisy pracownika o dacie rozpoczęcia w przedziale; `OvertimeYearToDate` to nadgodziny od 1 stycznia roku `toInclusive` do `toInclusive`, a `AnnualOvertimeLimitExceeded` z `OvertimeLimit.Exceeded`. Pracownik niewidoczny → `NotFound`; `toInclusive < from` → `Validation`; nakładające się wpisy w bazie (`ArgumentException` z silnika) → `Validation`.

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `UC12_Daily_rest_violation_is_reported`: wpisy Anny 12.10 08:00–22:00 i 13.10 08:00–16:00 → w `RestViolations` jedno `KP-132-1` z `RestHours 10`.
  - `UC13_Weekly_rest_violation_is_reported`: 7 dni pracy 08–16 (12–18.10) → `KP-133-1`.
  - `UC14_Average_over_48h_is_flagged`: 4 tygodnie po 5 × 10 h → `WeeklyAverage.ExceedsLimit == true`, `AverageWeeklyHours == 50`.
  - `UC10_Overtime_comes_from_engine`: wpis 08:00–18:00 w dzień roboczy → `Overtime.Hours50 == 2`, `WorkedHours == 10`.
  - `UC14_Norm_hours_for_october_2026_is_176`: bez nieobecności `NormHours == 176`; przy zatwierdzonym urlopie 12–16.10 (5 dni) `NormHours == 136`.
  - `UC14_Annual_overtime_limit_flag`: wpisy dające ponad 150 h nadgodzin od 1.01 do końca przedziału → `AnnualOvertimeLimitExceeded == true`, a przy 150 h dokładnie `false`.
  - `UC15_Manager_analyzes_team_member_but_not_other_team`: Ewa dla Anny → sukces, dla Tomasza → `NotFound`.
  - `UC15_Employee_analyzes_only_self`: Anna dla Jana → `NotFound`.
  - `UC15_Reversed_range_is_validation_error`.
  - `UC15_Overlapping_entries_in_database_give_validation_error`: dwa nakładające się wpisy dodane bezpośrednio do bazy → `Validation`, bez wyjątku.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `AnalyzeAsync`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj analizę czasu pracy opartą na silniku reguł"`

---

### Task 6: Zmiany płacowe z dwuosobowym zatwierdzaniem (UC-18, UC-19, UC-20)

**Files:**
- Create: `src/Hr.Core/Services/PayrollChangeService.cs`, `src/Hr.Core/Services/SalaryResolver.cs`
- Test: `tests/Hr.Core.IntegrationTests/Services/PayrollChangeTests.cs`

**Interfaces:**
- Consumes: `PayrollChange`, `PayrollComponent`, `PayrollChangeStatus` (plan 2), `DataScope`.
- Produces:
  - `public sealed class PayrollChangeService { public PayrollChangeService(HrDbContext db, ICurrentUser user, IClock clock); }`
    - `Task<ServiceResult<PayrollChange>> CreateAsync(Guid employeeId, PayrollComponent component, decimal value, DateOnly effectiveFrom, string reason, CancellationToken ct = default);` — tylko `HR` (inne role `Forbidden`); pracownik nieistniejący `NotFound`; `value < 0` lub brak uzasadnienia `Validation`; wynik w statusie `Draft`, `CreatedByUserId = user.UserId` (brak `UserId` → `Forbidden`).
    - `Task<ServiceResult<PayrollChange>> SubmitAsync(Guid changeId, CancellationToken ct = default);` — tylko autor w roli `HR`, tylko `Draft` → `Pending`; inny status `Conflict`; cudza zmiana `Forbidden`.
    - `Task<ServiceResult<PayrollChange>> ApproveAsync(Guid changeId, CancellationToken ct = default);` oraz `RejectAsync(Guid changeId, string reason, CancellationToken ct = default);` — tylko rola `HRApprover` (`HR` i inni `Forbidden`), tylko `Pending` (`Draft` i zdecydowane → `Conflict`), autor zmiany → `Forbidden`; odrzucenie bez uzasadnienia `Validation`; ustawia `DecidedByUserId`, `DecisionReason`.
    - `Task<IReadOnlyList<PayrollChange>> ListAsync(Guid? employeeId = null, CancellationToken ct = default);` — widoczne wg `DataScope`, od najnowszych po `CreatedAtUtc`.
  - `public static class SalaryResolver { public static Task<decimal> ResolveAsync(HrDbContext db, Guid employeeId, PayrollComponent component, DateOnly date, CancellationToken ct = default); }` — wartość z najnowszej (po `EffectiveFrom`, potem `CreatedAtUtc`) zmiany `Approved` o `EffectiveFrom <= date`; bez takiej zmiany: dla `BaseSalary` `Employee.MonthlySalary`, dla `FixedAllowance` `0`. Bez kontroli zakresu (użycie wewnętrzne serwisów).

Konta w testach: `piotr` (`HR`), `maria` (`HRApprover`), identyfikatory użytkowników jako stałe `Guid`.

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `UC18_Full_flow_hr_creates_submits_approver_approves`: Piotr tworzy (`Draft`), wysyła (`Pending`), Maria zatwierdza (`Approved`, `DecidedByUserId == Maria`).
  - `UC18_Employee_and_manager_cannot_create`: Anna i Ewa → `Forbidden`.
  - `UC18_Create_validates_value_and_reason`: wartość `-1` i pusty powód → `Validation`.
  - `UC19_Author_cannot_approve_own_change`: użytkownik z rolami `HR` i `HRApprover` (ten sam `UserId`) zatwierdzający własną zmianę → `Forbidden`, a w bazie status nadal `Pending`.
  - `UC19_HR_role_cannot_approve`: Piotr → `Forbidden`.
  - `UC19_Draft_cannot_be_approved`: Maria wobec `Draft` → `Conflict`.
  - `UC19_Only_author_can_submit`: inny `HR` wysyłający cudzy `Draft` → `Forbidden`.
  - `UC20_Reject_requires_reason_and_stores_it`: Maria odrzuca z powodem → `Rejected`, `DecisionReason` zapisany; bez powodu → `Validation`.
  - `UC20_Decided_change_cannot_be_decided_again`: ponowne zatwierdzenie lub odrzucenie → `Conflict`.
  - `UC18_Resolver_ignores_unapproved_and_respects_effective_date`: stawka bazowa 8000; zmiana 8500 od 2026-11-01 w `Pending` → rozwiązanie na 2026-11-15 daje `8000`; po zatwierdzeniu 2026-10-31 daje `8000`, 2026-11-01 daje `8500`.
  - `UC18_Resolver_picks_latest_effective_change`: dwie zatwierdzone (8500 od 11.2026, 9000 od 12.2026) → 15.12.2026 daje `9000`, 15.11.2026 daje `8500`.
  - `UC18_Resolver_fixed_allowance_defaults_to_zero`.
  - `UC03_List_scope`: Anna widzi tylko własne zmiany, Ewa zespołu, `HR` i `HRApprover` wszystkie.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `PayrollChangeService` i `SalaryResolver`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj zmiany płacowe z zatwierdzaniem przez drugą osobę"`

---

### Task 7: Stały dodatek w silniku naliczeń

**Files:**
- Modify: `src/Hr.Rules/Payroll/PayrollCalculator.cs`
- Test: `tests/Hr.Rules.Tests/Payroll/PayrollCalculatorTests.cs`

**Interfaces:**
- Produces (zmiana publicznego API, jedyny odbiorca to Task 8):
  - `public sealed record PayrollInput(decimal MonthlyBaseSalary, decimal NormHours, decimal Overtime50Hours, decimal Overtime100Hours, decimal FixedAllowance = 0m);`
  - `PayrollResult` zyskuje pole `decimal FixedAllowance` (po `HourlyRate`, przed `NormalOvertimePay`); `Gross = BaseSalary + FixedAllowance + NormalOvertimePay + Allowance50 + Allowance100`.
  - Stały dodatek nie wchodzi do podstawy stawki godzinowej (uproszczenie opisane w ADR-24, Task 9); ujemny `FixedAllowance` → `ArgumentOutOfRangeException`.

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `UC16_Fixed_allowance_is_added_to_gross`: `PayrollInput(8000m, 176m, 2m, 1m, 300m)` → `FixedAllowance 300`, `Gross == 8527.26m` (8227.26 + 300).
  - `UC16_Fixed_allowance_does_not_change_hourly_rate`: stawka nadal `45.45m`.
  - `UC16_Negative_fixed_allowance_throws`.
  - Istniejące testy (bez piątego parametru) przechodzą bez zmian.
- [ ] **Step 2: Uruchom — FAIL** (`dotnet test tests/Hr.Rules.Tests --filter PayrollCalculatorTests`).
- [ ] **Step 3: Zaimplementuj rozszerzenie `PayrollInput`, `PayrollResult` i `PayrollCalculator.Calculate`** (równość rekordów uwzględnia nowe pole).
- [ ] **Step 4: Uruchom całość — PASS** (`dotnet test`).
- [ ] **Step 5: Commit** — `git commit -m "Dodaj stały dodatek do wstępnego naliczenia"`

---

### Task 8: Miesięczne naliczenia (UC-16, UC-17)

**Files:**
- Create: `src/Hr.Core/Domain/PayrollResultRecord.cs`, `src/Hr.Core/Persistence/Configurations/PayrollResultConfiguration.cs`, `src/Hr.Core/Services/PayrollService.cs`, migracja `AddPayrollResults`
- Modify: `src/Hr.Core/Persistence/HrDbContext.cs` (`DbSet<PayrollResultRecord> PayrollResults`)
- Test: `tests/Hr.Core.IntegrationTests/Services/PayrollServiceTests.cs`

**Interfaces:**
- Consumes: `SalaryResolver` (Task 6), `PayrollCalculator` z `FixedAllowance` (Task 7), `WorkingTimeNorm`, `OvertimeCalculator`, `WorkEntryRecord.ToRulesEntry()`, `IEmployeeOwned`, `IAuditable`.
- Produces:
  - `PayrollResultRecord : IEmployeeOwned, IAuditable { Guid Id; Guid EmployeeId; int Year; int Month; decimal BaseSalary; decimal FixedAllowance; decimal NormHours; decimal Overtime50Hours; decimal Overtime100Hours; decimal NormalOvertimePay; decimal Allowance50; decimal Allowance100; decimal Gross; string RuleIds; DateTime CalculatedAtUtc; Guid? CalculatedByUserId }` (kwoty i godziny `decimal(18,2)`, `RuleIds` rozdzielone przecinkami); unikalny indeks `(EmployeeId, Year, Month)`; ograniczenie CHECK `Month BETWEEN 1 AND 12`.
  - `public sealed class PayrollService { public PayrollService(HrDbContext db, ICurrentUser user, IClock clock, WorkCalendar calendar); Task<ServiceResult<IReadOnlyList<PayrollResultRecord>>> RunMonthAsync(int year, int month, CancellationToken ct = default); Task<ServiceResult<PayrollResultRecord>> GetAsync(Guid employeeId, int year, int month, CancellationToken ct = default); }`
  - Reguły `RunMonthAsync`: tylko `HR` lub `HRApprover` (inni `Forbidden`); `month` poza 1–12 → `Validation`; dla każdego pracownika: `NormHours` nominalny z `WorkingTimeNorm.HoursForPeriod` dla całego miesiąca (bez nieobecności, podstawa stawki godzinowej), nadgodziny z `OvertimeCalculator` dla wpisów o dacie rozpoczęcia w miesiącu, `BaseSalary` i `FixedAllowance` z `SalaryResolver` na ostatni dzień miesiąca, całość przez `PayrollCalculator`. Ponowne uruchomienie aktualizuje istniejący wiersz (bez duplikatu), ustawia `CalculatedAtUtc` i `CalculatedByUserId`. Nakładające się wpisy czasu u pracownika → `Validation` dla całego uruchomienia, bez zapisu częściowego.
  - `GetAsync`: pracownik niewidoczny → `NotFound`; brak naliczenia za miesiąc → `NotFound`.

- [ ] **Step 1: Napisz testy, które mają się nie udać** (`TestOrg` bez wniosków; Anna pensja 8000 zł).
  - `UC16_Run_creates_one_row_per_employee`: `RunMonthAsync(2026, 10)` jako Piotr → 7 wierszy (liczba pracowników `TestOrg`), `NormHours 176` dla każdego.
  - `UC16_Run_uses_engine_for_overtime_and_gross`: Anna ma wpis 12.10 08:00–18:00 → `Overtime50Hours 2`, `NormalOvertimePay 90.91`, `Allowance50 45.45`, `Gross` równe wynikowi `PayrollCalculator.Calculate(new PayrollInput(8000m, 176m, 2m, 0m))`.
  - `UC16_Run_applies_approved_salary_change_from_effective_date`: zatwierdzona zmiana 8500 od 2026-11-01 → naliczenie za 10.2026 używa `8000`, za 11.2026 `8500`.
  - `UC16_Run_adds_approved_fixed_allowance`: zatwierdzony stały dodatek 300 od 1.10.2026 → `FixedAllowance 300`, `Gross` powiększony o 300.
  - `UC16_Rerun_updates_instead_of_duplicating`: dwa uruchomienia → nadal 7 wierszy, zmieniony wpis czasu zmienia wynik drugiego.
  - `UC16_Run_is_forbidden_for_employee_and_manager`: Anna i Ewa → `Forbidden`.
  - `UC16_Invalid_month_is_validation_error`: miesiąc 13.
  - `UC16_Overlapping_entries_fail_whole_run_without_partial_save`: dwa nakładające się wpisy dodane bezpośrednio do bazy u Jana → `Validation`, a `COUNT` wierszy naliczeń `0`.
  - `UC17_Employee_reads_own_result_only`: Anna czyta własne, Jana → `NotFound`; Ewa czyta naliczenie Anny; `HR` wszystkie; brak naliczenia → `NotFound`.
  - `PayrollResult_unique_per_employee_month`: wstawienie drugiego wiersza dla tego samego `(EmployeeId, Year, Month)` bezpośrednio → `DbUpdateException`.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj encję, konfigurację, migrację `AddPayrollResults` i `PayrollService`.**
- [ ] **Step 4: Uruchom — PASS** (`dotnet test`).
- [ ] **Step 5: Commit** — `git commit -m "Dodaj miesięczne naliczenia wynagrodzeń"`

---

### Task 9: Dokumentacja i weryfikacja

**Files:**
- Modify: `docs/arc42/09-decyzje-architektoniczne.md` (ADR-24 „Serwisy aplikacyjne i reguły ich działania”)
- Modify: `docs/arc42/05-widok-blokow.md` (sekcja 5.2: serwisy w `Hr.Core`, `ServiceResult`; sekcja 5.5: `PayrollResultRecord` w diagramie ER)
- Modify: `docs/arc42/06-widok-czasu-wykonania.md` (6.1: zgodność scenariusza wniosku urlopowego z `LeaveService`)
- Modify: `docs/arc42/10-jakosc.md` (tabela testów: testy serwisów)

**Interfaces:** brak nowych typów.

- [ ] **Step 1: Napisz ADR-24** z decyzjami: kody błędów serwisów i zasada „NotFound zamiast Forbidden dla niewidocznych”; role w przepływach (kto tworzy, wysyła, zatwierdza); wnioski w obrębie jednego roku, odrzucenie wniosku na 0 dni, liczenie dni oczekujących w saldzie; wpisy czasu nie mogą kończyć się w przyszłości; stały dodatek poza podstawą stawki godzinowej i stawka godzinowa z nominalnego wymiaru miesiąca; `FixedAllowance` w silniku; poza zakresem: wnioski przez granicę roku, anulowanie zatwierdzonych wniosków, korekty zamkniętych naliczeń.
- [ ] **Step 2: Zaktualizuj pozostałe pliki** zgodnie z listą.
- [ ] **Step 3: Uruchom pełną weryfikację**

Run: `dotnet build -warnaserror && dotnet test`
Expected: wszystkie testy PASS (silnik, architektura, integracyjne), 0 ostrzeżeń.

- [ ] **Step 4: Commit** — `git commit -m "Opisz serwisy aplikacyjne w dokumentacji"`
- [ ] **Step 5: Przygotuj PR.** Po końcowym przeglądzie gałęzi wypchnij `feature/plan3-services` i otwórz PR do `main` (`gh pr create`) z opisem: zakres, wynik testów, decyzje i odroczone drobiazgi.
