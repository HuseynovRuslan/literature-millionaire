"""Trims and resizes the catalogue collages so the plant fills the card instead of floating in it.

Each picture is several photographs from the book laid out on a white sheet, and most of that sheet is
empty - a 1436x1116 file whose content sits in the top-left third. The quiz shows it with object-contain,
so the empty half is scaled right along with the plant and the player squints at something the size of a
postage stamp. Trimming the blank margin is the whole difference.

Only a border that is genuinely uniform is taken, measured against the actual corner colour rather than
an assumed white, because a good few of these are cut-outs on black. Nothing inside the content is
touched: this reflows the sheet, it does not edit the photographs, and the pictures are the project's
own extraction from its own catalogue rather than someone else's licensed work.
"""
import json
import os

from PIL import Image, ImageChops

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = r"C:\Users\HP\LiteratureMillionaire\docs\greegardenbitkiler-import"
OUT = os.path.join(HERE, "photos")
MAX_EDGE = 900  # the card never shows more, and a slow picture is a lost question
PAD = 12

os.makedirs(OUT, exist_ok=True)
plants = json.load(open(os.path.join(SRC, "bitkiler.json"), encoding="utf-8"))["plants"]

trimmed, untouched, missing = 0, 0, []
for index, plant in enumerate(plants, 1):
    source = os.path.join(SRC, plant["images"][0]["file"].replace("/", os.sep))
    if not os.path.exists(source):
        missing.append(plant["images"][0]["file"])
        continue

    with Image.open(source) as im:
        im = im.convert("RGB")
        # The corner is whatever the sheet was laid out on; anything matching it all the way in is margin.
        background = Image.new("RGB", im.size, im.getpixel((0, 0)))
        box = ImageChops.difference(im, background).convert("L").point(lambda v: 255 if v > 18 else 0).getbbox()
        if box and (box[2] - box[0]) > 60 and (box[3] - box[1]) > 60:
            box = (max(box[0] - PAD, 0), max(box[1] - PAD, 0),
                   min(box[2] + PAD, im.width), min(box[3] + PAD, im.height))
            if (box[2] - box[0]) * (box[3] - box[1]) < im.width * im.height * 0.92:
                im = im.crop(box)
                trimmed += 1
            else:
                untouched += 1
        else:
            untouched += 1

        if max(im.size) > MAX_EDGE:
            scale = MAX_EDGE / max(im.size)
            im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
        im.save(os.path.join(OUT, f"gg-{index:03d}.webp"), "WEBP", quality=84, method=6)

total = sum(os.path.getsize(os.path.join(OUT, f)) for f in os.listdir(OUT))
print(f"hazırlandı: {len(os.listdir(OUT))} şəkil")
print(f"  haşiyəsi kəsildi : {trimmed}")
print(f"  olduğu kimi      : {untouched}")
print(f"  tapılmadı        : {len(missing)} {missing[:3]}")
print(f"  ümumi ölçü       : {total // 1024 // 1024} MB, orta {total // max(len(os.listdir(OUT)), 1) // 1024} KB")
