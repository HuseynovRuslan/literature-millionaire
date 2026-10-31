"""My own verdict on every candidate, and the bank that survives it.

The test is not "is the photo correct" - the automatic checks already answer that, and they answer yes
almost everywhere. The test is the one the player actually faces: looking at this picture, on a phone,
for ten seconds, can an ordinary person in Baku put a name to it? A true photograph of a mature ash
tree fails that test, and so does a species whose only Azerbaijani name nobody says out loud.

So a plant enters the bank on three counts together:
  1. the picture shows the thing that makes the plant itself - fruit, flower, bark, habit;
  2. the Azerbaijani name is one a person would use;
  3. nothing else in the bank looks like it.
Anything I could not tell apart myself is out. I am not a botanist, and that is exactly the point:
a question I cannot answer from the photo is a question the player cannot answer either.
"""
import json
import os
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROWS = {r["catalogId"]: r for r in json.load(open(os.path.join(HERE, "wikidata.json"), encoding="utf-8"))}

# catalogId: (verdict, difficulty-or-None, reason)
KEEP, ART, SAME, DULL, NAME, PHOTO = "keçdi", "şəkil rəsimdir", "ayırd edə bilmirəm", "tanınmayan görüntü", "ad tanınmır", "şəkil zəif"

V = {
 "PLT-001": (KEEP, "asan",  "çinar - tikanlı yumru meyvələr aydın görünür, Azərbaycanda hamı tanıyır"),
 "PLT-011": (KEEP, "asan",  "sallaq söyüd formasını başqa ağacla qarışdırmaq olmur"),
 "PLT-014": (KEEP, "orta",  "ağ qabıq tozağacını birmənalı verir"),
 "PLT-017": (KEEP, "asan",  "budaqdakı tut meyvəsi - şəkildə əsas əlamət var"),
 "PLT-036": (KEEP, "asan",  "əncir meyvəsi və yarpağı"),
 "PLT-037": (KEEP, "asan",  "nar meyvəsi - ən tanınan"),
 "PLT-078": (KEEP, "asan",  "yasəmən salxımı ön planda"),
 "PLT-113": (KEEP, "orta",  "gövdəyə sarılmış daşsarmaşığı"),
 "PLT-134": (KEEP, "asan",  "əzvay - ətli yarpaqlar, geniş tanınır"),
 "PLT-168": (KEEP, "asan",  "lalə"),
 "PLT-169": (KEEP, "asan",  "nərgiz"),
 "PLT-175": (KEEP, "asan",  "günəbaxan"),
 "PLT-003": (KEEP, "orta",  "ağcaqayın yarpağı və çiçəyi birlikdə görünür"),
 "PLT-029": (KEEP, "orta",  "iri ağ maqnoliya çiçəyi"),
 "PLT-082": (KEEP, "orta",  "hibiskus çiçəyi - mərkəzdəki tünd ləkə ilə"),
 "PLT-142": (KEEP, "orta",  "qızçiçəyi - sadə, aydın"),
 "PLT-167": (KEEP, "orta",  "sünbülçiçəyi (sümbül) - salxım forması xarakterik"),
 "PLT-180": (KEEP, "orta",  "anturium - qırmızı yelkən yarpağı, ev bitkisi kimi tanınır"),
 "PLT-102": (KEEP, "çətin", "rododendron - çiçək xarakterik, ad az işlənir"),
 "PLT-109": (KEEP, "çətin", "sumaq - qırmızı meyvə qozası birmənalıdır"),
 "PLT-112": (KEEP, "çətin", "buqenvilleya - rəngi başqa bitkidə yoxdur"),
 "PLT-115": (KEEP, "çətin", "nargülü - narıncı şeypur çiçək"),
 "PLT-122": (KEEP, "çətin", "qonaqotu çiçəyi - dünyada bənzəri yoxdur"),
 "PLT-163": (KEEP, "çətin", "boymadərən - dərman bitkisi kimi tanınır"),
}

R = {
 "PLT-089": ART, "PLT-098": ART, "PLT-123": ART, "PLT-148": ART,
 "PLT-002": SAME, "PLT-004": SAME, "PLT-005": SAME, "PLT-006": SAME, "PLT-007": SAME, "PLT-008": SAME,
 "PLT-009": SAME, "PLT-010": SAME, "PLT-018": SAME, "PLT-025": SAME, "PLT-026": SAME, "PLT-030": SAME,
 "PLT-041": SAME, "PLT-042": SAME, "PLT-043": SAME, "PLT-044": SAME, "PLT-045": SAME, "PLT-046": SAME,
 "PLT-047": SAME, "PLT-048": SAME, "PLT-049": SAME, "PLT-050": SAME, "PLT-051": SAME, "PLT-052": SAME,
 "PLT-053": SAME, "PLT-054": SAME, "PLT-056": SAME, "PLT-057": SAME, "PLT-058": SAME, "PLT-060": SAME,
 "PLT-064": SAME, "PLT-065": SAME, "PLT-066": SAME, "PLT-068": SAME, "PLT-070": SAME, "PLT-072": SAME,
 "PLT-073": SAME, "PLT-074": SAME, "PLT-075": SAME, "PLT-079": SAME, "PLT-081": SAME, "PLT-084": SAME,
 "PLT-085": SAME, "PLT-093": SAME, "PLT-094": SAME, "PLT-107": SAME, "PLT-118": SAME, "PLT-119": SAME,
 "PLT-120": SAME, "PLT-121": SAME, "PLT-124": SAME, "PLT-125": SAME, "PLT-127": SAME, "PLT-128": SAME,
 "PLT-132": SAME, "PLT-154": SAME, "PLT-219": SAME, "PLT-220": SAME, "PLT-221": SAME, "PLT-222": SAME,
 "PLT-228": SAME, "PLT-229": SAME, "PLT-087": SAME,
 "PLT-012": PHOTO, "PLT-024": PHOTO, "PLT-097": PHOTO, "PLT-101": PHOTO,
 "PLT-015": PHOTO, "PLT-020": PHOTO, "PLT-021": PHOTO, "PLT-028": PHOTO, "PLT-035": PHOTO,
 "PLT-061": PHOTO, "PLT-108": PHOTO, "PLT-114": PHOTO, "PLT-116": PHOTO, "PLT-145": PHOTO,
 "PLT-171": PHOTO, "PLT-223": PHOTO, "PLT-022": PHOTO,
 "PLT-016": NAME, "PLT-019": NAME, "PLT-023": NAME, "PLT-034": NAME, "PLT-090": NAME, "PLT-095": NAME,
 "PLT-103": NAME, "PLT-110": NAME, "PLT-129": NAME, "PLT-135": NAME, "PLT-153": NAME, "PLT-164": NAME,
 "PLT-172": NAME, "PLT-176": NAME, "PLT-184": NAME, "PLT-083": NAME,
}

usable = [r for r in ROWS.values() if r.get("azName") and (r.get("image") or {}).get("url")]
missing = [r["catalogId"] for r in usable if r["catalogId"] not in V and r["catalogId"] not in R]
assert not missing, f"qərarsız qalan: {missing}"

verdicts, bank = [], []
for row in usable:
    cid = row["catalogId"]
    if cid in V:
        verdict, level, why = V[cid]
        bank.append({
            "catalogId": cid, "name": row["azName"], "scientificName": row["scientificName"],
            "difficulty": level, "whyItWorks": why,
            "nameSource": row.get("azSource"), "wikidata": row.get("qid"),
            "image": {k: row["image"].get(k) for k in ("file", "url", "license", "author", "descriptionUrl")},
        })
    else:
        verdict, level, why = R[cid], None, None
    verdicts.append({"catalogId": cid, "name": row["azName"], "scientificName": row["scientificName"],
                     "verdict": verdict, "difficulty": level, "reason": why})

json.dump(verdicts, open(os.path.join(HERE, "verdicts.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
json.dump({"source": "Wikidata P225/P18 + az.wikipedia + Wikimedia Commons",
           "reviewedBy": "Claude - şəkillərə əl ilə baxılıb, 2026-09-17",
           "rule": "şəkildə bitkini verən əlamət olmalı, adı xalq işlətməli, bankda oxşarı olmamalı",
           "plants": bank},
          open(os.path.join(HERE, "yasil-baki-final.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

keep = os.path.join(HERE, "final-photos")
os.makedirs(keep, exist_ok=True)
for p in bank:
    src = os.path.join(HERE, "wiki-photos", f"{p['catalogId']}.webp")
    if os.path.exists(src):
        shutil.copy2(src, os.path.join(keep, f"{p['catalogId']}.webp"))

counts = {}
for v in verdicts:
    counts[v["verdict"]] = counts.get(v["verdict"], 0) + 1
print(f"baxılan: {len(verdicts)}")
for k, n in sorted(counts.items(), key=lambda x: -x[1]):
    print(f"  {k:<22} {n}")
levels = {}
for p in bank:
    levels[p["difficulty"]] = levels.get(p["difficulty"], 0) + 1
print(f"\nbank: {len(bank)} bitki ->", levels)
