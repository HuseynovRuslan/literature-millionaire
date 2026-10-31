"""Downloads the Wikidata pictures and lays them out for one careful look.

Two jobs in one pass because they belong together: a picture is only worth downloading if it is going
to be judged, and it can only be judged next to the name it claims. The label carries the Azerbaijani
name as a player would read it, the scientific name that was actually verified, and a mark when the
file name and categories did NOT confirm the species - those are the ones to look at hardest.
"""
import json
import os
import time
import urllib.request

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROWS = json.load(open(os.path.join(HERE, "wikidata.json"), encoding="utf-8"))
PHOTOS = os.path.join(HERE, "wiki-photos")
SHEETS = os.path.join(HERE, "wiki-sheets")
UA = {"User-Agent": "KitabxanaBot/1.0 (+https://book.qrlog.az; baxmmc.ai@gmail.com)"}
COLS, ROWCOUNT, CELL, LABEL = 4, 3, 360, 74

os.makedirs(PHOTOS, exist_ok=True)
os.makedirs(SHEETS, exist_ok=True)

usable = [r for r in ROWS if r.get("azName") and (r.get("image") or {}).get("url")]
print(f"endiriləcək: {len(usable)}")

ready = []
for n, row in enumerate(usable, 1):
    target = os.path.join(PHOTOS, f"{row['catalogId']}.webp")
    if not os.path.exists(target):
        try:
            request = urllib.request.Request(row["image"]["url"], headers=UA)
            with urllib.request.urlopen(request, timeout=45) as response:
                raw = response.read()
            temp = target + ".src"
            with open(temp, "wb") as handle:
                handle.write(raw)
            with Image.open(temp) as im:
                im = im.convert("RGB")
                if max(im.size) > 1400:
                    scale = 1400 / max(im.size)
                    im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
                im.save(target, "WEBP", quality=86, method=6)
            os.remove(temp)
            time.sleep(0.25)
        except Exception as exc:  # noqa: BLE001
            print(f"  uğursuz {row['catalogId']} {row['scientificName']}: {type(exc).__name__}")
            continue
    ready.append((row, target))
    if n % 25 == 0:
        print(f"  {n}/{len(usable)}", flush=True)

def font(size):
    for name in ("segoeui.ttf", "arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()

BIG, MID, SMALL = font(19), font(16), font(14)
per = COLS * ROWCOUNT
sheets = 0
for start in range(0, len(ready), per):
    batch = ready[start:start + per]
    sheet = Image.new("RGB", (COLS * CELL, ROWCOUNT * (CELL + LABEL)), (250, 250, 252))
    draw = ImageDraw.Draw(sheet)
    for index, (row, path) in enumerate(batch):
        col, r = index % COLS, index // COLS
        x, y = col * CELL, r * (CELL + LABEL)
        with Image.open(path) as im:
            im = im.convert("RGB")
            im.thumbnail((CELL - 8, CELL - 8), Image.LANCZOS)
            sheet.paste(im, (x + (CELL - im.width) // 2, y + (CELL - im.height) // 2))
        draw.rectangle([x, y, x + CELL - 1, y + CELL + LABEL - 1], outline=(214, 214, 222))
        draw.text((x + 8, y + CELL + 5), f"{row['catalogId']}  {row['azName']}", font=BIG, fill=(16, 16, 24))
        draw.text((x + 8, y + CELL + 30), row["scientificName"], font=MID, fill=(96, 96, 112))
        if not row.get("nameInFile"):
            draw.text((x + 8, y + CELL + 52), "! fayl adı növü təsdiqləmir", font=SMALL, fill=(178, 34, 52))
        elif row.get("confusableWith"):
            draw.text((x + 8, y + CELL + 52), f"~ oxşar: {', '.join(row['confusableWith'])[:40]}", font=SMALL, fill=(150, 110, 10))
    sheets += 1
    sheet.save(os.path.join(SHEETS, f"wiki-{sheets:02d}.webp"), "WEBP", quality=82, method=6)

print(f"\nhazır: {len(ready)} foto, {sheets} vərəq")
