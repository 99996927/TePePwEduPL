# Plan 7d: Angular — nowy wygląd (styl „Material Dashboard”) — projekt

**Data:** 2026-10-17
**Status:** do przeglądu
**Podstawa:** `docs/arc42` (ADR-14 stos frontendu, ADR-31), plany 1–7c zmergowane; wzór wizualny: https://www.creative-tim.com/product/material-dashboard-angular2 (odtwarzamy tylko wygląd, bez kodu ani licencji szablonu).
**Poza zakresem:** tryb ciemny, samodzielne hostowanie fontów (plan Docker), nowe funkcje, zmiany backendu i kontraktu API, testy E2E.

## 1. Cel

Aplikacja ma wyglądać atrakcyjniej: jasnoszare tło, białe karty z cieniem, kolorowe gradientowe akcenty i ikony, menu boczne z ikonami. Zmiana jest wyłącznie wizualna: logika, teksty, atrybuty `data-*` i role ARIA pozostają, więc istniejące testy mają przechodzić bez zmian.

### Kryteria sukcesu
1. `npm run verify` w `web/` przechodzi (klient API bez zmian, testy, build produkcyjny).
2. Wszystkie dotychczasowe testy przechodzą bez modyfikacji asercji; nowe testy obejmują tylko nowe elementy (ikony menu, chipy statusów).
3. `tools/dev/run.sh` stawia aplikację, a `tools/dev/smoke.sh` przechodzi.
4. Właściciel dostaje listę ekranów do obejrzenia (`docs/testy-reczne/plan-7d.md`) i ocenia wygląd przez tunel SSH; uwagi poprawiamy w jednej turze.

## 2. Decyzje

| Decyzja | Wybór | Uzasadnienie |
|---|---|---|
| Kierunek | A „klasyczny Material Dashboard” z jasnoszarym menu | Wybór właściciela na makietach (2026-10-17). |
| Zakres | Cała aplikacja | Wybór właściciela; spójność ekranów. |
| Technika | Nadpisanie motywu Material 3 plus wspólne style (`styles/`) i przebudowa szablonów | Komponenty, testy i dostępność Material zostają; najmniej ryzyka. |
| Fonty i ikony | Roboto i Material Symbols z Google Fonts, jak dotąd | Samodzielne hostowanie jest w planie Docker. |
| Tryb ciemny | Nie | Poza zakresem. |

## 3. Motyw i wspólne style

- **Tokeny** (zmienne CSS na `:root`, definiowane w `web/src/styles/`): tło strony `#eee`; karta biała z cieniem `0 2px 8px rgba(0,0,0,.12)` i promieniem 8 px; akcent główny gradient `linear-gradient(60deg, #ab47bc, #8e24aa)`; kolory statusów: zielony (zatwierdzone), pomarańczowy (oczekujące), czerwony (odrzucone, błędy, alerty), turkusowy (informacje). Każdy kolor ma gradient 60° jak we wzorze.
- **Motyw Material:** paleta główna fioletowa (`mat.$violet-palette` lub odpowiednik dostępny w Material 22), kolory błędów bez zmian semantyki; kontrast tekstu co najmniej WCAG AA (menu, chipy, tekst na gradiencie).
- **Wspólne klasy** (jeden plik stylów globalnych, importowany z `styles.scss`): `page-card` (biała karta), `page-card--tile` (karta z pływającą gradientową ikoną w lewym górnym rogu, jak na pulpicie wzoru), `page-card__header` (gradientowy nagłówek karty z tytułem, wystający nad treść), `status-chip` z wariantami kolorów, domyślny wygląd tabel (nagłówek, delikatne paski, odstępy), bazowe odstępy strony.
- **Statusy jako chipy:** statusy wniosków urlopowych, szkiców korekt, zmian płacowych i naliczeń wyświetlane chipem w kolorze statusu; tekst statusu (polska etykieta) bez zmian, kolor jest dodatkiem, nie jedynym nośnikiem informacji.

## 4. Szkielet (`core/layout`)

- **Menu boczne:** tło `#f4f5f7`, szerokość 260 px; u góry nazwa aplikacji; każda pozycja z ikoną (Material Symbols; mapowanie ikon na pozycje menu w jednym miejscu w `shell.ts`); aktywna pozycja z gradientem akcentu, białym tekstem i cieniem; na dole nazwa i rola użytkownika.
- **Pasek górny:** biały, z tytułem bieżącego ekranu (z danych trasy lub etykiety menu) i menu użytkownika z „Wyloguj” (na wąskim ekranie także przycisk menu). Zamiast kolorowego `mat-toolbar color="primary"`.
- **Zachowania bez zmian:** menu zależne od roli, zwijanie `over` poniżej 800 px, przekierowania guardów, `aria-label` przycisków; liczby pozycji menu (5/7/10) bez zmian.

## 5. Ekrany

| Ekran | Zmiana |
|---|---|
| Logowanie | Karta z gradientowym nagłówkiem na szarym tle; panel „Konta demo” w buildzie deweloperskim zachowany. |
| Pulpit | Cztery kafelki w stylu `page-card--tile` z kolorowymi ikonami (saldo, miesiąc, alerty, do zatwierdzenia); alerty w kolorze czerwonym, brak alertów w zielonym. |
| Urlopy, zatwierdzanie | Tabela w `page-card` z gradientowym nagłówkiem, filtr statusu w nagłówku, chipy statusów. |
| Czas pracy, analiza | Wpisy w karcie; panel analizy jako osobna karta z wyróżnionymi wartościami i kolorowymi alertami naruszeń. |
| Przegląd zespołu | Tabela w karcie, znaczniki alertów jako chipy. |
| Naliczenia, zmiany płacowe, szkice korekt | Karty i chipy statusów jak wyżej; kwoty wyrównane do prawej. |
| Audyt | Tabela w karcie z filtrem w nagłówku. |
| Agent | Lista rozmów jako karta; wiadomości jako dymki (użytkownik po prawej w kolorze akcentu, agent po lewej na białym), wywołania narzędzi jako zwijane bloki w stonowanym kolorze; ostrzeżenie o liczbach w kolorze pomarańczowym. |
| Dialogi | Zaokrąglone, z cieniem, tytuł z akcentem; bez zmian w polach i przyciskach. |

## 6. Testy i weryfikacja

- Istniejące testy komponentów przechodzą bez zmian asercji (selektory `data-*`, role i etykiety pozostają).
- Nowe testy: ikony w pozycjach menu dla każdej roli (liczby pozycji bez zmian), tytuł ekranu w pasku górnym, chip statusu z właściwym wariantem i polską etykietą (jeden wspólny element lub klasa, testowane raz).
- `npm run verify`; `dotnet build` i testy backendu nie są ruszane (brak zmian poza `web/` i dokumentacją).
- Ocena wizualna: właściciel przez tunel SSH według `docs/testy-reczne/plan-7d.md`. Agent nie ma przeglądarki, więc nie robi zrzutów ekranu; to jawne ograniczenie weryfikacji.

## 7. Dokumentacja

- `web/README.md`: struktura `styles/`, wspólne klasy, jak dodać chip lub kafelek.
- arc42: sekcja 5 (układ i style), nowy ADR „Motyw: Material 3 z nadpisaniem tokenów i wspólne klasy stylów”, status w README.
- `docs/testy-reczne/plan-7d.md`: lista ekranów i rzeczy do obejrzenia per rola.

## 8. Ryzyka

- **Kontrast:** biały tekst na gradiencie i kolorowe chipy mogą nie spełniać AA; łagodzi to dobór odcieni i sprawdzenie wartości kontrastu w planie.
- **Rozjazd z komponentami Material:** nadpisania tokenów mogą nie obejmować wszystkich kontrolek; łagodzi to ręczny przegląd ekranów przez właściciela.
- **Wygląd bez podglądu:** brak przeglądarki po stronie agenta oznacza, że pierwsze oceny wizualne pochodzą od właściciela; stąd jedna tura poprawek po jego uwagach.
- **Zależność od Google Fonts:** bez sieci wraca font systemowy; trwałe rozwiązanie w planie Docker.
