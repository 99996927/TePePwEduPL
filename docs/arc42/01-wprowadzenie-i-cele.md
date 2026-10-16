# 1. Wprowadzenie i cele

## 1.1 Opis zadania
System obsługuje podstawowe procesy kadrowo-płacowe fikcyjnej firmy na **danych syntetycznych**:

- wnioski urlopowe (składanie, zatwierdzanie, saldo),
- ewidencja czasu pracy (wpisy, korekty, nadgodziny),
- wstępne naliczanie wynagrodzeń,
- **agent AI**, który odpowiada pracownikom na pytania o urlop, nadgodziny i publiczne przepisy Kodeksu pracy,
  wykrywa naruszenia norm czasu pracy i przygotowuje szkice korekt dla działu kadr.

Architektura: **trójwarstwowa** (prezentacja — logika — dane), uruchamiana w Docker Engine w dwóch kontenerach.

## 1.2 Najważniejsze wymagania
| ID | Wymaganie |
|---|---|
| R1 | Wnioski urlopowe z kontrolą salda i zatwierdzaniem przez kierownika. |
| R2 | Ewidencja czasu pracy z wyliczaniem godzin nadliczbowych. |
| R3 | Wstępne naliczanie wynagrodzeń przez deterministyczny silnik reguł. |
| R4 | Agent AI odpowiadający na pytania i przygotowujący korekty (bez wprowadzania zmian samodzielnie). |
| R5 | Wykrywanie naruszeń norm czasu pracy (art. 129, 131, 132, 133 Kodeksu pracy). |
| R6 | Role i separacja danych: pracownik — swoje dane, kierownik — swój zespół, kadry — całość. |
| R7 | Zmiana płacowa wymaga zatwierdzenia przez dwie różne osoby. |
| R8 | Pełna powtarzalność obliczeń i pokrycie silnika reguł testami. |

Szczegółowe przypadki użycia: [sekcja 3](03-kontekst-i-zakres.md).

## 1.3 Cele jakościowe
| Priorytet | Cel | Opis |
|---|---|---|
| 1 | **Determinizm obliczeń** | Silnik reguł płacowych jest jedynym źródłem liczb. Agent go wywołuje i nigdy nie zastępuje własnymi obliczeniami. |
| 2 | **Bezpieczeństwo i separacja danych** | Autoryzacja egzekwowana po stronie serwera, zasada czterech oczu dla zmian płacowych. |
| 3 | **Testowalność** | Silnik reguł bez I/O, testy jednostkowe, testy E2E (Playwright) powiązane z przypadkami użycia. |
| 4 | **Prostota wdrożenia** | Całość uruchamiana jednym poleceniem `docker compose up` na jednej maszynie. |
| 5 | **Audytowalność** | Każda zmiana i każde wywołanie narzędzia przez agenta jest zapisywane. |

## 1.4 Interesariusze
| Rola | Oczekiwania |
|---|---|
| Pracownik | Szybkie odpowiedzi o urlopie i nadgodzinach, prosty wniosek urlopowy. |
| Kierownik | Zatwierdzanie wniosków zespołu, wgląd w ewidencję i alerty zespołu. |
| Kadry (specjalista) | Przygotowywanie zmian płacowych i korekt, przegląd naruszeń. |
| Kadry (zatwierdzający) | Druga, niezależna akceptacja zmian płacowych. |
| Autor/twórca systemu | Przejrzysta architektura, powtarzalne testy, dokumentacja. |
