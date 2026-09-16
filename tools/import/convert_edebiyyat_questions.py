"""Converts docs/edebiyyat-import/edebiyyat-dunyasi.xlsx into the seed file for the "Ədəbiyyat Dünyası" bank.

Development-time tool only (needs openpyxl); the API never reads the workbook. Run from the repo root:

    python tools/import/convert_edebiyyat_questions.py

- Writes backend/LiteratureMillionaire.API/Seed/Data/edebiyyat-dunyasi-questions.json (UTF-8, stable order).
- Keeps only rows whose Status is "Təsdiqlənib"; anything still awaiting review is skipped and reported.
- Copies images/literature-NNN.webp byte-for-byte into frontend/public/question-images (no re-encoding)
  and rewrites the reference as /question-images/literature-NNN.webp.
- Carries the image source and licence, because two of the pictures are published under CC BY / CC BY-SA
  and their attribution has to travel with the question.
- SourceId (LIT-001...) is kept for traceability only; the question source URL and the reviewer's notes
  are not carried into the application database.
- Refuses to write anything if the bank breaks a rule of the workbook's own "Təlimat" sheet: the
  difficulty and image quotas, a text question carrying image fields, an image question missing its
  source, licence or alt text, an alt text that gives the answer away, duplicate ids or texts,
  options that are not four distinct values, or an image file that is missing from the folder.
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
WORKBOOK = ROOT / "docs/edebiyyat-import/edebiyyat-dunyasi.xlsx"
IMAGES_IN = ROOT / "docs/edebiyyat-import/images"
IMAGES_OUT = ROOT / "frontend/public/question-images"
JSON_OUT = ROOT / "backend/LiteratureMillionaire.API/Seed/Data/edebiyyat-dunyasi-questions.json"

APPROVED_STATUS = "Təsdiqlənib"
IMAGE_URL_PREFIX = "/question-images/"
IMAGE_NAME = re.compile(r"^literature-\d{3}\.webp$")

EXPECTED_TOTAL = 150
EXPECTED_PER_DIFFICULTY = {"Easy": 45, "Medium": 60, "Hard": 45}
EXPECTED_IMAGES = 30
EXPECTED_IMAGES_PER_DIFFICULTY = {"Easy": 9, "Medium": 12, "Hard": 9}


def text(value):
    return "" if value is None else str(value).strip()


def main():
    sheet = openpyxl.load_workbook(WORKBOOK, read_only=True, data_only=True)["Questions"]
    rows = list(sheet.iter_rows(values_only=True))
    header = [text(h) for h in rows[0]]
    records = [dict(zip(header, r)) for r in rows[1:] if any(c not in (None, "") for c in r)]

    out, skipped, problems = [], Counter(), []

    for line, row in enumerate(records, start=2):  # +2: header line and 1-based line numbers
        source_id = text(row["ID"])
        status = text(row["Status"])
        if status != APPROVED_STATUS:
            skipped[status or "(boş status)"] += 1
            continue

        difficulty = text(row["Çətinlik"])
        correct = text(row["Düzgün cavab"])
        options = [text(row[c]) for c in ("A", "B", "C", "D")]
        image_file = text(row["Şəkil faylı"])
        alt_text = text(row["Şəkil alt mətni"])
        image_source = text(row["Şəkil mənbəyi"])
        image_license = text(row["Şəkil lisenziyası"])

        if difficulty not in EXPECTED_PER_DIFFICULTY:
            problems.append(f"line {line} ({source_id}): difficulty {difficulty!r} is not Easy/Medium/Hard")
        if correct not in ("A", "B", "C", "D"):
            problems.append(f"line {line} ({source_id}): correct answer {correct!r} is not A-D")
        if len({o for o in options if o}) != 4:
            problems.append(f"line {line} ({source_id}): options are not four distinct values")
        if not text(row["Sual"]):
            problems.append(f"line {line} ({source_id}): question text is empty")

        if image_file:
            if not IMAGE_NAME.match(image_file):
                problems.append(f"line {line} ({source_id}): image name {image_file!r} is not literature-NNN.webp")
            elif not (IMAGES_IN / image_file).is_file():
                problems.append(f"line {line} ({source_id}): {image_file} is missing from {IMAGES_IN.relative_to(ROOT)}")
            if not alt_text:
                problems.append(f"line {line} ({source_id}): illustrated question has no alt text")
            if not image_source:
                problems.append(f"line {line} ({source_id}): illustrated question has no image source")
            if not image_license:
                problems.append(f"line {line} ({source_id}): illustrated question has no image licence")
            # The alt text must describe the picture, never name the answer.
            answer = options["ABCD".index(correct)] if correct in "ABCD" else ""
            if answer and answer.casefold() in alt_text.casefold():
                problems.append(f"line {line} ({source_id}): alt text names the answer")
        else:
            for label, value in (("alt text", alt_text), ("image source", image_source), ("image licence", image_license)):
                if value:
                    problems.append(f"line {line} ({source_id}): text-only question carries an {label}")

        out.append({
            "sourceId": source_id,
            "category": text(row["Mövzu"]),
            "difficulty": difficulty,
            "text": text(row["Sual"]),
            "optionA": options[0],
            "optionB": options[1],
            "optionC": options[2],
            "optionD": options[3],
            "correctOption": correct,
            "explanation": text(row["İzah"]) or None,
            "imageUrl": IMAGE_URL_PREFIX + image_file if image_file else None,
            "imageAltText": alt_text or None,
            "imageSource": image_source or None,
            "imageLicense": image_license or None,
        })

    # Bank-wide rules from the workbook's own "Təlimat" sheet.
    if len(out) != EXPECTED_TOTAL:
        problems.append(f"Expected {EXPECTED_TOTAL} approved questions, got {len(out)}.")
    per_difficulty = Counter(q["difficulty"] for q in out)
    for difficulty, expected in EXPECTED_PER_DIFFICULTY.items():
        if per_difficulty[difficulty] != expected:
            problems.append(f"Expected {expected} {difficulty} questions, got {per_difficulty[difficulty]}.")
    illustrated = [q for q in out if q["imageUrl"]]
    if len(illustrated) != EXPECTED_IMAGES:
        problems.append(f"Expected {EXPECTED_IMAGES} illustrated questions, got {len(illustrated)}.")
    per_image_difficulty = Counter(q["difficulty"] for q in illustrated)
    for difficulty, expected in EXPECTED_IMAGES_PER_DIFFICULTY.items():
        if per_image_difficulty[difficulty] != expected:
            problems.append(f"Expected {expected} illustrated {difficulty} questions, got {per_image_difficulty[difficulty]}.")

    for field, label in (("sourceId", "id"), ("text", "question text"), ("imageUrl", "image")):
        duplicates = [v for v, c in Counter(q[field] for q in out if q[field]).items() if c > 1]
        if duplicates:
            problems.append(f"Duplicate {label}: {', '.join(sorted(map(str, duplicates))[:5])}")

    used = {q["imageUrl"].removeprefix(IMAGE_URL_PREFIX) for q in illustrated}
    stray = sorted(p.name for p in IMAGES_IN.glob("*") if p.is_file() and p.name not in used)
    if stray:
        problems.append(f"Images in the folder that no question uses: {', '.join(stray)}")

    if problems:
        print("Ədəbiyyat Dünyası conversion failed:", file=sys.stderr)
        for p in problems:
            print("  - " + p, file=sys.stderr)
        return 1

    # Copy the pictures only after the bank has been accepted.
    IMAGES_OUT.mkdir(parents=True, exist_ok=True)
    copied = 0
    for name in sorted(used):
        source, target = IMAGES_IN / name, IMAGES_OUT / name
        if not target.exists() or not filecmp.cmp(source, target, shallow=False):
            shutil.copy2(source, target)
            copied += 1

    JSON_OUT.parent.mkdir(parents=True, exist_ok=True)
    JSON_OUT.write_text(json.dumps({"questions": out}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(f"Wrote {JSON_OUT.relative_to(ROOT)}")
    print(f"  questions: {len(out)} ({', '.join(f'{d} {per_difficulty[d]}' for d in EXPECTED_PER_DIFFICULTY)})")
    print(f"  illustrated: {len(illustrated)} ({', '.join(f'{d} {per_image_difficulty[d]}' for d in EXPECTED_IMAGES_PER_DIFFICULTY)})")
    print(f"  images copied into {IMAGES_OUT.relative_to(ROOT)}: {copied} of {len(used)}")
    if skipped:
        print("  skipped rows: " + ", ".join(f"{status} {count}" for status, count in skipped.items()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
