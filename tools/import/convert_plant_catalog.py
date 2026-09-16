"""Converts docs/plants-import/mapping-and-sources.csv into the reviewable seed file for the plant catalogue.

Development-time tool only; the API never reads the CSV. Run from the repo root:

    python tools/import/convert_plant_catalog.py

- Writes backend/LiteratureMillionaire.API/Seed/Data/plant-catalog.json (UTF-8, stable order).
- Groups the 50 image rows into plants by "Bitki ID": one plant per catalogue id, its images in file order.
  Rows marked "əlavə foto" are extra photographs of a plant that already exists - never a new plant and
  never a new answer option.
- Carries the attribution the licences require (source, page, author, licence, licence link) plus the
  specimen species and botanical note, so the public "image sources" section can be built from the database.
- Keeps the catalogue answer names exactly as reviewed (including "Microphyllus", "Passifloraceae" and
  "Geran (Pelargonium)"); nothing is renamed, merged or split here.
- "Kataloqdakı quiz statusu" maps to QuizStatus: "Uyğundur" -> Approved, anything else -> Conditional.
  Conditional plants are imported but stay out of the live quiz until an administrator approves them.
- Verifies that every referenced image file exists under frontend/public/images/plants.
"""
import csv
import json
import sys
from collections import Counter, OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CSV_IN = ROOT / "docs/plants-import/mapping-and-sources.csv"
IMAGES_DIR = ROOT / "frontend/public/images/plants"
JSON_OUT = ROOT / "backend/LiteratureMillionaire.API/Seed/Data/plant-catalog.json"
IMAGE_URL_PREFIX = "/images/plants/"

PRIMARY_ROLE = "əsas foto"
APPROVED_STATUS = "Uyğundur"


def text(value):
    return "" if value is None else str(value).strip()


def main():
    with CSV_IN.open(encoding="utf-8-sig", newline="") as f:
        rows = [r for r in csv.DictReader(f) if text(r.get("Şəkil faylı"))]

    plants = OrderedDict()
    problems = []
    roles = Counter()

    for index, row in enumerate(rows, start=2):  # +2: header line and 1-based line numbers
        file_name = text(row["Şəkil faylı"])
        catalog_id = text(row["Bitki ID"])
        name = text(row["Kataloqdakı cavab adı"])
        role = text(row["Foto rolu"])
        roles[role] += 1

        if not (IMAGES_DIR / file_name).is_file():
            problems.append(f"line {index}: image file {file_name} is missing from {IMAGES_DIR.relative_to(ROOT)}")

        plant = plants.get(catalog_id)
        if plant is None:
            plant = {
                "catalogId": catalog_id,
                "name": name,
                "scientificName": text(row["Elmi/latın adı"]),
                "quizStatus": "Approved" if text(row["Kataloqdakı quiz statusu"]) == APPROVED_STATUS else "Conditional",
                "botanicalNote": text(row["Botaniki qeyd"]) or None,
                "images": [],
            }
            plants[catalog_id] = plant
        else:
            # The catalogue must agree with itself: every row of a plant carries the same answer name and status.
            if plant["name"] != name:
                problems.append(f"line {index}: {catalog_id} has two answer names ({plant['name']!r} and {name!r})")
            status = "Approved" if text(row["Kataloqdakı quiz statusu"]) == APPROVED_STATUS else "Conditional"
            if plant["quizStatus"] != status:
                problems.append(f"line {index}: {catalog_id} has two quiz statuses")

        plant["images"].append({
            "fileName": file_name,
            "imageUrl": IMAGE_URL_PREFIX + file_name,
            "isPrimary": role == PRIMARY_ROLE,
            "source": text(row["Mənbə"]),
            "sourceUrl": text(row["Mənbə faylı/səhifəsi"]) or None,
            "author": text(row["Müəllif"]) or None,
            "license": text(row["Lisenziya/istifadə statusu"]),
            "licenseUrl": text(row["Lisenziya linki"]) or None,
            "specimenSpecies": text(row["Şəkildəki nümunə növ"]) or None,
            "originalLabel": text(row["Orijinal foto etiketi"]) or None,
        })

    for catalog_id, plant in plants.items():
        primaries = sum(1 for i in plant["images"] if i["isPrimary"])
        if primaries != 1:
            problems.append(f"{catalog_id}: expected exactly one primary photo, found {primaries}")

    file_names = [i["fileName"] for p in plants.values() for i in p["images"]]
    duplicates = [n for n, c in Counter(file_names).items() if c > 1]
    if duplicates:
        problems.append(f"image files listed more than once: {', '.join(sorted(duplicates))}")

    stray = sorted(p.name for p in IMAGES_DIR.glob("*.webp") if p.name not in set(file_names))
    if stray:
        problems.append(f"images present on disk but not in the CSV: {', '.join(stray)}")

    if problems:
        print("Plant catalogue conversion failed:", file=sys.stderr)
        for p in problems:
            print("  - " + p, file=sys.stderr)
        return 1

    payload = {"plants": list(plants.values())}
    JSON_OUT.parent.mkdir(parents=True, exist_ok=True)
    JSON_OUT.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    approved = sum(1 for p in plants.values() if p["quizStatus"] == "Approved")
    print(f"Wrote {JSON_OUT.relative_to(ROOT)}")
    print(f"  plants: {len(plants)} ({approved} approved, {len(plants) - approved} conditional)")
    print(f"  images: {len(file_names)} ({roles[PRIMARY_ROLE]} primary, {len(file_names) - roles[PRIMARY_ROLE]} additional)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
