"""Chooses which of a plant's photographs become questions, and lays the survivors out to be judged.

The toolbox often has four pictures of the same white flower. Four questions built from those are one
question asked four times, which teaches nothing and bores the player. So the unit of variety is the
part, not the picture: at most one fruit, one flower, one leaf, one whole plant. A player who meets a
lime tree twice meets it once by its fruit and once by its flower.

Two kinds are thrown out before anyone looks:

  Bark and bare trunks. Twenty pictures here, and nobody names a tree from its bark - not the player,
  not me. They are good photographs of the wrong thing.

  Named cultivars - 'Sunburst', 'Nana', 'Variegata'. A cultivar is bred to look unlike the species, so
  a golden honey locust is a truthful picture and a false question.
"""
import json
import os
import re

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROWS = json.load(open(os.path.join(HERE, "ncsu.json"), encoding="utf-8"))
PHOTOS, OUT = os.path.join(HERE, "ncsu-photos"), os.path.join(HERE, "ncsu-sheets")
PICKED = os.path.join(HERE, "picked.json")
COLS, ROWCOUNT, CELL, LABEL = 4, 3, 340, 66

# The part of the plant the caption says it shows, most useful to a player first.
PARTS = [
    ("meyvə", re.compile(r"fruit|cone|berr|seed|nut\b|pod\b|samara|acorn|drupe", re.I)),
    ("çiçək", re.compile(r"flower|bloom|inflorescen|catkin", re.I)),
    ("yarpaq", re.compile(r"leaf|leaves|foliage|frond", re.I)),
    ("bütöv", re.compile(r"\bform\b|habit|\btree\b|\bshrub\b|\bplant\b", re.I)),
]
BARK = re.compile(r"bark|trunk\b", re.I)
CULTIVAR = re.compile(r"['‘’\"]\s*[A-Z][\w\- ]{2,}\s*['‘’\"]")


def part_of(caption):
    if BARK.search(caption):
        return None
    for name, pattern in PARTS:
        if pattern.search(caption):
            return name
    return "digər"


def pick(row):
    """At most one picture per part, best-scoring first, cultivars left out."""
    chosen = {}
    for photo in row["photos"]:
        caption = photo["caption"]
        if CULTIVAR.search(caption):
            continue
        part = part_of(caption)
        if part and part not in chosen:
            chosen[part] = {**photo, "part": part}
    order = [name for name, _ in PARTS] + ["digər"]
    return [chosen[p] for p in order if p in chosen]


picked = []
for row in ROWS:
    photos = pick(row)
    if photos:
        picked.append({**row, "photos": photos})

json.dump(picked, open(PICKED, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
total = sum(len(r["photos"]) for r in picked)
print(f"{len(ROWS)} bitki / {sum(len(r['photos']) for r in ROWS)} fotodan")
print(f"seçildi: {len(picked)} bitki / {total} foto")
counts = {}
for r in picked:
    counts[len(r["photos"])] = counts.get(len(r["photos"]), 0) + 1
print("bitki başına:", dict(sorted(counts.items())))

for name in os.listdir(OUT):
    os.remove(os.path.join(OUT, name))


def font(size):
    for name in ("segoeui.ttf", "arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


BIG, MID, SMALL = font(18), font(14), font(13)
cells = [(r, p) for r in picked for p in r["photos"]]
per = COLS * ROWCOUNT
sheets = 0
for start in range(0, len(cells), per):
    batch = cells[start:start + per]
    sheet = Image.new("RGB", (COLS * CELL, ROWCOUNT * (CELL + LABEL)), (250, 250, 252))
    draw = ImageDraw.Draw(sheet)
    for index, (row, photo) in enumerate(batch):
        col, r = index % COLS, index // COLS
        x, y = col * CELL, r * (CELL + LABEL)
        with Image.open(os.path.join(PHOTOS, photo["file"])) as im:
            im = im.convert("RGB")
            im.thumbnail((CELL - 8, CELL - 8), Image.LANCZOS)
            sheet.paste(im, (x + (CELL - im.width) // 2, y + (CELL - im.height) // 2))
        draw.rectangle([x, y, x + CELL - 1, y + CELL + LABEL - 1], outline=(214, 214, 222))
        draw.text((x + 8, y + CELL + 3), f"{photo['file'][:-5]}  {row['azName']}", font=BIG, fill=(16, 16, 24))
        draw.text((x + 8, y + CELL + 25), row["scientificName"], font=MID, fill=(96, 96, 112))
        draw.text((x + 8, y + CELL + 45), f"{photo['part']}  ·  {photo['caption'][:44]}",
                  font=SMALL, fill=(120, 100, 40))
    sheets += 1
    sheet.save(os.path.join(OUT, f"ncsu-{sheets:02d}.webp"), "WEBP", quality=82, method=6)

print(f"{sheets} vərəq")
