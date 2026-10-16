# 4. Strategia rozwiązania

## 4.1 Cele jakościowe → podejście
| Cel | Podejście |
|---|---|
| Determinizm obliczeń | Silnik reguł płacowych jako osobna biblioteka bez I/O, arytmetyka `decimal`, reguły parametryzowane datą obowiązywania. Agent dostaje wyłącznie narzędzia (tool use) i nie liczy sam. |
| Bezpieczeństwo, separacja danych | Autoryzacja po stronie serwera na podstawie roli i przynależności do zespołu, filtrowanie na poziomie zapytań. Narzędzia agenta działają w kontekście zalogowanego użytkownika. |
| Zasada czterech oczu | Zmiana płacowa ma stan (szkic → oczekuje → zatwierdzona/odrzucona). Zatwierdzający musi być inną osobą niż autor, sprawdzane w logice i w bazie. |
| Testowalność | Czyste funkcje silnika z testami jednostkowymi, testy integracyjne autoryzacji, testy E2E Playwright powiązane z `UC-xx`. |
| Prostota wdrożenia | Dwa kontenery, `docker compose up`, migracje i seed przy starcie. |
| Audytowalność | Dziennik audytowy zapisywany w bazie, w tym wywołania narzędzi agenta. |

## 4.2 Kluczowe decyzje architektoniczne
1. **Trzy warstwy:** prezentacja (Angular + kontrolery API), logika (serwisy, domena, silnik reguł, agent), dane (EF Core + MSSQL).
2. **Jeden kontener aplikacji** serwuje API i statyczny Angular (brak CORS, brak osobnego serwera www).
3. **Agent jako klient narzędzi:** Claude wybiera narzędzie, serwis aplikacyjny je wykonuje przez silnik. Liczby w odpowiedzi pochodzą wyłącznie z wyników narzędzi.
4. **Przepisy w bazie:** artykuły Kodeksu pracy importowane jednorazowo z API Sejmu, agent cytuje je narzędziem `search_labour_code`. Reguły silnika są kodowane ręcznie z odwołaniem do artykułu.
5. **Lokalne konta** (ASP.NET Core Identity) z rolami i seedem kont demo.

Uzasadnienia i alternatywy: [sekcja 9](09-decyzje-architektoniczne.md).

## 4.3 Strategia testowania
Piramida: wiele testów jednostkowych silnika (wartości graniczne, okresy rozliczeniowe), testy integracyjne warstwy danych i autoryzacji,
mniej testów E2E w Playwright dla przypadków użycia z [sekcji 3](03-kontekst-i-zakres.md). Szczegóły w [sekcji 10](10-jakosc.md).
