from __future__ import annotations

from pathlib import Path

import cv2
import numpy as np
import potrace
from PIL import Image, ImageDraw, ImageFont
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.t2CharStringPen import T2CharStringPen


ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(r"C:\Users\Administrator\Downloads\jimeng-2026-09-24-2732-把这张图变成超清4k图.png")
OUT = ROOT / "artifacts" / "sun-timer-4k-font"
UPM = 1000
ADVANCE = 650
CAP_HEIGHT = 700

# Connected-component bounding boxes in the original 3392 x 5248 image.
# Each chosen instance has an unobstructed outline and an intact counter.
DIGIT_BOXES = {
    "0": (357, 58, 577, 762),
    "1": (437, 2212, 335, 738),
    "2": (1762, 2201, 532, 749),
    "3": (1878, 58, 558, 762),
    "4": (1796, 3267, 598, 736),
    "5": (2504, 70, 552, 750),
    "6": (872, 2200, 573, 763),
    "7": (1036, 1133, 518, 737),
    "8": (996, 58, 576, 762),
    "9": (2440, 3255, 574, 762),
}


def circle(pen: T2CharStringPen, cx: float, cy: float, radius: float):
    k = 0.5522847498307936 * radius
    pen.moveTo((cx + radius, cy))
    pen.curveTo((cx + radius, cy + k), (cx + k, cy + radius), (cx, cy + radius))
    pen.curveTo((cx - k, cy + radius), (cx - radius, cy + k), (cx - radius, cy))
    pen.curveTo((cx - radius, cy - k), (cx - k, cy - radius), (cx, cy - radius))
    pen.curveTo((cx + k, cy - radius), (cx + radius, cy - k), (cx + radius, cy))
    pen.closePath()


def empty_glyph(width: int):
    return T2CharStringPen(width, None).getCharString()


def rectangle_glyph(width: int, x0: int, y0: int, x1: int, y1: int):
    pen = T2CharStringPen(width, None)
    pen.moveTo((x0, y0))
    pen.lineTo((x1, y0))
    pen.lineTo((x1, y1))
    pen.lineTo((x0, y1))
    pen.closePath()
    return pen.getCharString()


def traced_digit(mask: np.ndarray, box: tuple[int, int, int, int]):
    x, y, w, h = box
    pad = 15
    region = mask[y - pad : y + h + pad, x - pad : x + w + pad]

    # Suppress tiny AI-upscaling ripples, then trace the clean edge at a
    # consistent working resolution.  This preserves corners while giving
    # broad curved strokes a compact set of cubic Bezier segments.
    target_h = 520
    factor = target_h / h
    work = cv2.resize(region, None, fx=factor, fy=factor, interpolation=cv2.INTER_AREA)
    work = cv2.GaussianBlur(work, (0, 0), 1.35)
    _, work = cv2.threshold(work, 127, 255, cv2.THRESH_BINARY)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(work, connectivity=8)
    clean = np.zeros_like(work)
    for index in range(1, count):
        if stats[index, cv2.CC_STAT_AREA] > 150:
            clean[labels == index] = 255

    ys, xs = np.where(clean > 0)
    if len(xs) == 0:
        raise ValueError(f"No numeral in crop {box}")
    left, right = int(xs.min()), int(xs.max()) + 1
    bottom = int(ys.max()) + 1
    height = int(ys.max()) - int(ys.min()) + 1
    scale = CAP_HEIGHT / height
    glyph_width = (right - left) * scale
    x_offset = (ADVANCE - glyph_width) / 2

    def point(p):
        return (round(x_offset + (p.x - left) * scale, 2), round((bottom - p.y) * scale, 2))

    pen = T2CharStringPen(ADVANCE, None)
    # potrace's zero pixels are the filled side of the bitmap.
    paths = potrace.Bitmap(clean == 0).trace(
        turdsize=30, alphamax=0.85, opticurve=True, opttolerance=0.55
    )
    for path in paths:
        pen.moveTo(point(path.start_point))
        for segment in path:
            if segment.is_corner:
                pen.lineTo(point(segment.c))
                pen.lineTo(point(segment.end_point))
            else:
                pen.curveTo(point(segment.c1), point(segment.c2), point(segment.end_point))
        pen.closePath()
    return pen.getCharString()


def build_font(font_path: Path):
    source = cv2.imdecode(np.fromfile(SOURCE, dtype=np.uint8), cv2.IMREAD_COLOR)
    if source is None or source.shape[:2] != (5248, 3392):
        raise ValueError("The expected 3392 x 5248 source PNG could not be read")
    hsv = cv2.cvtColor(source, cv2.COLOR_BGR2HSV)
    foreground = np.where((hsv[:, :, 1] > 80) & (hsv[:, :, 2] > 180), 255, 0).astype(np.uint8)

    glyphs = {".notdef": rectangle_glyph(ADVANCE, 100, 0, 550, 700)}
    cmap: dict[int, str] = {}
    for digit, box in DIGIT_BOXES.items():
        name = f"digit{digit}"
        glyphs[name] = traced_digit(foreground, box)
        cmap[ord(digit)] = name

    colon = T2CharStringPen(300, None)
    circle(colon, 150, 540, 70)
    circle(colon, 150, 190, 70)
    glyphs["colon"] = colon.getCharString()

    period = T2CharStringPen(300, None)
    circle(period, 150, 70, 70)
    glyphs["period"] = period.getCharString()
    glyphs["space"] = empty_glyph(325)
    glyphs["hyphen"] = rectangle_glyph(400, 60, 315, 340, 385)
    cmap.update({32: "space", 45: "hyphen", 46: "period", 58: "colon"})

    order = [".notdef", "space"] + [f"digit{i}" for i in range(10)] + ["colon", "period", "hyphen"]
    metrics = {name: (ADVANCE, 0) for name in order}
    metrics.update({"space": (325, 0), "colon": (300, 0), "period": (300, 0), "hyphen": (400, 0)})
    family = "Sun Timer 4K Smooth"
    postscript = "SunTimer4KSmooth-Regular"
    fb = FontBuilder(UPM, isTTF=False)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupCFF(postscript, {"FullName": f"{family} Regular", "FamilyName": family, "Weight": "Regular"}, glyphs, {})
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=800, descent=-200)
    fb.setupNameTable({
        "familyName": family,
        "styleName": "Regular",
        "uniqueFontIdentifier": f"{family} Regular 1.000",
        "fullName": f"{family} Regular",
        "psName": postscript,
        "version": "Version 1.000",
    })
    fb.setupOS2(
        sTypoAscender=800, sTypoDescender=-200, sTypoLineGap=0,
        usWinAscent=800, usWinDescent=200, sxHeight=700, sCapHeight=700,
        usWeightClass=400, usWidthClass=4,
    )
    fb.setupPost(isFixedPitch=0)
    fb.save(font_path)


def specimen(font_path: Path, output: Path):
    canvas = Image.new("RGB", (2200, 1100), "#191b20")
    draw = ImageDraw.Draw(canvas)
    ui = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 52)
    large = ImageFont.truetype(str(font_path), 220)
    medium = ImageFont.truetype(str(font_path), 160)
    draw.text((90, 48), "SUN TIMER 4K SMOOTH", font=ui, fill="#FFF952")
    draw.line((90, 135, 2110, 135), fill="#50535a", width=2)
    draw.text((80, 170), "0123456789", font=large, fill="white")
    draw.text((80, 455), "08:35   07:52", font=medium, fill="#FFF952")
    draw.text((80, 680), "16:20   07:49", font=medium, fill="#FFF952")
    draw.text((90, 990), "0–9  :  .  -  SPACE", font=ui, fill="#aeb3bd")
    canvas.save(output)


def detail_specimen(font_path: Path, output: Path):
    canvas = Image.new("RGB", (2100, 1050), "#191b20")
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.truetype(str(font_path), 600)
    draw.text((30, 15), "0:9", font=font, fill="#FFF952")
    canvas.save(output)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    font_path = OUT / "SunTimer4KSmooth-Regular.otf"
    build_font(font_path)
    specimen(font_path, OUT / "SunTimer4KSmooth-Specimen.png")
    detail_specimen(font_path, OUT / "SunTimer4KSmooth-Detail.png")
    print(font_path)
