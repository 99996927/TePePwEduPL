import unittest

from clean import split_articles, strip_page_noise

PAGE_WITH_FOOTNOTES = """Art. 151[1]. § 1.41) W zamian za czas przepracowany pracodawca może udzielić czasu wolnego. 40) Odpracowanie nie może
naruszać prawa do odpoczynku.
§ 2. Drugi paragraf.
40) Zdanie pierwsze ze zmianami wprowadzonymi przez art. 1 pkt 5 ustawy, o której mowa w odnośniku 16.
41) Ze zmianami wprowadzonymi przez art. 1 pkt 6 ustawy,
o której mowa w odnośniku 16.
Dziennik Ustaw – 55 – Poz. 1245
§ 3. Trzeci paragraf, już na kolejnej stronie.
"""

ANNEX = """OBWIESZCZENIE
Art. 10. Ustawa wchodzi w życie z dniem 1 stycznia 2026 r.”;
Marszałek Sejmu: W. Czarzasty
Dziennik Ustaw – 4 – Poz. 1245
Załącznik do obwieszczenia Marszałka Sejmu
USTAWA
Kodeks pracy1)
Art. 1. Kodeks pracy określa prawa i obowiązki pracowników i pracodawców.
Art. 2. Pracownikiem jest osoba zatrudniona.
"""


class StripPageNoiseTests(unittest.TestCase):
    def test_strips_page_header_and_footnotes(self):
        cleaned = strip_page_noise(PAGE_WITH_FOOTNOTES)
        self.assertNotIn("Dziennik Ustaw", cleaned)
        self.assertNotIn("Zdanie pierwsze ze zmianami", cleaned)
        self.assertNotIn("o której mowa w odnośniku", cleaned)
        self.assertIn("Trzeci paragraf, już na kolejnej stronie.", cleaned)

    def test_strips_inline_footnote_markers(self):
        cleaned = strip_page_noise(PAGE_WITH_FOOTNOTES)
        self.assertIn("§ 1. W zamian za czas", cleaned)
        self.assertNotIn("40)", cleaned)
        self.assertNotIn("41)", cleaned)

    def test_keeps_list_points_that_look_like_footnotes(self):
        text = "Art. 5. Pracodawca:\n1) udziela urlopu;\n2) wypłaca wynagrodzenie.\nDziennik Ustaw – 7 – Poz. 1245\n"
        cleaned = strip_page_noise(text)
        self.assertIn("1) udziela urlopu;", cleaned)
        self.assertIn("2) wypłaca wynagrodzenie.", cleaned)

    def test_joins_hyphenated_line_breaks(self):
        self.assertIn("odpoczynku", strip_page_noise("prawo do odpo-\nczynku dobowego"))


class SplitArticlesTests(unittest.TestCase):
    def test_splits_articles_with_superscript_numbers(self):
        text = (
            "Art. 151. § 1. Praca ponad normy, zob. art. 151 § 1 oraz\n"
            "art. 151[1] § 1 dalej w tym samym artykule.\n"
            "Art. 151[1]. § 1. Za pracę w godzinach nadliczbowych przysługuje dodatek.\n"
            "Art. 94[3a]. Regulamin.\n"
        )
        articles = split_articles("Art. 1. Pierwszy.\n" + text)
        self.assertEqual(["1", "151", "151[1]", "94[3a]"], [a["number"] for a in articles])
        self.assertIn("art. 151[1] § 1 dalej", articles[1]["text"])

    def test_skips_preamble_before_first_article_of_code(self):
        articles = split_articles(strip_page_noise(ANNEX))
        self.assertEqual("1", articles[0]["number"])
        self.assertEqual(["1", "2"], [a["number"] for a in articles])

    def test_repeal_marker_kept(self):
        articles = split_articles("Art. 1. Pierwszy.\nArt. 12. (uchylony)\nArt. 13. Trzynasty.")
        self.assertEqual("(uchylony)", articles[1]["text"])

    def test_later_variant_of_a_repeated_article_is_dropped_and_first_is_kept(self):
        # Tekst jednolity podaje dwa brzmienia artykułu, gdy zmiana wejdzie w życie później; obowiązuje pierwsze.
        text = "Art. 1. Pierwszy.\nArt. 94[3]. § 1. Obecne brzmienie.\nArt. 94[3]. § 1. Przyszłe brzmienie.\nArt. 95. Następny."
        articles = split_articles(text)
        self.assertEqual(["1", "94[3]", "95"], [a["number"] for a in articles])
        self.assertEqual("§ 1. Obecne brzmienie.", articles[1]["text"])

    def test_chapter_headings_do_not_leak_into_previous_article(self):
        text = "Art. 1. Pierwszy.\nRozdział V\nPraca w godzinach nadliczbowych\nArt. 151. § 1. Praca."
        articles = split_articles(text)
        self.assertEqual("Pierwszy.", articles[0]["text"])

    def test_paragraphs_start_on_new_lines_and_wrapped_lines_are_joined(self):
        text = "Art. 1. § 1. Pierwszy akapit\nciąg dalszy.\n§ 2. Drugi akapit:\n1) punkt;\n2) drugi punkt."
        self.assertEqual(
            "§ 1. Pierwszy akapit ciąg dalszy.\n§ 2. Drugi akapit:\n1) punkt;\n2) drugi punkt.",
            split_articles(text)[0]["text"],
        )


if __name__ == "__main__":
    unittest.main()
