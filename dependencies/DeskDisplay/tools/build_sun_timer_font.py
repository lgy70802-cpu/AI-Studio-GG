from __future__ import annotations

from pathlib import Path

import cv2
import numpy as np
import potrace
from PIL import Image, ImageDraw, ImageFont
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.t2CharStringPen import T2CharStringPen


ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(r"C:\Users\Administrator\Desktop\未标题-4.jpg")
OUT = ROOT / "artifacts" / "sun-timer-font"

UPM = 1000
ADVANCE = 650
CAP_HEIGHT = 700


def charstring(polygons: list[list[tuple[float, float]]], width: int = ADVANCE):
    pen = T2CharStringPen(width, None)
    for polygon in polygons:
        if len(polygon) < 3:
            continue
        pen.moveTo(polygon[0])
        for point in polygon[1:]:
            pen.lineTo(point)
        pen.closePath()
    return pen.getCharString()


def rectangle(x0: int, y0: int, x1: int, y1: int):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def foreground_mask(image: np.ndarray):
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
    # The pale numerals have distinctly lower saturation than the yellow field.
    return np.where((hsv[:, :, 1] < 200) & (hsv[:, :, 2] > 220), 255, 0).astype(np.uint8)


def clean_component(mask: np.ndarray, crop: tuple[int, int, int, int], min_area: int = 500):
    x, y, w, h = crop
    local = mask[y : y + h, x : x + w].copy()
    count, labels, stats, _ = cv2.connectedComponentsWithStats(local, connectivity=8)
    clean = np.zeros_like(local)
    for index in range(1, count):
        if stats[index, cv2.CC_STAT_AREA] >= min_area:
            clean[labels == index] = 255
    return clean


def trace_mask(mask: np.ndarray, scale: float, baseline_bottom: int = 0, width: int = ADVANCE):
    ys, xs = np.where(mask > 0)
    if not len(xs):
        raise ValueError("Empty glyph mask")
    left, right = int(xs.min()), int(xs.max()) + 1
    top, bottom = int(ys.min()), int(ys.max()) + 1
    glyph_width = (right - left) * scale
    x_offset = (width - glyph_width) / 2
    # potracer treats zero-valued pixels as the black/filled region.
    traced = potrace.Bitmap(mask == 0).trace(
        turdsize=20,
        alphamax=0.72,
        opticurve=True,
        opttolerance=0.35,
    )
    pen = T2CharStringPen(width, None)

    def transform(point):
        px, py = point.x, point.y
        return (
            round(x_offset + (float(px) - left) * scale, 1),
            round(baseline_bottom + (bottom - float(py)) * scale, 1),
        )

    for curve in traced.curves:
        if curve.start_point is None:
            continue
        pen.moveTo(transform(curve.start_point))
        for segment in curve.segments:
            if segment.is_corner:
                pen.lineTo(transform(segment.c))
                pen.lineTo(transform(segment.end_point))
            else:
                pen.curveTo(
                    transform(segment.c1),
                    transform(segment.c2),
                    transform(segment.end_point),
                )
        pen.closePath()
    return pen.getCharString()


def build_font(output: Path):
    raw = np.fromfile(SOURCE, dtype=np.uint8)
    image = cv2.imdecode(raw, cv2.IMREAD_COLOR)
    if image is None:
        raise FileNotFoundError(SOURCE)
    mask = foreground_mask(image)

    # Best-preserved instance of each numeral in the supplied image.
    crops = {
        "0": (125, 1115, 216, 276),
        "1": (143, 753, 133, 274),
        "2": (598, 751, 204, 275),
        "3": (638, 11, 211, 281),
        "4": (608, 1116, 228, 273),
        "5": (852, 15, 210, 277),
        "6": (291, 750, 217, 279),
        "7": (347, 380, 198, 274),
        "8": (333, 10, 219, 282),
        "9": (831, 1113, 217, 279),
    }

    glyphs = {".notdef": charstring([rectangle(85, 0, 565, 700), rectangle(145, 60, 505, 640)])}
    cmap: dict[int, str] = {}
    for digit, crop in crops.items():
        local = clean_component(mask, crop)
        ys = np.where(local > 0)[0]
        scale = CAP_HEIGHT / (int(ys.max()) - int(ys.min()) + 1)
        name = f"digit{digit}"
        glyphs[name] = trace_mask(local, scale)
        cmap[ord(digit)] = name

    # Preserve the dot size and vertical placement relative to the numerals.
    colon_mask = clean_component(mask, (535, 410, 95, 215), min_area=400)
    digit_scale = CAP_HEIGHT / 258.0
    colon_height = (np.ptp(np.where(colon_mask > 0)[0]) + 1) * digit_scale
    glyphs["colon"] = trace_mask(colon_mask, digit_scale, round((CAP_HEIGHT - colon_height) / 2), 300)
    glyphs["space"] = charstring([], 325)
    glyphs["period"] = charstring([rectangle(103, 0, 197, 94)], 300)
    glyphs["hyphen"] = charstring([rectangle(60, 300, 340, 374)], 400)
    cmap.update({32: "space", 45: "hyphen", 46: "period", 58: "colon"})

    order = [".notdef", "space"] + [f"digit{i}" for i in range(10)] + ["colon", "period", "hyphen"]
    metrics = {name: (ADVANCE, 0) for name in order}
    metrics.update({"space": (325, 0), "colon": (300, 0), "period": (300, 0), "hyphen": (400, 0)})
    fb = FontBuilder(UPM, isTTF=False)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupCFF(
        "SunTimerDisplaySmooth-Regular",
        {"FullName": "Sun Timer Display Smooth Regular", "FamilyName": "Sun Timer Display Smooth", "Weight": "Regular"},
        glyphs,
        {},
    )
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=800, descent=-200)
    fb.setupNameTable(
        {
            "familyName": "Sun Timer Display Smooth",
            "styleName": "Regular",
            "uniqueFontIdentifier": "Sun Timer Display Smooth Regular 2.000",
            "fullName": "Sun Timer Display Smooth Regular",
            "psName": "SunTimerDisplaySmooth-Regular",
            "version": "Version 2.000",
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
        usWidthClass=4,
    )
    fb.setupPost(isFixedPitch=0)
    fb.save(output)


def make_specimen(font_path: Path, output: Path):
    canvas = Image.new("RGB", (1800, 950), "#17191D")
    draw = ImageDraw.Draw(canvas)
    ui_font = r"C:\Windows\Fonts\segoeui.ttf"
    title = ImageFont.truetype(ui_font, 58)
    huge = ImageFont.truetype(str(font_path), 210)
    medium = ImageFont.truetype(str(font_path), 112)
    small = ImageFont.truetype(ui_font, 30)
    draw.text((90, 55), "SUN TIMER DISPLAY — SMOOTH V2", font=title, fill="#FFF96A")
    draw.line((90, 145, 1710, 145), fill="#454A52", width=2)
    draw.text((90, 190), "0123456789", font=huge, fill="#F8F8F2")
    draw.text((90, 470), "08:35  07:52", font=medium, fill="#FFF96A")
    draw.text((90, 645), "16:20  07:49", font=medium, fill="#FFF96A")
    draw.text((90, 850), "DIGITS / COLON / PERIOD / HYPHEN / SPACE", font=small, fill="#9DA4AE")
    canvas.save(output)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    font_path = OUT / "SunTimerDisplaySmooth-Regular.otf"
    build_font(font_path)
    make_specimen(font_path, OUT / "SunTimerDisplaySmooth-Specimen.png")
    print(font_path)
