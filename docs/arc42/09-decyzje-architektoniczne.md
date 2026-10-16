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
| ADR-08 | Zasada czterech oczu dla zmian płacowych w logice i w bazie | Przyjęta |
| ADR-09 | Backend: cztery projekty `Api`, `Core`, `Rules`, `Agent` + test architektury | Przyjęta |
| ADR-10 | Sesja: cookie (Identity) z antyforgery, bez JWT | Przyjęta |
| ADR-11 | Pełna historia rozmów agenta w bazie, odczyt przez autora i kadry | Przyjęta |

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
