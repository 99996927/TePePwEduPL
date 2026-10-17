# Testy ręczne — plan 7a (Angular: logowanie, urlopy, czas pracy, przegląd zespołu)

Lista kroków do ręcznego sprawdzenia interfejsu po planie 7a. Konta i hasła: `docs/arc42/07-widok-wdrozenia.md`, sekcja 7.2;
scenariusze danych demo: sekcja 7.3 (profil `Full`, kotwica `2026-10-16`).

## Przygotowanie

1. `tools/dev/run.sh` (kończy się komunikatem `Test dymny zakończony powodzeniem.`), z innego komputera tunel SSH z komendy wypisanej przez skrypt.
2. Przeglądarka: `http://localhost:5080`. Na ekranie logowania jest panel „Konta demo” (build `development`); kliknięcie konta wypełnia login i hasło.
3. Czysty stan danych przed powtórką testów: `tools/dev/stop.sh --reset`, potem `tools/dev/run.sh`. Kroki zmieniające dane są oznaczone **(zmienia dane)**.
4. Kolejność ma znaczenie: wniosek Anny 2–6.11 jest używany w UC-08 przez Ewę; jeśli wcześniej zatwierdzi go kadra albo Anna go anuluje, Ewa nie będzie miała czego zatwierdzać.

Uwagi o zakresie:
- Interfejs planu 7a nie ma jeszcze ekranów agenta, naliczeń, zmian płacowych ani audytu (plany 7b i 7c), więc UC-16–UC-31 nie są tu testowane.
- Kafelki „Bieżący miesiąc” i „Alerty” na pulpicie liczą okres od 1. dnia bieżącego miesiąca do dziś (data przeglądarki). Oczekiwane wyniki poniżej zakładają test w październiku 2026.
- Ekran „Czas pracy” otwiera się na bieżącym miesiącu; poprzednie miesiące wybiera się strzałką `‹` (strzałka `›` jest nieaktywna w bieżącym miesiącu). Panel „Analiza czasu pracy” dotyczy wybranego miesiąca.
- Liczby godzin są wyświetlane z jednym miejscem po przecinku (np. `181,5 h`).

## Kroki

| # | UC | Konto | Kroki | Oczekiwany wynik |
|---|---|---|---|---|
| 1 | UC-01 | każde z 6 kont | Zaloguj się kolejno każdym kontem z panelu „Konta demo” i wyloguj przyciskiem „Wyloguj”. | Po zalogowaniu pulpit z imieniem i nazwiskiem oraz rolą w pasku. Menu: „Pulpit”, „Urlopy”, „Czas pracy”; dla `ewa.wisniewska`, `piotr.zielinski`, `maria.kaminska` dodatkowo „Zatwierdzanie urlopów” i „Przegląd zespołu”. Po wylogowaniu ekran logowania. |
| 2 | UC-02 | `tomasz.lis` | Wpisz login `tomasz.lis` i złe hasło, „Zaloguj”. | Komunikat „Login lub hasło są nieprawidłowe.”, pozostajesz na ekranie logowania (brak sesji: wejście na `http://localhost:5080/urlopy` wraca na logowanie). |
| 3 | UC-03 | `tomasz.lis` | Otwórz „Czas pracy”, a następnie wpisz w pasku adresu `http://localhost:5080/czas-pracy?pracownik=5c5a37a7-3d57-1a02-eb58-4ce004a62230&miesiac=2026-09` (identyfikator Anny). | Widać tylko własne wpisy Tomasza (parametr `pracownik` jest ignorowany dla roli Employee): wrzesień 152,0 h, „Brak naruszeń”. Nigdzie w UI nie ma danych Anny ani listy innych pracowników. |
| 4 | UC-03 | `ewa.wisniewska` | „Przegląd zespołu”, potem w „Czas pracy” rozwiń listę wyboru pracownika. | Lista 7 osób z `Zespołu Ewy`: Anna Nowak, Jan Kowalski, Ewa Wiśniewska oraz Filip Borkowski, Zbigniew Kowalczyk, Cezary Krawczyk, Bartosz Majewski. Brak Tomasza Lisa. |
| 5 | UC-04 | `anna.nowak` | Wpisz w pasku adresu `http://localhost:5080/zespol`, potem `http://localhost:5080/urlopy/zatwierdzanie`. | Za każdym razem przekierowanie na pulpit z komunikatem „Brak dostępu do tej strony.”. Menu nie zawiera tych pozycji. |
| 6 | UC-05 | `anna.nowak` | Pulpit, kafelek „Saldo urlopu”; potem „Urlopy”. | Wymiar 20, Wykorzystane 10, Oczekujące 5, Pozostałe 5 (to samo na obu ekranach). Lista: 2.11–6.11.2026 Oczekujący (5 dni, przycisk „Anuluj”), 7.12–11.12.2026 Zatwierdzony, 13.07–17.07.2026 Zatwierdzony. |
| 7 | UC-05 | `jan.kowalski` | Pulpit, kafelek „Saldo urlopu”. | Wymiar 26 (staż 12 lat), Wykorzystane 0, Oczekujące 0, Pozostałe 26. |
| 8 | UC-05 | `tomasz.lis` | Pulpit, kafelek „Saldo urlopu”. | Wymiar 20, Wykorzystane 9, Oczekujące 0, Pozostałe 11 (zatwierdzone 18.06–25.06 i 14.09–16.09.2026). |
| 9 | UC-06 | `tomasz.lis` | „Urlopy” → „Nowy wniosek”, zakres 9.11.2026–13.11.2026, „Zapisz”. **(zmienia dane)** | Komunikat „Wniosek złożony: 4 dni roboczych.” (11.11 to święto). Wniosek na liście jako Oczekujący; saldo: Oczekujące 4, Pozostałe 7. |
| 10 | UC-07 | `anna.nowak` | „Urlopy” → „Nowy wniosek”, zakres 16.11.2026–24.11.2026 (7 dni roboczych), „Zapisz”. | Okno zostaje otwarte z komunikatem „Niewystarczające saldo urlopu: brakuje 2 dni.”; wniosek nie powstaje, saldo bez zmian. |
| 11 | UC-08 | `ewa.wisniewska` | Pulpit: kafelek „Zatwierdzanie”; „Zatwierdzanie urlopów”. | „Do zatwierdzenia: 1”. Na liście tylko wniosek Anny Nowak (Zespół Ewy) 2.11–6.11.2026, 5 dni. Brak wniosku Tomasza z kroku 9 (inny zespół). |
| 12 | UC-08 | `ewa.wisniewska` | Przy wniosku Anny „Odrzuć”, pozostaw puste uzasadnienie i kliknij „Odrzuć” w oknie; potem „Anuluj” w oknie. | Komunikat „Podaj uzasadnienie odrzucenia.”, wniosek pozostaje na liście. |
| 13 | UC-08 | `ewa.wisniewska`, potem `anna.nowak` | Ewa: przy wniosku Anny „Zatwierdź” i zapisz. Anna: „Urlopy”. **(zmienia dane)** | Ewa: „Wniosek zatwierdzony.”, lista pusta („Brak wniosków do zatwierdzenia”). Anna: wniosek 2.11–6.11 Zatwierdzony, saldo 20 / 15 / 0 / 5. |
| 14 | UC-08 | `piotr.zielinski` | „Zatwierdzanie urlopów”; przy wniosku Tomasza z kroku 9 „Odrzuć” z uzasadnieniem „Test odrzucenia”. **(zmienia dane)** | Kadry widzą wnioski wszystkich zespołów (tu: Tomasza). Komunikat „Wniosek odrzucony.”; u Tomasza status Odrzucony z powodem „Test odrzucenia”, saldo wraca do 20 / 9 / 0 / 11. |
| 15 | UC-08, UC-09 | `jan.kowalski` | „Urlopy”, filtr „Status”. | Wniosek 3.08–7.08.2026 Odrzucony z powodem „Zbyt wielu pracowników na urlopie w tym tygodniu.” i 5.10–9.10.2026 Anulowany; przy żadnym nie ma przycisku „Anuluj”. |
| 16 | UC-09 | `anna.nowak` | Jeśli krok 13 nie był wykonany: przy wniosku 2.11–6.11 „Anuluj” i potwierdź przyciskiem „Anuluj wniosek”. **(zmienia dane)** | „Wniosek anulowany.”, status Anulowany, saldo 20 / 10 / 0 / 10. Przy wnioskach zatwierdzonych nie ma przycisku „Anuluj”. |
| 17 | UC-10 | `tomasz.lis` | „Czas pracy” (październik 2026) → „Dodaj wpis”: data 10.10.2026 (sobota), start 08:00, koniec 12:00, przerwa 0, zapisz. **(zmienia dane)** | „Wpis dodany.”, wiersz widoczny w tabeli (4,0 h). Analiza października: Przepracowane rośnie o 4,0 h (z 96,0 na 100,0 h), Nadgodziny 50% 4,0 h, Nadgodziny w roku 4,0 h z 150,0 h. |
| 18 | UC-10 | `tomasz.lis` | „Dodaj wpis” z datą w przyszłości (np. 30.10.2026), zapisz. | Wpis odrzucony z komunikatem „Wpis nie może kończyć się w przyszłości.” (o ile test odbywa się przed 30.10.2026). |
| 19 | UC-11 | `piotr.zielinski` (lub `maria.kaminska`) | „Czas pracy”, wybierz pracownika Anna Nowak, miesiąc wrzesień 2026. Przy wpisie 30.09 (07:30–15:30) „Koryguj”: start 09:30, koniec 17:30, przerwa 30, powód „Test korekty”; zapisz. **(zmienia dane)** | Bez powodu przycisk zapisu jest nieaktywny. Po zapisie „Wpis skorygowany.”; w analizie września znika naruszenie odpoczynku dobowego (zostaje tygodniowe). |
| 20 | UC-11 | `ewa.wisniewska` | „Czas pracy”, wybierz Annę Nowak, wrzesień 2026. | Kierownik widzi wpisy i analizę Anny, ale przy jej wpisach nie ma przycisku „Koryguj” (korygować mogą właściciel wpisu i kadry); nie ma też „Dodaj wpis”. |
| 21 | UC-11 | `anna.nowak` | „Czas pracy”, wrzesień 2026, „Koryguj” przy własnym wpisie (np. 28.09), zmień koniec na 16:00 z powodem; zapisz. **(zmienia dane)** | „Wpis skorygowany.”, wartości w tabeli i analizie przeliczone. |
| 22 | UC-12 | `anna.nowak` | „Czas pracy”, wrzesień 2026, sekcja „Naruszenia odpoczynku” (przed krokiem 19). | „Odpoczynek dobowy 29.09.2026 22:30 – 30.09.2026 07:30: 9,0 h wobec wymaganych 11,0 h (KP-132-1)”. Analiza: Norma 176,0 h, Przepracowane 181,5 h, Nadgodziny 50% 6,0 h. |
| 23 | UC-13 | `anna.nowak` | Jak wyżej. | „Odpoczynek tygodniowy 12.09.2026 20:00 – 14.09.2026 00:00: 28,0 h wobec wymaganych 35,0 h (KP-133-1)”. Pulpit w październiku: „Brak alertów” (naruszenia są we wrześniu). |
| 24 | UC-14 | `jan.kowalski` | „Czas pracy”, kolejno czerwiec, lipiec, sierpień i wrzesień 2026; pulpit. | Średnia tygodniowa oznaczona „ponad limit 48 h”: czerwiec 49,0 h, lipiec 51,9 h, wrzesień 51,3 h; sierpień 47,4 h bez oznaczenia. We wrześniu Nadgodziny w roku 174,0 h z 150,0 h i ostrzeżenie „Przekroczony roczny limit 150 h nadgodzin”. Pulpit (październik): alert „Przekroczony roczny limit 150 h nadgodzin”. |
| 25 | UC-12–UC-14 | `tomasz.lis` | Pulpit; „Czas pracy” dla dowolnego miesiąca kwiecień–październik 2026. | Pulpit: „Brak alertów”. Analiza: „Brak naruszeń”, średnia bez oznaczenia limitu, brak ostrzeżenia o limicie rocznym. |
| 26 | UC-15 | `ewa.wisniewska` | „Przegląd zespołu”, strzałką `‹` przejdź na wrzesień 2026. | 7 osób z kolumnami „Przepracowane / norma”, „Nadgodziny razem”, „Alerty”. Anna Nowak: 181,5 / 176,0 h, 6,0 h, odznaka „Odpoczynek (2)”. Jan Kowalski: 44,0 h nadgodzin, odznaka „Średnia >48 h”. Brak Tomasza. |
| 27 | UC-15 | `ewa.wisniewska` | W przeglądzie kliknij wiersz Jana (lub zaznacz go klawiszem Tab i naciśnij Enter). | Otwiera się „Czas pracy” Jana dla wybranego miesiąca (wrzesień 2026) z jego analizą. |
| 28 | UC-15, UC-03 | `piotr.zielinski`, `maria.kaminska` | „Przegląd zespołu” (wrzesień 2026); w „Czas pracy” lista wyboru pracownika. | Widać wszystkie 31 osób ze wszystkich zespołów (w tym Tomasza Lisa, Annę i Jana z alertami jak w krokach 22–24). |
| 29 | — | dowolne | Zaloguj się, w drugiej karcie wyloguj, w pierwszej wykonaj dowolną akcję (np. zmień miesiąc). | Przekierowanie na ekran logowania z komunikatem „Sesja wygasła. Zaloguj się ponownie.”. |
| 30 | UC-02 | `tomasz.lis` | Powtórz złe hasło łącznie 5 razy, potem wpisz poprawne `tomaszlis123`. | Po piątej nieudanej próbie konto jest zablokowane na 15 minut: komunikat „Konto jest zablokowane. Spróbuj ponownie później.”, także przy poprawnym haśle. **(zmienia dane: blokada)** Ten krok jest ostatni, bo blokuje Tomasza na 15 minut; kolejne kroki z Tomaszem wykonaj po 15 minutach albo po `stop.sh --reset`. |
