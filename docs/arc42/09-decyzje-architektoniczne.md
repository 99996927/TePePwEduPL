# 9. Decyzje architektoniczne

| ID | Decyzja | Status |
|---|---|---|
| ADR-01 | Architektura trójwarstwowa, jeden kontener aplikacji i jeden kontener bazy | Przyjęta |
| ADR-02 | .NET (ASP.NET Core) + Angular serwowany statycznie z backendu | Przyjęta |
| ADR-03 | MSSQL Server w kontenerze Linux | Przyjęta |
| ADR-04 | Agent: Anthropic API z tool use, silnik reguł jako jedyne źródło liczb | Przyjęta |
| ADR-05 | Przepisy: ręcznie kodowane reguły + import tekstu Kodeksu pracy do bazy | Przyjęta |
| ADR-06 | Lokalne konta (ASP.NET Core Identity) i poluzowana polityka haseł w demo | Przyjęta |
| ADR-07 | Docker Compose v2 do uruchamiania | Przyjęta |
| ADR-08 | Zasada czterech oczu dla zmian płacowych w logice i w bazie | Przyjęta, część bazodanowa zrealizowana w planie 2 |
| ADR-09 | Backend: cztery projekty `Api`, `Core`, `Rules`, `Agent` + test architektury | Przyjęta |
| ADR-10 | Sesja: cookie (Identity) z antyforgery, bez JWT | Przyjęta |
| ADR-11 | Pełna historia rozmów agenta w bazie, odczyt przez autora i kadry | Przyjęta |
| ADR-12 | Rozmowy: tabele `Conversation` i `Message` | Przyjęta, zrealizowana w planie 2 |
| ADR-13 | Audyt: jedna tabela `AuditLog` tylko do dopisywania | Przyjęta, zrealizowana w planie 2 (wyzwalacz) |
| ADR-14 | Odpowiedź agenta w całości (bez strumieniowania) | Przyjęta |
| ADR-15 | Zakres zmiany płacowej: stawka lub składnik pracownika z datą obowiązywania | Przyjęta |
| ADR-16 | Kontrakt API: OpenAPI generowane z kontrolerów, klient TypeScript generowany dla Angulara | Przyjęta |
| ADR-17 | Wpis czasu pracy: początek, koniec i przerwa, reszta wyliczana przez silnik | Przyjęta |
| ADR-18 | Jeden standardowy rozkład czasu pracy dla wszystkich pracowników | Przyjęta |
| ADR-19 | Dane demo: większy zestaw generowany z ustalonym ziarnem | Przyjęta |
| ADR-20 | Każdy wygenerowany pracownik ma konto: login `imię.nazwisko`, hasło `imięnazwisko123` | Przyjęta |
| ADR-21 | Skala danych demo: ok. 30 pracowników, 4–5 zespołów, 6 miesięcy ewidencji | Przyjęta |
| ADR-22 | Uproszczenia silnika reguł (plan 1) | Przyjęta |
| ADR-23 | Testy integracyjne warstwy danych na prawdziwym SQL Server w kontenerze (Testcontainers) | Przyjęta |
| ADR-24 | Serwisy aplikacyjne i reguły ich działania | Do przeglądu |
| ADR-25 | Konwencje API REST i uwierzytelnianie cookie | Do przeglądu |

## ADR-01 Trzy warstwy, dwa kontenery
**Kontekst:** wymóg trójwarstwowości i uruchomienia na jednej maszynie.
**Decyzja:** kontener `hr-app` (prezentacja + logika) i `hr-db` (dane).
**Konsekwencje:** prosty deployment, jedna jednostka skalowania aplikacji. Brak niezależnego skalowania frontendu.

## ADR-02 .NET + Angular z jednego procesu
**Decyzja:** build Angulara kopiowany do `wwwroot`, serwowany przez ASP.NET Core.
**Alternatywy:** osobny kontener nginx (odrzucone: łamie założenie dwóch kontenerów i dodaje CORS).

## ADR-04 Agent i silnik reguł
**Kontekst:** model językowy może się mylić w arytmetyce i przepisach.
**Decyzja:** Claude (Anthropic API, tool use) wybiera narzędzia, a obliczenia wykonuje deterministyczny silnik. Parametry tożsamości z sesji.
**Alternatywy:** model lokalny (odrzucone: dodatkowy kontener, wolne bez GPU), wymienny adapter dostawcy (odłożone, YAGNI).
**Konsekwencje:** zależność od zewnętrznego API i klucza. Funkcja agenta niedostępna offline, reszta systemu działa.

## ADR-05 Przepisy
**Decyzja:** źródło: API Sejmu ELI (Dz.U. 2026 poz. 1245, PDF). Reguły silnika kodowane ręcznie z odwołaniem do artykułu.
Tekst artykułów importowany jednorazowo do bazy na potrzeby `search_labour_code`.
**Konsekwencje:** import wymaga czyszczenia przypisów i nagłówków stron z PDF. Aktualizacja przepisów to świadomy, powtarzalny proces.

## ADR-06 Konta lokalne, poluzowana polityka haseł
**Kontekst:** hasła demo mają postać `imięnazwisko123`, co nie spełnia domyślnej polityki Identity (wielka litera, znak specjalny).
**Decyzja:** polityka haseł poluzowana wyłącznie w profilu demo (min. 8 znaków). Blokada konta pozostaje włączona.
**Konsekwencje:** niedopuszczalne produkcyjnie. Ryzyko R-03.

## ADR-08 Zasada czterech oczu
**Decyzja:** zmiana płacowa ma stan i wymaga zatwierdzenia przez osobę inną niż autor. Kontrola w serwisie i ograniczenie w bazie.
**Konsekwencje:** dwie role kadrowe, dodatkowy przepływ w UI.

## ADR-09 Cztery projekty backendu
**Kontekst:** silnik reguł musi być deterministyczny, a agent (model, sieć, klucz) jest zewnętrzny i nieprzewidywalny.
**Decyzja:** `Hr.Api` (REST), `Hr.Core` (domena, serwisy, dane), `Hr.Rules` (silnik, bez zależności), `Hr.Agent` (klient Anthropic, pętla tool use).
`Agent` korzysta tylko z interfejsu narzędzi z `Core`. Granica jest wymuszana testem architektury, bo referencja projektu nie ogranicza typów publicznych.
**Alternatywy:** pięć projektów warstwowych (Clean Architecture, więcej kodu łączącego), moduły w jednym projekcie (granice tylko konwencją),
osobny projekt `Contracts` z interfejsem narzędzi (bezpieczniejszy, ale zbyt ciężki na tę skalę).
**Konsekwencje:** silnik fizycznie odizolowany od bazy i agenta, agent wymienny w testach (stub modelu). `Core` pozostaje największym projektem.

## ADR-10 Cookie sesyjne zamiast JWT
**Kontekst:** jeden frontend (Angular) i API pod jednym originem.
**Decyzja:** ASP.NET Core Identity z cookie (HttpOnly, SameSite) i ochroną antyforgery.
**Alternatywy:** JWT w nagłówku (token dostępny dla JS, trudniejsze odwołanie), hybryda cookie z JWT (komplikacja bez korzyści).
**Konsekwencje:** natychmiastowe wylogowanie i blokada konta, testy Playwright logują się formularzem. Ewentualny klient mobilny wymagałby osobnego rozwiązania.

## ADR-11 Historia rozmów agenta
**Kontekst:** wymóg audytu i ciągłości rozmowy (dopytania).
**Decyzja:** pełna historia rozmów w bazie (pytania, odpowiedzi, wywołania narzędzi z parametrami i wynikiem). Czyta ją autor oraz kadry (`HR`, `HRApprover`).
Kierownik i inni pracownicy nie mają dostępu. Odczyt cudzej rozmowy przez kadry jest audytowany.
**Alternatywy:** kadry widzą tylko metadane i wywołania narzędzi (większa prywatność, mniejszy audyt) oraz dostęp kierownika do audytu narzędzi.
**Konsekwencje:** pełniejszy audyt, ale pracownicy mogą unikać wrażliwych pytań. Mitigacja: jawna informacja w UI (ryzyko R-10).

## ADR-12 Rozmowy: `Conversation` i `Message`
**Decyzja:** dwie tabele. Wiadomość ma rolę (`user`, `assistant`, `tool_call`, `tool_result`), treść oraz nazwę i parametry JSON dla narzędzi.
**Alternatywy:** osobna tabela `ToolCall` (wygodniejszy audyt narzędzi, więcej łączenia), jedna tabela z kolumną JSON (trudne filtrowanie).
**Konsekwencje:** proste mapowanie na format Anthropic API i odtwarzanie kontekstu. Audyt wywołań narzędzi przez filtr po roli i nazwie narzędzia.

## ADR-13 Audyt: `AuditLog` tylko do dopisywania
**Decyzja:** jedna tabela (kto, kiedy, typ zdarzenia, encja, stan przed i po w JSON), zapis w tej samej transakcji co zmiana.
**Realizacja (plan 2):** zakaz modyfikacji wymusza wyzwalacz `TR_AuditLog_AppendOnly` (`INSTEAD OF UPDATE, DELETE` z błędem „AuditLog is append-only”),
który blokuje operacje DML (UPDATE i DELETE) niezależnie od tego, jakim użytkownikiem łączy się aplikacja, i jest testowany surowym SQL.
Wyzwalacz nie chroni przed `TRUNCATE TABLE`, `DISABLE TRIGGER` ani `DROP TRIGGER`: przed nimi chroni dopiero warstwa uprawnień użytkownika bazy dodawana przy wdrożeniu. Zmiana względem pierwotnego zapisu: uprawnienia INSERT i SELECT dla osobnego
użytkownika aplikacji (bez UPDATE i DELETE) dojdą jako dodatkowa warstwa przy wdrożeniu (plan Docker), a nie jako jedyny mechanizm.
Wpisy dla zmian encji dopisuje `AuditSaveChangesInterceptor` w tej samej transakcji co zapis (nieudany zapis nie zostawia wpisów), a zdarzenia bez zmiany danych (np. odczyt cudzej rozmowy) `AuditWriter`.
**Alternatywy:** tabele czasowe SQL Server (nie obejmują zdarzeń bez zmiany danych, jak odczyt cudzej rozmowy, więc potrzebny byłby drugi mechanizm),
tabele historii przy każdej encji (dużo tabel, brak wspólnego widoku).
**Konsekwencje:** jeden widok audytu (UC-28), nowe zdarzenia bez zmiany schematu. Uprawnienia bazy wymagają osobnego użytkownika dla migracji.

## ADR-14 Odpowiedź agenta w całości
**Decyzja:** zwykłe żądanie POST, UI pokazuje wskaźnik „agent pisze…”, odpowiedź wraca po zakończeniu pętli narzędzi.
**Alternatywy:** strumieniowanie SSE (lepsze wrażenie, trudniejsze: przerwane połączenia, zapis niepełnej odpowiedzi, testy E2E).
**Konsekwencje:** prostsze testy ze stubem modelu i prostszy zapis. SSE może zostać dodane później bez zmiany reszty modelu danych.

## ADR-15 Zakres zmiany płacowej
**Decyzja:** zmiana płacowa dotyczy stawki lub składnika wynagrodzenia konkretnego pracownika (np. podwyżka, dodatek stały, stawka godzinowa)
i ma datę obowiązywania oraz uzasadnienie. Wymaga zatwierdzenia przez osobę inną niż autor (ADR-08). Silnik używa nowej wartości od daty
obowiązywania, tylko dla zmian zatwierdzonych. Szkice korekt agenta dotyczą ewidencji czasu pracy, nie wynagrodzenia.
**Alternatywy:** dodatkowo korekty naliczeń wstecz (wymagają przeliczania zamkniętych okresów), dodatkowo zmiany parametrów firmowych
(dotykają konfiguracji silnika i determinizmu).
**Konsekwencje:** prosty model (pracownik, składnik, wartość, od kiedy, status), bez przeliczeń wstecz i bez zmiany parametrów silnika.
Poza zakresem pozostają korekty zamkniętych okresów i parametry firmowe.

## ADR-16 Kontrakt API: OpenAPI z kontrolerów
**Decyzja:** backend jest źródłem prawdy. Specyfikacja OpenAPI jest generowana z kontrolerów `Hr.Api`, a z niej generowany jest klient TypeScript dla Angulara.
Plik specyfikacji jest zapisywany w repozytorium, a zmiana kontraktu widoczna w różnicy (diff) przy przeglądzie.
**Alternatywy:** API-first z ręcznie pisaną specyfikacją (podwójne utrzymanie), brak formalnej specyfikacji (ryzyko rozbieżności frontendu i backendu).
**Konsekwencje:** dokumentacja nie rozjeżdża się z kodem, testy Playwright i Angular korzystają z typów klienta. Kontroler musi mieć jawne typy odpowiedzi i kody błędów.

## ADR-17 Model wpisu czasu pracy
**Decyzja:** wpis czasu pracy zawiera datę, godzinę rozpoczęcia, godzinę zakończenia i długość przerwy. Przepracowane godziny, nadgodziny,
odpoczynek dobowy (11 h) i tygodniowy (35 h) są wyliczane przez silnik reguł, a nie zapisywane razem z wpisem.
**Alternatywy:** sama liczba godzin w dniu (uniemożliwia UC-12 i UC-13), oba podejścia jednocześnie (ryzyko niespójności przy korekcie, UC-11).
**Konsekwencje:** pełna możliwość wykrywania naruszeń z art. 132 i 133, jedno źródło prawdy, a korekta wpisu (UC-11) automatycznie zmienia wyniki.
Wpis przechodzący przez północ (praca nocna) musi być obsłużony jako jeden wpis, który kończy się następnego dnia.

## ADR-18 Jeden standardowy rozkład czasu pracy
**Decyzja:** wszyscy pracownicy mają ten sam rozkład: pełny etat, poniedziałek–piątek, 8 godzin na dobę, 40 godzin tygodniowo,
okres rozliczeniowy 4 miesiące (parametr silnika), z kalendarzem polskich dni wolnych od pracy.
**Alternatywy:** rozkład przypisany do pracownika z wariantami (część etatu, praca zmianowa), pełny harmonogram dzienny planowany przez kierownika.
**Konsekwencje:** prostszy silnik i mniej przypadków w testach. Poza zakresem: niepełny etat (art. 154 § 2), praca zmianowa, system równoważny, harmonogramy.
Wymiar urlopu zależy tylko od stażu (20 lub 26 dni). Okres rozliczeniowy pozostaje parametrem, więc dodanie wariantów jest możliwe później.
**Do weryfikacji przed implementacją:** lista dni wolnych od pracy (święta stałe i ruchome, ewentualne zmiany w ostatnich latach) pochodzi z przepisów
spoza Kodeksu pracy i musi być sprawdzona w oficjalnym źródle (API Sejmu), a nie z pamięci.

## ADR-19 Dane demo
**Decyzja:** dane demo są generowane deterministycznie (ustalone ziarno generatora): kilkudziesięciu fikcyjnych pracowników w kilku zespołach
i kilka miesięcy ewidencji, wniosków urlopowych i naliczeń. Konta logowania z sekcji 7.2 (6 osób) pozostają stałe.
**Alternatywy:** mały, ręcznie przygotowany zestaw (czytelne testy, mniej realistyczny pokaz), oba warianty przełączane profilem.
**Konsekwencje:** realistyczniejszy pokaz i większa baza do zapytań agenta. Aby testy UC-xx pozostały deterministyczne, generator dodaje
niezależnie od losowej części stały zestaw scenariuszowy przypisany do kont demo (np. naruszenie odpoczynku dobowego u `anna.nowak`,
nadgodziny ponad normę u `jan.kowalski`, wniosek urlopowy oczekujący, zmiana płacowa do zatwierdzenia). Ziarno jest zapisane w konfiguracji, a testy
nie polegają na wartościach z części losowej.

## ADR-20 Konta dla wszystkich wygenerowanych pracowników
**Decyzja:** każdy wygenerowany pracownik ma konto logowania według tego samego wzoru co konta z sekcji 7.2: login `imię.nazwisko`,
hasło `imięnazwisko123` (małe litery, bez znaków diakrytycznych i separatorów). Role: `Employee`, a w każdym zespole jeden `Manager`.
Generator gwarantuje unikalność loginów (brak powtórzeń kombinacji imię i nazwisko).
**Alternatywy:** tylko 6 kont stałych (mniej jawnych haseł), konta dla wybranej części pracowników (reguły wyboru do opisania).
**Konsekwencje:** można zalogować się jako dowolny pracownik i sprawdzić separację danych na większym zbiorze. Rośnie liczba jawnych, przewidywalnych
haseł (ryzyko R-03 większe, tylko środowisko demo). Lista wszystkich kont nie jest powielana w dokumentacji, bo jest wyznaczona regułą i ziarnem generatora.
Sześć kont z sekcji 7.2 pozostaje stałych i nazwanych, bo na nich opierają się testy UC-xx.

## ADR-21 Skala danych demo
**Decyzja:** generator tworzy ok. 30 pracowników w 4–5 zespołach oraz 6 miesięcy ewidencji, wniosków urlopowych i naliczeń (w tym 6 stałych kont z sekcji 7.2).
**Alternatywy:** ok. 100 pracowników i 12 miesięcy (realistyczniejsze, wolniejszy start), kilkaset pracowników (test wydajności, poza celem projektu).
**Konsekwencje:** seed kończy się w kilka sekund, a 6 miesięcy pokrywa okres rozliczeniowy 4 miesięcy (ADR-18) z zapasem. Okres rozliczeniowy do 12 miesięcy
(art. 129 § 2) nie jest pokryty danymi demo i wymaga testów jednostkowych silnika na danych syntetycznych poza seedem.

## ADR-22 Uproszczenia silnika reguł (plan 1)
**Kontekst:** Kodeks pracy nie wyznacza jednoznacznie algorytmu, który silnik musi stosować w kilku miejscach. Potrzebne są jawne, testowalne uproszczenia.
**Decyzja:**
- **Nadgodziny w ujęciu dobowym.** Dzień to data rozpoczęcia wpisu. W dzień roboczy nadgodzinami są godziny powyżej 8 h w dobie, traktowane jako ostatnie godziny dnia. W dni nieroboczne (sobota, niedziela, święto) cała praca jest nadgodzinami.
- **Dodatki.** 100% za nadgodziny w porze nocnej oraz w niedziele i święta, 50% w pozostałych przypadkach (art. 151¹ § 1). Pora nocna to parametr `NightWindow`, domyślnie 21:00–05:00 (8 godzin w ustawowym oknie 21:00–07:00, art. 151⁷).
- **Odpoczynek dobowy** sprawdzany tylko między wpisami o różnych datach rozpoczęcia. **Odpoczynek tygodniowy** liczony w tygodniu pon.–niedz., z lukami obciętymi do granic tygodnia.
- **Naliczenie** to wyłącznie brutto: wynagrodzenie zasadnicze + normalne wynagrodzenie za godziny nadliczbowe (art. 151¹ § 1: dodatek przysługuje „oprócz normalnego wynagrodzenia”) + dodatki 50% i 100%. Podstawą stawki godzinowej jest wynagrodzenie zasadnicze (uproszczenie art. 151¹ § 3), bez podatków i składek. Odbiór czasu wolnego zamiast wynagrodzenia (art. 151²) poza zakresem.
- **Walidacja wpisów:** nakładające się wpisy czasu pracy są odrzucane (`ArgumentException`) we wszystkich regułach, które je przyjmują.
- **Staż urlopowy** liczony od daty zatrudnienia, bez okresów nauki (art. 154¹).
- **Poza zakresem:** dodatek za przekroczenie średniej normy okresu (art. 151¹ § 2), odpoczynek zastępczy i dzień wolny za pracę w dniu wolnym (art. 151³), niepełny etat.
**Alternatywy:** ujęcie okresowe nadgodzin (nadgodziny to nadwyżka ponad wymiar z art. 130; dokładniejsze, ale wymaga reguł przypisania dodatków do godzin), pełne odwzorowanie art. 151¹ § 2.
**Konsekwencje:** prostsze testy i przewidywalny wynik. Wyniki mogą się różnić od praktyki kadrowej w przypadkach granicznych (praca przez północ, praca w sobotę w tygodniu z nieobecnością). Zaakceptowane przez właściciela projektu wraz ze zmergowaniem PR #1.

## ADR-23 Testy integracyjne na prawdziwym SQL Server (Testcontainers)
**Kontekst:** kluczowe gwarancje warstwy danych (ograniczenia CHECK, wyzwalacz audytu, filtrowanie zakresu danych tłumaczone na SQL, unikalne indeksy) zależą od zachowania SQL Server.
**Decyzja:** testy integracyjne używają pakietu Testcontainers.MsSql i obrazu `mcr.microsoft.com/mssql/server:2022-latest`. Jeden kontener na kolekcję testów, osobna, zmigrowana baza na każdy test.
**Alternatywy:** SQLite lub baza w pamięci EF (nie odwzorowują wyzwalaczy ani ograniczeń SQL Server, więc nie dowodzą wymagań), wspólna baza z Docker Compose (testy zależne od stanu).
**Konsekwencje:** testy wymagają dostępu do Dockera i pobrania obrazu przy pierwszym uruchomieniu. Docker Engine 24.0.9 (API 1.43) nie współpracuje z Testcontainers 4.x bez ustawienia
`DOCKER_API_VERSION=1.43`, co robi fixture testów; po aktualizacji Dockera ten wpis można usunąć.

## ADR-24 Serwisy aplikacyjne i reguły ich działania
**Kontekst:** UC-05 do UC-20 wymagają logiki, która łączy zakres danych, role, silnik reguł i zapis. Specyfikacja nie rozstrzyga kilku szczegółów, a API (plan 4) i agent (plan 5) będą się na nich opierać.
**Decyzja:**
- **Kody błędów (`ServiceResult`):** `NotFound` dla obiektów poza zakresem użytkownika i nieistniejących (bez ujawniania istnienia cudzych danych), `Forbidden` gdy obiekt jest widoczny, ale rola nie pozwala na operację, `Validation` dla naruszeń reguł (z `RuleId`, gdy pochodzi z silnika), `Conflict` dla niezgodnego stanu lub nakładania. Plan 4 mapuje je na 404, 403, 422 i 409.
- **Role w przepływach:** wniosek urlopowy zatwierdza kierownik zespołu lub kadry, nigdy autor; zmianę płacową tworzy i wysyła `HR`, zatwierdza lub odrzuca tylko `HRApprover` i nigdy autor (kontrola w serwisie przed ograniczeniem w bazie); naliczenia uruchamia `HR` lub `HRApprover`; wpis czasu koryguje autor albo kadry (kierownik nie), zawsze z uzasadnieniem. Właściciel potwierdził, że `HRApprover` **może** zatwierdzić zmianę dotyczącą własnego wynagrodzenia, o ile utworzyła ją inna osoba (zasada czterech oczu dotyczy autora zmiany, a nie beneficjenta).
- **Urlopy:** wniosek mieści się w jednym roku kalendarzowym i ma co najmniej 1 dzień roboczy; do salda liczą się dni zatwierdzone i oczekujące; wnioski nakładające się na oczekujące lub zatwierdzone są odrzucane; saldo liczone dla roku wniosku (staż na dziś dla bieżącego roku, na 1 stycznia dla pozostałych); zatwierdzenie ponownie sprawdza saldo.
- **Ewidencja:** wpis nie może kończyć się w przyszłości względem zegara ani nakładać się na inny wpis pracownika; analiza okresu zwraca wymiar czasu (pomniejszony o zatwierdzone urlopy), nadgodziny, naruszenia odpoczynków, średnią tygodniową i stan rocznego limitu 150 h.
- **Czas lokalny:** wpisy czasu pracy są w czasie lokalnym (Europe/Warsaw), więc sprawdzenie „wpis nie może kończyć się w przyszłości” i „dziś” używają `IClock.LocalNow`, a nie UTC.
- **Współbieżność:** operacje „sprawdź i zapisz” (złożenie wniosku, dodanie i korekta wpisu czasu) są serializowane per pracownik transakcją z blokadą aplikacyjną SQL Server (`sp_getapplock`), co zapobiega podwójnemu wnioskowi przy dwukliku. Zatwierdzanie i naliczenia nie mają blokad (zatwierdzenie przelicza saldo, a ponowne naliczenie jest idempotentne).
- **Audyt korekty:** uzasadnienie korekty wpisu czasu trafia do `AuditLog` jako zdarzenie `WorkEntryCorrected` w tej samej transakcji co zmiana (UC-11).
- **Zmiany płacowe:** szkice i oczekujące zmiany są stanem roboczym kadr; pracownik i kierownik widzą (w swoim zakresie) wyłącznie zmiany zatwierdzone. Pełną listę widzą `HR` i `HRApprover`.
- **Analiza czasu pracy** obejmuje okresy przechodzące przez granicę roku (wpisy ładowane od początku zakresu), a limit roczny nadgodzin liczy tylko od 1 stycznia roku końca zakresu.
- **Naliczenia:** stały dodatek (`FixedAllowance`) dochodzi do brutto, ale nie wchodzi do podstawy stawki godzinowej; stawka godzinowa liczona z nominalnego wymiaru miesiąca, bez odejmowania urlopów; stawki z zatwierdzonych zmian od daty obowiązywania włącznie; ponowne uruchomienie za ten sam miesiąc aktualizuje wiersz (unikalny `(EmployeeId, Year, Month)`), a błąd w ewidencji któregokolwiek pracownika przerywa całe uruchomienie bez zapisu częściowego.
**Alternatywy:** `Forbidden` także dla obiektów poza zakresem (ujawnia istnienie cudzych danych); wnioski przez granicę roku z podziałem na lata (więcej reguł, poza zakresem planu 3).
**Konsekwencje:** API i agent dostają jednolite kody błędów i gotowe reguły. Poza zakresem pozostają wnioski przez granicę roku, anulowanie zatwierdzonych wniosków i korekty zamkniętych naliczeń.

## ADR-25 Konwencje API REST i uwierzytelnianie cookie
**Kontekst:** `Hr.Api` jest jedyną granicą między przeglądarką (Angular, plan 6) i agentem (plan 5) a serwisami z `Hr.Core`. Konwencje muszą być spójne, bo klient TypeScript jest generowany z OpenAPI (ADR-16).
**Decyzja:**
- **Struktura:** trasy pod `/api`, kontrolery (nie minimal API), cienkie: DTO zamiast encji (statyczne fabryki `Of`), mapowanie `ServiceResult<T>` na odpowiedź HTTP w jednym miejscu. Kontrolery nie dotykają `HrDbContext` i nie mają atrybutów ról: role i zakres danych egzekwują serwisy (jedno źródło prawdy). Dostęp bez logowania blokuje domyślna polityka (`FallbackPolicy`), a test wylicza wszystkie endpointy i wymaga 401 dla każdego poza `health`, `login` i `antiforgery`.
- **Kody błędów (ProblemDetails, `application/problem+json`, pola `code` i `ruleId`):** 404 `NotFound`, 403 `Forbidden`, 422 `Validation`, 409 `Conflict`, 400 brak lub zły token antyforgery (`AntiforgeryTokenInvalid`) albo niepoprawne żądanie, 423 konto zablokowane (`LockedOut`), 500 bez szczegółów wyjątku (`ServerError`). Brak sesji to 401 bez ciała i bez przekierowania. Uwaga: przy domyślnej polityce wymagającej logowania także trasa niedopasowana do żadnego endpointu zwraca niezalogowanemu 401, a zalogowanemu 404.
- **Sesja:** cookie `hr.session` (`HttpOnly`, `SameSite=Strict`, wygasanie przesuwane 8 h), lokalne konta Identity (ADR-06, ADR-10). **Wylogowanie unieważnia sesję po stronie serwera:** `logout` zmienia stempel bezpieczeństwa konta, a walidator stempla działa przy każdym żądaniu (`ValidationInterval = 0`, jeden odczyt bazy na żądanie), więc skopiowane ciasteczko przestaje działać natychmiast. Skutek uboczny: wylogowanie kończy sesje użytkownika na wszystkich urządzeniach, a claimy (`display_name`, role) są odświeżane z każdym żądaniem. Blokada konta po 5 nieudanych próbach na 15 minut. Zły login i złe hasło dają identyczną odpowiedź 401, a blokada 423 (ujawnia istnienie tylko zablokowanych kont).
- **Antyforgery:** ciasteczko `hr.antiforgery` (`HttpOnly`) oraz token w ciasteczku `XSRF-TOKEN` czytelnym dla JavaScript i nagłówku `X-XSRF-TOKEN` (tak działa domyślnie Angular), wymagany dla metod innych niż GET, HEAD, OPTIONS i TRACE, także dla logowania. Token jest odświeżany po zalogowaniu i wylogowaniu, bo jest związany z tożsamością. Walidację realizuje własny filtr (wbudowany `AutoValidateAntiforgeryToken` wymaga usług widoków MVC).
- **Czas w JSON:** pola `*Utc` z sufiksem `Z`; wpisy czasu pracy jako czas lokalny bez strefy (ADR-17), a wartość ze strefą (`Z`, `+02:00`) jest odrzucana z 422 zamiast po cichu przesuwana (klient Angular musi wysyłać czas lokalny, nie `toISOString()`); zakresy w audycie jako UTC (`DateTimeOffset`).
- **Start aplikacji:** zawsze migruje bazę; konta i dane demo tylko przy `Hr:SeedDemoData=true` (domyślnie `false`).
- **OpenAPI:** dokument generowany z kontrolerów (`operationId` w postaci `Kontroler_Akcja`, jawne typy odpowiedzi i błędów, schemat zabezpieczeń cookie), wystawiany tylko w środowisku Development, z zatwierdzoną migawką `docs/openapi/hr-api.json` i testem, który wykrywa każdą zmianę kontraktu (aktualizacja: `UPDATE_OPENAPI=1`).
**Alternatywy:** role na kontrolerach (dubluje reguły serwisów), JWT (ADR-10 wybrał cookie), `AutoValidateAntiforgeryToken` z `AddControllersWithViews` (dodatkowe zależności), 404 zamiast 401 dla tras bez endpointu (wymagałoby własnej obsługi fallbacku).
**Konsekwencje:** angular i agent dostają jednolity kontrakt. Poza zakresem tego etapu: HTTPS i HSTS (plan Docker), ograniczanie liczby żądań poza blokadą konta, konwersacje agenta (plan 5), stronicowanie list innych niż audyt. Migracje przy starcie aplikacji są wygodne dla demo, ale w środowisku produkcyjnym powinny być osobnym krokiem wdrożenia.
