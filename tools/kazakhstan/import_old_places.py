"""One-time import: the places of the old kieli.kz («attraction», with their 360° views and translations) into
db/seed/kazakhstan.json and db/seed/i18n/*.json. On the next start the site adds every seed place it does not have yet
(Setup/DatabaseBootstrap.cs, AddMissingPlaces), so running sites get them too.

    python3 tools/kazakhstan/import_old_places.py db/kieli_db.sql

The dump of the old database is not part of the repository (it holds the old site's accounts): keep it local.
Places already in the seed (same legacyId) are left as they are. Titles of the old site are in capitals: the usual
spelling of each word is taken from the place's own text. Categories come from words of the title (see CATEGORY);
check them in the admin («Киелі» · Қазақстан → Нысандар).
"""
import html
import json
import os
import re
import sys
from html.parser import HTMLParser

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SEED = os.path.join(ROOT, "db", "seed")
OLD_SITE = "https://kieli.kz"
LANGS = ("ru", "en", "tr", "zh-cn")

# ---- reading a mysqldump ---------------------------------------------------------------------------------

def _rows(body):
    rows, row, i, n = [], [], 0, len(body)
    while i < n:
        c = body[i]
        if c == "(":
            row, i = [], i + 1
        elif c == "'":
            j, buf = i + 1, []
            while True:
                d = body[j]
                if d == "\\":
                    buf.append({"n": "\n", "r": "\r", "t": "\t", "0": "\0", "Z": "\x1a"}.get(body[j + 1], body[j + 1]))
                    j += 2
                elif d == "'" and j + 1 < n and body[j + 1] == "'":
                    buf.append("'")
                    j += 2
                elif d == "'":
                    break
                else:
                    buf.append(d)
                    j += 1
            row.append("".join(buf))
            i = j + 1
        elif c in ",; \n\r\t":
            i += 1
        elif c == ")":
            rows.append(row)
            i += 1
        else:
            tok = re.match(r"NULL|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?|0x[0-9A-Fa-f]+", body[i:i + 64]).group(0)
            row.append(None if tok == "NULL" else float(tok) if re.search(r"[.eE]", tok) and not tok.startswith("0x") else tok if tok.startswith("0x") else int(tok))
            i += len(tok)
    return rows


def read_tables(path, names):
    s = open(path, encoding="utf-8", errors="replace").read()
    out = {}
    for t in names:
        m = re.search(r"CREATE TABLE `%s` \((.*?)\n\) ENGINE" % t, s, re.S)
        cols = re.findall(r"^\s+`(\w+)`", m.group(1), re.M)
        out[t] = [dict(zip(cols, r)) for ins in re.finditer(r"INSERT INTO `%s` VALUES (.*?);\n" % t, s, re.S) for r in _rows(ins.group(1))]
    return out

# ---- text ------------------------------------------------------------------------------------------------

def absolute(url):
    url = (url or "").strip()
    if url.startswith("//"):
        return "https:" + url
    m = re.match(r"^(?:\.\./)*/?(uploads/.*)$", url)
    return OLD_SITE + "/" + m.group(1) if m else url


# YouTube videos of the old pages that YouTube no longer has (checked 2026-10-01, oEmbed 404): left out
DEAD_VIDEOS = {"3QTPtSbW2lA", "6N4EVJgr_9c", "8UtfaRDKNqs", "GsrF1LC_8c4", "UBReL700fIc", "WITDbVazH3w", "ZgD_hsKbqd4", "_7vIPPKhodg", "cJ-g0MZLhIY", "fhWgq34XP_g", "lP3wdjKE3TI", "nvTWeY4faRI", "ue5P9yugjNk", "y_1yxcRJOLY"}

IFRAME_HOSTS = ("www.youtube.com", "youtube.com", "www.youtube-nocookie.com", "player.vimeo.com", "www.google.com", "yandex.ru", "yandex.kz")


class Cleaner(HTMLParser):
    """The old editor's HTML with only plain article markup left; image credits (data-copyright) become captions."""
    KEEP = {"p", "br", "h2", "h3", "h4", "strong", "em", "u", "ul", "ol", "li", "blockquote", "a", "img", "figure",
            "figcaption", "iframe", "audio", "source", "table", "thead", "tbody", "tr", "th", "td", "sub", "sup"}
    RENAME = {"b": "strong", "i": "em", "h1": "h2", "h5": "h4", "h6": "h4"}
    VOID = {"br", "img", "source"}
    BLOCK = {"p", "h2", "h3", "h4", "ul", "ol", "table", "blockquote", "figure", "iframe"}
    DROP = {"script", "style", "noscript", "object", "embed"}

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.out, self.stack, self.drop = [], [], 0

    def _close(self, tag):
        while self.stack:
            t = self.stack.pop()
            self.out.append("</%s>" % t)
            if t == tag:
                break

    def handle_starttag(self, tag, attrs):
        tag = self.RENAME.get(tag, tag)
        if tag in self.DROP:
            self.drop += 1
            return
        if self.drop or tag not in self.KEEP:
            return
        a = {k.lower(): (v or "") for k, v in attrs}
        if tag in self.BLOCK and "p" in self.stack:
            self._close("p")          # a block inside a paragraph ends it, as in HTML
        if tag == "br":
            self.out.append("<br>")
            return
        if tag in ("img", "source"):
            src = absolute(a.get("src", ""))
            if not src.startswith("http"):
                return
            if tag == "source":
                self.out.append('<source src="%s">' % html.escape(src))
                return
            img = '<img src="%s" alt="%s" loading="lazy">' % (html.escape(src), html.escape(a.get("alt", "").strip()))
            credit = a.get("data-copyright", "").strip()
            if credit and "p" in self.stack:
                self._close("p")
            self.out.append("<figure>%s<figcaption>%s</figcaption></figure>" % (img, html.escape(credit)) if credit else img)
            return
        keep = []
        if tag == "a":
            href = absolute(a.get("href", ""))
            if not re.match(r"^(https?://|/)", href):
                return                # the link goes, its text stays
            keep.append(("href", href))
            if href.startswith("http") and not href.startswith(OLD_SITE):
                keep += [("target", "_blank"), ("rel", "noopener")]
        elif tag == "iframe":
            src = absolute(a.get("src", ""))
            if not src.startswith("https://") or re.sub(r"^https://([^/]+).*$", r"\1", src) not in IFRAME_HOSTS:
                return
            video = re.search(r"youtube(?:-nocookie)?\.com/embed/([\w-]+)", src)
            if video and video.group(1) in DEAD_VIDEOS:
                return
            keep += [("src", src), ("loading", "lazy"), ("allowfullscreen", "")]
        elif tag == "audio":
            src = absolute(a.get("src", ""))
            keep += ([("src", src)] if src.startswith("http") else []) + [("controls", ""), ("preload", "none")]
        self.out.append("<" + tag + "".join(' %s="%s"' % (k, html.escape(v)) if v else " " + k for k, v in keep) + ">")
        self.stack.append(tag)

    def handle_startendtag(self, tag, attrs):
        self.handle_starttag(tag, attrs)
        tag = self.RENAME.get(tag, tag)
        if tag not in self.VOID and self.stack and self.stack[-1] == tag:
            self._close(tag)

    def handle_endtag(self, tag):
        tag = self.RENAME.get(tag, tag)
        if tag in self.DROP:
            self.drop = max(0, self.drop - 1)
        elif not self.drop and tag in self.stack:
            self._close(tag)

    def handle_data(self, data):
        if not self.drop:
            self.out.append(html.escape(data.replace("\xa0", " "), quote=False))


def shouting(text):
    words = re.findall(r"\w+", text)
    return len(words) >= 3 and not re.search(r"[a-zа-яёәғқңөұүһі]", text)


def without_caps_opening(text):
    """«ОТАН-АНА» – ЕКІНШІ ... МОНУМЕНТ «Отан қорғаушылар» мемориалы … → «Отан қорғаушылар» мемориалы …"""
    tokens = text.split(" ")
    k = 0
    while k < len(tokens) and not re.search(r"[a-zа-яёәғқңөұүһі]", tokens[k]):
        k += 1
    return " ".join(tokens[k:]).lstrip(" –—-:,.") if k >= 3 and k < len(tokens) else text


def clean_html(raw, lang="kz"):
    c = Cleaner()
    c.feed(raw or "")
    c.close()
    while c.stack:
        c._close(c.stack[-1])
    s = "".join(c.out)
    s = re.sub(r"<em>\s*(<audio[^>]*>(?:\s|<source[^>]*>)*</audio>)\s*</em>", r"\1", s)
    s = re.sub(r"(<audio[^>]*>)\s+(</audio>)", r"\1\2", s)
    s = re.sub(r"[ \t\r\n]+", " ", s)
    s = re.sub(r"\s*(</?(?:p|h2|h3|h4|ul|ol|li|blockquote|figure|figcaption|table|thead|tbody|tr)>)\s*", r"\1", s)
    for _ in range(3):
        s = re.sub(r"<(p|strong|em|u|h2|h3|h4|li)>(?:\s|<br>)*</\1>", "", s)
    s = re.sub(r"(</(?:p|h2|h3|h4|ul|ol|blockquote|figure|table|iframe)>)", r"\1\n", s)
    # leading paragraphs that only repeat the title in capitals (Chinese has no capitals: nothing to look for)
    blocks = s.strip().split("\n")
    while lang != "zh-cn" and blocks and blocks[0].startswith("<p") and shouting(plain(blocks[0])):
        blocks.pop(0)
    return "\n".join(blocks).strip()


def plain(raw):
    t = re.sub(r"<[^>]+>", " ", raw or "")
    return re.sub(r"\s+", " ", html.unescape(t).replace("\xa0", " ")).strip()


def first_sentences(text, limit, lang="kz"):
    """The opening of a text, whole sentences up to limit characters (or a cut at a word with «…»)."""
    text = text.strip()
    if len(text) <= limit:
        return text
    joiner = "" if lang == "zh-cn" else " "
    parts = re.split(r"(?<=[.!?…])\s+(?=[«\"(A-ZА-ЯЁӘҒҚҢӨҰҮҺІ0-9])|(?<=[。！？])", text)
    out = ""
    for p in parts:
        if len(out) + len(p) + len(joiner) > limit:
            break
        out = (out + joiner + p).strip()
    if out:
        return out
    cut = text[:limit - 1]
    return (cut[:cut.rfind(" ")].rstrip(" ,;:—-") if " " in cut else cut) + "…"


def lower(word, lang):
    return word.replace("I", "ı").replace("İ", "i").lower() if lang == "tr" else word.lower()


def sentence_start(text, at):
    """Is the word at this place the first of a sentence? «А.Байтұрсынұлы» (an initial) is not; «ескерткіші.Кесене» is."""
    before = text[:at]
    if not before.strip() or re.search(r"\n\s*$", before):
        return True
    m = re.search(r"([.!?…])[\s«\"(]*$", before)
    if not m:
        return False
    return not (m.group(1) == "." and re.search(r"(?<!\w)\w\.\s*$", before))


LOOKALIKE = str.maketrans("ACEHKMOPTXYacekmopxy", "АСЕНКМОРТХУасекморху")

# spelling slips of the old titles, by place id
TITLE_FIXES = {12: [("сүргінқұрбандарына", "сүргін құрбандарына")], 55: [("(қарабалуанұлы)", "(Қарабалуанұлы)")]}

SMALL_EN = {"a", "an", "and", "at", "by", "for", "in", "of", "on", "or", "the", "to", "with", "from"}


def recase(title, text, lang):
    """A title written in capitals gets the spelling its words have in the place's own text."""
    title = re.sub(r"\s+", " ", (title or "").replace("\xa0", " ")).strip()
    title = re.sub(r",(?=[^\s\d])", ", ", title)
    title = re.sub(r"(?<=\w)\s+-(?=\w)", "-", title)
    if lang == "zh-cn":
        return title
    if lang in ("kz", "ru"):
        # Latin letters typed inside Cyrillic words (ЯСCЫ)
        title = re.sub(r"\w+", lambda m: m.group(0).translate(LOOKALIKE) if re.search(r"[А-яЁёӘәҒғҚқҢңӨөҰұҮүҺһІі]", m.group(0)) else m.group(0), title)
    tokens = re.split(r"([^\wʼ'’]+)", title)
    out = []
    first = True
    for tok in tokens:
        if not tok or not re.search(r"\w", tok):
            out.append(tok)
            continue
        word = tok
        letters = [ch for ch in word if ch.isalpha()]
        if len(letters) >= 3 and not word.isupper() and sum(ch.isupper() for ch in letters) >= 0.6 * len(letters):
            word = word.upper()           # «ZhANYS» is a name written in capitals
        if word.isupper() and len(word) == 1:
            word = word if (lang == "en" and word == "I") else lower(word, lang)
        elif word.isupper() and not re.fullmatch(r"[IVXLCDM]+", word):
            def found(w):
                return [(m.group(0)[:len(w)], m.start()) for m in re.finditer(r"(?<!\w)" + re.escape(w) + r"\w*", text, re.I)]
            forms = found(word)
            # another case of the same name: «АХМЕТА» → «Ахмет»
            for cut in (1, 2, 3, 4):
                if not forms and len(word) - cut >= 5:
                    forms = [(f + lower(word[len(word) - cut:], lang), at) for f, at in found(word[:-cut])]
            mid = [f for f, at in forms if not sentence_start(text, at)]
            proper = [f for f in mid if f[0].isupper() and not f.isupper()]
            common = [f for f in mid if f[0].islower()]
            caps = [f for f, at in forms if f.isupper()]
            if caps and len(caps) == len(forms) and len(word) <= 6:
                word = caps[0]
            elif proper and len(proper) >= len(common):
                word = proper[0]
            elif lang in ("en", "tr") and (lang != "en" or lower(word, lang) not in SMALL_EN or first):
                word = word[0] + lower(word[1:], lang)
            else:
                word = lower(word, lang)
        if first:
            word = word[0].upper() + word[1:]
            first = False
        out.append(word)
    s = "".join(out)
    # a name in quotes starts with a capital: «Қазақ елі»
    return re.sub(r"([«\"“])(\w)", lambda m: m.group(1) + m.group(2).upper(), s)


TRANSLIT = {
    "а": "a", "ә": "a", "б": "b", "в": "v", "г": "g", "ғ": "g", "д": "d", "е": "e", "ё": "io", "ж": "zh", "з": "z",
    "и": "i", "й": "i", "к": "k", "қ": "q", "л": "l", "м": "m", "н": "n", "ң": "n", "о": "o", "ө": "o", "п": "p",
    "р": "r", "с": "s", "т": "t", "у": "u", "ұ": "u", "ү": "u", "ф": "f", "х": "h", "һ": "h", "ц": "ts", "ч": "ch",
    "ш": "sh", "щ": "sh", "ъ": "", "ы": "y", "і": "i", "ь": "", "э": "e", "ю": "iu", "я": "ia",
}


def slugify(name, taken):
    s = "".join(TRANSLIT.get(ch, ch) for ch in name.lower())
    s = re.sub(r"[^a-z0-9]+", "-", s).strip("-")
    s = s[:60].rstrip("-") or "place"
    base, n = s, 2
    while s in taken:
        s = "%s-%d" % (base, n)
        n += 1
    taken.add(s)
    return s


# words of the title → category; the first rule that matches wins
CATEGORY = [
    ("tarih", r"ҒҰРЫПТЫҚ"),
    ("kieli", r"КЕСЕНЕ|МЕШІТ|ШІРКЕУ|МАЗАР|ҚОРЫМ|САҒАНА|ҚЫЛУЕТ|ЖЕРЛЕНГЕН|БҰЛАҒ|ҮҢГІР"),
    ("eskertkish", r"МОНУМЕНТ|ЕСКЕРТКІШ|МЕМОРИАЛ|АРКАСЫ|АЛАҢЫ|БЕЙБІТШІЛІК"),
    ("tarih", r"ҚАЛАШЫ|ҚОНЫС|МЕКЕН|ҚОРҒАН|ПЕТРОГЛИФ|АРХЕОЛОГИЯ|ТАРИХИ|РЕЗИДЕНЦИЯ|ҮЙІ|САРАЙ|ОРТАЛЫҒЫ|ҚАРЛАГ|МЕКТЕБ|МҰНАРА"),
    ("tabigat", r"КӨЛ|ШАТҚАЛ|ӨЗЕН|ОРМАН|ПАРК|ҚОРЫҚ"),
    ("kieli", r"\bАТА\b|\bАНА\b|ӘУЛИЕ|\bБАБ|ХАЗІРЕТ|\bТАУ\b"),
    ("tarih", r"КЕШЕН"),
]


def category(title):
    t = title.upper()
    return next((c for c, rx in CATEGORY if re.search(rx, t)), "kieli")


def pano_urls(rows):
    urls = []
    for r in sorted(rows, key=lambda r: (r["displayOrder"] or 0, r["id"])):
        m = re.search(r'src="([^"]+)"', r["embedCode"] or "")
        url = absolute(html.unescape(m.group(1))) if m else ""
        if url.startswith("https://www.google.com/maps/embed") and url not in urls:
            urls.append(url)
    return urls


def coords(location):
    m = re.search(r"!2d(-?\d+\.\d+)!3d(-?\d+\.\d+)", location or "")
    return (round(float(m.group(2)), 6), round(float(m.group(1)), 6)) if m else (0, 0)


def texts(title, short, full, lang):
    body = clean_html(full, lang)
    dense = lang == "zh-cn"
    lead = (plain(short) if dense else without_caps_opening(plain(short))) or first_sentences(plain(body), 200 if dense else 320, lang)
    if len(lead) > 1000:
        lead = first_sentences(lead, 990, lang)
    name = recase(title, plain(short) + " " + plain(body), lang)
    return {"name": name[:128], "fact": first_sentences(lead, 60 if dense else 120, lang), "lead": lead, "bodyHtml": body}


def save(path, data):
    with open(path, "w", encoding="utf-8") as f:
        f.write(json.dumps(data, ensure_ascii=False, indent=1) + "\n")


def main():
    dump = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "db", "kieli_db.sql")
    old = read_tables(dump, ["attraction", "attraction360image", "multilanguage"])
    seed = json.load(open(os.path.join(SEED, "kazakhstan.json"), encoding="utf-8"))
    i18n = {l: json.load(open(os.path.join(SEED, "i18n", l + ".json"), encoding="utf-8")) for l in LANGS}
    have = {int(p.get("legacyId") or 0) for p in seed["places"]}
    maps = {r["mapId"] for r in seed["regions"]}
    taken = {p["slug"] for p in seed["places"]}
    tr = {}
    for m in old["multilanguage"]:
        if m["tableName"] == "attraction" and m["qStatus"] == 0 and m["language"] in LANGS:
            tr[(m["columnId"], m["language"], m["columnName"].lower())] = m["columnValue"] or ""
    pano = {}
    for r in old["attraction360image"]:
        if r["qStatus"] == 0:
            pano.setdefault(r["attractionId"], []).append(r)
    added, report = 0, []
    for a in sorted(old["attraction"], key=lambda a: a["id"]):
        if a["qStatus"] != 0 or a["id"] in have:
            continue
        if a["regionId"] not in maps:
            report.append("skipped %d %s: region %s is not on the map" % (a["id"], a["title"], a["regionId"]))
            continue
        kz = texts(a["title"], a["shortDescription"], a["fullDescription"], "kz")
        for wrong, right in TITLE_FIXES.get(a["id"], []):
            kz["name"] = kz["name"].replace(wrong, right)
        lat, lon = coords(a["location"])
        slug = slugify(kz["name"], taken)
        place = {
            "slug": slug, "legacyId": a["id"], "regionMapId": a["regionId"], "category": category(a["title"]),
            "name": kz["name"], "fact": kz["fact"], "lead": kz["lead"], "bodyHtml": kz["bodyHtml"], "facts": [],
            "imageUrl": absolute(re.sub(r"_(small|middle)(\.\w+)$", r"_big\2", a["thumbnailUrl"] or "")),
            "pano": pano_urls(pano.get(a["id"], [])), "lat": lat, "lon": lon,
        }
        seed["places"].append(place)
        added += 1
        for l in LANGS:
            title = tr.get((a["id"], l, "title"), "").strip()
            full = tr.get((a["id"], l, "fulldescription"), "")
            if not title and not plain(full):
                continue
            t = texts(title or a["title"], tr.get((a["id"], l, "shortdescription"), ""), full, l)
            entry = {k: v for k, v in t.items() if v and (k != "name" or title)}
            if entry:
                i18n[l].setdefault("place", {})[slug] = entry
        report.append("%3d %-11s %-4s %s" % (a["id"], place["category"], "360" if place["pano"] else "", place["name"]))
    save(os.path.join(SEED, "kazakhstan.json"), seed)
    for l in LANGS:
        save(os.path.join(SEED, "i18n", l + ".json"), i18n[l])
    print("\n".join(report))
    print("added %d places (%d in the seed now)" % (added, len(seed["places"])))


if __name__ == "__main__":
    main()
