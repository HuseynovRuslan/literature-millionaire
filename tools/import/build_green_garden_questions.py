"""Builds the Green Garden bank from the catalogue extraction.

The delivery is one photograph and one question per catalogue entry, and it is honest about its weak
spot: the catalogue prints Latin only, so not one of the 272 Azerbaijani names came from the book - 173
are the model's recollection and 99 it flagged itself as unsure. A name nobody can source is not a name
to put on a button as the right answer, so the Azerbaijani half of a label is kept only where Wikidata
and az.wikipedia carry it for that species. The rest stand on their Latin name, which is what the
catalogue prints and what a nursery actually calls them.

Cultivars stay as separate questions, because in a catalogue the cultivar IS the plant - 'Blue Chip' and
'Pendula' are what a landscaper orders. But thirteen Acer palmatum cultivars are one photograph thirteen
times over to anybody's eye, so no two rows of the same genus are ever offered against each other.
"""
import json
import os
import random
import re

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = r"C:\Users\HP\LiteratureMillionaire\docs\greegardenbitkiler-import\bitkiler.json"
NAMES = os.path.join(HERE, "aznames.json")
OUT = os.path.join(HERE, "green-garden-questions.json")

CATALOGUE = json.load(open(SRC, encoding="utf-8"))
WIKI = json.load(open(NAMES, encoding="utf-8")) if os.path.exists(NAMES) else {}

# Words the catalogue prints in the Latin line that are not part of a name.
NOISE = re.compile(r"^\(|\)$")


def latin(raw):
    """'ABELIA GRANDIFLORA EDWARD GOUCHER' -> ('Abelia grandiflora', \"'Edward Goucher'\")."""
    words = [NOISE.sub("", w) for w in raw.split() if NOISE.sub("", w)]
    genus = words[0].capitalize()
    species = words[1].lower() if len(words) > 1 else ""
    cultivar = " ".join(w.capitalize() for w in words[2:])
    return (f"{genus} {species}".strip(), cultivar)


def label_for(plant):
    """A garden label: the Latin name the catalogue prints, and a sourced Azerbaijani name if one exists."""
    binomial, cultivar = latin(plant["scientificName"])
    name = f"{binomial} '{cultivar}'" if cultivar else binomial
    entry = WIKI.get(binomial.capitalize()) or WIKI.get(binomial)
    azeri = entry and entry.get("az")
    return f"{name} ({azeri})" if azeri else name


plants = []
for index, plant in enumerate(CATALOGUE["plants"], 1):
    binomial, _ = latin(plant["scientificName"])
    plants.append({
        "number": index,
        "label": label_for(plant),
        "genus": binomial.split(" ")[0],
        "page": plant["page"],
        "difficulty": plant.get("difficulty"),
    })

# Two rows that ended up with the same label are one unanswerable question however it is worded.
seen, unique = set(), []
for plant in plants:
    if plant["label"] in seen:
        continue
    seen.add(plant["label"])
    unique.append(plant)
dropped = [p["label"] for p in plants if p not in unique]
plants = unique

by_number = {p["number"]: p for p in plants}
questions_in = {q.get("imageFile"): q for q in CATALOGUE["questions"]}
source_images = {i + 1: p["images"][0]["file"] for i, p in enumerate(CATALOGUE["plants"])}

rng = random.Random(20260917)
questions, skipped = [], []
for plant in plants:
    pool = [o for o in plants if o["genus"] != plant["genus"]]
    if len(pool) < 3:
        skipped.append(plant["label"])
        continue
    wrong = rng.sample(pool, 3)
    options = [plant["label"]] + [w["label"] for w in wrong]
    rng.shuffle(options)
    correct = "ABCD"[options.index(plant["label"])]

    delivered = questions_in.get(source_images[plant["number"]], {})
    difficulty = delivered.get("difficulty") if delivered.get("difficulty") in ("Easy", "Medium", "Hard") else "Medium"

    questions.append({
        "sourceId": f"gg-{plant['number']:03d}",
        "category": "Green Garden kataloqu",
        "difficulty": difficulty,
        "text": "Şəkildəki bitki hansıdır?",
        "optionA": options[0], "optionB": options[1],
        "optionC": options[2], "optionD": options[3],
        "correctOption": correct,
        "explanation": f"{plant['label']} — kataloqun {plant['page']}-ci səhifəsi.",
        "imageUrl": f"/question-images/gg-{plant['number']:03d}.webp",
        "imageAltText": "Green Garden kataloqundan bir bitkinin şəkli.",
        "imageSource": f"Green Garden bitki kataloqu (2018), səh. {plant['page']}",
        "imageLicense": "Green Garden bitki kataloqu",
    })

json.dump({"questions": questions}, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

levels = {}
for q in questions:
    levels[q["difficulty"]] = levels.get(q["difficulty"], 0) + 1
withaz = sum(1 for p in plants if p["label"].endswith(")"))
print(f"kataloq sətri : {len(CATALOGUE['plants'])}")
print(f"sual          : {len(questions)}")
print(f"  təkrar ad üstündə atıldı: {len(dropped)} {dropped[:3]}")
print(f"  variant tapılmadı       : {len(skipped)}")
print(f"mənbəli Azərbaycanca adı olan: {withaz}/{len(plants)} (qalanı yalnız latınca)")
print(f"çətinlik: {levels}")
answers = {}
for q in questions:
    answers[q["correctOption"]] = answers.get(q["correctOption"], 0) + 1
print(f"doğru cavab hərfləri: {dict(sorted(answers.items()))}")
print("\nnümunə:")
for q in questions[:2]:
    print(f"  {q['optionA']} / {q['optionB']} / {q['optionC']} / {q['optionD']}  -> {q['correctOption']}")
