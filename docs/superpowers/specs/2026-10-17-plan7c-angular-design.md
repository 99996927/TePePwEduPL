# Plan 7c: Angular — czat z agentem, historia i odczyt rozmów przez kadry — projekt

**Data:** 2026-10-17
**Status:** do przeglądu
**Podstawa:** `docs/arc42` (ADR-11, ADR-14, ADR-26, ADR-30; sekcje 3, 6.3, 8.3a; R-10), plany 1–7b zmergowane.
**Poza zakresem tego planu:** strumieniowanie odpowiedzi (ADR-14: zwykły POST), usuwanie i zmiana nazw rozmów, Docker Compose (plan Docker), testy E2E w Playwright (plan E2E).

## 1. Cel

Ostatni plan frontendu: ekran agenta na tym samym stosie co 7a i 7b (Angular 22, Material, standalone, signals, klient z `ng-openapi-gen`).

| UC | Zakres w UI |
|---|---|
| 21–25, 27 | Czat: pytanie, odpowiedź, wskaźnik oczekiwania, błędy i ostrzeżenie o liczbach spoza narzędzi |
| 26 | Szkice korekt tworzy agent po stronie backendu; ekran szkiców istnieje (7b), czat tylko pokazuje wywołanie narzędzia |
| 30 | Historia własnych rozmów i powrót do rozmowy (dopytywanie) |
| 31 | Kadry (`HR`, `HRApprover`) czytają rozmowy wszystkich; odczyt jest zapisywany w audycie przez backend |

**Wymóg właściciela:** po planie całe rozwiązanie działa (`tools/dev/run.sh`), a właściciel testuje przez tunel SSH. Właściciel podaje klucz Anthropic w `.env`, więc ręczny test obejmuje prawdziwy model.

### Kryteria sukcesu
1. `npm run verify` w `web/` przechodzi (`api:check`, testy, build produkcyjny).
2. `dotnet build Hr.sln` i `dotnet test Hr.sln` przechodzą (w tym migawka OpenAPI).
3. `tools/dev/run.sh` stawia aplikację na `http://127.0.0.1:5080`, a `tools/dev/smoke.sh` przechodzi.
4. Właściciel dostaje kroki testu ręcznego (`docs/testy-reczne/plan-7c.md`) i komendę tunelu SSH.

## 2. Decyzje

| Decyzja | Wybór | Uzasadnienie |
|---|---|---|
| Układ | Jeden ekran: lista rozmów po lewej, rozmowa po prawej | Właściciel; jedna pozycja menu, naturalne „wróć do rozmowy”. |
| Klucz modelu | `ANTHROPIC_API_KEY` z `.env`, przekazywany przez `run.sh` | Właściciel ma klucz; bez klucza backend zwraca 503 (ADR-26), reszta działa. Testy automatyczne bez prawdziwego modelu. |
| Kontrakt API | Bez zmian | `Conversations_List`, `Conversations_Get`, `Conversations_Ask` wystarczają; nazwiska autorów z `EmployeeDirectory` (ADR-30). |
| Dane demo | Jedna deterministyczna rozmowa Anny (zmiana wymaga `stop.sh --reset`) | Kadry mogą przetestować UC-31 bez klucza i bez wcześniejszej rozmowy. |

## 3. Ekran agenta

**Trasy:** `/agent` (nowa rozmowa) i `/agent/:id` (wybrana), ładowane leniwie, dostępne dla każdej zalogowanej roli. Jedna pozycja menu „Agent”.

**Panel rozmów (lewy):** wynik `Conversations_List`: podgląd pierwszego pytania, data, liczba wiadomości, przycisk „Nowa rozmowa”. Dla kadr lista obejmuje rozmowy wszystkich (zakres egzekwuje backend); przy cudzych rozmowach widać autora (nazwisko z katalogu pracowników po `userId`) i przełącznik „Tylko moje”. Rola bez dostępu do cudzych rozmów nie widzi ani autora, ani przełącznika, bo lista zawiera tylko jej rozmowy.

**Rozmowa (prawy):**
- Dymki użytkownika i agenta. Wywołania narzędzi to zwijane wiersze „Narzędzie: nazwa” z parametrami jako tekstem; widoczne w widoku rozmowy dla każdej roli (to dane zapisane w rozmowie, UC-31).
- Wysłanie: `POST /api/conversations/messages`. Dymek użytkownika pojawia się od razu, pod nim wskaźnik „agent pisze…”. Po odpowiedzi do rozmowy trafiają `messages` z odpowiedzi. Pierwsza odpowiedź nowej rozmowy przejmuje `conversationId` i zmienia adres na `/agent/:id`, a lista się odświeża.
- Enter wysyła, Shift+Enter wstawia nową linię. Limit 4000 znaków (`AgentRunner.MaxQuestionLength`) z licznikiem. Pole jest zablokowane w trakcie wysyłania, puste pytanie nie jest wysyłane.
- Stała informacja przy własnej rozmowie: kadry mogą czytać rozmowy (R-10).
- Rozmowa cudza (autor ≠ zalogowany): tryb tylko do odczytu, bez pola pytania, z banerem „Czytasz rozmowę: <autor>. Odczyt jest zapisany w audycie.”

**Obsługa błędów:**

| Odpowiedź | Zachowanie |
|---|---|
| 422 | Komunikat pod polem, tekst zostaje |
| 409 | Komunikat „Rozmowa zmieniła się, spróbuj ponownie”; rozmowa przeładowana z serwera; tekst zostaje w polu |
| 503 | Komunikat o niedostępnym agencie; rozmowa przeładowana (backend zapisał pytanie, `conversationId` jest w problemie); tekst nie ginie |
| 404 | Snackbar, powrót do `/agent` |
| 401 | Jak dotąd: czyszczenie sesji i logowanie |
| `ungroundedNumbers` niepuste | Pod odpowiedzią ostrzeżenie „Liczby nie pochodzą z danych systemu: …” (ADR-26); po przeładowaniu rozmowy znika, bo nie jest zapisywane w wiadomości |

## 4. Backend, dane demo i narzędzia

- **Kontrakt API:** bez zmian, `docs/openapi/hr-api.json` i klient nie zmieniają się.
- **Dane demo:** jedna rozmowa Anny z deterministycznym identyfikatorem: pytanie o saldo urlopu, wywołanie narzędzia i odpowiedź. Wiadomości o rolach i polach zgodnych z `ConversationStore`; generator i seeder jak przy szkicu korekty w 7b.
- **`tools/dev/run.sh`:** jeżeli `.env` zawiera `ANTHROPIC_API_KEY`, jest eksportowany procesowi API; klucz nigdy nie jest wypisywany ani commitowany. Bez klucza komunikat informuje, że czat zwróci 503.
- **`tools/dev/smoke.sh`:** nowe kroki: lista rozmów Anny zawiera rozmowę demo; kadry (kto widzi wszystkich) otwierają ją; w audycie pojawia się `ConversationRead`; pracownik spoza zakresu dostaje 404. Smoke nie pyta modelu.

## 5. Testy

**Vitest:** wysłanie pytania ze wskaźnikiem i doklejeniem wiadomości; przejęcie `conversationId` i zmiana adresu; Enter/Shift+Enter, limit znaków, brak wysłania pustego; 422, 409 i 503 (komunikat, przeładowanie, tekst zachowany); ostrzeżenie `ungroundedNumbers`; tryb tylko do odczytu z banerem dla cudzej rozmowy; autor i „Tylko moje” widoczne tylko dla kadr; wywołanie narzędzia jako zwijany wiersz; trasa dostępna dla wszystkich ról (`routes.spec.ts`).

**Backend:** test generatora i seedera dla rozmowy demo (liczba, idempotencja); `dotnet test Hr.sln` bez regresji.

**Całość:** `npm run verify`, `tools/dev/smoke.sh`. E2E w Playwright to osobny plan; dla 7c powstaje lista kroków ręcznych.

## 6. Dokumentacja

- `docs/testy-reczne/plan-7c.md` (kroki per rola, z kontami demo; część z kluczem modelu opisana osobno).
- arc42: sekcje 5, 7 (klucz w `.env`, rozmowa demo), 10 (testy), ADR-31 (ekran agenta: jeden widok, bez strumieniowania, ostrzeżenie o liczbach w UI), status w README; `web/README.md`.

## 7. Ryzyka

- **Odpowiedź w całości** potrafi trwać kilkanaście sekund przy pętli narzędzi: wskaźnik i blokada pola, bez limitu czasu po stronie UI poza domyślnym.
- **Prawdziwy model w teście ręcznym** daje niedeterministyczne odpowiedzi; kryteria testu dotyczą zachowania ekranu, nie treści odpowiedzi.
- **Jawność rozmów** (R-10): baner i stała informacja w UI; audyt odczytu po stronie backendu.
- **409 przy dwóch kartach** z tą samą rozmową: komunikat i przeładowanie, bez automatycznego ponawiania.
