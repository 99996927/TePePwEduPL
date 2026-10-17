# Plan 6: dane demo — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zastąpić seed 6 kont deterministycznym zestawem danych demo (ok. 30 pracowników, 6 miesięcy ewidencji, urlopy, zmiany płacowe, rozmowy, naliczenia) ze stałym zestawem scenariuszy dla kont demo (UC-29, ADR-19, ADR-20, ADR-21).

**Architecture:** Dwie warstwy w `Hr.Core`. Warstwa danych `Hr.Core.Seeding.Demo` jest czysta (bez `DbContext`, bez zegara, bez `Guid.NewGuid()`): `DemoDataGenerator.Build(DemoOptions)` zwraca `DemoDataSet` z encjami w pamięci. Cienki `DemoSeeder` (`Hr.Core.Seeding`) zapisuje wynik w jednej transakcji razem z kontami Identity, a potem woła `PayrollService.RunMonthAsync` jako HR dla pełnych miesięcy przed miesiącem kotwicy.

**Tech Stack:** .NET 9, EF Core 9, ASP.NET Core Identity, xUnit, NetArchTest, Testcontainers.MsSql.

**Spec:** `docs/superpowers/specs/2026-10-17-plan6-dane-demo-design.md` (zmergowany w #6). Odstępstwa od speca wynikają z kodu i są zapisane w Global Constraints (profil `Accounts`, zespół Kadry bez `Manager`, audyt seeda).

## Global Constraints

- `net9.0`, `Nullable` włączone, ostrzeżenia jako błędy. Komunikaty błędów i commitów po polsku, commity w formie rozkazującej, bez emoji. Testy integracyjne: `DOCKER_API_VERSION=1.43 dotnet test Hr.sln`.
- Kotwica `2026-10-16` (piątek), ziarno `20261016`. Okno ewidencji: od `new DateOnly(anchor.Year, anchor.Month, 1).AddMonths(-6)` (dla kotwicy 1.04.2026) do kotwicy włącznie. Naliczenia: pełne miesiące od miesiąca początku okna do miesiąca przed miesiącem kotwicy (kwiecień–wrzesień); miesiąca kotwicy nie liczymy.
- Warstwa danych: żadnego `DbContext`, `IClock`, `DateTime.Now/UtcNow`, `Guid.NewGuid()`, `Random` bez ziarna. Wszystkie identyfikatory i znaczniki czasu wynikają z (ziarno, kotwica). Czas wpisów to czas lokalny (`DateTimeKind.Unspecified`), `CreatedAtUtc` to czas lokalny przeliczony przez `PolishTime`.
- **Profil** `Hr:DemoProfile` (`Full` domyślnie, `Accounts`): `Accounts` zakłada dokładnie dzisiejszy zestaw (3 zespoły, 6 osób i kont, bez danych i bez naliczeń) i jest używany przez `HrApiFactory`, żeby ~wszystkie istniejące testy API nadal działały na minimalnych danych. To rozszerzenie speca (sekcja 6).
- Sześć kont stałych zachowuje obecne imiona, loginy, role, zespoły, daty zatrudnienia i wynagrodzenia z `DemoSeeder` (Anna 7200, Jan 8000, Tomasz 6800, Ewa 11000, Piotr 9000, Maria 12000). Hasło: login bez kropki + `123`.
- Skład `Full`: pięć zespołów (`Zespół Ewy` 7 osób, `Zespół B` 7, `Zespół C` 7, `Zespół D` 7, `Kadry` 3 = 31 osób). Zespół biznesowy ma dokładnie jednego `Manager`; zespół `Kadry` (Piotr `HR`, Maria `HRApprover`, jedna osoba `Employee`) nie ma `Manager` (doprecyzowanie ADR-20). Nazwiska wszystkich 31 osób są unikalne (testy wybierają pracowników po nazwisku), loginy unikalne, bez znaków diakrytycznych.
- Wynagrodzenie wygenerowanej osoby: `5000 + 300 * min(lata stażu, 10)` ±5% zaokrąglone do 50 zł, `Manager` ×1,25; zakres sprawdza `DemoPeople.SalaryRange(years, role)`. Data zatrudnienia wygenerowanych: od 2008-01-01 do `WindowStart - 1 dzień`.
- Wpisy czasu: 8 h pracy + 30 min przerwy, start 7:15 / 7:30 / 7:45 / 8:00 / 8:15 / 8:30 / 8:45 (stały dla pracownika), tylko dni robocze wg `WorkCalendar`, z pominięciem dni zatwierdzonego urlopu. Bez nadgodzin i bez naruszeń poza scenariuszami i poza czterema wygenerowanymi osobami z nadgodzinami (co ok. 20. dzień +1 h na końcu zmiany).
- Urlopy: dni liczy `WorkCalendar.CountWorkingDays`; suma dni (zatwierdzone + oczekujące) w roku ≤ `LeaveEntitlement.AnnualDays(YearsOfService(hire, anchor))`; wnioski aktywne (zatwierdzone, oczekujące) jednej osoby nie nakładają się i mieszczą się w jednym roku. `DecidedByEmployeeId` to kierownik zespołu wnioskodawcy, a dla kierowników i zespołu `Kadry` Piotr Zieliński.
- Audyt: zapis przez `HrDbContext` z przechwytywaczem audytu dopisuje wiersze `AuditLog` dla seeda (aktor `null`); jest to zamierzone i opisane w dokumentacji. Seed nie wyłącza audytu.
- Idempotencja: dane są zasiewane, gdy brak konta `anna.nowak`; naliczenia dla każdego miesiąca bez wyników w `PayrollResults`. Seed nigdy nie nadpisuje ani nie usuwa danych.

## Review Focus

1. **Kotwica niepoprawna** (nie jest dniem roboczym, zły format, ziarno nie jest liczbą, nieznany profil): start kończy się `InvalidOperationException` z polskim komunikatem (Task 1).
2. **Kotwica przesunięta** (np. `2026-11-13`, `2027-03-12`): zbiór nadal spełnia wszystkie niezmienniki, scenariusze mają daty względem kotwicy (Task 6).
3. **Awaria po zapisie danych, przed naliczeniem** albo ponowny start: drugi start nie dubluje danych i dokończa naliczenia (Task 7).
4. **Baza z dawnym seedem 6 kont** i włączony profil `Full`: brak wyjątku, brak nadpisania (Task 7).
5. **Nazwiska z diakrytykami i powtórzenia** (Wiśniewska, Żółkiewski): loginy ASCII, unikalne, hasła akceptowane przez Identity (Task 2, 7).

---

## Pliki

| Plik | Odpowiedzialność |
|---|---|
| `src/Hr.Core/Seeding/Demo/DemoOptions.cs` | `DemoOptions`, `DemoProfile`, walidacja i parsowanie. |
| `src/Hr.Core/Seeding/Demo/DemoIds.cs` | Deterministyczne `Guid` z (ziarno, klucz). |
| `src/Hr.Core/Seeding/Demo/DemoDataSet.cs` | Typy wyniku: `DemoPerson`, `DemoTeam`, `DemoDataSet`. |
| `src/Hr.Core/Seeding/Demo/DemoPeople.cs` | Zespoły i osoby, loginy, wynagrodzenia. |
| `src/Hr.Core/Seeding/Demo/DemoScenarios.cs` | Daty i stałe scenariuszy względem kotwicy. |
| `src/Hr.Core/Seeding/Demo/DemoLeave.cs` | Wnioski urlopowe. |
| `src/Hr.Core/Seeding/Demo/DemoTimesheets.cs` | Wpisy czasu pracy. |
| `src/Hr.Core/Seeding/Demo/DemoPayrollChanges.cs` | Zmiany płacowe. |
| `src/Hr.Core/Seeding/Demo/DemoConversations.cs` | Rozmowy z agentem. |
| `src/Hr.Core/Seeding/Demo/DemoDataGenerator.cs` | Składa wszystko w `DemoDataSet`. |
| `src/Hr.Core/Seeding/DemoSeeder.cs` | Zapis, konta Identity, naliczenia (przebudowa). |
| `src/Hr.Core/Seeding/SeedCurrentUser.cs` | `ICurrentUser` HR dla `PayrollService` w seedzie. |
| `tests/Hr.Core.Tests/` | Nowy projekt testów jednostkowych (bez Dockera). |

### Task 1: Projekt testów, `DemoOptions` i parsowanie konfiguracji

**Files:**
- Create: `tests/Hr.Core.Tests/Hr.Core.Tests.csproj` (jak `Hr.Core.IntegrationTests.csproj`, referencje `Hr.Core` i `Hr.Rules`, bez `Hr.TestSupport`), `tests/Hr.Core.Tests/Seeding/DemoOptionsTests.cs`
- Create: `src/Hr.Core/Seeding/Demo/DemoOptions.cs`
- Modify: `Hr.sln` (`dotnet sln add tests/Hr.Core.Tests`)

**Interfaces:**
- Produces: `enum DemoProfile { Full, Accounts }`; `sealed record DemoOptions(int Seed, DateOnly AnchorDate, DemoProfile Profile)` z `static DemoOptions Default` (`20261016`, `2026-10-16`, `Full`), `DateOnly WindowStart`, `IReadOnlyList<(int Year, int Month)> PayrollMonths`, `static DemoOptions Parse(string? anchorDate, string? seed, string? profile, WorkCalendar calendar)` (brak wartości → domyślna; błąd → `InvalidOperationException`).

- [ ] **Step 1: Napisz testy (RED)** w `DemoOptionsTests`:
  - `Default_has_anchor_2026_10_16_seed_20261016_profile_full`.
  - `Window_starts_on_first_day_six_months_before_anchor_month` (kotwica 2026-10-16 → `2026-04-01`; kotwica 2027-03-12 → `2026-09-01`).
  - `PayrollMonths_are_full_months_before_anchor_month` (domyślnie 6 pozycji od `(2026,4)` do `(2026,9)`).
  - `Parse_rejects_non_working_day_anchor` (`2026-10-17` sobota, `2026-11-01` niedziela i święto) — wyjątek, komunikat zawiera `dzień roboczy`.
  - `Parse_rejects_bad_format_seed_and_profile` (`"16.10.2026"`, seed `"abc"`, profil `"Mini"`) — wyjątek z nazwą klucza (`Hr:DemoAnchorDate`, `Hr:DemoSeed`, `Hr:DemoProfile`).
  - `Parse_accepts_values_and_defaults_missing` (`null` → wartości domyślne; `"2026-11-13"`, `"7"`, `"Accounts"` → odpowiednie).
- [ ] **Step 2: Uruchom** `dotnet test tests/Hr.Core.Tests` — oczekiwane: błąd kompilacji (brak typów), potem po dodaniu pustych typów FAIL.
- [ ] **Step 3: Zaimplementuj** typy z Interfaces w `DemoOptions.cs` (rekord z metodami; `Parse` używa `DateOnly.TryParseExact("yyyy-MM-dd")`, `int.TryParse` niezmiennie kulturowo, `Enum.TryParse` bez rozróżniania wielkości liter i tylko dla zdefiniowanych wartości).
- [ ] **Step 4: Uruchom** `dotnet test tests/Hr.Core.Tests` — PASS.
- [ ] **Step 5: Commit** `Dodaj projekt testów Hr.Core.Tests i opcje danych demo`.

### Task 2: Identyfikatory i osoby (`DemoIds`, `DemoPeople`)

**Files:**
- Create: `src/Hr.Core/Seeding/Demo/DemoIds.cs`, `DemoDataSet.cs`, `DemoPeople.cs`
- Test: `tests/Hr.Core.Tests/Seeding/DemoPeopleTests.cs`

**Interfaces:**
- Consumes: `DemoOptions`, `Roles`, `Team`, `Employee`.
- Produces: `static class DemoIds { static Guid For(int seed, string key) }` (deterministyczny GUID z hasha `seed:key`); `sealed record DemoPerson(string First, string Last, string Login, string Role, string TeamName, DateOnly HireDate, decimal MonthlySalary, Guid EmployeeId, Guid UserId, bool IsNamed) { string Password }` (hasło jak dotąd: login bez kropki + `123`); `sealed record DemoTeam(Guid Id, string Name, Guid? ManagerEmployeeId)`; `sealed record DemoDataSet` (na razie `IReadOnlyList<DemoTeam> Teams`, `IReadOnlyList<DemoPerson> People`; kolejne pola dodają następne zadania); `static class DemoPeople { static (IReadOnlyList<DemoTeam>, IReadOnlyList<DemoPerson>) Build(DemoOptions o); static (decimal Min, decimal Max) SalaryRange(int yearsOfService, string role); const string TeamEwa, TeamB, TeamC, TeamD, TeamHr; }`. Dla `DemoProfile.Accounts` zwraca tylko 3 zespoły i 6 osób stałych.

- [ ] **Step 1: Napisz testy (RED)** w `DemoPeopleTests`:
  - `Full_has_31_people_in_5_teams_with_sizes_7_7_7_7_3`.
  - `Accounts_profile_has_only_six_named_people_in_three_teams`.
  - `Named_accounts_keep_login_role_team_hire_and_salary` (tabela 6 kont z Global Constraints, w tym `Password == "annanowak123"`).
  - `Each_business_team_has_exactly_one_manager_and_hr_team_none` oraz `Team.ManagerEmployeeId` wskazuje tego Managera (Ewa dla `Zespół Ewy`).
  - `Last_names_and_logins_are_unique_and_ascii` (login `^[a-z]+\.[a-z]+$`, hasło `^[a-z]+123$`).
  - `Generated_salary_and_hire_date_within_ranges` (`SalaryRange`, zatrudnienie ≤ `WindowStart - 1`).
  - `Same_seed_gives_same_people_and_other_seed_gives_other_generated_names` (JSON dwóch wywołań; inne ziarno zmienia co najmniej jedno wygenerowane nazwisko, ale nie sześć stałych).
  - `DemoIds_are_stable_and_distinct_per_key`.
- [ ] **Step 2: Uruchom** — FAIL.
- [ ] **Step 3: Zaimplementuj.** Pula imion i nazwisk w kodzie jako pary rodzajów (`(Male, Female)` dla nazwisk na -ski/-cki/-ak itd.; ≥ 40 nazwisk bez sześciu stałych; ≥ 20 imion żeńskich i męskich, z diakrytykami, np. `Żółkiewski`/`Żółkiewska`); ziarnowany `Random(seed)` tasuje pule; login i hasło przez transliterację ASCII (ą→a, ć→c, ę→e, ł→l, ń→n, ó→o, ś→s, ź→z, ż→z). `Manager` zespołów B, C, D to wygenerowane osoby; zespół Ewy ma Ewę. Kolejność osób stała (stałe konta najpierw).
- [ ] **Step 4: Uruchom** — PASS.
- [ ] **Step 5: Commit** `Dodaj deterministyczne identyfikatory i generator osób demo`.

### Task 3: Scenariusze i urlopy (`DemoScenarios`, `DemoLeave`)

**Files:**
- Create: `src/Hr.Core/Seeding/Demo/DemoScenarios.cs`, `DemoLeave.cs`; Modify: `DemoDataSet.cs` (`IReadOnlyList<LeaveRequest> LeaveRequests`)
- Test: `tests/Hr.Core.Tests/Seeding/DemoLeaveTests.cs`

**Interfaces:**
- Consumes: `DemoPerson`, `DemoOptions`, `WorkCalendar`, `LeaveRequest`, `LeaveEntitlement`.
- Produces: `static class DemoScenarios` z metodami względem kotwicy `a`: `DateOnly AnnaDailyRestTuesday(DateOnly a)` (ostatni wtorek miesiąca poprzedzającego miesiąc kotwicy; dla domyślnej 2026-09-29), `DateOnly AnnaWeeklyRestSaturday(DateOnly a)` (druga sobota tego miesiąca; 2026-09-12), `(DateOnly From, DateOnly To) JanOvertimePeriod(DateOnly a)` (od 1. dnia miesiąca 4 miesiące przed miesiącem kotwicy do ostatniego dnia miesiąca poprzedniego; 2026-06-01..2026-09-30), `(DateOnly From, DateOnly To) AnnaLeaveRealized/AnnaLeaveFuture/AnnaLeavePending/JanLeaveRejected/JanLeaveCancelled(DateOnly a)` (każdy to poniedziałek–piątek wskazanego tygodnia; reguły względem miesiąca kotwicy M: realized = 2. poniedziałek miesiąca M−3, future = 1. poniedziałek miesiąca M+2, pending = 1. poniedziałek miesiąca M+1, rejected = 1. poniedziałek miesiąca M−2, cancelled = 1. poniedziałek miesiąca M; dla kotwicy 2026-10-16 dają to 13–17.07, 7–11.12, 2–6.11, 3–7.08 i 5–9.10.2026); `const string RejectionReason = "Zbyt wielu pracowników na urlopie w tym tygodniu."`. `static class DemoLeave { static IReadOnlyList<LeaveRequest> Build(DemoOptions o, IReadOnlyList<DemoPerson> people, WorkCalendar calendar) }`.
- Zasada „względem miesiąca kotwicy” zapisana jest w komentarzu `DemoScenarios`; daty domyślne pilnuje test.

- [ ] **Step 1: Napisz testy (RED)** w `DemoLeaveTests`:
  - `Scenario_dates_for_default_anchor` (wszystkie daty z listy powyżej, wtorek/sobota są właściwymi dniami tygodnia).
  - `Anna_has_exactly_realized_future_and_pending_leave` (statusy `Approved`, `Approved`, `Pending`; `Days == 5`; zatwierdzone mają `DecidedByEmployeeId` = Ewa; saldo 20 − 10 − 5 = 5) oraz `Jan_has_exactly_rejected_with_reason_and_cancelled` (`DecisionReason == RejectionReason`, `Days` zgodne z kalendarzem).
  - `Other_people_have_one_or_two_past_approved_leaves_within_entitlement_and_without_overlap`.
  - `Days_match_working_calendar_for_every_request` (`CountWorkingDays(From, To)`; `Days > 0`).
  - `Active_leaves_stay_within_one_year_and_entitlement` (wszystkie osoby; wymiar z `AnnualDays(YearsOfService(HireDate, anchor))`).
  - `Leave_ids_and_timestamps_are_deterministic` (dwa wywołania, ten sam JSON).
- [ ] **Step 2: Uruchom** — FAIL.
- [ ] **Step 3: Zaimplementuj.** Dla Anny i Jana wyłącznie wnioski scenariuszowe (`CreatedAtUtc` = 14 dni przed `From`, 8:00 lokalnie → UTC); dla pozostałych jeden lub dwa zatwierdzone wnioski 3–10 dni roboczych w oknie przed kotwicą, wybierane `Random(seed ^ hash(login))`, bez nakładania; id przez `DemoIds.For(seed, $"leave:{login}:{n}")`.
- [ ] **Step 4: Uruchom** — PASS.
- [ ] **Step 5: Commit** `Dodaj scenariusze demo i wnioski urlopowe`.

### Task 4: Wpisy czasu pracy (`DemoTimesheets`)

**Files:**
- Create: `src/Hr.Core/Seeding/Demo/DemoTimesheets.cs`; Modify: `DemoDataSet.cs` (`IReadOnlyList<WorkEntryRecord> WorkEntries`)
- Test: `tests/Hr.Core.Tests/Seeding/DemoTimesheetsTests.cs`

**Interfaces:**
- Consumes: `DemoOptions`, `DemoPerson`, `LeaveRequest`, `DemoScenarios`, `WorkCalendar`, `WorkEntryRecord`.
- Produces: `static class DemoTimesheets { static IReadOnlyList<WorkEntryRecord> Build(DemoOptions o, IReadOnlyList<DemoPerson> people, IReadOnlyList<LeaveRequest> leave, WorkCalendar calendar) }`.

- [ ] **Step 1: Napisz testy (RED)** w `DemoTimesheetsTests` (każdy wywołuje silnik z `Hr.Rules` na wpisach pracownika, zakres `WindowStart..anchor`):
  - `Entries_fall_only_on_working_days_inside_window_except_annas_saturday` (poza `AnnaWeeklyRestSaturday`: dzień roboczy wg `WorkCalendar`, brak wpisów w dniach zatwierdzonego urlopu, brak wpisów po kotwicy).
  - `Regular_people_have_no_rest_violations_and_no_weekly_excess` (dla wszystkich poza Anną i Janem: `RestRules.Check` puste, `WeeklyAverageRules.Check(...).ExceedsLimit == false` dla okresów kwiecień–lipiec i czerwiec–wrzesień).
  - `Only_four_generated_people_have_overtime_and_Tomasz_has_none` (`OvertimeCalculator.Calculate`: `Total > 0` dokładnie u czterech wygenerowanych; Tomasz, Ewa, Piotr, Maria 0).
  - `UC12_Anna_has_exactly_one_daily_rest_violation_on_scenario_tuesday` (`KP-132-1` dokładnie jedno, `From` w `AnnaDailyRestTuesday` 22:30; wpisy: wtorek 14:00–22:30 przerwa 30 min, środa 7:30–15:30 przerwa 30 min).
  - `UC13_Anna_has_exactly_one_weekly_rest_violation_in_scenario_week` (`KP-133-1` dokładnie jedno, w tygodniu soboty; sobota 14:00–20:00, przerwa 0) i `Anna_has_no_other_violations`.
  - `UC14_Jan_exceeds_48h_average_over_overtime_period_and_has_overtime_each_month` (`JanOvertimePeriod`: dzień roboczy 8:00–18:30 przerwa 30 min, średnia > 48 h, `ExceedsLimit`; nadgodziny > 0 w czerwcu, lipcu, sierpniu i wrześniu; `Jan` nie ma naruszeń odpoczynku) oraz `Tomasz_has_zero_alerts` (brak naruszeń, nadgodzin i przekroczenia).
  - `Entries_never_overlap_and_ids_are_deterministic`.
- [ ] **Step 2: Uruchom** — FAIL.
- [ ] **Step 3: Zaimplementuj.** Dla każdej osoby i dnia roboczego w oknie bez zatwierdzonego urlopu wpis wg zasad z Global Constraints; start wybierany `Random(seed ^ hash(login))` raz na osobę; nadgodziny: cztery wygenerowane osoby (indeksy 3, 9, 14, 20 w kolejności osób generowanych) co 20. dzień roboczy +1 h na końcu; Jan i Anna wg scenariuszy (Jan: zmiana 8:00–18:30 w `JanOvertimePeriod`, w pozostałych dniach standardowa; Anna: zastąp wpisy z wtorku i środy scenariusza, dodaj sobotę); id `DemoIds.For(seed, $"work:{login}:{date:yyyyMMdd}:{n}")`.
- [ ] **Step 4: Uruchom** — PASS. Jeśli test odpoczynku tygodniowego Anny nie daje dokładnie jednego naruszenia, popraw godziny soboty, tak by najdłuższa przerwa tygodnia była krótsza niż 35 h, i zachowaj test.
- [ ] **Step 5: Commit** `Dodaj generator wpisów czasu pracy ze scenariuszami`.

### Task 5: Zmiany płacowe i rozmowy

**Files:**
- Create: `src/Hr.Core/Seeding/Demo/DemoPayrollChanges.cs`, `DemoConversations.cs`; Modify: `DemoDataSet.cs` (`IReadOnlyList<PayrollChange> PayrollChanges`, `IReadOnlyList<Conversation> Conversations`)
- Test: `tests/Hr.Core.Tests/Seeding/DemoPayrollChangesTests.cs`, `DemoConversationsTests.cs`

**Interfaces:**
- Consumes: `DemoPerson`, `DemoOptions`, `LeaveRequest`, `PayrollChange`, `Conversation`, `Message`, `MessageRole`.
- Produces: `static class DemoPayrollChanges { static IReadOnlyList<PayrollChange> Build(DemoOptions o, IReadOnlyList<DemoPerson> people) }`; `static class DemoConversations { static IReadOnlyList<Conversation> Build(DemoOptions o, IReadOnlyList<DemoPerson> people, IReadOnlyList<LeaveRequest> leave) }`.

- [ ] **Step 1: Napisz testy (RED).** Zmiany płacowe (`CreatedByUserId` to `UserId` Piotra, `DecidedByUserId` to `UserId` Marii):
  - `UC18_Approved_historic_change_is_jans_fixed_allowance_400_from_june_first` (`Component = FixedAllowance`, `Value = 400`, `EffectiveFrom = 2026-06-01`, `Status = Approved`, powód niepusty).
  - `UC18_Pending_change_is_annas_base_salary_7800_from_november_first_created_by_piotr_without_decision` (`DecidedByUserId == null`, `DecisionReason == null`).
  - `UC20_Rejected_change_is_tomaszs_fixed_allowance_300_from_september_first_with_reason` (`DecisionReason` niepusty).
  - `UC19_No_change_is_decided_by_its_creator_and_all_dates_within_window_plus_two_months`.
  Rozmowy (`Seq` od 1, `Conversation.UserId` = `UserId` właściciela):
  - `UC30_Anna_conversation_has_user_toolcall_toolresult_assistant_in_order` (`get_leave_balance`, `Content` wywołania to id `toolu_demo_anna_01`, `ToolParamsJson == "{}"`).
  - `UC21_Annas_tool_result_matches_leave_balance_from_dataset` (JSON z kluczami `entitlementDays`, `approvedDays`, `pendingDays`, `remainingDays`, `year`, `ruleId = "KP-154-1"` o wartościach 20, 10, 5, 5 liczonych z wniosków i `LeaveEntitlement`; odpowiedź asystenta zawiera te liczby cyframi i żadnych innych liczb).
  - `UC24_Tomasz_conversation_asks_about_Annas_leave_and_gets_refusal_without_tool_calls` (user, assistant; odpowiedź odmawia i wskazuje własne dane).
  - `Conversations_are_deterministic_and_owned_by_their_users`.
- [ ] **Step 2: Uruchom** — FAIL.
- [ ] **Step 3: Zaimplementuj** jak w testach; kształt JSON narzędzia skopiuj z `AgentToolbox.GetLeaveBalanceAsync` (`employeeId` = Anna, `year` = rok kotwicy), serializacja `camelCase` jak w toolboxie; id wiadomości i rozmów przez `DemoIds`; `CreatedAtUtc` rozmów: kotwica 9:00 lokalnie, kolejne wiadomości co minutę.
- [ ] **Step 4: Uruchom** — PASS.
- [ ] **Step 5: Commit** `Dodaj zmiany płacowe i rozmowy z agentem do danych demo`.

### Task 6: Składanie zbioru i niezmienniki

**Files:**
- Create: `src/Hr.Core/Seeding/Demo/DemoDataGenerator.cs`, `tests/Hr.Core.Tests/Seeding/DemoDataInvariantsTests.cs`, `tests/Hr.Architecture.Tests/DemoDataIsolationTests.cs`

**Interfaces:**
- Consumes: wszystkie moduły z Task 2–5, `WorkCalendar`.
- Produces: `static class DemoDataGenerator { static DemoDataSet Build(DemoOptions options, WorkCalendar calendar) }` (kolejność: osoby → urlopy → wpisy → zmiany płacowe → rozmowy); dla profilu `Accounts` zbiór zawiera tylko zespoły i osoby (pozostałe listy puste).

- [ ] **Step 1: Napisz testy (RED).**
  - `Determinism_two_builds_taken_apart_serialize_identically` (dwa wywołania z odstępem ≥ 20 ms; porównanie JSON całego `DemoDataSet`).
  - Niezmienniki przez `[Theory]` dla trzech kotwic (`2026-10-16`, `2026-11-13`, `2027-03-12`) i dwóch ziaren: kalendarz i okno (brak wpisów poza oknem), brak naruszeń odpoczynku dla osób nie scenariuszowych, urlopy w wymiarze, loginy/hasła/menedżerowie, zmiany płacowe bez samozatwierdzenia, brak powtórzonych identyfikatorów w całym zbiorze, `Accounts` zwraca 6 osób bez danych.
  - Architektura (`DemoDataIsolationTests`, w stylu `RulesIsolationTests`): typy z przestrzeni `Hr.Core.Seeding.Demo` nie zależą od `Hr.Core.Persistence`, `Microsoft.EntityFrameworkCore`, `Hr.Core.Abstractions.IClock` ani `Microsoft.AspNetCore.Identity`; z kodu produkcyjnego z `Demo` korzysta tylko `Hr.Core.Seeding.DemoSeeder`.
- [ ] **Step 2: Uruchom** `dotnet test tests/Hr.Core.Tests` oraz `dotnet test tests/Hr.Architecture.Tests` — FAIL.
- [ ] **Step 3: Zaimplementuj** `DemoDataGenerator.Build`; popraw ewentualne niezmienniki naruszone przy innych kotwicach w modułach z Task 2–5 (scenariusze względem miesiąca kotwicy).
- [ ] **Step 4: Uruchom** oba projekty — PASS.
- [ ] **Step 5: Commit** `Złóż zbiór danych demo i dodaj testy niezmienników`.

### Task 7: `DemoSeeder` na nowej warstwie danych

**Files:**
- Create: `src/Hr.Core/Seeding/SeedCurrentUser.cs`
- Modify: `src/Hr.Core/Seeding/DemoSeeder.cs`, `tests/Hr.Core.IntegrationTests/DemoSeederTests.cs`
- Test: `tests/Hr.Core.IntegrationTests/DemoDataSeedTests.cs`

**Interfaces:**
- Consumes: `DemoDataGenerator.Build`, `DemoOptions`, `PayrollService(HrDbContext, ICurrentUser, IClock, WorkCalendar)`, `UserManager<ApplicationUser>`, `RoleManager<IdentityRole<Guid>>`.
- Produces: `sealed class SeedCurrentUser : ICurrentUser` (`UserId`, `EmployeeId`, rola `HR`); `static Task DemoSeeder.SeedAsync(IServiceProvider services, DemoOptions options, CancellationToken ct = default)` (dotychczasowe `SeedAsync(IServiceProvider, CancellationToken)` znika; `IClock` i `WorkCalendar` z `GetService`, z domyślnymi `SystemClock` i `new WorkCalendar()`, gdy nie zarejestrowano).

- [ ] **Step 1: Napisz testy (RED).** W `DemoSeederTests` przełącz wywołania na `DemoOptions.Default with { Profile = DemoProfile.Accounts }` (wszystkie dotychczasowe asercje o 6/6/3/4 mają nadal przechodzić bez zmian). W `DemoDataSeedTests` (profil `Full`, `[Collection("mssql")]`):
  - `Full_seed_creates_31_people_accounts_five_teams_and_ends_in_under_60_seconds`.
  - `Seed_is_idempotent_and_never_duplicates_data` (liczby `Employees`, `WorkEntries`, `LeaveRequests`, `PayrollChanges`, `Conversations`, `PayrollResults` po dwóch uruchomieniach równe).
  - `Payroll_exists_for_april_to_september_for_every_employee_and_not_for_october` (31 × 6 wierszy; `CalculatedByUserId` = `UserId` Piotra).
  - `Payroll_matches_calculator_for_annas_september_and_reflects_approved_change` (Anna wrzesień zgodne z `PayrollCalculator` dla danych z bazy; Jan: `FixedAllowance` = 0 w maju i 400 od czerwca, według `PayrollResults`).
  - `Seed_completes_missing_payroll_after_crash_between_data_and_payroll` (po pierwszym seedzie usuń wiersze `PayrollResults`, uruchom drugi raz: naliczenia wracają, dane bez duplikatów).
  - `Seed_does_not_overwrite_existing_old_six_account_database` (najpierw profil `Accounts`, potem `Full`: brak wyjątku, nadal 6 osób, brak wpisów czasu).
  - `Every_generated_account_can_sign_in_with_its_password_pattern` (`CheckPasswordAsync` dla wszystkich 31).
  - `UC03_Tomasz_does_not_see_Annas_entries_through_data_scope` (zapytanie przez `Visible(db, user)` z `TestUser` Tomasza).
- [ ] **Step 2: Uruchom** `DOCKER_API_VERSION=1.43 dotnet test tests/Hr.Core.IntegrationTests --filter "FullyQualifiedName~DemoSeeder|FullyQualifiedName~DemoDataSeed"` — FAIL.
- [ ] **Step 3: Zaimplementuj `DemoSeeder.SeedAsync`.** Role (jak dotąd). Gdy konto `anna.nowak` nie istnieje: jedna transakcja `db.Database.BeginTransactionAsync` obejmująca zespoły bez kierowników → `SaveChanges` → pracownicy → `SaveChanges` → kierownicy zespołów → pozostałe encje zbioru → konta Identity z rolami → commit (błąd cofa wszystko). Dla `Accounts` na tym kończy się praca. Dla `Full`: po commicie, dla każdego miesiąca z `PayrollMonths` bez wierszy w `PayrollResults`, `new PayrollService(db, new SeedCurrentUser(piotr), clock, calendar).RunMonthAsync(y, m)`; `IsSuccess == false` → `InvalidOperationException` z komunikatem serwisu. Loguje `ILogger<DemoSeeder>` (jeśli zarejestrowany): początek, koniec, czas, liczby rekordów; nigdy haseł. Dane `Accounts` powstają tą samą ścieżką z `DemoDataGenerator`.
- [ ] **Step 4: Uruchom** polecenie z Step 2 — PASS.
- [ ] **Step 5: Commit** `Przebuduj DemoSeeder na warstwę danych z naliczeniami z PayrollService`.

### Task 8: Podłączenie w API, testy API, dokumentacja

**Files:**
- Modify: `src/Hr.Api/Program.cs:152-155`, `tests/Hr.Api.IntegrationTests/HrApiFactory.cs:29`, `docs/arc42/07-widok-wdrozenia.md`, `docs/arc42/09-decyzje-architektoniczne.md`, `docs/arc42/README.md`, `docs/superpowers/specs/2026-10-17-plan6-dane-demo-design.md` (sekcja 6: klucz `Hr:DemoProfile`)
- Test: `tests/Hr.Api.IntegrationTests/DemoDataApiTests.cs`

**Interfaces:**
- Consumes: `DemoOptions.Parse`, `DemoSeeder.SeedAsync(services, options)`.

- [ ] **Step 1: Napisz testy (RED)** w `DemoDataApiTests` (fabryka z `Hr:DemoProfile=Full`; dodaj do `HrApiFactory` opcjonalny parametr `demoProfile = "Accounts"` ustawiany przez `builder.UseSetting("Hr:DemoProfile", demoProfile)`):
  - `UC29_Full_profile_serves_logins_for_named_and_generated_accounts` (logowanie `anna.nowak` i jednego wygenerowanego konta).
  - `UC05_Annas_balance_is_20_10_5_5` (przez API salda; zegar fabryki `2026-10-16`).
  - `UC17_Anna_sees_only_her_payroll_for_september_and_none_for_october`.
  - `UC31_HR_can_open_Annas_seeded_conversation_and_read_is_audited`.
  - `Startup_fails_with_polish_message_for_non_working_day_anchor` (`Hr:DemoAnchorDate=2026-10-17` → wyjątek przy tworzeniu hosta z komunikatem po polsku).
- [ ] **Step 2: Uruchom** `DOCKER_API_VERSION=1.43 dotnet test tests/Hr.Api.IntegrationTests --filter DemoDataApiTests` — FAIL.
- [ ] **Step 3: Zaimplementuj.** W `Program.cs`: `var options = DemoOptions.Parse(config["Hr:DemoAnchorDate"], config["Hr:DemoSeed"], config["Hr:DemoProfile"], new WorkCalendar())` tylko gdy `Hr:SeedDemoData=true`, potem `DemoSeeder.SeedAsync(app.Services, options)`. `HrApiFactory`: domyślny profil `Accounts`. Dokumentacja: 7.2 (konta wygenerowane i rola `Manager` w zespołach biznesowych), nowa sekcja 7.3 „Dane demo” (jak włączyć: `Hr:SeedDemoData=true`, `Hr:DemoAnchorDate`, `Hr:DemoSeed`, `Hr:DemoProfile`; lista scenariuszy; ostrzeżenie R-03; wiersze audytu seeda), ADR-19 i ADR-21 (okno, kotwica), dwa nowe ADR o kolejnych wolnych numerach (po ostatnim w pliku): „Dane demo: dane wejściowe wprost do bazy, naliczenia z `PayrollService`” oraz „Stała kotwica demo i profile `Full`/`Accounts`”, tabela ADR na górze pliku, README: status „Zrealizowane: … plan 6 (dane demo)”.
- [ ] **Step 4: Pełny zestaw:** `DOCKER_API_VERSION=1.43 dotnet test Hr.sln` — wszystkie zielone, build bez ostrzeżeń. Jeśli któryś test API zawodzi z powodu danych, ustaw jego fabrykę na profil `Accounts` zamiast zmieniać dane.
- [ ] **Step 5: Commit** `Podłącz dane demo w API i opisz je w dokumentacji`.
