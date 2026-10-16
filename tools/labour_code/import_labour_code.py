"""Pobiera jednolity tekst Kodeksu pracy z API Sejmu (ELI) i zapisuje artykuły do JSON osadzanego w Hr.Core."""

import datetime
import io
import json
import sys
import urllib.request
from pathlib import Path

from pypdf import PdfReader

from clean import split_articles, strip_page_noise

PDF_URL = "https://api.sejm.gov.pl/eli/acts/DU/2026/1245/text.pdf"
SOURCE = "Dz.U. 2026 poz. 1245"
ELI = "DU/2026/1245"
OUTPUT = Path(__file__).resolve().parents[2] / "src" / "Hr.Core" / "Resources" / "labour-code.json"

# Artykuły, na których opierają się reguły silnika (R-06): brak któregokolwiek oznacza błąd importu.
REQUIRED = ["129", "130", "131", "132", "133", "151", "151[1]", "154"]
# Wyrywkowa kontrola treści kluczowych przepisów: (artykuł, fraza).
EXPECTED_PHRASES = [("131", "przeciętnie 48 godzin"), ("132", "11 godzin")]


def verify(articles: list[dict]) -> list[str]:
    problems: list[str] = []
    numbers = [a["number"] for a in articles]
    duplicates = sorted({n for n in numbers if numbers.count(n) > 1})
    if duplicates:
        problems.append(f"Powtórzone numery artykułów: {', '.join(duplicates)}")
    by_number = {a["number"]: a["text"] for a in articles}
    for number in REQUIRED:
        if number not in by_number:
            problems.append(f"Brak artykułu {number}")
    for number, phrase in EXPECTED_PHRASES:
        if number in by_number and phrase not in by_number[number]:
            problems.append(f"Art. {number} nie zawiera frazy „{phrase}”")
    return problems


def main() -> int:
    with urllib.request.urlopen(PDF_URL, timeout=120) as response:
        pdf_bytes = response.read()
    reader = PdfReader(io.BytesIO(pdf_bytes))
    raw = "\n".join(page.extract_text() or "" for page in reader.pages)
    articles = split_articles(strip_page_noise(raw))

    problems = verify(articles)
    if problems:
        print("Import przerwany:\n- " + "\n- ".join(problems), file=sys.stderr)
        return 1

    document = {
        "source": SOURCE,
        "eli": ELI,
        "retrievedOn": datetime.date.today().isoformat(),
        "articles": articles,
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(document, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(f"Zapisano {len(articles)} artykułów do {OUTPUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
