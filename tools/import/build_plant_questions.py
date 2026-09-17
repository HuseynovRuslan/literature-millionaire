"""Turns the chosen photographs into the Yaşıl Bakı bank.

One photograph, one question. The plant is the answer; the work is in the other three options, because
that is where the first attempt at this bank went wrong. Two cedars are indistinguishable in any
photograph ever taken, and dropping both was the wrong fix - the right one is never to put them in each
other's options. A cedar beside a pomegranate, a tulip and a fig is a question a person can answer.

So an option is refused when it shares a genus with the answer, when the catalogue already recorded it
as confusable with the answer, or when both are conifers - needles look like needles. What is left is
chosen at random from a seeded generator, so the same bank comes out of every run.

Difficulty carries over the judgement of the first pass, when every one of these plants was looked at
by hand: the ones that survived it are the easy ones, the ones dropped for an obscure name or a
lookalike are the hard ones. A leaf or a whole-plant shot is one step harder than a fruit or a flower,
because that is the difference between seeing the thing itself and seeing a green shape.
"""
import json
import os
import random
import re

HERE = os.path.dirname(os.path.abspath(__file__))
PICKED = json.load(open(os.path.join(HERE, "picked.json"), encoding="utf-8"))
CATALOG = {r["catalogId"]: r for r in json.load(open(os.path.join(HERE, "wikidata.json"), encoding="utf-8"))}
VERDICT = {v["catalogId"]: v["verdict"]
           for v in json.load(open(os.path.join(HERE, "verdicts.json"), encoding="utf-8"))}
OUT = os.path.join(HERE, "yasil-baki-questions.json")

# Pictures the toolbox keeps for a gardener, not for a quiz: the pest that eats the plant, an old
# engraving, and cultivars bred to look unlike the species they belong to.
PEST = re.compile(r"leafminer|leaf miner|\bmite|aphid|borer|beetle|weevil|caterpillar|larva|whitefly"
                  r"|thrips|blight|canker|mildew|wilt|infest|\bgall\b|moth\b|wasp|butterfly", re.I)
DRAWN = re.compile(r"illustration|drawing|engrav|herbarium|specimen sheet", re.I)
CULTIVAR = re.compile(r"variegat|\bcv\.|['‘’]\s*[A-Z][\w\- ]{2,}\s*['‘’]")

CONIFERS = {"Pinus", "Picea", "Abies", "Cedrus", "Cupressus", "Chamaecyparis", "Juniperus", "Thuja",
            "Platycladus", "Taxus", "Cryptomeria", "Sequoiadendron", "Ginkgo"}

# What the first pass decided about the plant, before its picture was replaced. A plant dropped then for
# looking like another one is not hard any more - that was fixed in the options, not in the picture - so
# it sits in the middle. An obscure name is still hard: no photograph teaches a word nobody says.
BASE = {"keçdi": 0, "şəkil zəif": 1, "şəkil rəsimdir": 1, "ayırd edə bilmirəm": 1, "ad tanınmır": 2}
LEVELS = ["Easy", "Medium", "Hard"]
HARDER_PARTS = {"yarpaq", "bütöv", "digər"}

ASKED = {
    "meyvə": "Şəkildəki meyvə hansı bitkiyə aiddir?",
    "çiçək": "Şəkildəki çiçək hansı bitkiyə aiddir?",
    "yarpaq": "Şəkildəki yarpaq hansı bitkiyə aiddir?",
    "bütöv": "Şəkildə hansı bitki göstərilib?",
    # A different wording, because a plant can have both a whole-plant shot and an unlabelled one, and
    # two questions worded identically would read as the same question asked twice.
    "digər": "Şəkildəki bitki hansıdır?",
}
ALT = {
    "meyvə": "Bir bitkinin meyvəsinin yaxın planı.",
    "çiçək": "Bir bitkinin çiçəyinin yaxın planı.",
    "yarpaq": "Bir bitkinin yarpaqlarının yaxın planı.",
    "bütöv": "Bir bitkinin bütöv görünüşü.",
    "digər": "Bir bitkinin fotoşəkli.",
}


def genus(scientific):
    return scientific.split(" ")[0]


def usable(photo):
    caption = photo["caption"]
    return not (PEST.search(caption) or DRAWN.search(caption) or CULTIVAR.search(caption))


plants = []
for row in PICKED:
    photos = [p for p in row["photos"] if usable(p)]
    if not photos:
        continue
    catalog = CATALOG.get(row["catalogId"], {})
    plants.append({
        "catalogId": row["catalogId"], "azName": row["azName"],
        "scientificName": row["scientificName"], "genus": genus(row["scientificName"]),
        "page": row["page"], "photos": photos,
        "confusable": set(catalog.get("confusableWith") or []),
        "base": BASE.get(VERDICT.get(row["catalogId"], "ayırd edə bilmirəm"), 2),
    })

# Two plants that happen to share an Azerbaijani name would make an unanswerable question whatever else
# is done, so the second one is not carried.
seen, unique = set(), []
for plant in plants:
    if plant["azName"] in seen:
        continue
    seen.add(plant["azName"])
    unique.append(plant)
plants = unique


def distractors(answer, rng):
    """Three plants that cannot be mistaken for the answer in a photograph."""
    pool = []
    for other in plants:
        if other["catalogId"] == answer["catalogId"] or other["genus"] == answer["genus"]:
            continue
        if other["scientificName"] in answer["confusable"] or answer["scientificName"] in other["confusable"]:
            continue
        if answer["genus"] in CONIFERS and other["genus"] in CONIFERS:
            continue
        pool.append(other)
    return rng.sample(pool, 3) if len(pool) >= 3 else None


rng = random.Random(20260917)
questions, skipped = [], []
for plant in plants:
    for photo in plant["photos"]:
        wrong = distractors(plant, rng)
        if wrong is None:
            skipped.append(f"{plant['catalogId']} {plant['azName']}")
            break

        options = [plant["azName"]] + [w["azName"] for w in wrong]
        rng.shuffle(options)
        correct = "ABCD"[options.index(plant["azName"])]

        level = plant["base"] + (1 if photo["part"] in HARDER_PARTS else 0)
        number = photo["file"].removeprefix("PLT-").removesuffix(".webp")
        credit = f"NC State Extension Plant Toolbox — {photo['author']} ({plant['page']})"

        questions.append({
            "sourceId": f"plant-{number}",
            "category": "Bakı bitkiləri",
            "difficulty": LEVELS[min(level, 2)],
            "text": ASKED[photo["part"]],
            "optionA": options[0], "optionB": options[1],
            "optionC": options[2], "optionD": options[3],
            "correctOption": correct,
            "explanation": f"{plant['azName']} ({plant['scientificName']}).",
            "imageUrl": f"/question-images/plant-{number}.webp",
            "imageAltText": ALT[photo["part"]],
            "imageSource": credit,
            "imageLicense": photo["license"],
        })

json.dump({"questions": questions}, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

levels = {}
for q in questions:
    levels[q["difficulty"]] = levels.get(q["difficulty"], 0) + 1
print(f"bitki  : {len(plants)}")
print(f"sual   : {len(questions)}")
print(f"çətinlik: {levels}")
print(f"buraxılan: {len(skipped)} {skipped[:5]}")
answers = {}
for q in questions:
    answers[q["correctOption"]] = answers.get(q["correctOption"], 0) + 1
print(f"doğru cavab hərfləri: {dict(sorted(answers.items()))}")
