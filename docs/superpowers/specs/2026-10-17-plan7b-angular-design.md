# Plan 7b: Angular — naliczenia, zmiany płacowe, szkice korekt i audyt — projekt

**Data:** 2026-10-17
**Status:** do przeglądu
**Podstawa:** `docs/arc42` (ADR-15, ADR-24, ADR-25, ADR-27, ADR-28, ADR-29; sekcje 3.5 UC-16 do UC-20, UC-26, UC-28; 7.3, 7.4), plany 1–7a zmergowane, `docs/superpowers/specs/2026-10-17-plan7a-angular-design.md`.
**Poza zakresem tego planu:** agent i historia rozmów (plan 7c), Docker Compose (plan Docker), testy E2E w Playwright (plan E2E).

## 1. Cel i kryteria sukcesu

Dokończenie interfejsu kadrowo-płacowego rozpoczętego w planie 7a: naliczenia miesięczne (podgląd przez pracownika, uruchamianie przez kadry), zmiany płacowe z zatwierdzaniem przez drugą osobę, szkice korekt ewidencji (decyzja kadr) i dziennik audytu (UC-16 do UC-20, część kadrowa UC-26, UC-28). Zasady z 7a obowiązują bez zmian: Angular 22 z Angular Material, generowany klient API, sesja i CSRF, mapowanie błędów, czas lokalny bez strefy dla wpisów, uruchomienie całości skryptem do testów ręcznych przez tunel SSH.

### Kryteria sukcesu
1. `npm run verify` w `web/` przechodzi (z odświeżonym klientem: `api:check`, testy, build produkcyjny bez ostrzeżeń).
2. `dotnet build Hr.sln` oraz cały zestaw testów API i seeda przechodzą, w tym migawka OpenAPI.
3. Po `tools/dev/stop.sh --reset` aplikacja startuje (`tools/dev/run.sh`), a `tools/dev/smoke.sh` przechodzi.
4. Właściciel dostaje listę kroków do ręcznego testu (`docs/testy-reczne/plan-7b.md`) i komendę tunelu SSH.

## 2. Decyzje

| Decyzja | Wybór | Uzasadnienie |
|---|---|---|
| Nazwiska autorów i decydentów | **A:** opcjonalne pole `userId` w `EmployeeDto` (z `ApplicationUser.EmployeeId`); UI buduje mapę `userId` → imię i nazwisko z `EmployeeDirectory` | Zmiany płacowe, szkice i audyt wskazują ludzi przez `userId`, a UI nie miał jak go rozwiązać; mała addytywna zmiana kontraktu zamiast nowego endpointu. |
| Szkice korekt do testów ręcznych | **A:** jeden oczekujący szkic w danych demo | Realistyczny scenariusz UC-26, wykorzysta go też agent w 7c; ekran nie jest pusty. |
| Zakres | Jeden plan 7b | Pięć ekranów na istniejącym szkielecie 7a; bez dalszego podziału. |

## 3. Zmiany w backendzie (addytywne)

1. **`userId` w katalogu pracowników.** `EmployeeSummary` i `EmployeeDto` dostają opcjonalne `UserId` (`Guid?`), wyznaczane złączeniem z kontami (`ApplicationUser.EmployeeId`); `null` dla osoby bez konta. Zakres danych bez zmian: `/api/employees` zwraca tylko pracowników widocznych dla użytkownika, więc osoba widzi `userId` wyłącznie przy pracownikach ze swojego zakresu (kierownik dla zespołu, kadry dla wszystkich). Aktualizacja migawki `docs/openapi/hr-api.json` i wygenerowanego klienta (`npm run api:gen`).
2. **Szkic korekty w danych demo.** `DemoDataSet` dostaje listę `CorrectionDrafts`. Jeden szkic w stanie `Pending` dla wpisu Anny ze scenariuszowego wtorku (domyślnie 29.09.2026, 14:00–22:30, przerwa 30 min): proponowane godziny 12:00–20:30 z przerwą 30 min, więc odpoczynek do środy 7:30 wynosi 11 h i naruszenie `KP-132-1` znika. Autor: `piotr.zielinski`; powód po polsku (naruszenie odpoczynku dobowego, art. 132 § 1); `ConversationId` pusty; znaczniki czasu względem kotwicy (przed kotwicą). Identyfikator deterministyczny (`DemoIds`). `DemoSeeder` zapisuje szkice w tej samej transakcji co pozostałe dane. Istniejące bazy pomijają seed, więc szkic pojawia się po `tools/dev/stop.sh --reset`.

## 4. Role i menu (dodatki do 7a)

| Pozycja menu | Kto | Zawartość | UC |
|---|---|---|---|
| Naliczenia `/naliczenia` | wszyscy | Własne naliczenie wybranego miesiąca; kadry: wybór pracownika, zestawienie miesiąca, uruchomienie naliczenia. | 16, 17 |
| Zmiany płacowe `/zmiany-platowe` | `HR`, `HRApprover` | Lista z filtrami; `HR` tworzy i wysyła, `HRApprover` zatwierdza lub odrzuca. | 18–20 |
| Szkice korekt `/szkice-korekt` | `HR`, `HRApprover` | Lista szkiców; zatwierdzenie lub odrzucenie z notatką. | 26 |
| Dziennik audytu `/audyt` | `HR`, `HRApprover` | Filtry, strony, podgląd „przed i po”. | 28 |

Nowe trasy mają guardy ról jak w 7a (wejście po URL bez uprawnień kończy się przekierowaniem na pulpit z komunikatem). Menu per rola: Pracownik 4 pozycje, Kierownik 6, `HR` i `HRApprover` 9. Pulpit bez zmian.

## 5. Ekrany

**Wspólne zasady:** `422` i `409` w oknie dialogowym lub przy formularzu; `403` i `404` jako powiadomienie (poza `404` naliczenia, które jest stanem informacyjnym); stany ładowania i puste; `Spróbuj ponownie` przy błędach ładowania; dialogi nie zamykają się w trakcie wysyłania (`disableClose`); brak stałych czekań w specach. Kwoty w formacie `1 234,56 zł` (nowy helper, `pl-PL`). `EmployeeDirectory` dostaje metody: nazwa po `userId`, rozpoznanie własnego `userId` jako „Ty”, nieznany `userId` jako „—”.

### Naliczenia (`/naliczenia`)
- `MonthSwitcher` (bez przyszłości; bieżący miesiąc dozwolony) i stała uwaga „Naliczenie ma charakter wstępny i demonstracyjny.”
- **Karta naliczenia** (własna albo wybranego pracownika): pensja zasadnicza, dodatek stały, norma czasu (h), nadgodziny 50% i 100% (h), wynagrodzenie za nadgodziny, dodatki 50% i 100%, **brutto**, zastosowane reguły (numery przepisów), data obliczenia. `404` to „Brak naliczenia za ten miesiąc.”.
- **Kadry** (`HR`, `HRApprover`): lista wyboru pracownika, tabela „Zestawienie miesiąca” (pracownik, zespół, brutto, nadgodziny razem; osobne pobranie dla każdej osoby z limitem 4 jednoczesnych żądań, brak naliczenia jako „—”; kliknięcie wiersza wybiera pracownika) i przycisk **„Uruchom naliczenie”** → okno potwierdzenia → `POST /api/payroll/runs`. Dla bieżącego miesiąca okno ostrzega: „Miesiąc jeszcze trwa. Naliczenie będzie wstępne i można je powtórzyć.” Sukces: „Naliczono N pracowników.”, tabela aktualizuje się z odpowiedzi (lista wyników wszystkich pracowników). Błąd (np. `422` z nazwiskiem osoby z nieprawidłową ewidencją) w trwałym komunikacie na stronie (`role="alert"`).

### Zmiany płacowe (`/zmiany-platowe`)
- Tabela: pracownik, składnik (`Pensja zasadnicza` / `Dodatek stały`), wartość, obowiązuje od, status (`Szkic`, `Oczekuje na zatwierdzenie`, `Zatwierdzona`, `Odrzucona`), autor, decydent, uzasadnienie; filtry: status i pracownik; sortowanie od najnowszych.
- **„Nowa zmiana”** (`HR`): pracownik, składnik, wartość (≥ 0, przecinek dziesiętny), obowiązuje od, uzasadnienie (wymagane); błędy API w oknie. Wynik to szkic (`POST /api/payroll-changes`: `{employeeId, component, value, effectiveFrom, reason}`).
- Akcje w wierszu: własny `Szkic` (`HR`): **„Wyślij do zatwierdzenia”** (potwierdzenie); `Oczekuje` (`HRApprover`, nie autor): **„Zatwierdź”** (potwierdzenie) i **„Odrzuć”** (uzasadnienie wymagane). Gdy użytkownik jest autorem zmiany oczekującej, przyciski decyzji są ukryte i widać „Autor nie może zatwierdzić własnej zmiany.” (UC-19). `409` (status zmienił się w międzyczasie) odświeża listę z komunikatem.

### Szkice korekt (`/szkice-korekt`)
- Domyślny filtr „Oczekujące”. Kolumny: pracownik, data wpisu, **obecny wpis** (z listy wpisów miesiąca po `workEntryId`, a przy braku „—”), **proponowane godziny** (start–koniec, przerwa, znacznik `+1 dzień`), powód, kto utworzył, status, notatka decyzji.
- **„Zatwierdź”** i **„Odrzuć”** z notatką (opcjonalna przy zatwierdzeniu, wymagana przy odrzuceniu). Zatwierdzenie stosuje korektę wpisu, więc `422`/`409` (np. nakładanie się wpisów) są w oknie. Po zatwierdzeniu wpis w „Czas pracy” ma nowe godziny.

### Dziennik audytu (`/audyt`)
- Filtry: typ encji (lista z polskimi nazwami dla `Employee`, `Team`, `LeaveRequest`, `WorkEntryRecord`, `PayrollChange`, `PayrollResultRecord`, `CorrectionDraft`, `Conversation`), identyfikator encji (GUID z walidacją), użytkownik (lista z katalogu pracowników po `userId`), zakres dat (lokalne dni zamieniane na chwile w UTC przez `toISOString()`; sprawdzić w `AuditService`, czy `to` jest włączające) oraz „Szukaj” i „Wyczyść”.
- Tabela ze stronami po stronie serwera (25/50/100, `skip`/`take`, łączna liczba z API, od najnowszych): czas lokalny, użytkownik (nazwisko, „System (seed)” dla zapisów bez aktora, „—” dla nieznanego), zdarzenie (`Dodano`, `Zmieniono`, `Usunięto`, `Odczyt rozmowy`; nieznane bez zmian), typ encji po polsku, skrócony identyfikator (pełny w podpowiedzi). Rozwijany wiersz „Szczegóły”: **Przed** i **Po** jako sformatowany JSON tylko do odczytu (interpolacja tekstu, bez `innerHTML`).
- Dane demo zawierają kilka tysięcy wierszy audytu bez aktora, więc stronicowanie jest konieczne.

## 6. Testy

**Backend:**
- `EmployeeDirectoryService`: test integracyjny — `userId` osoby z kontem i `null` bez konta, w zakresie danych użytkownika.
- API: `GET /api/employees` zawiera `userId` (kadry dla wszystkich, pracownik tylko dla siebie); migawka OpenAPI zaktualizowana.
- Dane demo: niezmienniki (silnik reguł): po zastosowaniu proponowanej korekty u Anny znika naruszenie odpoczynku dobowego, a szkic nie nakłada się na inne wpisy; dla innych kotwic i ziaren niezmienniki nadal przechodzą. Test integracyjny seeda: dokładnie jeden szkic `Pending`, ponowny start niczego nie dubluje. Test API: `piotr.zielinski` widzi szkic w `GET /api/correction-drafts`.

**Frontend** (runner CLI 22, bez stałych czekań):
- Naliczenia: formatowanie kwot i godzin, `404` jako informacja, zestawienie z limitem 4 żądań, uruchomienie z ostrzeżeniem dla bieżącego miesiąca, trwały komunikat błędu.
- Zmiany płacowe: etykiety statusów, filtry, tworzenie (treść żądania), wysyłanie, zatwierdzanie, odrzucanie (uzasadnienie wymagane), ukryte przyciski dla autora z wyjaśnieniem, `409` odświeża listę, nazwiska z `userId` (także „Ty” i „—”).
- Szkice korekt: domyślny filtr, dopasowanie obecnego wpisu (albo „—”), decyzja z błędem `422`/`409` w oknie.
- Audyt: parametry zapytania (typ, id, użytkownik, zakres jako chwile w UTC), stronicowanie, rozwijanie „Przed/Po” jako tekst, etykiety zdarzeń, „System (seed)”, walidacja GUID.
- Wspólne: nowe metody `EmployeeDirectory`, helper kwot, guardy nowych tras (UC-04), menu per rola (4/6/9 pozycji).
- Zasady z 7a: `disableClose` podczas wysyłania, sprzątanie subskrypcji, niezależność od strefy czasowej, `npm run verify`.

## 7. Uruchomienie i przekazanie

- `tools/dev/smoke.sh` dostaje kroki 7b: odczyt naliczenia Anny za wrzesień, `userId` w `/api/employees` dla kadr, jeden oczekujący szkic dla kadr.
- Seed się zmienił, a istniejące bazy go pomijają: po wdrożeniu planu wykonuję `tools/dev/stop.sh --reset` i `tools/dev/run.sh`; dokumentacja (7.4) opisuje ten krok po aktualizacji.
- `docs/testy-reczne/plan-7b.md`: kroki per rola dla UC-16–20, UC-26 (część kadrowa) i UC-28 na danych demo: Anna ma zmianę pensji do 7800 zł od 1.11 oczekującą na Marię, Jan ma zatwierdzony dodatek 400 zł od 1.06 (widać różnicę między majem a czerwcem), Tomasz ma odrzucony dodatek, szkic korekty dotyczy wpisu Anny z 29.09, naliczenia istnieją dla kwietnia–września, więc październik pozwala sprawdzić uruchomienie.

## 8. Dokumentacja do aktualizacji

arc42: sekcja 7.3 (szkic w danych demo), 7.4 (ponowne zasianie po aktualizacji), nowy ADR-30 (`userId` w `EmployeeDto`: decyzja, alternatywy — osobny endpoint, brak zmian; konsekwencje), status w README; `web/README.md`; `docs/testy-reczne/plan-7b.md`.

## 9. Ryzyka

- **Kontrakt:** pole `userId` jest widoczne dla każdego, kto widzi daną osobę w `/api/employees` (np. kierownik dla zespołu); nie jest tajne, ale zmienia kontrakt, więc jest opisane w ADR-30.
- **Seed:** istniejące bazy nie dostaną szkicu bez `--reset`.
- **Zestawienie naliczeń:** 31 wywołań z limitem 4; dla demo wystarcza, endpoint zbiorczy to osobna decyzja.
- **Audyt:** duży JSON „przed/po” i tysiące wierszy; łagodzi to stronicowanie i rozwijanie na żądanie.
- **Zakres czasu w audycie:** semantyka `to` (włączające czy nie) musi być sprawdzona w `AuditService` przy implementacji, żeby filtr dnia nie gubił wpisów z jego końca.
