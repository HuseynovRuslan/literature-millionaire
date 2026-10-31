"""Rebuilds the plant catalogue from Wikidata, where the name and the picture come from one record.

Why here rather than iNaturalist: an observation photo is made to let an expert confirm an
identification - bark, a bud, a seed pod, a bare twig in winter. Looking at the downloaded set showed
what that means in practice: of the first twelve trees, one was recognisable as the plant it names.
A species' Wikidata image is chosen by editors to show the species, which is the same thing a quiz
needs, and the Azerbaijani label on that record is a citable name rather than a guess.

Neither is a guarantee. It is a documented chain - GBIF for the scientific name, az.wikipedia for the
Azerbaijani one, the same record for the picture - which is what a project without a botanist can
honestly stand behind.
"""
import json
import os
import time
import urllib.parse
import urllib.request

SRC = r"C:\Users\HP\Downloads\yasil-baki-katalog.json"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "wikidata.json")
UA = {"User-Agent": "KitabxanaBot/1.0 (+https://book.qrlog.az; baxmmc.ai@gmail.com)"}


def api(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=30) as response:
        return json.loads(response.read().decode("utf-8"))


def commons(filename):
    """Licence, author and categories for a Commons file - the attribution has to travel with it."""
    name = urllib.parse.quote(filename.replace(" ", "_"))
    data = api(
        "https://commons.wikimedia.org/w/api.php?action=query&format=json&titles=File:"
        f"{name}&prop=imageinfo|categories&iiprop=url|extmetadata&cllimit=60"
    )
    page = next(iter(data["query"]["pages"].values()))
    info = (page.get("imageinfo") or [{}])[0]
    meta = info.get("extmetadata", {})
    return {
        "url": info.get("url"),
        "license": (meta.get("LicenseShortName") or {}).get("value"),
        "author": (meta.get("Artist") or {}).get("value", "").replace("<", " <")[:180],
        "descriptionUrl": info.get("descriptionurl"),
        "categories": [c["title"].removeprefix("Category:") for c in page.get("categories", [])],
    }


def taxon(scientific):
    query = urllib.parse.quote(scientific)
    hits = api(
        "https://www.wikidata.org/w/api.php?action=wbsearchentities"
        f"&search={query}&language=en&type=item&limit=5&format=json"
    ).get("search", [])
    for hit in hits:
        entity = api(
            "https://www.wikidata.org/w/api.php?action=wbgetentities"
            f"&ids={hit['id']}&props=labels|claims|sitelinks&languages=az&format=json"
        )["entities"][hit["id"]]
        claims = entity.get("claims", {})
        name = claims.get("P225", [])
        if not name or name[0]["mainsnak"]["datavalue"]["value"].lower() != scientific.lower():
            continue
        image = claims.get("P18", [])
        return {
            "qid": hit["id"],
            "label": (entity.get("labels", {}).get("az") or {}).get("value"),
            "page": (entity.get("sitelinks", {}).get("azwiki") or {}).get("title"),
            "imageFile": image[0]["mainsnak"]["datavalue"]["value"] if image else None,
        }
    return None


plants = json.load(open(SRC, encoding="utf-8"))["plants"]
rows = []
for n, plant in enumerate(plants, 1):
    scientific = plant["scientificName"]
    row = {"catalogId": plant["catalogId"], "scientificName": scientific,
           "givenName": plant["name"], "confusableWith": plant.get("confusableWith") or []}
    try:
        found = taxon(scientific)
        if found:
            azeri = found["label"] or found["page"]
            # A page titled with the Latin name means the wiki carries no Azerbaijani name either.
            row["azName"] = None if (azeri or "").lower() == scientific.lower() else azeri
            row["azSource"] = f"az.wikipedia.org/wiki/{found['page']}" if found["page"] else None
            row["qid"] = found["qid"]
            if found["imageFile"]:
                info = commons(found["imageFile"])
                row["image"] = {"file": found["imageFile"], **info}
                # Cheap cross-check: does the file name or a category name the species?
                genus, _, species = scientific.partition(" ")
                haystack = (found["imageFile"] + " " + " ".join(info["categories"])).lower()
                row["nameInFile"] = genus.lower() in haystack and (not species or species.lower() in haystack)
    except Exception as exc:  # noqa: BLE001
        row["error"] = type(exc).__name__
    rows.append(row)
    if n % 25 == 0:
        print(f"  {n}/{len(plants)}", flush=True)
    time.sleep(0.35)

json.dump(rows, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

named = [r for r in rows if r.get("azName")]
withimg = [r for r in named if r.get("image", {}).get("url")]
crossed = [r for r in withimg if r.get("nameInFile")]
print(f"\n216 bitkidən:")
print(f"  mənbəli Azərbaycanca adı var : {len(named)}")
print(f"  həm də şəkli var             : {len(withimg)}")
print(f"  şəkil adı/kateqoriyası növü təsdiqləyir: {len(crossed)}")
print(f"  təsdiqləmir (əl ilə baxılmalı): {len(withimg) - len(crossed)}")
licences = {}
for r in withimg:
    key = (r["image"].get("license") or "?")[:40]
    licences[key] = licences.get(key, 0) + 1
print("\n  lisenziyalar:", dict(sorted(licences.items(), key=lambda x: -x[1])))
