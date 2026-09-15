"""Converts docs/import/quiz_questions.xlsx into the reviewable seed file for the "Bilik yarışı" campaign.

Development-time tool only (needs openpyxl); the API never reads the workbook. Run from the repo root:

    python tools/import/convert_quiz_questions.py

- Writes backend/LiteratureMillionaire.API/Seed/Data/bilik-yarisi-questions.json (UTF-8, stable order).
- Skips rows whose Notes contain "Yoxlanılmalı" (not yet verified).
- Does not carry SourceUrl, Notes or Status; SourceId is kept in the JSON for traceability only.
- Maps ImageFilename "flag-images/flag-001.webp" to ImageUrl "/question-images/flag-001.webp" and copies the
  file byte-for-byte from docs/import/flag-images to frontend/public/question-images (no re-encoding).
"""
import filecmp
import json
import re
import shutil
import sys
from collections import Counter
from pathlib import Path

import openpyxl

ROOT = Path(__file__).resolve().parents[2]
WORKBOOK = ROOT / "docs/import/quiz_questions.xlsx"
IMAGES_IN = ROOT / "docs/import/flag-images"
IMAGES_OUT = ROOT / "frontend/public/question-images"
JSON_OUT = ROOT / "backend/LiteratureMillionaire.API/Seed/Data/bilik-yarisi-questions.json"
SKIP_MARKER = "Yoxlanılmalı"


def text(value):
    return "" if value is None else str(value).strip()


def main():
    rows = list(openpyxl.load_workbook(WORKBOOK, read_only=True, data_only=True)["Questions"].iter_rows(values_only=True))
    header = [text(h) for h in rows[0]]
    records = [dict(zip(header, r)) for r in rows[1:] if any(c not in (None, "") for c in r)]

    out, skipped = [], Counter()
    for r in records:
        if SKIP_MARKER in text(r.get("Notes")):
            skipped[text(r["Category"])] += 1
            continue
        image, alt = text(r.get("ImageFilename")), text(r.get("ImageAltText"))
        image_url = None
        if image:
            match = re.fullmatch(r"flag-images/(flag-\d{3}\.webp)", image)
            if not match:
                sys.exit(f"{text(r['SourceId'])}: unexpected ImageFilename {image!r}")
            name = match.group(1)
            source, target = IMAGES_IN / name, IMAGES_OUT / name
            if target.exists() and not filecmp.cmp(source, target, shallow=False):
                sys.exit(f"refusing to overwrite a different existing file: {target}")
            shutil.copyfile(source, target)
            image_url = f"/question-images/{name}"
        out.append({
            "sourceId": text(r["SourceId"]),
            "category": text(r["Category"]),
            "difficulty": text(r["Difficulty"]),
            "text": text(r["Question"]),
            "optionA": text(r["OptionA"]),
            "optionB": text(r["OptionB"]),
            "optionC": text(r["OptionC"]),
            "optionD": text(r["OptionD"]),
            "correctOption": text(r["CorrectOption"]),
            "explanation": text(r.get("Explanation")) or None,
            "imageUrl": image_url,
            "imageAltText": alt or None,
        })

    JSON_OUT.write_text(json.dumps(out, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"rows in workbook: {len(records)}; skipped ({SKIP_MARKER}): {sum(skipped.values())} {dict(skipped)}")
    print(f"written: {len(out)} -> {JSON_OUT.relative_to(ROOT)}")
    print("by category:", dict(Counter(q["category"] for q in out)))
    print("by difficulty:", dict(Counter(q["difficulty"] for q in out)))
    print("with image:", sum(1 for q in out if q["imageUrl"]))


if __name__ == "__main__":
    main()
