from __future__ import annotations

from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.t2CharStringPen import T2CharStringPen


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "edge9-font"
DIGITS_IMAGE = Path(r"C:\Users\Administrator\Desktop\9px_edge_10.png")
LETTERS_IMAGE = Path(r"C:\Users\Administrator\Desktop\9px_edge_16.png")

UNITS_PER_EM = 1000
ADVANCE = 620
CAP_HEIGHT = 700
SIDE_BEARING = 60


def polygon_glyph(polygons: list[list[tuple[float, float]]]):
    pen = T2CharStringPen(ADVANCE, None)
    for polygon in polygons:
        if len(polygon) < 3:
            continue
        pen.moveTo(polygon[0])
        for point in polygon[1:]:
            pen.lineTo(point)
        pen.closePath()
    return pen.getCharString()


def extract_glyph(image: np.ndarray, cell: tuple[int, int, int, int]):
    x0, y0, x1, y1 = cell
    gray = image[y0 + 12 : y1 - 12, x0 + 12 : x1 - 12]
    mask = np.where(gray >= 180, 255, 0).astype(np.uint8)

    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask, connectivity=8)
    keep = np.zeros_like(mask)
    for index in range(1, count):
        if stats[index, cv2.CC_STAT_AREA] >= 500:
            keep[labels == index] = 255

    ys, xs = np.where(keep > 0)
    if len(xs) == 0:
        raise ValueError(f"No glyph found in cell {cell}")

    left, right = int(xs.min()), int(xs.max()) + 1
    top, bottom = int(ys.min()), int(ys.max()) + 1
    contours, _ = cv2.findContours(keep, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)

    scale = CAP_HEIGHT / (bottom - top)
    glyph_width = (right - left) * scale
    x_offset = (ADVANCE - glyph_width) / 2
    polygons: list[list[tuple[float, float]]] = []

    for contour in contours:
        if abs(cv2.contourArea(contour)) < 100:
            continue
        simplified = cv2.approxPolyDP(contour, 1.0, True).reshape(-1, 2)
        polygon = []
        for x, y in simplified:
            fx = x_offset + (float(x) - left) * scale
            fy = (bottom - float(y)) * scale
            polygon.append((round(fx), round(fy)))
        polygons.append(polygon)

    return polygon_glyph(polygons)


def rect(x0: int, y0: int, x1: int, y1: int):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def build_font(output: Path):
    digit_img = cv2.imread(str(DIGITS_IMAGE), cv2.IMREAD_GRAYSCALE)
    letter_img = cv2.imread(str(LETTERS_IMAGE), cv2.IMREAD_GRAYSCALE)
    if digit_img is None or letter_img is None:
        raise FileNotFoundError("Source PNG files could not be loaded")

    glyphs = {".notdef": polygon_glyph([rect(80, 0, 540, 700), rect(140, 60, 480, 640)])}
    cmap: dict[int, str] = {}

    # Exact guide-box coordinates in the two supplied 3840px source images.
    digit_starts_x = [729, 1229, 1729, 2229, 2729]
    digit_starts_y = [399, 899]
    for value in range(10):
        row, col = divmod(value, 5)
        cell = (digit_starts_x[col], digit_starts_y[row], digit_starts_x[col] + 381, digit_starts_y[row] + 381)
        name = f"digit{value}"
        glyphs[name] = extract_glyph(digit_img, cell)
        cmap[ord(str(value))] = name

    letter_starts_x = [627, 1007, 1387, 1767, 2147, 2527, 2907]
    letter_starts_y = [327, 707, 1087, 1467]
    for index, char in enumerate("ABCDEFGHIJKLMNOPQRSTUVWXYZ"):
        row, col = divmod(index, 7)
        cell = (letter_starts_x[col], letter_starts_y[row], letter_starts_x[col] + 307, letter_starts_y[row] + 305)
        glyphs[char] = extract_glyph(letter_img, cell)
        cmap[ord(char)] = char

    glyphs["space"] = polygon_glyph([])
    glyphs["hyphen"] = polygon_glyph([rect(175, 300, 445, 365)])
    glyphs["period"] = polygon_glyph([rect(275, 0, 345, 70)])
    glyphs["colon"] = polygon_glyph([rect(275, 170, 345, 240), rect(275, 470, 345, 540)])
    glyphs["slash"] = polygon_glyph([[(145, 0), (225, 0), (475, 700), (395, 700)]])
    cmap.update({32: "space", 45: "hyphen", 46: "period", 47: "slash", 58: "colon"})

    glyph_order = [".notdef", "space"] + list("ABCDEFGHIJKLMNOPQRSTUVWXYZ") + [f"digit{i}" for i in range(10)] + ["hyphen", "period", "colon", "slash"]
    metrics = {name: (ADVANCE, 0) for name in glyph_order}

    fb = FontBuilder(UNITS_PER_EM, isTTF=False)
    fb.setupGlyphOrder(glyph_order)
    fb.setupCharacterMap(cmap)
    fb.setupCFF(
        "Edge9Display-Regular",
        {"FullName": "Edge9 Display Regular", "FamilyName": "Edge9 Display", "Weight": "Regular"},
        glyphs,
        {},
    )
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=800, descent=-200)
    fb.setupNameTable(
        {
            "familyName": "Edge9 Display",
            "styleName": "Regular",
            "uniqueFontIdentifier": "Edge9 Display Regular 1.000",
            "fullName": "Edge9 Display Regular",
            "psName": "Edge9Display-Regular",
            "version": "Version 1.000",
        }
    )
    fb.setupOS2(
        sTypoAscender=800,
        sTypoDescender=-200,
        sTypoLineGap=0,
        usWinAscent=800,
        usWinDescent=200,
        sxHeight=700,
        sCapHeight=700,
        usWeightClass=400,
        usWidthClass=5,
    )
    fb.setupPost(isFixedPitch=1)
    fb.save(output)


def make_specimen(font_path: Path, output: Path):
    width, height = 1800, 850
    image = Image.new("RGB", (width, height), "#111318")
    draw = ImageDraw.Draw(image)
    title_font = ImageFont.truetype(str(font_path), 70)
    big_font = ImageFont.truetype(str(font_path), 118)
    small_font = ImageFont.truetype(str(font_path), 58)
    white, muted = "#F4F4F2", "#9CA3AF"
    draw.text((90, 65), "EDGE9 DISPLAY", font=title_font, fill=white)
    draw.line((90, 165, 1710, 165), fill="#343842", width=2)
    draw.text((90, 225), "0123456789", font=big_font, fill=white)
    draw.text((90, 385), "ABCDEFG", font=big_font, fill=white)
    draw.text((90, 535), "HIJKLMNOPQRSTUVWXYZ", font=small_font, fill=white)
    draw.text((90, 670), "A1-B2 / 09:45.", font=small_font, fill=muted)
    draw.text((90, 775), "EDGE9 DISPLAY  |  REGULAR  |  OTF", font=ImageFont.truetype(str(font_path), 28), fill=muted)
    image.save(output)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    font_path = OUT / "Edge9Display-Regular.otf"
    build_font(font_path)
    make_specimen(font_path, OUT / "Edge9Display-Specimen.png")
    print(font_path)
