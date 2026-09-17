"""Collects better photographs for the plants we already verified, from NC State's Plant Toolbox.

Wikidata gives one picture per species, chosen to represent the taxon - often a whole mature tree,
sometimes a nineteenth-century plate. That is why a hundred plants were dropped: the picture was right
and still unusable. NC State's toolbox carries many photographs per plant and, decisively, labels each
one with the part it shows. A quiz needs the fruit, the flower, the bark - the thing that makes the
plant itself - and here that can be asked for by name instead of hoped for.

Licence. The quiz is free to play, sells nothing and carries no advertising, so NonCommercial terms are
within their bounds. NoDerivatives is a separate promise and it survives that: resizing and changing
file format are not adaptations, but cropping, filtering and drawing over the picture are. Every image
therefore carries `noDerivatives`, and anything marked with it has to be shown whole - contain, never
cover - for the rest of its life in this project. Images with no licence at all are left alone.

Restraint. Only the plants already verified are asked for - about 124 pages, by name, not a crawl. One
connection, a real User-Agent with a contact address, a pause between requests, and pages are cached.

The S3 links are signed and expire within the hour, so a picture has to be downloaded in the same run
that found it; when a cached page's signatures have gone stale the page is asked for once more.
"""
import html
import json
import os
import re
import time
import urllib.error
import urllib.request

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = r"C:\Users\HP\LiteratureMillionaire\docs\yasil-baki\tools\wikidata.json"
CACHE, PHOTOS = os.path.join(HERE, "ncsu-pages"), os.path.join(HERE, "ncsu-photos")
OUT = os.path.join(HERE, "ncsu.json")
UA = {"User-Agent": "KitabxanaBot/1.0 (+https://book.qrlog.az; baxmmc.ai@gmail.com)"}
PER_PLANT = 4

# Any Creative Commons grant, plus the public-domain marks. A photo with no licence stated is not ours.
USABLE = re.compile(r"^(CC BY|CC0|Public Domain|PDM)", re.I)

# What the picture shows, as the toolbox labels it. A close-up of the fruit is worth more to a player
# than a correct photograph of the whole tree, which is the mistake the Wikidata set kept making.
WORTH = [
    (re.compile(r"\bfruit|\bcone|\bberr|\bseed|\bnut\b|\bpod\b", re.I), 5),
    (re.compile(r"\bflower|\bbloom|\binflorescen", re.I), 4),
    (re.compile(r"\bbark|\btrunk|\bstem", re.I), 3),
    (re.compile(r"\bleaf|\bleaves|\bfoliage", re.I), 2),
]

FIGURE = re.compile(
    r'<img class="img-thumbnail modal_img" src="(?P<url>[^"]+)".*?'
    r'data-caption="(?P<caption>[^"]*)".*?'
    r'data-attrib="(?P<attrib>[^"]*)".*?'
    r'data-license="(?P<license>.*?)".*?'
    r'data-image-id="(?P<id>\d+)"',
    re.S,
)


def fetch(url, timeout=45):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=timeout) as response:
        return response.read()


def page(slug, refresh=False):
    """The plant's page, from disk unless the signatures inside it have expired."""
    path = os.path.join(CACHE, f"{slug}.html")
    if os.path.exists(path) and not refresh:
        return open(path, encoding="utf-8").read()
    body = fetch(f"https://plants.ces.ncsu.edu/plants/{slug}/").decode("utf-8", "replace")
    open(path, "w", encoding="utf-8").write(body)
    time.sleep(0.7)
    return body


def candidates(body):
    seen, out = set(), []
    for m in FIGURE.finditer(body):
        licence = html.unescape(re.sub("<.*?>", "", m["license"])).strip()
        if not USABLE.match(licence) or m["id"] in seen:
            continue
        seen.add(m["id"])
        caption = html.unescape(m["caption"]).strip()
        out.append({
            "id": m["id"], "url": html.unescape(m["url"]), "caption": caption,
            "author": html.unescape(m["attrib"]).strip() or "naməlum", "license": licence,
            "noDerivatives": "-ND" in licence.upper(),
            "score": next((v for p, v in WORTH if p.search(caption)), 0),
        })
    # Best part first; among equals, the toolbox's own order, which puts its better pictures early.
    return sorted(out, key=lambda c: -c["score"])


def save(raw, target):
    temp = target + ".src"
    open(temp, "wb").write(raw)
    try:
        with Image.open(temp) as im:
            im = im.convert("RGB")
            if max(im.size) > 1400:  # a resize is not an adaptation; a crop would be
                k = 1400 / max(im.size)
                im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
            im.save(target, "WEBP", quality=86, method=6)
    finally:
        os.remove(temp)


def collect(row):
    """Every usable picture we managed to bring down for one plant, best part first."""
    slug = row["scientificName"].strip().lower().replace(" ", "-")
    body = page(slug)
    kept, refreshed = [], False
    rank = 0
    while rank < min(PER_PLANT, len(candidates(body))):
        pick = candidates(body)[rank]
        rank += 1
        target = os.path.join(PHOTOS, f"{row['catalogId']}-{rank}.webp")
        if os.path.exists(target):
            kept.append({"file": os.path.basename(target), **pick})
            continue
        try:
            save(fetch(pick["url"]), target)
            time.sleep(0.3)
        except urllib.error.HTTPError as exc:
            if exc.code == 403 and not refreshed:
                # The cached page's signed links have aged out; ask for it once more and start over.
                body, refreshed, kept, rank = page(slug, refresh=True), True, [], 0
                continue
            print(f"  alinmadi {row['catalogId']}-{rank}: HTTP {exc.code}", flush=True)
            continue
        except Exception as exc:  # noqa: BLE001
            print(f"  alinmadi {row['catalogId']}-{rank}: {type(exc).__name__}", flush=True)
            continue
        kept.append({"file": os.path.basename(target), **pick})
    return slug, kept


os.makedirs(CACHE, exist_ok=True)
os.makedirs(PHOTOS, exist_ok=True)
rows = json.load(open(SRC, encoding="utf-8"))
wanted = [r for r in rows if r.get("azName")]
print(f"sorusulacaq: {len(wanted)} bitki", flush=True)

results, missing, dry = [], [], []
for n, row in enumerate(wanted, 1):
    try:
        slug, kept = collect(row)
    except urllib.error.HTTPError as exc:
        missing.append(f"{row['catalogId']} {row['scientificName']}"
                       + ("" if exc.code == 404 else f" (HTTP {exc.code})"))
        continue
    except Exception as exc:  # noqa: BLE001
        missing.append(f"{row['catalogId']} {row['scientificName']} ({type(exc).__name__})")
        continue

    if kept:
        results.append({"catalogId": row["catalogId"], "azName": row["azName"],
                        "scientificName": row["scientificName"],
                        "page": f"https://plants.ces.ncsu.edu/plants/{slug}/", "photos": kept})
    else:
        dry.append(f"{row['catalogId']} {row['scientificName']}")
    if n % 20 == 0:
        print(f"  {n}/{len(wanted)} ... {len(results)} bitki, "
              f"{sum(len(r['photos']) for r in results)} foto", flush=True)

json.dump(results, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
photos = [p for r in results for p in r["photos"]]
print(f"\nsekli olan bitki : {len(results)}")
print(f"umumi foto       : {len(photos)}")
print(f"  ND (kesilmemeli): {sum(1 for p in photos if p['noDerivatives'])}")
print(f"saytda tapilmadi : {len(missing)}")
print(f"lisenziyali sekli yox: {len(dry)}")
for line in (missing + dry)[:20]:
    print("   ", line)
