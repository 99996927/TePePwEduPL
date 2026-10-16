# 8. Pojęcia przekrojowe

## 8.1 Bezpieczeństwo i autoryzacja
- **Uwierzytelnianie:** ASP.NET Core Identity, lokalne konta w bazie, **cookie sesyjne** (HttpOnly, SameSite) z ochroną antyforgery (ADR-10).
  Angular i API są pod jednym originem, więc token nie jest dostępny dla JavaScriptu.
- **Role:** `Employee`, `Manager`, `HR`, `HRApprover`.
- **Zakres danych** (własne / zespół / wszyscy) wyznaczany w warstwie logiki i stosowany jako filtr w zapytaniach, a nie tylko w UI.
  Brak dostępu zwraca 403/404 (UC-03, UC-04).
- **Zasada czterech oczu:** zmiana płacowa wymaga innej osoby jako zatwierdzającej (UC-18, UC-19).
- **Blokada konta** po kilku nieudanych logowaniach (UC-02).
- **Sekrety** (klucz API, hasło SA) tylko w zmiennych środowiskowych, nigdy w repozytorium.

## 8.2 Determinizm obliczeń i rola agenta
- Wszystkie liczby (dni urlopu, godziny, nadgodziny, kwoty) pochodzą z silnika reguł.
- Agent ma dostęp do silnika wyłącznie przez narzędzia. Parametry tożsamości pochodzą z sesji, nie od modelu.
- Wynik narzędzia zawiera identyfikator reguły i odwołanie do artykułu, co umożliwia weryfikację odpowiedzi.
- Brak wyniku lub błąd narzędzia oznacza brak liczby w odpowiedzi (UC-25).

## 8.3 Reguły czasu pracy i wersjonowanie
Reguły są parametryzowane datą obowiązywania. Kluczowe parametry:
- norma: 8 h na dobę i przeciętnie 40 h tygodniowo w okresie rozliczeniowym do 4 miesięcy, do 12 miesięcy w uzasadnionych przypadkach ✔ art. 129,
- limit łącznie z nadgodzinami: przeciętnie 48 h tygodniowo ✔ art. 131 § 1,
- odpoczynek: 11 h dobowo ✔ art. 132 § 1, 35 h tygodniowo ✔ art. 133 § 1,
- nadgodziny: definicja ✔ art. 151 § 1, limit 150 h w roku kalendarzowym ✔ art. 151 § 3 (z możliwością innego ustalenia w § 4),
- dodatki: 100% (noc, niedziele i święta niebędące dniami pracy, dni wolne za pracę w niedzielę lub święto) i 50% (inne dni) ✔ art. 151¹ § 1,
- wymiar urlopu: 20 dni (do 10 lat) lub 26 dni (od 10 lat), proporcjonalnie przy niepełnym etacie ✔ art. 154 § 1–2.

Stawki podatkowo-składkowe w naliczeniu są uproszczone i oznaczone jako wstępne.

## 8.3a Historia rozmów z agentem
Cała rozmowa (pytania, odpowiedzi, wywołania narzędzi z parametrami i wynikiem) jest zapisywana w bazie i powiązana z użytkownikiem.
Czyta ją autor oraz kadry (`HR`, `HRApprover`), które mają wgląd w pełną treść. Kierownik i inni pracownicy nie mają dostępu.
Każdy odczyt cudzej rozmowy przez kadry trafia do audytu (UC-31). Użytkownik jest informowany w interfejsie, że kadry mogą
czytać rozmowy (ADR-11, ryzyko R-10).

## 8.4 Audyt
Jedna tabela `AuditLog` tylko do dopisywania (zapis w tej samej transakcji co zmiana, użytkownik bazy bez UPDATE i DELETE, ADR-13). Zapis: kto, kiedy, co, stan przed i po. Dotyczy zmian ewidencji, wniosków, zmian płacowych, zatwierdzeń i każdego wywołania
narzędzia przez agenta (narzędzie, parametry, wynik, użytkownik).

## 8.5 Obsługa błędów
Błędy walidacji i reguł zwracane są jako ustrukturyzowane odpowiedzi (kod, komunikat, identyfikator reguły). Błędy zewnętrzne
(Anthropic API) nie przerywają pracy systemu poza funkcją agenta, a agent informuje użytkownika o niedostępności.

## 8.6 Testowalność
Silnik reguł jest czysty i testowany jednostkowo. Czas pochodzi z wstrzykiwanego dostawcy zegara, aby testy były powtarzalne.
Dane demo ładowane seedem umożliwiają deterministyczne testy E2E.

## 8.7 Dane syntetyczne
Imiona, wynagrodzenia i ewidencje są fikcyjne. Importowany tekst Kodeksu pracy to publiczny akt prawny.
