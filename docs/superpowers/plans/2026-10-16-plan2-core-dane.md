# Plan 2: `Hr.Core` — model danych, EF Core + MSSQL, audyt i zakres danych — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zbudować warstwę danych systemu: projekt `Hr.Core` z modelem domenowym, EF Core na MSSQL (migracje), dziennikiem audytu tylko do dopisywania, zasadą czterech oczu w bazie, filtrowaniem zakresu danych wg roli oraz kontami demo (seed), wszystko sprawdzone testami integracyjnymi na prawdziwym SQL Server w kontenerze.

**Architecture:** `Hr.Core` (zależy tylko od `Hr.Rules`) trzyma encje, `HrDbContext` (Identity + dane), interceptor audytu i rozszerzenia zakresu danych. Autoryzacja zakresu to filtry `IQueryable` budowane z `ICurrentUser`, więc ograniczenia działają w SQL, a nie w pamięci. Reguły integralności (cztery oczu, append-only audytu) są egzekwowane w bazie, niezależnie od warstwy serwisów.

**Tech Stack:** .NET 9, EF Core 9 (SqlServer), ASP.NET Core Identity (EntityFramework store), xUnit, Testcontainers.MsSql (obraz `mcr.microsoft.com/mssql/server:2022-latest`).

**Spec:** `docs/arc42/README.md` (sekcje 3.5, 5.2, 5.5, 8.1, 8.3a, 9: ADR-06, 08, 11, 12, 13, 15, 19, 20). Poprzedni plan: `docs/superpowers/plans/2026-10-16-plan1-silnik-regul.md` (zrealizowany, zmergowany). Kolejne plany: serwisy aplikacyjne (plan 3), Api + OpenAPI (plan 4), Agent (plan 5), Angular (plan 6), generator danych demo, import Kodeksu pracy, Docker i E2E (plan 7).

## Global Constraints

- `net9.0`, `Nullable` włączone, ostrzeżenia jako błędy (jak w `Directory.Build.props`); EF Core i pakiety Identity w wersji `9.*`.
- `Hr.Core` zależy wyłącznie od `Hr.Rules` (i pakietów EF/Identity). Nie zna `Hr.Agent` ani `Hr.Api`. `Hr.Rules` nie może zależeć od `Hr.Core`.
- Kwoty `decimal(18,2)`; wartości enum zapisywane w bazie jako tekst; klucze `Guid`, poza `AuditLog.Id` (`bigint` identity).
- Czas z `IClock` (UTC), nigdzie `DateTime.UtcNow` bezpośrednio poza `SystemClock`.
- Brak dostępu (rola bez powiązanego pracownika, nieznany użytkownik) daje **pusty wynik**, nigdy pełny zakres (fail closed).
- Polityka haseł demo (ADR-06): min. 8 znaków, bez wymogu wielkich liter, cyfr i znaków specjalnych; blokada konta po 5 nieudanych próbach.
- Nazwy testów zaczynają się od przypadku użycia (`UC03_...`), jeśli odpowiada im przypadek z `docs/arc42/03-kontekst-i-zakres.md`.
- Komunikaty commitów po polsku, w formie rozkazującej, bez emoji.

## Review Focus

1. **Separacja danych w SQL:** `tomasz.lis` nie widzi wniosków ani wpisów Anny, także przy odczycie po identyfikatorze; kierownik innego zespołu nie widzi cudzych (Task 7).
2. **Cztery oczy w bazie:** wiersz z `DecidedByUserId == CreatedByUserId` odrzucany przez SQL Server mimo obejścia serwisów (Task 4).
3. **Audyt tylko do dopisywania:** `UPDATE` i `DELETE` na `AuditLog` odrzucone nawet surowym SQL; nieudany zapis nie zostawia wpisów audytu (Task 6).
4. **Fail closed:** użytkownik bez `EmployeeId` lub z nieznaną rolą dostaje pusty zakres (Task 7, 8).
5. **Seed idempotentny:** dwukrotne uruchomienie nie duplikuje kont, a hasła z `docs/arc42/07-widok-wdrozenia.md` działają (Task 9).

---

## Struktura plików

| Ścieżka | Odpowiedzialność |
|---|---|
| `src/Hr.Core/Abstractions/` | `IClock`, `SystemClock`, `ICurrentUser`, `Roles` |
| `src/Hr.Core/Domain/` | Encje: `Team`, `Employee`, `ApplicationUser`, `LeaveRequest`, `WorkEntryRecord`, `PayrollChange`, `Conversation`, `Message`, `AuditLog` |
| `src/Hr.Core/Persistence/` | `HrDbContext`, konfiguracje encji, `HrDbContextFactory` (design-time), `Migrations/` |
| `src/Hr.Core/Auditing/` | `IAuditable`, `AuditSaveChangesInterceptor`, `IAuditWriter` |
| `src/Hr.Core/Security/` | `IEmployeeOwned`, `DataScope`, `ConversationAccess`, `ConversationReader` |
| `src/Hr.Core/Seeding/` | `HrIdentityOptions`, `DemoSeeder` |
| `tests/Hr.Core.IntegrationTests/` | `MsSqlFixture`, `TestUser`, `FakeClock` i testy integracyjne |
| `.config/dotnet-tools.json` | Lokalne narzędzie `dotnet-ef` |

---

### Task 1: Szkielet `Hr.Core`, abstrakcje i fixture MSSQL

**Files:**
- Create: `src/Hr.Core/Hr.Core.csproj`, `src/Hr.Core/Abstractions/IClock.cs`, `SystemClock.cs`, `ICurrentUser.cs`, `Roles.cs`
- Create: `tests/Hr.Core.IntegrationTests/Hr.Core.IntegrationTests.csproj`, `MsSqlFixture.cs`, `TestUser.cs`, `FakeClock.cs`
- Modify: `Hr.sln`, `tests/Hr.Architecture.Tests/RulesIsolationTests.cs` (dodaj test)
- Test: `tests/Hr.Core.IntegrationTests/MsSqlFixtureTests.cs`

**Interfaces:**
- Produces:
  - `public interface IClock { DateTime UtcNow { get; } }`, `public sealed class SystemClock : IClock`.
  - `public interface ICurrentUser { Guid? UserId { get; } Guid? EmployeeId { get; } IReadOnlySet<string> Roles { get; } bool IsInRole(string role); }`
  - `public static class Roles { public const string Employee = "Employee", Manager = "Manager", HR = "HR", HRApprover = "HRApprover"; public static readonly IReadOnlyList<string> All; }`
  - `public sealed class MsSqlFixture : IAsyncLifetime { public Task<string> NewDatabaseAsync(); }` zwracająca connection string do świeżej, pustej bazy w jednym kontenerze współdzielonym przez kolekcję testów (`[CollectionDefinition("mssql")]`).
  - `TestUser : ICurrentUser` (konstruktor `TestUser(Guid? userId, Guid? employeeId, params string[] roles)`), `FakeClock : IClock` (ustawialny `UtcNow`).

- [ ] **Step 1: Utwórz projekty.** `Hr.Core` (classlib, referencja do `Hr.Rules`, pakiety `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, wszystkie `--version 9.*`), `Hr.Core.IntegrationTests` (xunit, referencja do `Hr.Core`, pakiet `Testcontainers.MsSql`). Dodaj do `Hr.sln`.
- [ ] **Step 2: Napisz testy, które mają się nie udać.**
  - `MsSqlFixtureTests.Fresh_database_accepts_queries`: `NewDatabaseAsync()` zwraca connection string, `SELECT 1` przez `SqlConnection` zwraca `1`; dwa wywołania dają dwie różne bazy (`SELECT DB_NAME()` różne).
  - W `RulesIsolationTests`: `Rules_does_not_depend_on_Core` — typy z `Hr.Rules` nie mają zależności od przestrzeni `Hr.Core`.
- [ ] **Step 3: Uruchom — FAIL**

Run: `dotnet test tests/Hr.Core.IntegrationTests`
Expected: błąd kompilacji (brak `MsSqlFixture`).

- [ ] **Step 4: Zaimplementuj abstrakcje i `MsSqlFixture`.** Fixture startuje jeden kontener SQL Server (obraz `2022-latest`, hasło testowe poza repozytorium kodu sekretów produkcyjnych) w `InitializeAsync`, a `NewDatabaseAsync` tworzy bazę o nazwie `test_<guid>`.
- [ ] **Step 5: Uruchom — PASS**

Run: `dotnet test`
Expected: wszystkie testy PASS (pierwsze uruchomienie dłuższe z powodu pobrania obrazu).

- [ ] **Step 6: Commit** — `git commit -m "Dodaj szkielet Hr.Core i fixture MSSQL do testów integracyjnych"`

---

### Task 2: Model organizacyjny i konta

**Files:**
- Create: `src/Hr.Core/Domain/Team.cs`, `Employee.cs`, `ApplicationUser.cs`
- Create: `src/Hr.Core/Persistence/HrDbContext.cs`, `HrDbContextFactory.cs`, `Configurations/OrgConfiguration.cs`
- Create: `.config/dotnet-tools.json` (`dotnet-ef` 9.*), migracja `AddOrgModel`
- Modify: `tests/Hr.Core.IntegrationTests/MsSqlFixture.cs` (dodaj `NewContextAsync`)
- Test: `tests/Hr.Core.IntegrationTests/OrgModelTests.cs`

**Interfaces:**
- Consumes: `MsSqlFixture`, `ICurrentUser` (Task 1).
- Produces:
  - `Team { Guid Id; string Name; Guid? ManagerEmployeeId }`
  - `Employee { Guid Id; string FirstName; string LastName; DateOnly HireDate; Guid TeamId; decimal MonthlySalary }`
  - `ApplicationUser : IdentityUser<Guid> { Guid? EmployeeId }`
  - `HrDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` z `DbSet<Team> Teams`, `DbSet<Employee> Employees`; konstruktor `HrDbContext(DbContextOptions<HrDbContext> options)`.
  - `MsSqlFixture.NewContextAsync(ICurrentUser? actor = null) : Task<HrDbContext>` — nowa baza z zastosowanymi migracjami (`Database.MigrateAsync()`).

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `Org_model_roundtrips_in_mssql`: zapis `Team` + `Employee` (`MonthlySalary = 8000.00m`, `HireDate = 2019-03-01`), odczyt świeżym kontekstem zwraca równe wartości.
  - `Employee_requires_existing_team`: `Employee` z nieistniejącym `TeamId` → `DbUpdateException` przy `SaveChangesAsync`.
  - `MonthlySalary_keeps_two_decimals`: zapis `1234.56m` wraca jako `1234.56m` (kolumna `decimal(18,2)`).
- [ ] **Step 2: Uruchom — FAIL** (`dotnet test tests/Hr.Core.IntegrationTests --filter OrgModelTests`).
- [ ] **Step 3: Zaimplementuj encje, `HrDbContext`, konfigurację i fabrykę design-time.** Enumy i kwoty jak w Global Constraints. Zainstaluj lokalnie `dotnet-ef` i wygeneruj migrację `AddOrgModel` (`dotnet ef migrations add AddOrgModel -p src/Hr.Core -o Persistence/Migrations`).
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj model organizacyjny, konta i migrację AddOrgModel"`

---

### Task 3: Wnioski urlopowe i ewidencja czasu pracy

**Files:**
- Create: `src/Hr.Core/Domain/LeaveRequest.cs`, `WorkEntryRecord.cs`, `Security/IEmployeeOwned.cs`
- Modify: `HrDbContext.cs` (dwa `DbSet`), migracja `AddLeaveAndTime`
- Test: `tests/Hr.Core.IntegrationTests/LeaveAndTimeTests.cs`

**Interfaces:**
- Consumes: `Employee`, `HrDbContext` (Task 2); `Hr.Rules.Time.WorkEntry`.
- Produces:
  - `public interface IEmployeeOwned { Guid EmployeeId { get; } }`
  - `LeaveStatus { Pending, Approved, Rejected, Cancelled }`
  - `LeaveRequest : IEmployeeOwned { Guid Id; Guid EmployeeId; DateOnly From; DateOnly To; int Days; LeaveStatus Status; Guid? DecidedByEmployeeId; string? DecisionReason; DateTime CreatedAtUtc }`
  - `WorkEntryRecord : IEmployeeOwned { Guid Id; Guid EmployeeId; DateTime Start; DateTime End; TimeSpan Break; Hr.Rules.Time.WorkEntry ToRulesEntry(); }`

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `LeaveRequest_roundtrips_with_status_as_text`: zapis, odczyt, a zapytanie SQL `SELECT Status FROM LeaveRequests` zwraca `"Pending"`.
  - `WorkEntryRecord_maps_to_rules_entry`: wpis 08:00–17:00 z przerwą 1 h → `ToRulesEntry().WorkedHours == 8.0m`.
  - `LeaveRequest_rejects_to_before_from`: `To < From` → `DbUpdateException` (ograniczenie CHECK `CK_LeaveRequest_Period`).
  - `LeaveRequest_rejects_non_positive_days`: `Days = 0` → `DbUpdateException`.
  - `WorkEntryRecord_rejects_end_not_after_start`: `End <= Start` → `DbUpdateException` (`CK_WorkEntry_Period`).
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj encje, konfigurację z ograniczeniami CHECK i migrację `AddLeaveAndTime`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj wnioski urlopowe i ewidencję czasu pracy"`

---

### Task 4: Zmiany płacowe i zasada czterech oczu w bazie (UC-18, UC-19)

**Files:**
- Create: `src/Hr.Core/Domain/PayrollChange.cs`
- Modify: `HrDbContext.cs`, migracja `AddPayrollChange`
- Test: `tests/Hr.Core.IntegrationTests/PayrollChangeTests.cs`

**Interfaces:**
- Consumes: `IEmployeeOwned`, `HrDbContext`.
- Produces:
  - `PayrollComponent { BaseSalary, FixedAllowance }`, `PayrollChangeStatus { Draft, Pending, Approved, Rejected }`
  - `PayrollChange : IEmployeeOwned { Guid Id; Guid EmployeeId; PayrollComponent Component; decimal Value; DateOnly EffectiveFrom; string Reason; PayrollChangeStatus Status; Guid CreatedByUserId; Guid? DecidedByUserId; string? DecisionReason; DateTime CreatedAtUtc }`
  - Ograniczenie CHECK `CK_PayrollChange_FourEyes`: `DecidedByUserId IS NULL OR DecidedByUserId <> CreatedByUserId` (ADR-08).

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `UC18_Approved_by_different_user_is_stored`: `CreatedByUserId != DecidedByUserId`, status `Approved` zapisuje się i wraca.
  - `UC19_Database_rejects_self_approval`: `DecidedByUserId == CreatedByUserId` → `DbUpdateException` z nazwą ograniczenia `CK_PayrollChange_FourEyes` w komunikacie (z pominięciem jakichkolwiek serwisów).
  - `UC19_Pending_without_decider_is_allowed`: `DecidedByUserId = null`, status `Pending` zapisuje się.
  - `PayrollChange_requires_non_negative_value`: `Value = -1m` → `DbUpdateException`.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj encję, ograniczenia i migrację `AddPayrollChange`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj zmiany płacowe z zasadą czterech oczu w bazie"`

---

### Task 5: Rozmowy z agentem

**Files:**
- Create: `src/Hr.Core/Domain/Conversation.cs`, `Message.cs`
- Modify: `HrDbContext.cs`, migracja `AddConversations`
- Test: `tests/Hr.Core.IntegrationTests/ConversationTests.cs`

**Interfaces:**
- Consumes: `ApplicationUser`, `HrDbContext`.
- Produces:
  - `MessageRole { User, Assistant, ToolCall, ToolResult }`
  - `Conversation { Guid Id; Guid UserId; DateTime StartedAtUtc; List<Message> Messages }`
  - `Message { Guid Id; Guid ConversationId; int Seq; MessageRole Role; string Content; string? ToolName; string? ToolParamsJson; DateTime CreatedAtUtc }`
  - Unikalny indeks `(ConversationId, Seq)`.

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `Conversation_with_tool_call_roundtrips`: rozmowa z 3 wiadomościami (`User`, `ToolCall` z `ToolName="calculate_overtime"` i JSON `{"month":"2026-10"}`, `Assistant`) wraca w kolejności `Seq`, a `ToolParamsJson` bez zmian.
  - `Duplicate_seq_in_conversation_is_rejected`: dwie wiadomości o tym samym `Seq` → `DbUpdateException`.
  - `Role_is_stored_as_text`: SQL zwraca `"ToolCall"`.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj encje, konfigurację i migrację `AddConversations`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj rozmowy z agentem i wiadomości"`

---

### Task 6: Dziennik audytu tylko do dopisywania (UC-28)

**Files:**
- Create: `src/Hr.Core/Domain/AuditLog.cs`, `Auditing/IAuditable.cs`, `AuditSaveChangesInterceptor.cs`, `IAuditWriter.cs`
- Modify: `HrDbContext.cs`, migracja `AddAuditLog` (zawiera wyzwalacz `TR_AuditLog_AppendOnly`)
- Modify: `MsSqlFixture.NewContextAsync` (rejestruje interceptor z `actor` i `FakeClock`)
- Test: `tests/Hr.Core.IntegrationTests/AuditTests.cs`

**Interfaces:**
- Consumes: `ICurrentUser`, `IClock` (Task 1), encje z Task 2–4.
- Produces:
  - `AuditLog { long Id; Guid? ActorUserId; DateTime AtUtc; string EventType; string EntityType; string EntityId; string? BeforeJson; string? AfterJson }`
  - `public interface IAuditable { }` — znacznik implementowany przez `Team`, `Employee`, `LeaveRequest`, `WorkEntryRecord`, `PayrollChange`.
  - `AuditSaveChangesInterceptor(ICurrentUser actor, IClock clock) : SaveChangesInterceptor` — dla każdej zmienionej encji `IAuditable` dodaje wpis w tej samej transakcji (`EventType` = `Added|Modified|Deleted`, `Before/AfterJson` jako JSON zmienionych właściwości).
  - `public interface IAuditWriter { void Record(string eventType, string entityType, string entityId, string? afterJson = null); }` — dodaje `AuditLog` do bieżącego kontekstu (zapis przy najbliższym `SaveChanges`); implementacja na `HrDbContext`.
  - Wyzwalacz `INSTEAD OF UPDATE, DELETE` na `AuditLog` rzucający błąd `THROW 50001` („AuditLog is append-only”). Zmiana względem ADR-13 (uprawnienia użytkownika bazy): wyzwalacz działa niezależnie od tego, jakim użytkownikiem łączy się aplikacja, i jest testowalny; uprawnienia INSERT/SELECT dla użytkownika aplikacji dochodzą w planie wdrożeniowym.

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `UC28_Modifying_employee_writes_audit_with_before_and_after`: aktor `TestUser` z `UserId = u1`; zmiana `MonthlySalary` 8000→8500 daje jeden wpis `Modified`, `EntityType="Employee"`, `ActorUserId == u1`, `AtUtc` z `FakeClock`, `BeforeJson` zawiera `8000`, `AfterJson` zawiera `8500`.
  - `UC28_Adding_leave_request_writes_added_entry`: wpis `Added` z `AfterJson`.
  - `AuditLog_update_is_rejected_by_database`: surowy `UPDATE AuditLog SET EventType='x'` → `SqlException` z komunikatem „append-only”.
  - `AuditLog_delete_is_rejected_by_database`: surowy `DELETE FROM AuditLog` → `SqlException`.
  - `Failed_save_leaves_no_audit_rows`: w jednym `SaveChanges` zmień `Employee` i dodaj `LeaveRequest` łamiący `CK_LeaveRequest_Period` → `DbUpdateException`, a `COUNT(*)` z `AuditLog` wynosi `0`.
  - `AuditWriter_records_custom_event`: `Record("ConversationRead","Conversation","<id>")` + `SaveChanges` → wpis z tym typem zdarzenia.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj encję, interceptor, `IAuditWriter` i migrację z wyzwalaczem.** JSON przez `System.Text.Json`; pomijaj `AuditLog` samego siebie.
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj dziennik audytu tylko do dopisywania z interceptorem"`

---

### Task 7: Zakres danych pracowników (UC-03)

**Files:**
- Create: `src/Hr.Core/Security/DataScope.cs`
- Test: `tests/Hr.Core.IntegrationTests/DataScopeTests.cs`

**Interfaces:**
- Consumes: `ICurrentUser`, `Roles`, `Employee`, `Team`, `IEmployeeOwned`, `HrDbContext`.
- Produces:
  - `public static class DataScope { public static IQueryable<Guid> VisibleEmployeeIds(this HrDbContext db, ICurrentUser user); public static IQueryable<T> Visible<T>(this IQueryable<T> source, HrDbContext db, ICurrentUser user) where T : IEmployeeOwned; public static IQueryable<Employee> VisibleEmployees(this HrDbContext db, ICurrentUser user); }`
  - Reguły: `HR`/`HRApprover` → wszyscy; `Manager` → pracownicy zespołów, w których `Team.ManagerEmployeeId == user.EmployeeId`, oraz on sam; `Employee` → tylko on sam; brak `EmployeeId` albo brak rozpoznanej roli → pusty zbiór. Role sumują się. Filtr tłumaczy się na SQL (podzapytanie), nie filtruje w pamięci.

Dane testowe (budowane w teście): zespół A (kierownik Ewa) z Anną i Janem, zespół B z Tomaszem, zespół Kadry z Piotrem i Marią; po jednym `LeaveRequest` i `WorkEntryRecord` na osobę.

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `UC03_Employee_sees_only_own_leave`: Anna → jej 1 wniosek; Tomasz → tylko własny, nie Anny.
  - `UC03_Employee_cannot_load_other_employees_leave_by_id`: `Visible()` + `FirstOrDefault(x => x.Id == annasId)` dla Tomasza → `null`.
  - `UC03_Manager_sees_own_team_but_not_other_team`: Ewa → wnioski Anny, Jana i własny; nie widzi wniosku Tomasza.
  - `UC03_Manager_of_other_team_does_not_see_first_team`: kierownik zespołu B (dodatkowy użytkownik w teście) nie widzi Anny ani Jana.
  - `UC04_HR_and_approver_see_everyone`: Piotr i Maria widzą wszystkie 6+ wniosków.
  - `UC03_User_without_employee_link_sees_nothing`: `TestUser(userId, employeeId: null, Roles.HR)` → pusty zbiór (fail closed).
  - `UC03_Unknown_role_sees_nothing`: rola `"Intern"` → pusty zbiór.
  - `UC03_Scope_is_translated_to_sql`: `ToQueryString()` dla `Visible()` zawiera `WHERE` z podzapytaniem (a nie samo `SELECT * FROM`), a liczba zwróconych wierszy zgadza się z oczekiwaną.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `DataScope`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj filtrowanie zakresu danych wg roli"`

---

### Task 8: Dostęp do rozmów i odczyt przez kadry (UC-30, UC-31)

**Files:**
- Create: `src/Hr.Core/Security/ConversationAccess.cs`, `ConversationReader.cs`
- Test: `tests/Hr.Core.IntegrationTests/ConversationAccessTests.cs`

**Interfaces:**
- Consumes: `Conversation`, `Message`, `ICurrentUser`, `IAuditWriter` (Task 6), `HrDbContext`.
- Produces:
  - `public static class ConversationAccess { public static IQueryable<Conversation> Visible(this IQueryable<Conversation> source, ICurrentUser user); }` — autor widzi swoje; `HR`/`HRApprover` widzą wszystkie; kierownik i pracownik tylko własne; brak `UserId` → pusty zbiór.
  - `public sealed class ConversationReader { public ConversationReader(HrDbContext db, IAuditWriter audit, ICurrentUser user); public Task<Conversation?> OpenAsync(Guid conversationId, CancellationToken ct = default); }` — zwraca rozmowę z wiadomościami albo `null`; jeśli czytelnik nie jest autorem, zapisuje w audycie zdarzenie `ConversationRead` (`EntityType="Conversation"`, `EntityId` = id rozmowy) w tym samym `SaveChanges`.

- [ ] **Step 1: Napisz testy, które mają się nie udać.**
  - `UC30_Author_reads_own_conversation_without_audit`: Anna otwiera swoją rozmowę → zwrócona, `COUNT` wpisów `ConversationRead` = 0.
  - `UC30_Employee_cannot_open_other_employees_conversation`: Tomasz otwiera rozmowę Anny → `null`.
  - `Q08_Manager_cannot_open_team_members_conversation`: Ewa otwiera rozmowę Anny → `null`.
  - `UC31_HR_opens_any_conversation_and_read_is_audited`: Piotr otwiera rozmowę Anny → zwrócona z wiadomościami w kolejności `Seq`, wpis `ConversationRead` z `ActorUserId == Piotr`.
  - `UC31_Approver_can_read_too`: Maria → zwrócona i audytowana.
  - `UC30_User_without_id_sees_no_conversations`: `TestUser(null, null, Roles.HR)` → `null` i brak audytu.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `ConversationAccess` i `ConversationReader`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj dostęp do rozmów z audytem odczytu przez kadry"`

---

### Task 9: Konta demo i seed (UC-01, UC-29)

**Files:**
- Create: `src/Hr.Core/Seeding/HrIdentityOptions.cs`, `DemoSeeder.cs`
- Test: `tests/Hr.Core.IntegrationTests/DemoSeederTests.cs`

**Interfaces:**
- Consumes: `HrDbContext`, `ApplicationUser`, `Roles`, `UserManager<ApplicationUser>`, `RoleManager<IdentityRole<Guid>>`.
- Produces:
  - `public static class HrIdentityOptions { public static void Apply(IdentityOptions options); }` — polityka haseł i blokada konta z Global Constraints.
  - `public static class DemoSeeder { public static Task SeedAsync(IServiceProvider services, CancellationToken ct = default); }` — tworzy role, zespoły, pracowników i 6 kont z `docs/arc42/07-widok-wdrozenia.md`; idempotentny (po loginie).
  - Dane: zespoły „Zespół Ewy” (kierownik Ewa), „Zespół B”, „Kadry”; pracownicy i data zatrudnienia: Anna Nowak 2019-03-01, Jan Kowalski 2014-05-01, Tomasz Lis 2022-09-01, Ewa Wiśniewska 2012-01-02, Piotr Zieliński 2018-06-01, Maria Kamińska 2015-02-01; wynagrodzenia 7200, 8000, 6800, 11000, 9000, 12000. Loginy `imie.nazwisko`, hasła `imienazwisko123` (małe litery, bez znaków diakrytycznych), role: Anna, Jan, Tomasz → `Employee`; Ewa → `Manager`; Piotr → `HR`; Maria → `HRApprover`.

- [ ] **Step 1: Napisz testy, które mają się nie udać.** Pomocnik buduje `ServiceCollection` z `AddDbContext<HrDbContext>` (świeża baza z fixture), `AddIdentityCore<ApplicationUser>(HrIdentityOptions.Apply).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<HrDbContext>()`.
  - `UC29_Seed_creates_six_accounts_with_roles`: po `SeedAsync` jest 6 użytkowników, a `anna.nowak` ma rolę `Employee`, `ewa.wisniewska` `Manager`, `piotr.zielinski` `HR`, `maria.kaminska` `HRApprover`.
  - `UC01_All_demo_passwords_are_valid`: dla każdej pary login/hasło z tabeli `CheckPasswordAsync` zwraca `true`; złe hasło `false`.
  - `UC29_Seed_is_idempotent`: dwa wywołania → nadal 6 użytkowników i 6 pracowników.
  - `UC03_Seed_places_tomasz_in_other_team_than_anna`: `Tomasz.TeamId != Anna.TeamId`, a `Team.ManagerEmployeeId` zespołu Ewy to Ewa.
  - `UC05_Jan_has_26_days_and_Anna_20`: z `LeaveEntitlement.YearsOfService(HireDate, 2026-10-16)` i `AnnualDays` wychodzi 26 dla Jana i 20 dla Anny.
  - `UC02_Account_locks_after_five_failed_attempts`: 5 wywołań `UserManager.AccessFailedAsync(user)` → `IsLockedOutAsync(user) == true`.
- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `HrIdentityOptions` i `DemoSeeder`.**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj konta demo i idempotentny seed"`

---

### Task 10: Dokumentacja i weryfikacja

**Files:**
- Modify: `docs/arc42/05-widok-blokow.md` (sekcja 5.5: pełny diagram ER encji z tego planu)
- Modify: `docs/arc42/09-decyzje-architektoniczne.md` (ADR-13: wyzwalacz zamiast uprawnień; ADR-23: Testcontainers i fixture MSSQL; dopisz status „zrealizowane w planie 2” przy ADR-08, 12, 13)
- Modify: `docs/arc42/10-jakosc.md` (tabela: testy integracyjne na MSSQL w kontenerze)

**Interfaces:** brak nowych typów.

- [ ] **Step 1: Zaktualizuj dokumentację** zgodnie z listą plików; ADR-13 ma wprost opisać wyzwalacz `TR_AuditLog_AppendOnly` i fakt, że uprawnienia użytkownika bazy dochodzą przy wdrożeniu.
- [ ] **Step 2: Uruchom pełną weryfikację**

Run: `dotnet build -warnaserror && dotnet test`
Expected: wszystkie testy PASS (silnik, architektura, integracyjne), 0 ostrzeżeń.

- [ ] **Step 3: Commit** — `git commit -m "Zaktualizuj dokumentację o warstwę danych"`
- [ ] **Step 4: Przygotuj PR.** Po końcowym przeglądzie gałęzi (zgodnie z procesem) wypchnij `feature/plan2-core-data` i otwórz PR do `main` (`gh pr create`) z opisem: zakres, wynik testów, decyzje i odroczone drobiazgi.
