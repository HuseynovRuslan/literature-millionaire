Question illustrations served at /question-images/<file>. Question.ImageUrl must use exactly these paths.

Current files are optimised WebP (1536x1024, ~180-280 KB). File names are intentionally neutral: the URL appears in the DOM, so it must not hint at the correct answer.

Flag images flag-001.webp ... flag-100.webp ("Bilik yarışı" bank): 1200x900 WebP copied byte-for-byte from docs/import/flag-images by tools/import/convert_quiz_questions.py (workbook note: flag-icons SVG -> 1200 px WebP). Numbered names never reveal the country; the seed rejects alt texts that contain the correct answer.

Original PNG sources live in frontend/question-images-src/ (not served, not built). To add or replace an illustration: put the source PNG there, export WebP (quality ~82) into this folder with a neutral name, then set ImageUrl/ImageAltText on the question (admin page or the Ölülər media seed).
