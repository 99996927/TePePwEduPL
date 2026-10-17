# Plan 6: dane demo — projekt

**Data:** 2026-10-17
**Status:** do przeglądu
**Podstawa:** `docs/arc42` (UC-29; ADR-19, ADR-20, ADR-21; sekcje 7.2, 8, 10 Q-06), plan 5 zmergowany.
**Poza zakresem:** Angular, Docker, testy E2E (kolejne plany).

## 1. Cel i kryteria sukcesu

Po uruchomieniu aplikacji z `Hr:SeedDemoData=true` baza zawiera realistyczne, **spójne z silnikiem reguł** dane syntetyczne,
na których można pokazać system, odpytać agenta i oprzeć testy UC-xx oraz późniejsze E2E.

Kryteria:
- **Daty:** wsteczne, zgodne z kalendarzem (dni robocze, weekendy, święta z `WorkCalendar`).
- **Ludzie:** poprawne konta i dane (staż, zespół, rola), wynagrodzenia z widełek zależnych od stażu, nie losowe liczby.
- **Czas pracy:** typowe zmiany z przerwą, nadgodziny tylko tam, gdzie są zamierzone.
- **Deterministyczność:** to samo ziarno i ta sama kotwica dają identyczne dane.
- **Alerty zamierzone:** każde naruszenie reguł w danych jest scenariuszem stałym, nie przypadkiem generatora.

## 2. Decyzje

| Decyzja | Wybór | Uzasadnienie |
|---|---|---|
| Sposób wstawiania danych | **B:** dane wejściowe wprost do bazy (`HrDbContext`), bez serwisów aplikacyjnych | Czysta architektura, brak podszywania się pod użytkowników i sterowania zegarem serwisów. |
| Naliczenia | Z `PayrollService.RunMonthAsync`, nie z seeda | Seed nie duplikuje logiki silnika; wartości wynikają z prawdziwego kodu. |
| "Dziś" | **Stała kotwica** `2026-10-16` (piątek) w konfiguracji | Pełna deterministyczność dla testów i E2E. |
| Okno ewidencji | od 1.04.2026 do kotwicy (6 pełnych miesięcy plus bieżący w toku) | ADR-21. |
| Okno naliczeń | kwiecień–wrzesień; października brak | Miesiąc bieżący jest otwarty; daje scenariusz dla agenta. |
| Organizacja kodu | **Dwie warstwy** w `Hr.Core/Seeding/Demo/` | Warstwa danych testowalna bez bazy. |

Ustalenia z kodu, które wpływają na projekt:
- `SalaryResolver` bierze stawkę z **zatwierdzonych** zmian płacowych według daty obowiązywania, więc zmiany płacowe muszą być w bazie przed `RunMonthAsync`.
- `RunMonthAsync` wymaga `ICurrentUser` z rolą `HR` lub `HRApprover` i zapisuje `CalculatedAtUtc = clock.UtcNow` (czas uruchomienia seeda) oraz `CalculatedByUserId`.

## 3. Architektura

### 3.1 Warstwa danych (czysta)
Brak `DbContext`, brak `DateTime.Now`. Wejście: `DemoOptions { int Seed; DateOnly AnchorDate }`. Wyjście: `DemoDataSet` z encjami w pamięci
(zespoły, pracownicy, konta do utworzenia, wpisy czasu, wnioski urlopowe, zmiany płacowe, rozmowy z wiadomościami).

| Moduł | Odpowiedzialność |
|---|---|
| `DemoPeople` | ok. 30 osób w 4–5 zespołach; pula polskich imion i nazwisk bez powtórzeń kombinacji; data zatrudnienia i wynagrodzenie z widełek; w każdym zespole jeden `Manager`; 6 stałych kont nazwanych (anna.nowak, jan.kowalski, tomasz.lis, ewa.wisniewska, piotr.zielinski, maria.kaminska). |
| `DemoTimesheets` | dla każdego dnia roboczego z `WorkCalendar` zmiana 8:00–16:00, 7:30–15:30 lub 9:00–17:00, przerwa 30 min, wahanie do 15 min; pomija dni zatwierdzonego urlopu; nadgodziny tylko u wybranych osób i dni. |
| `DemoLeave` | wnioski z liczbą dni liczoną silnikiem (`LeaveRequestRules.Evaluate`), w limicie wymiaru z `LeaveEntitlement`. |
| `DemoPayrollChanges` | zatwierdzone podwyżki historyczne, jedna oczekująca, jedna odrzucona. |
| `DemoConversations` | dwie rozmowy z parami `ToolCall`/`ToolResult` w konwencji z planu 5. |
| `DemoScenarios` | nazwane metody dla scenariuszy stałych (sekcja 4). |

Okno: początek to pierwszy dzień miesiąca sprzed 6 miesięcy względem kotwicy (dla 16.10.2026: 1.04.2026). Scenariusze stałe są
definiowane względem kotwicy (np. "ostatni wtorek poprzedniego miesiąca"), więc działają także przy innej kotwicy.

### 3.2 Warstwa zapisu
`DemoSeeder` (istniejący, przebudowany):
1. odczytuje `DemoOptions`, buduje `DemoDataSet`;
2. zapisuje dane w jednej transakcji, a konta Identity tworzy po zapisie danych;
3. jest idempotentny: istnienie konta `anna.nowak` oznacza "już zasiane";
4. w osobnym zakresie DI z `ICurrentUser` jako HR (Piotr Zieliński) woła `RunMonthAsync` dla kwietnia–września.

### 3.3 Przepływ przy starcie
migracje → `LabourCodeSeeder` → (przy `Hr:SeedDemoData=true`) `DemoSeeder` → `RunMonthAsync` × 6.
Punkt wejścia w `Program.cs` pozostaje; dochodzą `Hr:DemoAnchorDate` i `Hr:DemoSeed`.

## 4. Scenariusze stałe

Testy UC i E2E mogą polegać **wyłącznie** na poniższych scenariuszach.

| Konto | Scenariusz | UC |
|---|---|---|
| anna.nowak | Naruszenie odpoczynku dobowego (<11 h) w ustalonym dniu września i jedno naruszenie tygodniowego (<35 h); wpis do korekty przez kadry. | 11, 12, 13, 26 |
| jan.kowalski | Nadgodziny w kilku miesiącach, w jednym z przekroczoną średnią 48 h; staż 12 lat (26 dni). | 10, 14, 16 |
| tomasz.lis | Poprawny przebieg bez alertów; staż poniżej 10 lat (20 dni); rozmowa z agentem. | 3, 24 |
| Anna (urlopy) | Wniosek oczekujący, zatwierdzony na przyszłość i zrealizowany; saldo spójne z wnioskami. | 5, 6, 8, 9 |
| Jan (urlopy) | Wniosek odrzucony z uzasadnieniem i anulowany. | 8, 9 |
| piotr.zielinski, maria.kaminska | Zmiana płacowa oczekująca (do zatwierdzenia przez Marię), zatwierdzona historycznie i odrzucona; oczekująca nie jest tworzona i zatwierdzana przez tę samą osobę. | 18, 19, 20 |
| wszyscy | Naliczenia za kwiecień–wrzesień; października brak. | 16, 17 |
| agent | Zapisana rozmowa Anny (do odczytu przez kadry z audytem) i rozmowa Tomasza. | 30, 31 |

Święta w oknie: 1.05, 4.06 (Boże Ciało); seed pomija te dni, a poprawność weryfikują testy spójności.

## 5. Testy

### 5.1 Poziom 1: warstwa danych, bez bazy
Niezmienniki dla całego zbioru:
- **Determinizm:** to samo ziarno i kotwica dają identyczny `DemoDataSet`; inne ziarno daje inne dane spełniające te same niezmienniki.
- **Kalendarz:** brak wpisów w weekendy, święta i dni zatwierdzonego urlopu oraz poza oknem.
- **Ewidencja bez naruszeń** poza kontami scenariuszowymi: `RestRules.Check` zwraca 0 naruszeń, `WeeklyAverageRules.Check` nie przekracza 48 h.
- **Urlopy:** suma dni zatwierdzonych w roku nie przekracza `AnnualDays(YearsOfService(...))`; liczba dni zgodna z `LeaveRequestRules.Evaluate`; zatwierdzone wnioski jednej osoby nie nakładają się.
- **Ludzie:** loginy unikalne, hasło zgodne ze wzorem `imięnazwisko123`, jeden `Manager` w zespole, zatrudnienie nie później niż początek okna, wynagrodzenie w widełkach.
- **Zmiany płacowe:** brak samozatwierdzenia, daty obowiązywania w oknie.

Scenariusze stałe: jeden test na wiersz tabeli z sekcji 4 (m.in. dokładnie 1 naruszenie dobowe i 1 tygodniowe u Anny we wskazanych terminach).

### 5.2 Poziom 2: jeden test integracyjny (Testcontainers MSSQL)
- Pełny seed na pustej bazie kończy się bez błędu w kilka sekund (ADR-21).
- Idempotencja: drugie uruchomienie nie zmienia liczby rekordów.
- Naliczenia istnieją dla kwietnia–września dla każdego pracownika, a dla października nie.
- Naliczenie Anny za wrzesień zgadza się z `PayrollCalculator`; zatwierdzona podwyżka wpływa na miesiące od daty obowiązywania, nie na wcześniejsze.
- Tomasz nie widzi ewidencji Anny (UC-03).

### 5.3 Poziom 3: architektura (`Hr.Architecture.Tests`)
Warstwa danych nie referencjonuje `DbContext` ani `DateTime.Now` i nie jest używana poza `DemoSeeder`.

## 6. Konfiguracja

Sekcja `Hr:` (nadpisywalna zmiennymi środowiskowymi, np. `Hr__DemoAnchorDate`):

| Klucz | Domyślnie | Znaczenie |
|---|---|---|
| `Hr:SeedDemoData` | `false` | Włącza cały seed (już istnieje). |
| `Hr:DemoAnchorDate` | `2026-10-16` | Kotwica okna; musi być dniem roboczym. |
| `Hr:DemoSeed` | `20261016` | Ziarno generatora (liczba całkowita). |
| `Hr:DemoProfile` | `Full` | `Full` (domyślnie): pełny zbiór danych demo; `Accounts`: dokładnie 3 zespoły i 6 kont nazwanych, bez danych i bez naliczeń (używany przez `HrApiFactory` w testach API). |

Wartości domyślne są w kodzie (`DemoOptions`), nie w `appsettings.json`. Walidacja przy odczycie w `Program.cs`.

Uwaga (doprecyzowanie ADR-20): w profilu `Full` każdy zespół biznesowy ma dokładnie jednego `Manager`, a zespół `Kadry` (Piotr `HR`, Maria `HRApprover`,
jedna osoba `Employee`) nie ma `Manager`; wnioski urlopowe kierowników i członków `Kadr` rozpatruje Piotr Zieliński.

## 7. Obsługa błędów

- **Niepoprawna konfiguracja** (zła data, kotwica nie jest dniem roboczym, ziarno nie jest liczbą): `InvalidOperationException` z komunikatem po polsku, aplikacja nie startuje.
- **Niespójność generatora:** wykrywają testy z sekcji 5; w runtime tanie asercje na własnych niezmiennikach.
- **Błąd zapisu:** jedna transakcja, więc cofnięcie całości; konta Identity po zapisie danych, a idempotencja po koncie `anna.nowak`, więc częściowy seed nie jest uznany za wykonany i kolejny start powtarza go od zera.
- **`RunMonthAsync` zwraca `Fail`:** wyjątek z komunikatem serwisu; dane zostają, a naliczenia są idempotentne, więc ponowny start je dokończy.
- **Niepusta baza z innymi danymi:** seed nigdy nie nadpisuje ani nie usuwa danych.
- **Logowanie:** początek, koniec, czas i liczby rekordów (Information); nigdy haseł.
- **Bezpieczeństwo:** hasła jawne i przewidywalne (R-03), dlatego seed jest domyślnie wyłączony, a dokumentacja powtarza ostrzeżenie.

## 8. Dokumentacja do aktualizacji

- arc42: 7.2 (konta demo), ADR-19 i ADR-21 (okno i kotwica), README (status: zrealizowany plan 6).
- Nowe ADR: dane wejściowe wprost do bazy z naliczeniami z `PayrollService`; stała kotwica demo.
- README repozytorium: jak włączyć seed i co zawiera.

## 9. Ryzyka

- Rozjazd danych demo z regułami przy zmianie silnika: łagodzą testy spójności poziomu 1.
- Zależność scenariuszy od kalendarza (święta): testy sprawdzają dni wprost z `WorkCalendar`.
- Wydłużenie startu: ok. 30 osób × 6 miesięcy to rzędu kilku tysięcy wpisów; sprawdza to test poziomu 2.
