# Testy ręczne — plan 7b (Angular: naliczenia, zmiany płacowe, szkice korekt, audyt)

Lista kroków do ręcznego sprawdzenia interfejsu po planie 7b. Konta i hasła: `docs/arc42/07-widok-wdrozenia.md`, sekcja 7.2;
scenariusze danych demo: sekcja 7.3 (profil `Full`, kotwica `2026-10-16`). Kroki logowania, urlopów i czasu pracy są w `docs/testy-reczne/plan-7a.md`.

## Przygotowanie

1. Plan 7b dodaje do danych demo szkic korekty ewidencji, więc na bazie założonej wcześniej wykonaj `tools/dev/stop.sh --reset`, a potem `tools/dev/run.sh`
   (kończy się komunikatem `Test dymny zakończony powodzeniem.`; z innego komputera tunel SSH z komendy wypisanej przez skrypt).
2. Przeglądarka: `http://localhost:5080`; panel „Konta demo” na ekranie logowania wypełnia login i hasło.
3. Kroki zmieniające dane są oznaczone **(zmienia dane)**. Czysty stan: `tools/dev/stop.sh --reset` i `tools/dev/run.sh`.
4. Kolejność ma znaczenie: kroki 4 i 5 używają zmiany płacowej Anny oczekującej na Marię, a krok 9 szkicu Anny; krok 13 (odrzucenie) potrzebuje zmiany z kroku 12.

Uwagi o zakresie:
- Dokument obejmuje ekrany „Naliczenia”, „Zmiany płacowe”, „Szkice korekt” i „Audyt” (UC-16–UC-20, UC-26 w części kadrowej, UC-28). Pozostałe przypadki użycia nie są tu testowane.
- Ekran „Naliczenia” otwiera się na bieżącym miesiącu (październik 2026); poprzednie miesiące wybiera się strzałką `‹`.
- Kwoty są wyświetlane w formacie `pl-PL` (np. `7 800,00 zł`).

## Kroki

| # | UC | Konto | Kroki | Oczekiwany wynik |
|---|---|---|---|---|
| 1 | UC-16 | `anna.nowak` | „Naliczenia”, strzałką `‹` przejdź na wrzesień 2026. | Karta naliczenia Anny z kwotami brutto i netto oraz składnikami. Brak listy pracowników, brak przycisku „Uruchom naliczenie” i wyboru pracownika. Za październik: „Brak naliczenia za ten miesiąc.”. |
| 2 | UC-16 | `jan.kowalski` | „Naliczenia”, porównaj czerwiec i maj 2026. | W czerwcu wynagrodzenie brutto jest wyższe o dodatek 400 zł (zmiana płacowa obowiązująca od 1.06); w maju bez dodatku. |
| 3 | UC-17 | `piotr.zielinski` | „Naliczenia”, wrzesień 2026. | Zestawienie miesiąca dla wszystkich 31 osób z sumami; wybór wiersza (lub pracownika z listy) pokazuje kartę naliczenia tej osoby. Widoczny przycisk „Uruchom naliczenie”. |
| 4 | UC-18 | `piotr.zielinski` | „Zmiany płacowe”, filtr „Status”. | Zmiany demo: Anna — podstawa 7 800,00 zł od 1.11.2026, oczekująca (autor Piotr); Jan — dodatek 400,00 zł od 1.06.2026, zatwierdzona; Tomasz — odrzucony dodatek z uzasadnieniem. Przy zmianie Anny brak przycisków „Zatwierdź” i „Odrzuć” (Piotr jest autorem; decyduje kto inny). |
| 5 | UC-19 | `maria.kaminska` | „Zmiany płacowe”, zmiana Anny (7 800,00 zł od 1.11). | Widoczne przyciski „Zatwierdź” i „Odrzuć”. Nie zatwierdzaj jeszcze, jeśli chcesz powtórzyć krok 12–13 na tym samym stanie. |
| 6 | UC-18 | `anna.nowak`, `ewa.wisniewska`, `tomasz.lis` | Zaloguj się każdym kontem. | W menu brak pozycji „Zmiany płacowe”, „Szkice korekt” i „Audyt” (są tylko dla kadr). Wpisanie `http://localhost:5080/zmiany-platowe` (lub `/audyt`, `/szkice-korekt`) przekierowuje na pulpit z komunikatem „Brak dostępu do tej strony.”. Kierownik i pracownik mają pozycję „Naliczenia” z własnym naliczeniem. |
| 7 | UC-17 | `piotr.zielinski` | „Naliczenia”, październik 2026 (miesiąc kotwicy, bez naliczenia), „Uruchom naliczenie” i potwierdź. **(zmienia dane)** | Okno z informacją, że miesiąc jeszcze trwa i naliczenie będzie wstępne. Po potwierdzeniu zestawienie października z wynikami dla pracowników; ponowne uruchomienie jest dozwolone. |
| 8 | UC-26 | `piotr.zielinski` | „Szkice korekt”. | Jeden szkic ze statusem Oczekujący dla Anny Nowak: obecny wpis 14:00–22:30, proponowane godziny 12:00–20:30 (przerwa 30 min), powód o naruszeniu odpoczynku dobowego, autor Piotr. Brak przycisków decyzji (Piotr nie jest zatwierdzającym). |
| 9 | UC-26 | `maria.kaminska` | „Szkice korekt”, przy szkicu Anny „Zatwierdź”, opcjonalna notatka, zapisz. **(zmienia dane)** | Szkic ma status Zatwierdzony z decydentem Maria i notatką. Po zatwierdzeniu w „Czas pracy” (Anna, wrzesień 2026) wpis z dnia naruszenia ma godziny 12:00–20:30, a w analizie września znika naruszenie odpoczynku dobowego (zostaje tygodniowe). |
| 10 | UC-12, UC-26 | `piotr.zielinski` | Po kroku 9 „Szkice korekt”, filtr „Status”. | Szkic widoczny jako Zatwierdzony; brak szkiców oczekujących. |
| 11 | UC-26 | `anna.nowak` | „Czas pracy”, wrzesień 2026, po kroku 9. | Własny wpis ma godziny 12:00–20:30 i adnotację korekty. |
| 12 | UC-18 | `piotr.zielinski` | „Zmiany płacowe” → „Nowa zmiana”: pracownik Tomasz Lis, składnik dodatek stały, wartość 300, obowiązuje od 1.12.2026, uzasadnienie; „Zapisz”. Przy nowej zmianie „Wyślij do zatwierdzenia” i potwierdź. **(zmienia dane)** | Zmiana powstaje jako szkic, po wysłaniu ma status Oczekująca. Piotr nie widzi przycisków „Zatwierdź” i „Odrzuć”. |
| 13 | UC-19 | `maria.kaminska` | „Zmiany płacowe”, nowa zmiana Tomasza: „Odrzuć” bez uzasadnienia, potem z uzasadnieniem „Test odrzucenia”. **(zmienia dane)** | Bez uzasadnienia komunikat o konieczności jego podania, zmiana pozostaje. Z uzasadnieniem status Odrzucona z notatką „Test odrzucenia”. |
| 14 | UC-19 | `maria.kaminska` | Zatwierdź zmianę Anny (7 800,00 zł od 1.11). **(zmienia dane)** | Status Zatwierdzona. Zmiana obowiązuje od 1.11.2026, więc naliczenie września się nie zmienia. |
| 15 | UC-20 | `piotr.zielinski` | „Audyt”. | Dziennik zdarzeń z kolumnami Czas, Zdarzenie, Typ encji, Identyfikator, Użytkownik; wpisy seeda mają użytkownika „System (seed)”. Stronicowanie przesuwa listę; „Szczegóły” rozwija podgląd zmian (wartości przed i po). |
| 16 | UC-20 | `piotr.zielinski` | „Audyt”, filtr „Typ encji” = zmiana płacowa. | Tylko zdarzenia zmian płacowych, w tym wpisy z kroków 12–14 (utworzenie, wysłanie, odrzucenie, zatwierdzenie). „Wyczyść” przywraca pełną listę. |
| 17 | UC-20 | `piotr.zielinski` | „Audyt”, filtr „Użytkownik” = Maria Kamińska. | Tylko zdarzenia wykonane przez Marię (decyzje z kroków 9, 13, 14). |
| 18 | UC-20 | `maria.kaminska` | „Audyt”, filtr „Zakres dat” na dzisiejszy dzień. | Widoczne zdarzenia z tej sesji testowej; wpisy seeda (sprzed „dziś”) są poza zakresem. |
| 19 | UC-28 | `piotr.zielinski` | Po kroku 7 „Audyt”, filtr „Typ encji” = naliczenie. | Zdarzenia naliczenia za październik z podglądem zmian. |
