# 10. Wymagania jakościowe i testy

## 10.1 Drzewo jakości
- **Poprawność obliczeń:** determinizm, powtarzalność, pokrycie testami.
- **Bezpieczeństwo:** separacja danych, czterech oczu, audyt.
- **Testowalność:** czysty silnik reguł, seed danych, E2E.
- **Prostota wdrożenia:** `docker compose up`.

## 10.2 Scenariusze jakościowe
| ID | Scenariusz | Oczekiwanie |
|---|---|---|
| Q-01 | Te same dane wejściowe dla silnika wywołane dwukrotnie | Identyczny wynik. |
| Q-02 | `tomasz.lis` żąda zasobu Anny bezpośrednim wywołaniem API | 403/404, brak danych w odpowiedzi. |
| Q-03 | `piotr.zielinski` próbuje zatwierdzić własną zmianę | Odmowa w serwisie i w bazie. |
| Q-04 | Model zwraca w odpowiedzi liczbę niepochodzącą z wyniku narzędzia | Test wykrywa niezgodność (odpowiedź zawiera tylko liczby z wyników narzędzi). |
| Q-05 | Anthropic API niedostępne | Reszta systemu działa, agent informuje o niedostępności. |
| Q-06 | Start od pustego środowiska | Po `docker compose up` baza jest zmigrowana i zasilona danymi demo. |
| Q-07 | `Hr.Agent` próbuje użyć typu `Hr.Rules` lub `DbContext` | Test architektury kończy się niepowodzeniem. |
| Q-08 | Kierownik lub inny pracownik żąda cudzej rozmowy z agentem | 403/404. Kadry otrzymują treść, a odczyt jest w audycie. |

## 10.3 Poziomy testów
| Poziom | Zakres | Narzędzie (wstępnie) |
|---|---|---|
| Jednostkowe | Silnik reguł, wartości graniczne (norma 8 h, 11 h, 35 h, średnia 48 h, limit 150 h, dodatki 50% i 100%, wymiar urlopu 20/26) | xUnit |
| Integracyjne | EF Core + MSSQL, autoryzacja zakresu danych, zasada czterech oczu | xUnit, kontener MSSQL |
| Kontrakt agenta | Narzędzia agenta ze stubem modelu, ograniczenie liczb do wyników narzędzi | xUnit |
| Architektura | Zależności między projektami (`Agent` tylko do interfejsu narzędzi, `Rules` bez zależności, `Core` nie zna `Agent`) | NetArchTest |
| E2E | Przypadki użycia UC-xx w przeglądarce | Playwright |

## 10.4 Śledzenie przypadków użycia do testów Playwright
Plik testu: `e2e/tests/<grupa>.spec.ts`, nazwa testu zaczyna się od `UC-xx`.

| Przypadek | Grupa | Test |
|---|---|---|
| UC-01, UC-02, UC-03, UC-04 | auth | `auth.spec.ts` |
| UC-05, UC-06, UC-07, UC-08, UC-09 | urlopy | `leave.spec.ts` |
| UC-10, UC-11, UC-12, UC-13, UC-14, UC-15 | czas pracy | `timesheet.spec.ts` |
| UC-16, UC-17, UC-18, UC-19, UC-20 | płace | `payroll.spec.ts` |
| UC-21, UC-22, UC-23, UC-24, UC-25, UC-26, UC-27, UC-30, UC-31 | agent (ze stubem modelu) | `agent.spec.ts` |
| UC-28, UC-29 | audyt, start | `admin.spec.ts` |

Testy agenta w E2E używają stubu modelu, aby były deterministyczne i niezależne od sieci. Osobny, opcjonalny zestaw smoke
sprawdza prawdziwe Anthropic API.
