# Import Kodeksu pracy

Jednorazowy skrypt (narzędzie deweloperskie, poza rozwiązaniem .NET) pobiera jednolity tekst Kodeksu pracy
(Dz.U. 2026 poz. 1245) z API Sejmu i zapisuje artykuły do `src/Hr.Core/Resources/labour-code.json`.
Plik jest osadzony w `Hr.Core` i ładowany do bazy przy starcie aplikacji (`LabourCodeSeeder`).

```bash
cd tools/labour_code
python3 -m venv .venv && .venv/bin/pip install pypdf
python3 -m unittest -v                # testy czyszczenia tekstu
.venv/bin/python import_labour_code.py # pobiera PDF i zapisuje JSON
```

Skrypt kończy się kodem niezerowym, gdy brakuje któregoś z artykułów `129, 130, 131, 132, 133, 151, 151[1], 154`,
numery się powtarzają albo art. 131 i 132 nie zawierają oczekiwanych fraz (`przeciętnie 48 godzin`, `11 godzin`).

## Uwagi (R-06)

- Tekst pochodzi z ekstrakcji PDF, więc zdarzają się drobne artefakty (np. `dodate k` zamiast `dodatek`). Po każdym imporcie
  sprawdź wyrywkowo kluczowe artykuły z oryginałem.
- Numer artykułu zapisujemy jak w źródle: indeks górny w nawiasach kwadratowych (`151[1]`, `94[3a]`).
- Gdy zmiana wejdzie w życie później, tekst jednolity podaje dwa brzmienia tego samego artykułu (z przypisem „W tym
  brzmieniu obowiązuje do wejścia w życie zmiany…”). Import zachowuje **pierwsze**, obowiązujące w dniu publikacji tekstu
  jednolitego; późniejsze jest pomijane. Przy ponownym imporcie po wejściu zmian w życie warto sprawdzić te artykuły
  (w wersji z 2026 r.: `18[3d]`, `18[3e]`, `94[3]`).
- Pobrany PDF i katalog `.venv` nie są dodawane do repozytorium.
