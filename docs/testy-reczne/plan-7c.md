# Testy ręczne — plan 7c (Angular: czat z agentem, historia i odczyt rozmów przez kadry)

Lista kroków do ręcznego sprawdzenia ekranu „Agent” po planie 7c. Konta i hasła: `docs/arc42/07-widok-wdrozenia.md`, sekcja 7.2;
scenariusze danych demo: sekcja 7.3 (profil `Full`, kotwica `2026-10-16`). Kroki pozostałych ekranów są w `docs/testy-reczne/plan-7a.md` i `docs/testy-reczne/plan-7b.md`.

## Przygotowanie

1. Plan 7c nie zmienia danych demo (rozmowy Anny i Tomasza są w seedzie od planu 6), więc reset bazy nie jest potrzebny: wystarczy `tools/dev/run.sh`
   (kończy się komunikatem `Test dymny zakończony powodzeniem.`, kroki 1–15; z innego komputera tunel SSH z komendy wypisanej przez skrypt).
2. **Klucz Anthropic** (kroki oznaczone **(wymaga klucza)**): dopisz do pliku `.env` w katalogu głównym repozytorium wiersz `ANTHROPIC_API_KEY=...` (sama wartość,
   bez cudzysłowów). Plik jest poza gitem (`.gitignore`) i ma uprawnienia `600`; klucza nie zapisuj w żadnym pliku repozytorium. Potem ponownie `tools/dev/run.sh`:
   skrypt wypisuje `Klucz Anthropic wczytany z .env (czat korzysta z prawdziwego modelu).`, a bez klucza
   `Brak ANTHROPIC_API_KEY w .env: czat agenta zwróci komunikat o niedostępności (503); pozostałe ekrany działają.` Klucz jest brany wyłącznie z `.env`
   (klucz wyeksportowany w powłoce nie trafia do `Hr.Api`).
3. Przeglądarka: `http://localhost:5080`; panel „Konta demo” na ekranie logowania wypełnia login i hasło. Ekran: pozycja „Agent” w menu.
4. Każde wysłane pytanie zapisuje rozmowę w bazie, więc takie kroki są oznaczone **(zmienia dane)**. Czysty stan: `tools/dev/stop.sh --reset` i `tools/dev/run.sh`.
5. Kolejność ma znaczenie: krok 12 (`ewa.wisniewska`, pusta lista) wymaga, by Ewa nie wysłała wcześniej żadnego pytania; krok 12 używa identyfikatora rozmowy Anny z kroku 10.
   `tomasz.lis` pyta tylko w istniejącej rozmowie (krok 8): test dymny (krok 15) oczekuje, że Tomasz ma dokładnie jedną rozmowę.

Uwagi o zakresie:
- Dokument obejmuje ekran „Agent” (UC-21–UC-24, UC-27, UC-30, UC-31). UC-25 (awaria silnika) nie jest tu testowany, bo wymaga wywołania awarii silnika reguł. Szkic korekty proponowany przez agenta (UC-26) i jego decyzja w „Szkice korekt” są w `plan-7b.md`.
- Odpowiedzi prawdziwego modelu nie są deterministyczne: kryteria kroków z kluczem dotyczą zachowania ekranu (wskaźnik, wiersze narzędzi, zgodność liczb z innymi ekranami),
  a nie dosłownej treści odpowiedzi.
- Odpowiedź przychodzi w całości (bez strumieniowania); w trakcie oczekiwania pole pytania jest zablokowane, a w rozmowie widać dymek „Agent pisze…”.
- Gdy odpowiedź zawiera liczbę, której nie ma w wynikach narzędzi, pod odpowiedzią pojawia się ostrzeżenie „Liczby nie pochodzą z danych systemu: …” (ADR-26). Jeśli się
  pojawi, zanotuj pytanie i liczby: to nie jest błąd ekranu, tylko sygnał osłony liczb.

## Kroki

| # | UC | Konto | Kroki | Oczekiwany wynik |
|---|---|---|---|---|
| 1 | UC-21, UC-30 | `anna.nowak` | „Agent”, na liście po lewej kliknij rozmowę „Ile dni urlopu mi zostało?”. | Lista ma jedną rozmowę z datą i „Wiadomości: 4”, bez autora i bez przełącznika „Tylko moje”. W rozmowie: pytanie Anny, zwinięte wiersze „Wywołanie narzędzia: get_leave_balance” i „Wynik narzędzia: get_leave_balance” oraz odpowiedź agenta z liczbą pozostałych dni. Kliknięcie wiersza wywołania pokazuje parametry (`{}`), a wiersza wyniku JSON z polami `entitlementDays`, `remainingDays` i `ruleId` `KP-154-1`. Pod rozmową pole „Pytanie do agenta” z licznikiem `0/4000`, informacja „Kadry mogą czytać rozmowy z agentem.” i nieaktywny przycisk „Wyślij”. |
| 2 | UC-21 | `anna.nowak` | „Nowa rozmowa”. | Adres `/agent`, żadna rozmowa nie jest zaznaczona, w miejscu rozmowy podpowiedź „Zadaj pytanie agentowi, np. „Ile mam dni urlopu?”.”. „Wyślij” staje się aktywny po wpisaniu tekstu. |
| 3 | UC-21, Q-05 | `anna.nowak` | **Bez klucza** (komunikat `Brak ANTHROPIC_API_KEY w .env…` w `run.sh`): „Nowa rozmowa”, wpisz „Ile mam dni urlopu?”, „Wyślij”. **(zmienia dane)** | Na chwilę „Agent pisze…”, potem pod polem komunikat „Nie udało się połączyć z modelem. Pytanie zostało zapisane w rozmowie; reszta systemu działa.”. Tekst pytania zostaje w polu (nie ginie), pytanie jest widoczne w rozmowie, adres zmienia się na `/agent/<id>`, a rozmowa pojawia się na liście. „Wyślij” można nacisnąć ponownie. Pozostałe ekrany (np. „Urlopy”) działają. |
| 4 | UC-21 | `anna.nowak` | **(wymaga klucza)** „Nowa rozmowa”, wpisz „Ile mam dni urlopu?” i naciśnij Enter. **(zmienia dane)** | Pole jest zablokowane, a w rozmowie dymek „Agent pisze…”. Po odpowiedzi: pole jest puste i odblokowane, adres to `/agent/<id>`, nowa rozmowa jest na liście (zaznaczona), w rozmowie są wiersze wywołania i wyniku narzędzia (np. `get_leave_balance`). Liczba pozostałych dni w odpowiedzi jest taka sama jak na ekranie „Urlopy”. Shift+Enter w polu dodaje nowy wiersz zamiast wysyłać. |
| 5 | UC-30 | `anna.nowak` | **(wymaga klucza)** W rozmowie z kroku 4 dopytaj: „A ile z tego czeka na decyzję?”. **(zmienia dane)** | Pytanie i odpowiedź są dopisane do tej samej rozmowy (bez nowej pozycji na liście; licznik „Wiadomości” tej rozmowy na liście rośnie po każdej odpowiedzi). Odpowiedź korzysta z kontekstu poprzedniego pytania; liczba dni oczekujących zgadza się z ekranem „Urlopy”. |
| 6 | UC-22 | `anna.nowak` | **(wymaga klucza)** „Nowa rozmowa”: „Ile mam nadgodzin w tym miesiącu?”. **(zmienia dane)** | W rozmowie wiersze wywołania i wyniku narzędzia (np. `calculate_overtime`) z parametrami miesiąca. Odpowiedź podaje liczbę godzin nadliczbowych zgodną z analizą na ekranie „Czas pracy” dla października 2026 (lub informuje, że ich nie ma). |
| 7 | UC-23 | `anna.nowak` | **(wymaga klucza)** „Nowa rozmowa”: „Czy we wrześniu naruszyłam odpoczynek dobowy? Podaj przepis.”. **(zmienia dane)** | Wiersze narzędzi (np. `check_work_time_violations`, `search_labour_code`). Odpowiedź wskazuje dzień naruszenia zgodny z analizą września na ekranie „Czas pracy” i powołuje się na przepis Kodeksu pracy o odpoczynku dobowym (art. 132). |
| 8 | UC-24 | `tomasz.lis` | „Agent”: otwórz rozmowę demo „Czy Anna Nowak ma zaplanowany urlop w grudniu?”. **(wymaga klucza)** W tej samej rozmowie dopytaj: „Ile dni urlopu zostało Annie Nowak?”. **(zmienia dane)** | Rozmowa demo pokazuje odmowę agenta. Nowa odpowiedź także jest odmową bez danych Anny (najwyżej propozycja sprawdzenia własnych danych Tomasza); żadne narzędzie nie zwraca danych Anny. Na liście nadal jest jedna rozmowa. |
| 9 | UC-27 | `anna.nowak` | **(wymaga klucza)** „Nowa rozmowa”: „Jaka będzie jutro pogoda w Warszawie?”. **(zmienia dane)** | Agent odpowiada, że to poza jego zakresem (sprawy kadrowe, urlopy, czas pracy, naliczenia), bez wymyślonych danych; brak ostrzeżenia o liczbach. |
| 10 | UC-31 | `piotr.zielinski` | „Agent”. Włącz i wyłącz „Tylko moje”. Kliknij rozmowę Anny „Ile dni urlopu mi zostało?”. | Lista zawiera rozmowy wszystkich osób z autorem (Anna Nowak, Tomasz Lis oraz rozmowy z poprzednich kroków). „Tylko moje” zostawia wyłącznie rozmowy Piotra („Brak rozmów.”, jeśli Piotr jeszcze nie pytał). Po kliknięciu rozmowy Anny: baner „Czytasz rozmowę: Anna Nowak. Odczyt jest zapisany w audycie.”, wiersze narzędzi rozwijają się jak w kroku 1, a pod rozmową nie ma pola pytania, przycisku „Wyślij” ani informacji o czytaniu przez kadry. Zanotuj adres `/agent/<id>` rozmowy Anny. |
| 11 | UC-31, UC-20 | `piotr.zielinski` | Po kroku 10 „Audyt”, filtr „Typ encji” = „Rozmowa”, „Szukaj”. | Zdarzenie „Odczyt rozmowy” wykonane przez Piotra Zielińskiego z bieżącym czasem i identyfikatorem rozmowy Anny (oprócz wpisów z wcześniejszych testów dymnych). |
| 12 | UC-31, Q-08 | `ewa.wisniewska` | „Agent”. Następnie wpisz w pasku adresu `http://localhost:5080/agent/<id>` z kroku 10. | Brak przełącznika „Tylko moje” i autorów; lista „Brak rozmów.” (kierownik widzi tylko własne rozmowy), w miejscu rozmowy podpowiedź z kroku 2. Wejście w rozmowę Anny przez adres: komunikat „Nie znaleziono rozmowy.” i powrót na `/agent`; treść rozmowy nie jest pokazana. |
| 13 | UC-31 | `maria.kaminska` | „Agent”, rozmowa Tomasza. | Jak w kroku 10: wszystkie rozmowy z autorami, baner „Czytasz rozmowę: Tomasz Lis. Odczyt jest zapisany w audycie.”, brak pola pytania. |
| 14 | UC-21 | `anna.nowak` | „Nowa rozmowa”, wklej tekst dłuższy niż 4000 znaków. Potem zastąp go tekstem `<b>pogrubienie</b> <script>alert(1)</script> Ile mam dni urlopu?` i „Wyślij” (z kluczem albo bez). **(zmienia dane)** | Przy ponad 4000 znaków licznik (np. `4001/4000`) jest wyróżniony, a „Wyślij” nieaktywny. Pytanie ze znacznikami HTML jest pokazane dosłownie jako tekst (bez pogrubienia, bez okna `alert`); długie wiersze w dymkach się zawijają. |
