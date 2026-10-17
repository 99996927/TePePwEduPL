# Dokumentacja architektury — Asystent kadrowo-płacowy (arc42)

Dokumentacja systemu według szablonu [arc42](https://arc42.org/overview/). Język: polski.

**System:** Asystent kadrowo-płacowy z rozliczaniem czasu pracy dla fikcyjnej firmy. Obsługuje wnioski urlopowe,
ewidencję czasu pracy i wstępne naliczanie wynagrodzeń na danych syntetycznych. Agent AI odpowiada na pytania
pracowników, wykrywa naruszenia norm czasu pracy i przygotowuje korekty dla działu kadr.

**Status dokumentu:** wersja robocza do przeglądu. Zrealizowane: silnik reguł (plan 1), warstwa danych (plan 2), serwisy aplikacyjne (plan 3), API REST (plan 4), agent (plan 5), plan 6 (dane demo), plan 7a (Angular: logowanie, urlopy, czas pracy, przegląd zespołu; skrypty `tools/dev`). Pozostałe: plan 7b (ekrany płac i kadr), plan 7c (ekran agenta), Docker i testy E2E. ADR-26 czeka na akceptację.

| Sekcja | Plik |
|---|---|
| 1. Wprowadzenie i cele | [01-wprowadzenie-i-cele.md](01-wprowadzenie-i-cele.md) |
| 2. Ograniczenia | [02-ograniczenia.md](02-ograniczenia.md) |
| 3. Kontekst i zakres (w tym przypadki użycia) | [03-kontekst-i-zakres.md](03-kontekst-i-zakres.md) |
| 4. Strategia rozwiązania | [04-strategia-rozwiazania.md](04-strategia-rozwiazania.md) |
| 5. Widok bloków budulcowych | [05-widok-blokow.md](05-widok-blokow.md) |
| 6. Widok czasu wykonania | [06-widok-czasu-wykonania.md](06-widok-czasu-wykonania.md) |
| 7. Widok wdrożenia (w tym konta demo) | [07-widok-wdrozenia.md](07-widok-wdrozenia.md) |
| 8. Pojęcia przekrojowe | [08-pojecia-przekrojowe.md](08-pojecia-przekrojowe.md) |
| 9. Decyzje architektoniczne | [09-decyzje-architektoniczne.md](09-decyzje-architektoniczne.md) |
| 10. Wymagania jakościowe i testy | [10-jakosc.md](10-jakosc.md) |
| 11. Ryzyka i dług techniczny | [11-ryzyka.md](11-ryzyka.md) |
| 12. Słownik pojęć | [12-slownik.md](12-slownik.md) |

Diagramy są zapisane w Mermaid (renderowane przez GitHub).

## Zastrzeżenie prawne
Dane są syntetyczne. Reguły oparto na jednolitym tekście Kodeksu pracy (Dz.U. 2026 poz. 1245, stan prawny na 27.08.2026,
źródło: API Sejmu ELI). Naliczenia mają charakter **wstępny i demonstracyjny** i nie stanowią porady prawnej ani kadrowo-płacowej.
