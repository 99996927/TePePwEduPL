# Plan 1: Szkielet rozwiązania i silnik reguł `Hr.Rules` — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zbudować szkielet rozwiązania .NET oraz kompletny, deterministyczny silnik reguł `Hr.Rules` (kalendarz, urlop, wymiar czasu pracy, nadgodziny, odpoczynki, średnia tygodniowa, wstępne naliczenie brutto) pokryty testami jednostkowymi.

**Architecture:** Biblioteka klas bez zależności i bez I/O (czyste funkcje, `decimal`, dane wejściowe niemutowalne). Każdy wynik niesie identyfikator reguły (`KP-…`) z odwołaniem do artykułu. Zgodność z granicami projektów pilnuje test architektury (NetArchTest).

**Tech Stack:** .NET 9 (SDK 9.0.317 na hoście), C#, xUnit, NetArchTest.Rules.

**Spec:** `docs/arc42/README.md` (sekcje 3.5, 5.3, 8.3, 9: ADR-09, ADR-17, ADR-18). Ten plan jest pierwszym z kilku (kolejne: Core + baza + Identity, Api + OpenAPI, Agent, Angular, Docker + E2E).

## Global Constraints

- Docelowa platforma: `net9.0`, `Nullable` włączone, ostrzeżenia traktowane jako błędy.
- Silnik używa wyłącznie `decimal` dla godzin i kwot, bez `double` i `float`.
- Silnik nie czyta zegara (`DateTime.Now`, `UtcNow`), nie robi I/O, nie ma referencji do pakietów NuGet (poza testami).
- Czas pracy: pełny etat, poniedziałek–piątek, 8 h na dobę, 40 h tygodniowo (ADR-18). Dni wolne: niedziele i święta z ustawy o dniach wolnych od pracy, Dz.U. 2025 poz. 296, w tym 24 grudnia od 2025 r.
- Identyfikatory reguł: `KP-129-1`, `KP-130-1`, `KP-131-1`, `KP-132-1`, `KP-133-1`, `KP-151-1`, `KP-151-1-1`, `KP-151-3`, `KP-154-1`.
- Zaokrąglanie kwot do 2 miejsc: `MidpointRounding.AwayFromZero`.
- Nazwy klas testowych odpowiadają przypadkom użycia z `docs/arc42/03-kontekst-i-zakres.md` (np. `UC14_...`).
- Komunikaty commitów po polsku, w formie rozkazującej, bez emoji.

## Review Focus

1. **Praca przez północ** (22:00–06:00): wpis przypisany do daty rozpoczęcia, godziny nocne rozpoznane jako 100% (test w Task 6).
2. **Przerwa dłuższa niż wpis lub koniec przed początkiem:** konstruktor `WorkEntry` rzuca `ArgumentException` (Task 3).
3. **Święto w sobotę** (26.12.2026) obniża wymiar czasu pracy o 8 h, święto w niedzielę (1.11.2026) nie obniża (Task 5).
4. **Rok przestępny i Wielkanoc:** 29.02.2028 jest dniem roboczym, Wielkanoc 2024 to 31.03 (Task 2).
5. **Wpisy nieposortowane lub nakładające się:** odpoczynki sortują wpisy, nakładanie się rzuca `ArgumentException` (Task 7).

---

## Struktura plików

| Ścieżka | Odpowiedzialność |
|---|---|
| `Hr.sln`, `global.json`, `Directory.Build.props`, `.gitignore` | Rozwiązanie, wersja SDK, wspólne ustawienia, wyjątki git |
| `src/Hr.Rules/Calendar/PolishHolidays.cs`, `WorkCalendar.cs` | Święta i dni robocze |
| `src/Hr.Rules/Time/WorkEntry.cs`, `WorkingTimeNorm.cs` | Wpis czasu pracy, wymiar czasu pracy (art. 130) |
| `src/Hr.Rules/Leave/LeaveEntitlement.cs`, `LeaveRequestRules.cs` | Wymiar urlopu i ocena wniosku |
| `src/Hr.Rules/Overtime/OvertimeCalculator.cs`, `NightWindow.cs`, `OvertimeLimit.cs` | Nadgodziny, dodatki, limit roczny |
| `src/Hr.Rules/Rest/RestRules.cs`, `WeeklyAverageRules.cs` | Odpoczynki i średnia tygodniowa |
| `src/Hr.Rules/Payroll/PayrollCalculator.cs` | Wstępne naliczenie brutto |
| `tests/Hr.Rules.Tests/…` | Testy jednostkowe, lustrzane do `src` |
| `tests/Hr.Architecture.Tests/RulesIsolationTests.cs` | Test izolacji silnika |

---

### Task 1: Szkielet rozwiązania i test izolacji silnika

**Files:**
- Create: `Hr.sln`, `global.json`, `Directory.Build.props`, `.gitignore`
- Create: `src/Hr.Rules/Hr.Rules.csproj`, `tests/Hr.Rules.Tests/Hr.Rules.Tests.csproj`, `tests/Hr.Architecture.Tests/Hr.Architecture.Tests.csproj`
- Test: `tests/Hr.Architecture.Tests/RulesIsolationTests.cs`

**Interfaces:**
- Produces: przestrzeń nazw główna `Hr.Rules`, asembly `Hr.Rules`. Później używane przez `Hr.Core`.

- [ ] **Step 1: Utwórz szkielet**

`dotnet new sln -n Hr`, `dotnet new classlib -n Hr.Rules -o src/Hr.Rules -f net9.0`, dwa projekty `xunit` w `tests/`, referencje testów do `Hr.Rules`, pakiet `NetArchTest.Rules` w testach architektury. `global.json` przypina SDK 9.0.317 z `rollForward: latestFeature`. `Directory.Build.props`: `Nullable=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`. Usuń `Class1.cs` i przykładowe testy. `.gitignore`: wzorzec .NET plus `.env`, `bin/`, `obj/`.

- [ ] **Step 2: Napisz test, który ma się nie udać**

`RulesIsolationTests.Rules_does_not_depend_on_io_data_or_http()` używa `Types.InAssembly(typeof(Hr.Rules.AssemblyMarker).Assembly).ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "System.Net.Http", "System.Data", "System.IO", "Anthropic")` i sprawdza `result.IsSuccessful`.

- [ ] **Step 3: Uruchom, spodziewana porażka kompilacji**

Run: `dotnet test tests/Hr.Architecture.Tests`
Expected: błąd kompilacji (brak `AssemblyMarker`).

- [ ] **Step 4: Dodaj `public sealed class AssemblyMarker` w `src/Hr.Rules/AssemblyMarker.cs`**

- [ ] **Step 5: Uruchom testy**

Run: `dotnet build -warnaserror && dotnet test`
Expected: PASS (1 test).

- [ ] **Step 6: Commit** — `git add -A && git commit -m "Dodaj szkielet rozwiązania i test izolacji silnika reguł"`

---

### Task 2: Kalendarz — święta i dni robocze

**Files:**
- Create: `src/Hr.Rules/Calendar/PolishHolidays.cs`, `src/Hr.Rules/Calendar/WorkCalendar.cs`
- Test: `tests/Hr.Rules.Tests/Calendar/WorkCalendarTests.cs`

**Interfaces:**
- Produces:
  - `public static class PolishHolidays { public static IReadOnlyList<DateOnly> ForYear(int year); }` — posortowane daty świąt, bez niedziel jako takich, ale ze świętami przypadającymi w niedzielę. 24.12 uwzględniane od 2025.
  - `public sealed class WorkCalendar { public bool IsHoliday(DateOnly d); public bool IsWorkingDay(DateOnly d); public int CountWorkingDays(DateOnly from, DateOnly toInclusive); }`

- [ ] **Step 1: Napisz testy, które mają się nie udać**

```csharp
[Fact] void ForYear_2026_contains_movable_holidays() {
  var h = PolishHolidays.ForYear(2026);
  Assert.Contains(new DateOnly(2026,4,5), h);   // Wielkanoc
  Assert.Contains(new DateOnly(2026,4,6), h);   // Poniedziałek Wielkanocny
  Assert.Contains(new DateOnly(2026,5,24), h);  // Zielone Świątki
  Assert.Contains(new DateOnly(2026,6,4), h);   // Boże Ciało
  Assert.Equal(14, h.Count);   // 9 stałych + 24.12 + 4 ruchome
}
[Fact] void ForYear_2024_Easter_is_31_March_and_no_Christmas_Eve() {
  var h = PolishHolidays.ForYear(2024);
  Assert.Contains(new DateOnly(2024,3,31), h);
  Assert.DoesNotContain(new DateOnly(2024,12,24), h);
}
[Fact] void ForYear_2025_contains_Christmas_Eve() => Assert.Contains(new DateOnly(2025,12,24), PolishHolidays.ForYear(2025));
[Fact] void CountWorkingDays_week_without_holiday_is_5() => Assert.Equal(5, new WorkCalendar().CountWorkingDays(new(2026,10,12), new(2026,10,16)));
[Fact] void CountWorkingDays_week_with_11_Nov_is_4() => Assert.Equal(4, new WorkCalendar().CountWorkingDays(new(2026,11,9), new(2026,11,13)));
[Fact] void IsWorkingDay_29_Feb_2028_leap_year() => Assert.True(new WorkCalendar().IsWorkingDay(new(2028,2,29)));
[Fact] void IsWorkingDay_false_on_weekend_and_holiday() { var c=new WorkCalendar(); Assert.False(c.IsWorkingDay(new(2026,10,17))); Assert.False(c.IsWorkingDay(new(2026,10,18))); Assert.False(c.IsWorkingDay(new(2026,12,24))); }
```

- [ ] **Step 2: Uruchom — spodziewane FAIL (brak typów)**

Run: `dotnet test tests/Hr.Rules.Tests --filter WorkCalendarTests`

- [ ] **Step 3: Zaimplementuj `PolishHolidays.ForYear` i `WorkCalendar`**

Święta stałe: 1.01, 6.01, 1.05, 3.05, 15.08, 1.11, 11.11, 25.12, 26.12, plus 24.12 od 2025. Ruchome liczone od Wielkanocy (algorytm Meeusa/Jonesa/Butchera dla kalendarza gregoriańskiego): Wielkanoc, +1, +49, +60 dni. `WorkCalendar` korzysta z `PolishHolidays` (cache wyników rocznych w słowniku) i jest czysty. Źródło: Dz.U. 2025 poz. 296, art. 1.

- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj kalendarz dni wolnych i dni roboczych"`

---

### Task 3: Wpis czasu pracy

**Files:**
- Create: `src/Hr.Rules/Time/WorkEntry.cs`
- Test: `tests/Hr.Rules.Tests/Time/WorkEntryTests.cs`

**Interfaces:**
- Produces: `public sealed record WorkEntry { public WorkEntry(DateTime start, DateTime end, TimeSpan breakTime); DateTime Start; DateTime End; TimeSpan Break; decimal WorkedHours { get; } }` — `WorkedHours` to `(End - Start - Break)` w godzinach jako `decimal`. Czas lokalny, bez przeliczeń stref.

- [ ] **Step 1: Napisz testy, które mają się nie udać**

```csharp
[Fact] void WorkedHours_subtracts_break() => Assert.Equal(8.0m, new WorkEntry(new(2026,10,12,8,0,0), new(2026,10,12,17,0,0), TimeSpan.FromHours(1)).WorkedHours);
[Fact] void WorkedHours_supports_minutes() => Assert.Equal(9.6m, new WorkEntry(new(2026,10,12,8,0,0), new(2026,10,12,17,36,0), TimeSpan.Zero).WorkedHours);
[Fact] void Throws_when_end_not_after_start() => Assert.Throws<ArgumentException>(() => new WorkEntry(new(2026,10,12,8,0,0), new(2026,10,12,8,0,0), TimeSpan.Zero));
[Fact] void Throws_when_break_longer_than_entry() => Assert.Throws<ArgumentException>(() => new WorkEntry(new(2026,10,12,8,0,0), new(2026,10,12,9,0,0), TimeSpan.FromHours(2)));
[Fact] void Throws_when_break_negative() => Assert.Throws<ArgumentException>(() => new WorkEntry(new(2026,10,12,8,0,0), new(2026,10,12,9,0,0), TimeSpan.FromMinutes(-1)));
[Fact] void Supports_entry_across_midnight() => Assert.Equal(8.0m, new WorkEntry(new(2026,10,12,22,0,0), new(2026,10,13,6,0,0), TimeSpan.Zero).WorkedHours);
```

- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `WorkEntry`** z walidacją w konstruktorze (`End > Start`, `0 <= Break < End - Start`).
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj wpis czasu pracy z walidacją"`

---

### Task 4: Urlop (UC-05, UC-06, UC-07)

**Files:**
- Create: `src/Hr.Rules/Leave/LeaveEntitlement.cs`, `src/Hr.Rules/Leave/LeaveRequestRules.cs`
- Test: `tests/Hr.Rules.Tests/Leave/LeaveRulesTests.cs`

**Interfaces:**
- Consumes: `WorkCalendar` (Task 2).
- Produces:
  - `public static class LeaveEntitlement { public static int YearsOfService(DateOnly hireDate, DateOnly asOf); public static int AnnualDays(int yearsOfService); }` — 20 dni poniżej 10 lat, 26 od 10 lat (art. 154 § 1). Staż liczony jako pełne lata od daty zatrudnienia; okresy nauki (art. 154¹) poza zakresem.
  - `public sealed record LeaveCheck(int Days, bool Sufficient, int RemainingAfter, string RuleId);`
  - `public static class LeaveRequestRules { public static LeaveCheck Evaluate(int entitlementDays, int usedDays, DateOnly from, DateOnly to, WorkCalendar calendar); }` — `Days` to dni robocze w przedziale włącznie, `RuleId` = `KP-154-1`, `ArgumentException` gdy `to < from`.

- [ ] **Step 1: Napisz testy, które mają się nie udać**

```csharp
[Theory][InlineData(0,20)][InlineData(9,20)][InlineData(10,26)][InlineData(25,26)] void AnnualDays_by_service(int years,int days)=>Assert.Equal(days, LeaveEntitlement.AnnualDays(years));
[Fact] void YearsOfService_counts_full_years() { Assert.Equal(9, LeaveEntitlement.YearsOfService(new(2016,10,17), new(2026,10,16))); Assert.Equal(10, LeaveEntitlement.YearsOfService(new(2016,10,16), new(2026,10,16))); }
[Fact] void Evaluate_week_is_5_days_and_sufficient() { var r=LeaveRequestRules.Evaluate(20,10,new(2026,10,12),new(2026,10,16),new WorkCalendar()); Assert.Equal(5,r.Days); Assert.True(r.Sufficient); Assert.Equal(5,r.RemainingAfter); Assert.Equal("KP-154-1",r.RuleId); }
[Fact] void Evaluate_over_balance_is_not_sufficient() { var r=LeaveRequestRules.Evaluate(20,18,new(2026,10,12),new(2026,10,16),new WorkCalendar()); Assert.False(r.Sufficient); Assert.Equal(-3,r.RemainingAfter); }
[Fact] void Evaluate_skips_weekend_and_holiday() => Assert.Equal(4, LeaveRequestRules.Evaluate(20,0,new(2026,11,9),new(2026,11,13),new WorkCalendar()).Days);
[Fact] void Evaluate_throws_when_to_before_from() => Assert.Throws<ArgumentException>(() => LeaveRequestRules.Evaluate(20,0,new(2026,10,16),new(2026,10,12),new WorkCalendar()));
```

- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `LeaveEntitlement` i `LeaveRequestRules`**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj reguły wymiaru urlopu i oceny wniosku urlopowego"`

---

### Task 5: Wymiar czasu pracy w okresie rozliczeniowym (art. 130)

**Files:**
- Create: `src/Hr.Rules/Time/WorkingTimeNorm.cs`
- Test: `tests/Hr.Rules.Tests/Time/WorkingTimeNormTests.cs`

**Interfaces:**
- Consumes: `WorkCalendar`, `PolishHolidays` (Task 2).
- Produces: `public static class WorkingTimeNorm { public static decimal HoursForPeriod(DateOnly start, DateOnly endInclusive, WorkCalendar calendar, decimal absenceHours = 0m); }` — reguła `KP-130-1`: 40 h × pełne tygodnie od początku okresu + 8 h × dni pon.–pt. pozostałe do końca okresu − 8 h za każde święto przypadające w dniu innym niż niedziela − godziny usprawiedliwionej nieobecności (art. 130 § 1–3). `ArgumentException` gdy `endInclusive < start` lub `absenceHours < 0`.

- [ ] **Step 1: Napisz testy, które mają się nie udać**

```csharp
[Fact] void October_2026_is_176h() => Assert.Equal(176m, WorkingTimeNorm.HoursForPeriod(new(2026,10,1), new(2026,10,31), new WorkCalendar()));
[Fact] void November_2026_sunday_holiday_does_not_reduce_but_11_Nov_does() => Assert.Equal(160m, WorkingTimeNorm.HoursForPeriod(new(2026,11,1), new(2026,11,30), new WorkCalendar()));
[Fact] void December_2026_saturday_holiday_26_Dec_reduces_norm() => Assert.Equal(160m, WorkingTimeNorm.HoursForPeriod(new(2026,12,1), new(2026,12,31), new WorkCalendar()));
[Fact] void Absence_hours_reduce_norm() => Assert.Equal(168m, WorkingTimeNorm.HoursForPeriod(new(2026,10,1), new(2026,10,31), new WorkCalendar(), 8m));
[Fact] void Throws_when_period_reversed() => Assert.Throws<ArgumentException>(() => WorkingTimeNorm.HoursForPeriod(new(2026,10,31), new(2026,10,1), new WorkCalendar()));
```

- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `HoursForPeriod`** według wzoru z art. 130 (pełne tygodnie liczone od pierwszego dnia okresu, reszta dni pon.–pt. po 8 h, odjęcie świąt nieniedzielnych i nieobecności).
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj wymiar czasu pracy w okresie rozliczeniowym (art. 130)"`

---

### Task 6: Nadgodziny i dodatki (UC-10, UC-16)

**Files:**
- Create: `src/Hr.Rules/Overtime/NightWindow.cs`, `OvertimeCalculator.cs`, `OvertimeLimit.cs`
- Test: `tests/Hr.Rules.Tests/Overtime/OvertimeTests.cs`

**Interfaces:**
- Consumes: `WorkEntry` (Task 3), `WorkCalendar` (Task 2).
- Produces:
  - `public sealed record NightWindow(TimeOnly Start, TimeOnly End) { public static NightWindow Default { get; } }` — domyślnie 21:00–05:00 (8 h w ustawowym oknie 21:00–07:00, art. 151⁷; konkretne godziny ustala pracodawca, tu parametr).
  - `public sealed record OvertimeResult(decimal Hours50, decimal Hours100, IReadOnlyList<string> RuleIds) { public decimal Total => Hours50 + Hours100; }`
  - `public static class OvertimeCalculator { public static OvertimeResult Calculate(IReadOnlyList<WorkEntry> entries, WorkCalendar calendar, NightWindow night); }`
  - `public static class OvertimeLimit { public const decimal AnnualHours = 150m; public static bool Exceeded(decimal overtimeHoursInYear); }` — `KP-151-3`, przekroczenie oznacza `> 150`.

Zasady (uproszczenie opisane w ADR-22, Task 10): dzień to data rozpoczęcia wpisu. W dzień roboczy nadgodzinami są godziny powyżej 8 h w dobie, liczone jako ostatnie godziny pracy tego dnia (analiza wpisów od końca, każdy wpis kotwiczony na końcu). W dni nieroboczne (sobota, niedziela, święto) cała praca jest nadgodzinami. Dodatek 100%: nadgodziny w porze nocnej oraz w niedzielę i święto (art. 151¹ § 1 pkt 1), pozostałe 50%. Przekroczenie średniej normy okresu (art. 151¹ § 2) poza zakresem tego planu.

- [ ] **Step 1: Napisz testy, które mają się nie udać**

```csharp
static OvertimeResult Calc(params WorkEntry[] e) => OvertimeCalculator.Calculate(e, new WorkCalendar(), NightWindow.Default);
static WorkEntry E(int d,int h1,int m1,int d2,int h2,int m2,int brk=0)=>new(new(2026,10,d,h1,m1,0),new(2026,10,d2,h2,m2,0),TimeSpan.FromMinutes(brk));
[Fact] void Within_norm_is_zero() { var r=Calc(E(12,8,0,12,17,0,60)); Assert.Equal(0m,r.Total); }
[Fact] void Weekday_two_hours_over_is_50() { var r=Calc(E(12,8,0,12,18,0)); Assert.Equal(2m,r.Hours50); Assert.Equal(0m,r.Hours100); }
[Fact] void Weekday_late_hours_in_night_are_100() { var r=Calc(E(12,10,0,12,22,0)); Assert.Equal(3m,r.Hours50); Assert.Equal(1m,r.Hours100); }
[Fact] void Saturday_work_is_50() { var r=Calc(E(17,8,0,17,12,0)); Assert.Equal(4m,r.Hours50); }
[Fact] void Sunday_work_is_100() { var r=Calc(E(18,8,0,18,12,0)); Assert.Equal(4m,r.Hours100); }
[Fact] void Holiday_work_is_100() { var r=OvertimeCalculator.Calculate(new[]{ new WorkEntry(new(2026,11,11,8,0,0), new(2026,11,11,12,0,0), TimeSpan.Zero)}, new WorkCalendar(), NightWindow.Default); Assert.Equal(4m,r.Hours100); }
[Fact] void Night_shift_across_midnight_assigned_to_start_date() { var r=Calc(E(12,22,0,13,6,0)); Assert.Equal(0m,r.Total); }
[Fact] void Two_entries_same_day_sum_over_8h() { var r=Calc(E(12,6,0,12,12,0), E(12,14,0,12,19,0)); Assert.Equal(3m,r.Hours50); }
[Fact] void Result_lists_rule_ids() { var r=Calc(E(12,8,0,12,18,0)); Assert.Contains("KP-151-1", r.RuleIds); Assert.Contains("KP-151-1-1", r.RuleIds); }
[Theory][InlineData(150,false)][InlineData(150.5,true)] void Annual_limit_is_150(decimal h,bool exceeded)=>Assert.Equal(exceeded, OvertimeLimit.Exceeded(h));
[Fact] void Calculate_is_deterministic() { var e=new[]{E(12,8,0,12,18,0)}; Assert.Equal(OvertimeCalculator.Calculate(e,new WorkCalendar(),NightWindow.Default), OvertimeCalculator.Calculate(e,new WorkCalendar(),NightWindow.Default)); }
```

Uwaga do `Calculate_is_deterministic`: rekordy z listą muszą porównywać się po zawartości. Zaimplementuj `Equals` dla `OvertimeResult` porównujący `RuleIds` sekwencyjnie.

- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `NightWindow`, `OvertimeCalculator`, `OvertimeLimit`.** Atrybucja co do minuty (kotwica na końcu wpisu, klasyfikacja 100% wg okna nocnego i dnia wolnego).
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj obliczanie nadgodzin, dodatków i limitu rocznego"`

---

### Task 7: Odpoczynek dobowy i tygodniowy (UC-12, UC-13)

**Files:**
- Create: `src/Hr.Rules/Rest/RestRules.cs`
- Test: `tests/Hr.Rules.Tests/Rest/RestRulesTests.cs`

**Interfaces:**
- Consumes: `WorkEntry` (Task 3).
- Produces:
  - `public sealed record RestViolation(string RuleId, DateTime From, DateTime To, decimal RestHours, decimal RequiredHours);`
  - `public static class RestRules { public static IReadOnlyList<RestViolation> Check(IReadOnlyList<WorkEntry> entries); }` — sortuje wpisy, rzuca `ArgumentException` przy nakładaniu się.
    - `KP-132-1` (11 h): dla kolejnych wpisów o **różnych datach rozpoczęcia** przerwa między końcem poprzedniego a początkiem następnego < 11 h.
    - `KP-133-1` (35 h): dla każdego tygodnia pon.–niedz., w którym zaczyna się co najmniej jeden wpis, najdłuższy nieprzerwany odpoczynek w granicach tygodnia (luki obcięte do granic tygodnia) < 35 h. `RestHours` to najdłuższa luka.

- [ ] **Step 1: Napisz testy, które mają się nie udać**

```csharp
static WorkEntry W(int d,int h1,int d2,int h2)=>new(new(2026,10,d,h1,0,0),new(2026,10,d2,h2,0,0),TimeSpan.Zero);
[Fact] void Daily_rest_10h_is_violation() { var v=RestRules.Check(new[]{W(12,8,12,22), W(13,8,13,16)}); var d=Assert.Single(v,x=>x.RuleId=="KP-132-1"); Assert.Equal(10m,d.RestHours); Assert.Equal(11m,d.RequiredHours); }
[Fact] void Daily_rest_exactly_11h_is_ok() => Assert.DoesNotContain(RestRules.Check(new[]{W(12,8,12,21), W(13,8,13,16)}), x=>x.RuleId=="KP-132-1");
[Fact] void Same_day_entries_are_not_checked_for_daily_rest() => Assert.Empty(RestRules.Check(new[]{W(12,8,12,12), W(12,13,12,17)}));
[Fact] void Night_shift_then_early_next_entry_violates() { var v=RestRules.Check(new[]{W(12,22,13,6), W(13,14,13,20)}); Assert.Contains(v,x=>x.RuleId=="KP-132-1" && x.RestHours==8m); }
[Fact] void Normal_week_has_no_violation() => Assert.Empty(RestRules.Check(Enumerable.Range(12,5).Select(d=>W(d,8,d,16)).ToArray()));
[Fact] void Seven_day_week_violates_weekly_rest() { var v=RestRules.Check(Enumerable.Range(12,7).Select(d=>W(d,8,d,16)).ToArray()); var w=Assert.Single(v,x=>x.RuleId=="KP-133-1"); Assert.Equal(16m,w.RestHours); Assert.Equal(35m,w.RequiredHours); }
[Fact] void Unsorted_input_is_sorted() => Assert.Empty(RestRules.Check(new[]{W(14,8,14,16), W(12,8,12,16), W(13,8,13,16)}));
[Fact] void Overlapping_entries_throw() => Assert.Throws<ArgumentException>(() => RestRules.Check(new[]{W(12,8,12,16), W(12,15,12,20)}));
```

- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `RestRules.Check`**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj wykrywanie naruszeń odpoczynku dobowego i tygodniowego"`

---

### Task 8: Średni tygodniowy czas pracy (UC-14)

**Files:**
- Create: `src/Hr.Rules/Rest/WeeklyAverageRules.cs`
- Test: `tests/Hr.Rules.Tests/Rest/WeeklyAverageRulesTests.cs`

**Interfaces:**
- Consumes: `WorkEntry` (Task 3).
- Produces: `public sealed record AverageHoursResult(decimal AverageWeeklyHours, bool ExceedsLimit, string RuleId);` oraz `public static class WeeklyAverageRules { public static AverageHoursResult Check(IReadOnlyList<WorkEntry> entries, DateOnly start, DateOnly endInclusive); }` — średnia = suma godzin wpisów rozpoczętych w okresie ÷ (liczba dni okresu ÷ 7). `ExceedsLimit` gdy średnia **> 48** (art. 131 § 1, `KP-131-1`). Godziny nadliczbowe wliczają się, bo liczone są wszystkie przepracowane godziny.

- [ ] **Step 1: Napisz testy, które mają się nie udać**

```csharp
static IEnumerable<WorkEntry> Weeks(DateTime monday,int weeks,TimeSpan len)=>Enumerable.Range(0,weeks*7).Select(i=>monday.AddDays(i)).Where(d=>d.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday).Select(d=>new WorkEntry(d.AddHours(8), d.AddHours(8)+len, TimeSpan.Zero));
[Fact] void Average_50h_exceeds() { var r=WeeklyAverageRules.Check(Weeks(new(2026,10,5),4,TimeSpan.FromHours(10)).ToList(), new(2026,10,5), new(2026,11,1)); Assert.Equal(50m,r.AverageWeeklyHours); Assert.True(r.ExceedsLimit); Assert.Equal("KP-131-1", r.RuleId); }
[Fact] void Average_exactly_48h_does_not_exceed() { var r=WeeklyAverageRules.Check(Weeks(new(2026,10,5),4,TimeSpan.FromMinutes(576)).ToList(), new(2026,10,5), new(2026,11,1)); Assert.Equal(48m,r.AverageWeeklyHours); Assert.False(r.ExceedsLimit); }
[Fact] void Standard_40h_week_is_ok() { var r=WeeklyAverageRules.Check(Weeks(new(2026,10,5),4,TimeSpan.FromHours(8)).ToList(), new(2026,10,5), new(2026,11,1)); Assert.Equal(40m,r.AverageWeeklyHours); Assert.False(r.ExceedsLimit); }
[Fact] void Throws_when_period_reversed() => Assert.Throws<ArgumentException>(() => WeeklyAverageRules.Check(Array.Empty<WorkEntry>(), new(2026,11,1), new(2026,10,5)));
```

- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `WeeklyAverageRules.Check`**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj kontrolę średniego tygodniowego czasu pracy (art. 131)"`

---

### Task 9: Wstępne naliczenie brutto (UC-16)

**Files:**
- Create: `src/Hr.Rules/Payroll/PayrollCalculator.cs`
- Test: `tests/Hr.Rules.Tests/Payroll/PayrollCalculatorTests.cs`

**Interfaces:**
- Consumes: wyniki `OvertimeResult` i `WorkingTimeNorm` (jako liczby, bez referencji między plikami).
- Produces:
  - `public sealed record PayrollInput(decimal MonthlyBaseSalary, decimal NormHours, decimal Overtime50Hours, decimal Overtime100Hours);`
  - `public sealed record PayrollResult(decimal BaseSalary, decimal HourlyRate, decimal Allowance50, decimal Allowance100, decimal Gross, IReadOnlyList<string> RuleIds);`
  - `public static class PayrollCalculator { public static PayrollResult Calculate(PayrollInput input); }`
  - Stawka godzinowa = wynagrodzenie podstawowe ÷ `NormHours` (podstawa dodatku uproszczona do wynagrodzenia zasadniczego, art. 151¹ § 3). Dodatek = `Round(godziny × stawka × procent, 2)`, z zaokrągleniem `AwayFromZero`; `Gross = BaseSalary + Allowance50 + Allowance100`. `HourlyRate` w wyniku zaokrąglone do 2 miejsc, ale obliczenia dodatków na wartości niezaokrąglonej. Brak podatków i składek (poza zakresem tego planu). `ArgumentOutOfRangeException` gdy `NormHours <= 0` lub którykolwiek parametr ujemny.

- [ ] **Step 1: Napisz testy, które mają się nie udać**

```csharp
[Fact] void No_overtime_gross_equals_base() { var r=PayrollCalculator.Calculate(new(8000m,176m,0m,0m)); Assert.Equal(8000m,r.Gross); Assert.Equal(0m,r.Allowance50); }
[Fact] void Overtime_allowances_are_computed_from_unrounded_rate() { var r=PayrollCalculator.Calculate(new(8000m,176m,2m,1m)); Assert.Equal(45.45m,r.HourlyRate); Assert.Equal(45.45m,r.Allowance50); Assert.Equal(45.45m,r.Allowance100); Assert.Equal(8090.90m,r.Gross); }
[Fact] void RuleIds_reference_overtime_premiums() { var r=PayrollCalculator.Calculate(new(8000m,176m,2m,1m)); Assert.Contains("KP-151-1-1", r.RuleIds); }
[Theory][InlineData(0)][InlineData(-1)] void Norm_hours_must_be_positive(decimal n)=>Assert.Throws<ArgumentOutOfRangeException>(() => PayrollCalculator.Calculate(new(8000m,n,0m,0m)));
[Fact] void Negative_overtime_throws() => Assert.Throws<ArgumentOutOfRangeException>(() => PayrollCalculator.Calculate(new(8000m,176m,-1m,0m)));
[Fact] void Calculate_is_deterministic() => Assert.Equal(PayrollCalculator.Calculate(new(8000m,176m,2m,1m)), PayrollCalculator.Calculate(new(8000m,176m,2m,1m)));
```

(`PayrollResult` porównuje `RuleIds` po zawartości, jak w Task 6.)

- [ ] **Step 2: Uruchom — FAIL**
- [ ] **Step 3: Zaimplementuj `PayrollCalculator.Calculate`**
- [ ] **Step 4: Uruchom — PASS**
- [ ] **Step 5: Commit** — `git commit -m "Dodaj wstępne naliczenie wynagrodzenia brutto"`

---

### Task 10: Dokumentacja uproszczeń i pełna weryfikacja

**Files:**
- Modify: `docs/arc42/09-decyzje-architektoniczne.md` (dodaj ADR-22 i wiersz w tabeli)
- Modify: `docs/arc42/05-widok-blokow.md:63-67` (sekcja 5.3: dodaj `KP-130-1`)
- Modify: `docs/arc42/08-pojecia-przekrojowe.md` (sekcja 8.3: art. 130 ✔ i źródło listy dni wolnych ✔)

**Interfaces:** brak nowych typów.

- [ ] **Step 1: Dopisz ADR-22 „Uproszczenia silnika reguł (plan 1)”.** Treść: nadgodziny w ujęciu dobowym z atrybucją do ostatnich godzin dnia i do daty rozpoczęcia wpisu, okno nocne 21:00–05:00 jako parametr, odpoczynek tygodniowy liczony w tygodniu pon.–niedz. z lukami obciętymi do granic tygodnia, odpoczynek dobowy sprawdzany tylko między wpisami o różnych datach rozpoczęcia, podstawa dodatku to wynagrodzenie zasadnicze, brak podatków i składek, brak art. 151¹ § 2 (przekroczenie średniej normy), staż bez okresów nauki (art. 154¹). Wymień alternatywy i konsekwencje, tak jak w istniejących ADR.
- [ ] **Step 2: Zaktualizuj sekcje 5.3 i 8.3** zgodnie z listą plików powyżej.
- [ ] **Step 3: Uruchom pełną weryfikację**

Run: `dotnet build -warnaserror && dotnet test`
Expected: wszystkie testy PASS, 0 ostrzeżeń.

- [ ] **Step 4: Commit** — `git commit -m "Opisz uproszczenia silnika reguł jako ADR-22"`
- [ ] **Step 5: Wypchnij gałąź i przygotuj pull request**

Run: `git push -u origin feature/implementation-plan`
Następnie otwórz PR do `main` (`gh pr create`) z opisem: zakres planu 1, lista tasków, odwołanie do ADR-22, wyniki `dotnet test`.
