"""Czyszczenie tekstu PDF jednolitego tekstu Kodeksu pracy (Dz.U. 2026 poz. 1245) i podział na artykuły."""

import re

_PAGE_HEADER = re.compile(r"^\s*Dziennik Ustaw\s+–\s+\d+\s+–\s+Poz\.\s+\d+\s*$")
_FOOTNOTE_START = re.compile(r"^\s*(\d{1,3})\)\s+\S")
_ARTICLE_HEADING = re.compile(r"^Art\.\s+(\d+(?:\[[0-9a-z]+\])?)\.\s*(.*)$")
_STRUCTURE_HEADING = re.compile(r"^(DZIAŁ|Rozdział|Oddział)\b")
_PARAGRAPH_START = re.compile(r"^(§\s*\d|\d+\)\s|[a-z]\)\s|–\s)")
_ANNEX_MARKER = re.compile(r"^Załącznik do obwieszczenia")
_HYPHEN_BREAK = re.compile(r"([a-ząćęłńóśźż])-\n([a-ząćęłńóśźż])")


def _has_inline_marker(lines: list[str], number: int) -> bool:
    """Odnośnik do przypisu stoi w treści za tekstem (np. `§ 1.41) W zamian`), a nie na początku wiersza jak punkt listy."""
    pattern = re.compile(rf"\S ?{number}\)")
    return any(pattern.search(line) for line in lines)


def _strip_footnotes_of_page(lines: list[str], last_footnote: int) -> tuple[list[str], int]:
    for index, line in enumerate(lines):
        match = _FOOTNOTE_START.match(line)
        if not match:
            continue
        number = int(match.group(1))
        if number > last_footnote and _has_inline_marker(lines[:index], number):
            body = lines[:index]
            numbers = [int(m.group(1)) for m in map(_FOOTNOTE_START.match, lines[index:]) if m]
            last = max(n for n in numbers if n >= number and n <= number + len(numbers))
            marker = re.compile(rf"(?<=\S) ?(?:{'|'.join(str(n) for n in numbers)})\)(?=\s|$)")
            return [marker.sub("", b) for b in body], last
    return lines, last_footnote


def _drop_preamble(lines: list[str]) -> list[str]:
    """Obwieszczenie Marszałka Sejmu, tytuł ustawy i przypis o dyrektywach UE poprzedzają art. 1 Kodeksu i nie są jego treścią."""
    for index, line in enumerate(lines):
        if _ANNEX_MARKER.match(line):
            for start in range(index + 1, len(lines)):
                match = _ARTICLE_HEADING.match(lines[start])
                if match and match.group(1) == "1":
                    return lines[start:]
            raise ValueError("Nie znaleziono art. 1 Kodeksu pracy po załączniku.")
    return lines


def strip_page_noise(raw_text: str) -> str:
    """Usuwa nagłówki stron, przypisy dolne (wraz z odnośnikami w treści) i dzieli wyrazy z przeniesienia."""
    pages: list[list[str]] = [[]]
    for line in _drop_preamble(raw_text.splitlines()):
        if _PAGE_HEADER.match(line):
            pages.append([])
        else:
            pages[-1].append(line.rstrip())

    kept: list[str] = []
    last_footnote = 0
    for page in pages:
        body, last_footnote = _strip_footnotes_of_page(page, last_footnote)
        kept.extend(body)

    text = "\n".join(kept)
    text = _HYPHEN_BREAK.sub(r"\1\2", text)
    return "\n".join(re.sub(r"[ \t]{2,}", " ", line).strip() for line in text.splitlines())


def _article_lines(cleaned: str) -> list[str]:
    lines = cleaned.splitlines()
    for index, line in enumerate(lines):
        if _ANNEX_MARKER.match(line):
            lines = lines[index + 1:]
            break
    for index, line in enumerate(lines):
        match = _ARTICLE_HEADING.match(line)
        if match and match.group(1) == "1":
            return lines[index:]
    raise ValueError("Nie znaleziono art. 1 Kodeksu pracy.")


def _join(lines: list[str]) -> str:
    paragraphs: list[str] = []
    for line in lines:
        if not line:
            continue
        if paragraphs and not _PARAGRAPH_START.match(line):
            paragraphs[-1] += " " + line
        else:
            paragraphs.append(line)
    return "\n".join(paragraphs)


def split_articles(cleaned_text: str) -> list[dict]:
    """Dzieli oczyszczony tekst na artykuły: `{"number": "151[1]", "text": "…"}` w kolejności występowania."""
    articles: list[dict] = []
    current: list[str] | None = None
    number = ""
    skipping_title = False
    seen: set[str] = set()
    dropped = False

    def flush() -> None:
        if current is not None and not dropped:
            articles.append({"number": number, "text": _join(current)})

    for line in _article_lines(cleaned_text):
        heading = _ARTICLE_HEADING.match(line)
        if heading:
            flush()
            number = heading.group(1)
            # Gdy zmiana wejdzie w życie później, tekst jednolity podaje dwa brzmienia tego samego artykułu:
            # obowiązuje pierwsze, późniejsze pomijamy.
            dropped = number in seen
            seen.add(number)
            current = [heading.group(2)]
            skipping_title = False
            continue
        if _STRUCTURE_HEADING.match(line):
            skipping_title = True
            continue
        if skipping_title:
            # Tytuł działu lub rozdziału (kilka wierszy) kończy się przed pierwszym paragrafem albo artykułem.
            if _PARAGRAPH_START.match(line):
                skipping_title = False
            else:
                continue
        assert current is not None
        current.append(line)
    flush()
    return articles
