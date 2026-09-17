"""Prepares the category card artwork from docs/cards-image.

The source PNGs are mostly empty canvas - between 64% and 84% of every pixel is fully transparent, and
the cypress is a small object in the corner of a 1660px sheet. CSS sizes an image by its box, not by what
is drawn in it, so with that margin left in, "make the globe 220px tall" makes a 220px box with a much
smaller globe floating somewhere inside, and no two cards could be tuned against each other.

So each file is cropped to what is actually painted (by its alpha channel, with a little room so soft
edges and glows are not clipped), sized for the card with room for a high-density screen, and written as
WebP with its alpha intact. The book cover has no alpha to crop by and goes to /covers/oluler.webp - the
address the database already holds for that book, which until now pointed at nothing.

Run from the repository root:  python tools/import/prepare_category_art.py
"""
import os

from PIL import Image

SOURCE = os.path.join("docs", "cards-image")
ART = os.path.join("frontend", "public", "images")
COVERS = os.path.join("frontend", "public", "covers")

# source file -> (destination, longest edge). Matched by looking at the pictures, not by their names.
ASSETS = {
    "bilik-dunyasi-3d-globe.png": (os.path.join(ART, "bilik-dunyasi.webp"), 900),
    "edebiyyat-dunyasi-3d-book.png": (os.path.join(ART, "edebiyyat-dunyasi.webp"), 900),
    "yasil-baki-cupressus-overlay.png": (os.path.join(ART, "yasil-baki.webp"), 900),
    "green-garden-premium-3d.png": (os.path.join(ART, "green-garden.webp"), 900),
    "oluler.jpg": (os.path.join(COVERS, "oluler.webp"), 900),
}

ALPHA_FLOOR = 8   # below this an edge pixel is anti-aliasing dust, not artwork
PADDING = 0.03    # of the painted size, so a glow or a soft shadow is not cut flat


def crop_to_painted(im):
    alpha = im.getchannel("A").point(lambda v: 255 if v > ALPHA_FLOOR else 0)
    box = alpha.getbbox()
    if not box:
        return im
    pad_x = round((box[2] - box[0]) * PADDING)
    pad_y = round((box[3] - box[1]) * PADDING)
    return im.crop((max(box[0] - pad_x, 0), max(box[1] - pad_y, 0),
                    min(box[2] + pad_x, im.width), min(box[3] + pad_y, im.height)))


os.makedirs(ART, exist_ok=True)
for source, (target, edge) in ASSETS.items():
    with Image.open(os.path.join(SOURCE, source)) as im:
        before = im.size
        if im.mode in ("RGBA", "LA") or "transparency" in im.info:
            im = crop_to_painted(im.convert("RGBA"))
        else:
            im = im.convert("RGB")
        if max(im.size) > edge:
            scale = edge / max(im.size)
            im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
        # Lossy colour, lossless-quality alpha: exact edges matter more than the last kilobyte.
        im.save(target, "WEBP", quality=88, alpha_quality=100, method=6)
        print(f"{source:<36} {before} -> {im.size} {im.mode:<4} {os.path.getsize(target) // 1024:>4} KB  {target}")
