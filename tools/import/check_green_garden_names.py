"""Checks the Green Garden catalogue's Azerbaijani names against Wikidata and the Azerbaijani Wikipedia.

The delivery is honest about where its names came from: not one of the 272 is marked "kitabdan". The
catalogue prints Latin only - which is why the scientific name is the sourced half of every row and the
Azerbaijani half is a model's recollection, 99 of them flagged "əmin deyiləm" by the model itself.

A name nobody can source is not a name a quiz can put on a button as the right answer. So each species
is looked up by its scientific name - matched on P225, the taxon name, so a near-miss cannot pass for
the plant - and the Azerbaijani label or article title that comes back is the one a player will read.
Where the wiki has none, the row keeps its Latin name alone, which is still true and still the name the
catalogue itself prints.

Cultivars are looked up by their species: 'Edward Goucher' is an Abelia grandiflora whatever else it is.
"""
import json
import os
import time
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = r"C:\Users\HP\LiteratureMillionaire\docs\greegardenbitkiler-import\bitkiler.json"
OUT = os.path.join(HERE, "aznames.json")
UA = {"User-Agent": "KitabxanaBot/1.0 (+https://book.qrlog.az; baxmmc.ai@gmail.com)"}


def api(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=30) as response:
        return json.loads(response.read().decode("utf-8"))


def lookup(scientific):
    """The Azerbaijani label and article for a taxon, or None when the wiki carries neither."""
    hits = api(
        "https://www.wikidata.org/w/api.php?action=wbsearchentities"
        f"&search={urllib.parse.quote(scientific)}&language=en&type=item&limit=5&format=json"
    ).get("search", [])
    for hit in hits:
        entity = api(
            "https://www.wikidata.org/w/api.php?action=wbgetentities"
            f"&ids={hit['id']}&props=labels|claims|sitelinks&languages=az&format=json"
        )["entities"][hit["id"]]
        taxon = entity.get("claims", {}).get("P225", [])
        if not taxon or taxon[0]["mainsnak"]["datavalue"]["value"].lower() != scientific.lower():
            continue
        label = (entity.get("labels", {}).get("az") or {}).get("value")
        page = (entity.get("sitelinks", {}).get("azwiki") or {}).get("title")
        azeri = label or page
        # An article titled with the Latin name means the wiki has no Azerbaijani name either.
        if azeri and azeri.lower() == scientific.lower():
            azeri = None
        return {"qid": hit["id"], "az": azeri, "page": page}
    return None


plants = json.load(open(SRC, encoding="utf-8"))["plants"]
species = sorted({" ".join(p["scientificName"].split()[:2]).capitalize() for p in plants})
print(f"{len(plants)} sətir, {len(species)} növ soruşulur", flush=True)

found = {}
for n, name in enumerate(species, 1):
    try:
        found[name] = lookup(name)
    except Exception as exc:  # noqa: BLE001
        found[name] = None
        print(f"  xəta {name}: {type(exc).__name__}", flush=True)
    if n % 25 == 0:
        got = sum(1 for v in found.values() if v and v["az"])
        print(f"  {n}/{len(species)} … {got} adı tapıldı", flush=True)
    time.sleep(0.3)

json.dump(found, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
named = sum(1 for v in found.values() if v and v["az"])
print(f"\nazərbaycanca adı olan növ: {named}/{len(species)}")
print(f"yalnız latınca qalacaq   : {len(species) - named}")
