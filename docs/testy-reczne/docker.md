# Testy ręczne — Docker (Compose, uruchomienie lokalne)

Sprawdza uruchomienie całego systemu w Docker Compose. Konta i hasła: `docs/arc42/07-widok-wdrozenia.md`, sekcja 7.2. Szczegóły instrukcji: `README.md`.

## Przygotowanie

1. Jeśli działa `tools/dev/run.sh`, zatrzymaj go: `tools/dev/stop.sh` (oba sposoby używają portu 5080).
2. `tools/docker/up.sh`. Oczekiwany koniec: `Test dymny zakończony powodzeniem.`, `Wszystkie kontrole przeszły.`, `==> Gotowe`.
3. Tunel SSH z Twojego komputera: `ssh -N -L 5080:127.0.0.1:5080 azureuser@tepecompute`, przeglądarka: http://localhost:5080.

## Co sprawdzić

| # | Krok | Oczekiwany wynik |
|---|---|---|
| 1 | Zaloguj się jako `anna.nowak` / `annanowak123` | Wchodzi pulpit w nowym stylu; ikony menu to rysunki (nie tekst `dashboard`, `beach_access`), polskie znaki (ą, ę, ł, ż) wyglądają poprawnie. |
| 2 | Przeklikaj konta: `ewa.wisniewska` (zatwierdzanie, przegląd zespołu), `piotr.zielinski` (zmiany płacowe, szkice korekt, audyt), `maria.kaminska` | Ekrany jak po planach 7a–7d. |
| 3 | Czat agenta bez klucza (`.env` bez `ANTHROPIC_API_KEY`): zadaj pytanie | Komunikat „Agent jest chwilowo niedostępny”, reszta aplikacji działa. |
| 4 | Czat z kluczem: dopisz `ANTHROPIC_API_KEY=...` do `.env`, uruchom `tools/docker/up.sh`, zadaj pytanie „Ile dni urlopu mi zostało?” jako Anna | Odpowiedź modelu; baza nie jest odtwarzana (dane z kroku 1 zostają). |
| 5 | Narzędzia przeglądarki, zakładka „Sieć”, odśwież stronę (Ctrl+F5) | Wszystkie żądania idą do `localhost:5080`; brak żądań do `fonts.googleapis.com` i `fonts.gstatic.com`. Opcjonalnie: tryb offline przeglądarki, strona nadal się wyświetla poprawnie po załadowaniu. |
| 6 | `tools/docker/down.sh`, potem `tools/docker/up.sh` | Dane zostają (te same wnioski i naliczenia); start jest szybki. |
| 7 | `docker compose ps` | `hr-db` i `hr-app` mają stan `healthy`; port `hr-app`: `127.0.0.1:5080->8080/tcp`; `hr-db` bez publikacji. |
| 8 | `tools/docker/down.sh --reset`, potem `tools/docker/up.sh` | Baza założona od nowa z danymi demo; test dymny przechodzi. |
| 9 | Na hoście: `ss -ltn 'sport = :5080'` | Nasłuch tylko na `127.0.0.1:5080`. |
