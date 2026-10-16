# 2. Ograniczenia

## 2.1 Ograniczenia techniczne
| Ograniczenie | Uzasadnienie |
|---|---|
| Backend: **.NET (ASP.NET Core)** | Decyzja właściciela projektu. Na maszynie dostępne SDK 9.0. |
| Frontend: **Angular** (build statyczny) | Decyzja właściciela projektu. Serwowany przez ten sam proces co API. |
| Baza danych: **Microsoft SQL Server** (kontener Linux) | Decyzja właściciela projektu. Wymaga min. 2 GB RAM. |
| Środowisko: **Docker Engine** na jednej maszynie, **dwa kontenery** | Kontener 1: backend .NET + statyczny Angular. Kontener 2: MSSQL. |
| Orkiestracja: **Docker Compose v2** | Zainstalowany pakietem `docker-compose-v2` (wersja 2.40.3). Docker 24.0.9 z pakietów `moby-*` nie zawiera Compose domyślnie. |
| Agent AI: **Anthropic API** (Claude, tool use) | Wymaga klucza API i dostępu do internetu z kontenera aplikacji. |
| Uwierzytelnianie: **lokalne konta w bazie** (ASP.NET Core Identity) | Bez zewnętrznego dostawcy tożsamości, aby zachować dwa kontenery. |

## 2.2 Zasoby maszyny docelowej (stan na 2026-10-16)
Linux x86_64, 4 vCPU, 32 GB RAM, ok. 54 GB wolnego miejsca, Docker 24.0.9, .NET SDK 9.0.

## 2.3 Ograniczenia organizacyjne i prawne
| Ograniczenie | Uzasadnienie |
|---|---|
| Wyłącznie **dane syntetyczne** | Brak danych osobowych prawdziwych osób. |
| Źródło przepisów: **Kodeks pracy** z API Sejmu ELI | Publiczne, oficjalne źródło (Dz.U. 2026 poz. 1245). |
| Naliczenia **wstępne, demonstracyjne** | Brak roszczenia do zgodności z aktualnym stanem podatkowo-składkowym. |
| Dokumentacja po polsku | Decyzja właściciela projektu. |

## 2.4 Konwencje
- Dokumentacja w formacie arc42, diagramy w Mermaid.
- Kwoty w arytmetyce `decimal`, brak liczb zmiennoprzecinkowych w silniku reguł.
- Identyfikatory przypadków użycia `UC-xx` są używane jako nazwy testów E2E.
