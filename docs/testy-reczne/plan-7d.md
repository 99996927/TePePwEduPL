# Testy ręczne — plan 7d (Angular: nowy wygląd, styl „Material Dashboard”)

Plan 7d zmienia tylko wygląd: logika, teksty i dane są bez zmian. Poniżej lista ekranów do obejrzenia. Ocena wizualna należy do właściciela:
testy automatyczne sprawdzają strukturę (klasy, ikony, tony chipów), a nie piksele. Konta i hasła: `docs/arc42/07-widok-wdrozenia.md`, sekcja 7.2.

## Przygotowanie

1. `tools/dev/run.sh` (kończy się komunikatem `Test dymny zakończony powodeniem.`). Reset bazy nie jest potrzebny.
2. Tunel SSH: `ssh -N -L 5080:127.0.0.1:5080 azureuser@tepecompute`, przeglądarka: http://localhost:5080. Po zmianie wyglądu odśwież stronę z pominięciem pamięci podręcznej (Ctrl+F5).
3. Czcionki i ikony są ładowane z Google Fonts (potrzebny internet w przeglądarce).

## Co obejrzeć

| # | Konto | Ekran | Co sprawdzić |
|---|---|---|---|
| 1 | — | Logowanie | Szare tło, karta z fioletowym gradientowym nagłówkiem „Asystent kadrowo-płacowy”; panel „Konta demo” działa. |
| 2 | `anna.nowak` | Menu i pasek | Jasnoszare menu z ikonami przy każdej pozycji; aktywna pozycja z fioletowym gradientem i cieniem; pasek biały z okruszkami „Asystent kadrowo-płacowy / Pulpit”, nazwa, rola i „Wyloguj”. Po przejściu na „Urlopy” tytuł w pasku się zmienia. |
| 3 | `anna.nowak` | Pulpit | Cztery kafelki z kolorowymi ikonami (saldo, miesiąc, alerty; „Zatwierdzanie” tylko u kierownika i kadr); ikona alertów zielona bez alertów, czerwona z alertami. |
| 4 | `anna.nowak` | Urlopy | Tabela w karcie z gradientowym nagłówkiem i przyciskiem „Nowy wniosek” (biały); statusy jako kolorowe chipy (zielony zatwierdzone, pomarańczowy oczekujące, czerwony odrzucone, szary anulowane). Dialog „Nowy wniosek” zaokrąglony, tytuł z akcentem. |
| 5 | `ewa.wisniewska` | Zatwierdzanie, Przegląd zespołu | Tabele w kartach; znaczniki alertów w „Przeglądzie zespołu” jako chipy. |
| 6 | `anna.nowak` | Czas pracy | Karta z tabelą i oddzielna karta „Analiza czasu pracy” (nagłówek turkusowy); naruszenia odpoczynku w czerwonych chipach, „Brak naruszeń” w zielonym. |
| 7 | `piotr.zielinski` | Naliczenia | Karta pracownika z turkusowym nagłówkiem; kwoty wyrównane do prawej. |
| 8 | `piotr.zielinski` | Zmiany płacowe, Szkice korekt, Audyt | Tabele w kartach, chipy statusów, filtry w ciele karty. |
| 9 | `anna.nowak` | Agent | Lista rozmów i rozmowa w jednej karcie; pytania w fioletowych dymkach po prawej, odpowiedzi w białych po lewej; wiersze narzędzi w jasnoszarym tle. Bez klucza Anthropic odpowiedź zwróci komunikat o niedostępności (503). |
| 10 | dowolne | Wąskie okno | Okno węższe niż 800 px: menu wysuwane przyciskiem w pasku, ikona „menu” widoczna (nie tekst „menu”), po wyborze pozycji menu się zamyka. |
| 11 | dowolne | Klawiatura | Klawisz Tab: widoczny kontur fokusu na pozycjach menu, przyciskach w nagłówku karty i w tabelach. |
| 12 | dowolne | Czytelność | Czy tekst na gradientach i w chipach jest czytelny; czy długie nazwy (rola „Zatwierdzający kadr”) nie rozjeżdżają paska i menu. |

Uwagi wizualne zgłoś listą (ekran, co jest nie tak); poprawki wejdą w jednej turze.
