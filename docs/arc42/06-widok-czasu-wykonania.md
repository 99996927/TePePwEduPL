# 6. Widok czasu wykonania

## 6.1 UC-06 Złożenie wniosku urlopowego
```mermaid
sequenceDiagram
    actor P as Pracownik
    participant UI as Angular
    participant API as API
    participant APP as Serwis wniosków
    participant ENG as Silnik reguł
    participant DB as MSSQL
    P->>UI: wybiera daty urlopu
    UI->>API: POST /leave-requests
    API->>APP: wniosek + tożsamość
    APP->>DB: saldo i kalendarz
    APP->>ENG: dni robocze, saldo po wniosku
    ENG-->>APP: wynik (liczba dni, wystarczające saldo?)
    alt saldo wystarczające
        APP->>DB: zapis wniosku (oczekuje) i audyt
        APP-->>UI: 201, wniosek do kierownika
    else brak dni (UC-07)
        APP-->>UI: 422, komunikat o braku dni
    end
```

## 6.2 UC-12/14 Wykrycie naruszeń czasu pracy
Silnik analizuje wpisy czasu: odpoczynek dobowy (11 h, art. 132 § 1), tygodniowy (35 h, art. 133 § 1) i średnią tygodniową
w okresie rozliczeniowym (48 h łącznie z nadgodzinami, art. 131 § 1). Wynik to lista naruszeń z identyfikatorem reguły,
datą i wartościami. Trafia do alertów pracownika i kierownika oraz do szkiców korekt dla kadr.

## 6.3 UC-22 Pytanie agenta o nadgodziny
```mermaid
sequenceDiagram
    actor P as Pracownik
    participant API as API
    participant AG as Moduł agenta
    participant C as Anthropic API
    participant APP as Serwis ewidencji
    participant ENG as Silnik reguł
    P->>API: "Ile mam nadgodzin w tym miesiącu?"
    API->>AG: pytanie + tożsamość użytkownika
    AG->>C: wiadomość + definicje narzędzi
    C-->>AG: wywołaj calculate_overtime(miesiąc)
    AG->>APP: wywołanie w kontekście użytkownika
    APP->>ENG: oblicz nadgodziny
    ENG-->>APP: wynik z identyfikatorem reguły
    APP-->>AG: wynik (audyt wywołania)
    AG->>C: wynik narzędzia
    C-->>AG: odpowiedź tekstowa
    AG-->>P: odpowiedź oparta na wyniku silnika
```
**Odpowiedź w całości:** UI wysyła zwykłe żądanie POST i pokazuje wskaźnik „agent pisze…”. Odpowiedź wraca po zakończeniu pętli narzędzi (ADR-14).

**Historia:** pytanie, wywołania narzędzi i odpowiedź są zapisywane w rozmowie użytkownika (ADR-11), a kolejne pytania dostają kontekst z historii.

**Zasada:** odpowiedź może zawierać liczby wyłącznie z wyników narzędzi. Przy błędzie narzędzia agent informuje o błędzie (UC-25).
Parametr identyfikujący pracownika nie pochodzi od modelu, tylko z sesji użytkownika (UC-24).

## 6.4 UC-18/19 Zmiana płacowa z dwuosobowym zatwierdzeniem
Zmiana dotyczy stawki lub składnika wynagrodzenia konkretnego pracownika. Ma datę obowiązywania i uzasadnienie (ADR-15).
Silnik reguł używa nowej wartości od tej daty, tylko dla zmian w stanie „Zatwierdzona”.

```mermaid
stateDiagram-v2
    [*] --> Szkic: HR tworzy zmianę
    Szkic --> Oczekuje: HR wysyła do zatwierdzenia
    Oczekuje --> Zatwierdzona: HRApprover zatwierdza (inna osoba niż autor)
    Oczekuje --> Odrzucona: HRApprover odrzuca z uzasadnieniem
    Zatwierdzona --> [*]
    Odrzucona --> [*]
```
Kontrola „autor ≠ zatwierdzający” jest realizowana w serwisie i dodatkowo ograniczeniem w bazie. Obie operacje trafiają do audytu.

## 6.5 UC-26 Szkic korekty przygotowany przez agenta
Agent wywołuje `check_work_time_violations`, a następnie `propose_correction`. Narzędzie zapisuje szkic korekty **wpisu czasu pracy**
ze statusem „do przeglądu”. Dane źródłowe (ewidencja, naliczenia) nie są zmieniane. Korektę przegląda i zatwierdza człowiek z kadr (UC-11).
Po zatwierdzeniu zmienia się wpis czasu, a naliczenie można uruchomić ponownie na poprawionych danych (UC-16).
Zmiany stawek i składników wynagrodzenia przechodzą osobny przepływ dwuosobowy według 6.4 i nie są tworzone przez agenta.
