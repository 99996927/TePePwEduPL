# 11. Ryzyka i dług techniczny

| ID | Ryzyko | Wpływ | Środek zaradczy |
|---|---|---|---|
| R-01 | Model w odpowiedzi poda liczbę niepochodzącą z silnika | Błędne informacje dla pracownika | Narzędzia jako jedyne źródło liczb, test kontraktu, instrukcja systemowa, odpowiedzi z odwołaniem do reguły. |
| R-02 | Uproszczone reguły płacowe odbiegają od aktualnego stanu prawnego | Wprowadzające w błąd naliczenia | Oznaczenie „wstępne”, odwołania do artykułów, ręczna weryfikacja przed kodowaniem reguły. |
| R-03 | Jawne, przewidywalne hasła (wszystkie konta, także wygenerowane: `imięnazwisko123`) i poluzowana polityka | Przejęcie kont, jeśli środowisko wyjdzie poza demo | Tylko środowisko demo, dane syntetyczne, blokada konta, nie wystawiać do internetu. |
| R-04 | Zależność od Anthropic API (dostępność, koszt, klucz) | Brak funkcji agenta | Degradacja do działania bez agenta, limity, klucz w `.env`. |
| R-05 | Wyciek klucza API lub hasła SA | Nadużycie | `.env` poza repozytorium, `.gitignore`, brak sekretów w obrazach. |
| R-06 | Błędy ekstrakcji tekstu z PDF Kodeksu pracy | Błędne cytaty agenta | Czyszczenie przypisów i nagłówków, weryfikacja numerów artykułów, test próbny. |
| R-07 | Wstrzyknięcie poleceń przez treść pytania (prompt injection) | Obejście zasad | Uprawnienia egzekwowane po stronie serwera, narzędzia bez zapisu danych, szkice wymagają człowieka. |
| R-08 | Złożoność okresów rozliczeniowych (4 do 12 miesięcy) | Błędne średnie | Okres jako jawny parametr, testy graniczne. |
| R-09 | Docker Compose nie był domyślnie zainstalowany na hoście | Problem przy odtwarzaniu środowiska | Opisane w sekcji 2, wymaga pakietu `docker-compose-v2`. |
| R-10 | Kadry czytają pełną treść rozmów, pracownicy mogą unikać wrażliwych pytań | Mniejsze zaufanie, ograniczone użycie agenta | Jawna informacja w UI, audyt każdego odczytu, tylko role `HR`/`HRApprover`, dane syntetyczne w demo. |

## Dług techniczny (świadomy)
- Brak zewnętrznego SSO i MFA.
- Uproszczone stawki podatkowo-składkowe.
- Jeden host, brak wysokiej dostępności i kopii zapasowych poza wolumenem Docker.
- Brak HTTPS na poziomie hosta (do dodania przy ekspozycji poza sieć lokalną).
