"""The bank on two sheets - the last look before anything is called finished."""
import json, os
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__))
bank = json.load(open(os.path.join(HERE, "yasil-baki-final.json"), encoding="utf-8"))["plants"]
C, R, CELL, LAB = 4, 4, 340, 58
def font(s):
    for n in ("segoeui.ttf", "arial.ttf", "DejaVuSans.ttf"):
        try: return ImageFont.truetype(n, s)
        except OSError: pass
    return ImageFont.load_default()
BIG, SM = font(19), font(15)
for i in range(0, len(bank), C * R):
    batch = bank[i:i + C * R]
    sheet = Image.new("RGB", (C * CELL, R * (CELL + LAB)), (250, 250, 252))
    d = ImageDraw.Draw(sheet)
    for j, p in enumerate(batch):
        col, row = j % C, j // C
        x, y = col * CELL, row * (CELL + LAB)
        with Image.open(os.path.join(HERE, "final-photos", f"{p['catalogId']}.webp")) as im:
            im = im.convert("RGB"); im.thumbnail((CELL - 8, CELL - 8), Image.LANCZOS)
            sheet.paste(im, (x + (CELL - im.width) // 2, y + (CELL - im.height) // 2))
        d.rectangle([x, y, x + CELL - 1, y + CELL + LAB - 1], outline=(214, 214, 222))
        d.text((x + 8, y + CELL + 5), p["name"], font=BIG, fill=(16, 16, 24))
        d.text((x + 8, y + CELL + 30), f"{p['catalogId']}  ·  {p['difficulty']}", font=SM, fill=(96, 96, 112))
    sheet.save(os.path.join(HERE, f"final-{i // (C * R) + 1:02d}.webp"), "WEBP", quality=84, method=6)
print("hazır")
