from __future__ import annotations

from pathlib import Path

import cv2
import numpy as np
import potrace
from PIL import Image, ImageDraw, ImageFont
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.t2CharStringPen import T2CharStringPen


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "narrow-timer-font"
SOURCE = OUT / "source.png"
CAP_HEIGHT = 700
ADVANCE = 255

# Bounds of 0–9 in the supplied 5568 x 3136 PNG, from left to right.
BOXES = [
    (147, 645, 510, 1742),
    (701, 655, 291, 1726),
    (1065, 645, 500, 1736),
    (1606, 645, 501, 1742),
    (2156, 655, 545, 1726),
    (2746, 655, 491, 1732),
    (3281, 645, 514, 1742),
    (3829, 655, 488, 1726),
    (4338, 645, 525, 1742),
    (4905, 645, 502, 1742),
]


def circle(pen: T2CharStringPen, x: float, y: float, radius: float):
    k = radius * 0.5522847498307936
    pen.moveTo((x + radius, y))
    pen.curveTo((x + radius, y + k), (x + k, y + radius), (x, y + radius))
    pen.curveTo((x - k, y + radius), (x - radius, y + k), (x - radius, y))
    pen.curveTo((x - radius, y - k), (x - k, y - radius), (x, y - radius))
    pen.curveTo((x + k, y - radius), (x + radius, y - k), (x + radius, y))
    pen.closePath()


def glyph_from_box(foreground: np.ndarray, box: tuple[int, int, int, int]):
    x, y, w, h = box
    padding = 22
    crop = foreground[y - padding : y + h + padding, x - padding : x + w + padding]
    work = cv2.resize(crop, None, fx=0.42, fy=0.42, interpolation=cv2.INTER_AREA)
    work = cv2.GaussianBlur(work, (0, 0), 1.15)
    _, work = cv2.threshold(work, 127, 255, cv2.THRESH_BINARY)

    # Only the large numeral component should be retained. Its interior holes
    # remain zero-valued and become separate counter outlines in potrace.
    count, labels, stats, _ = cv2.connectedComponentsWithStats(work, connectivity=8)
    largest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    work = np.where(labels == largest, 255, 0).astype(np.uint8)
    ys, xs = np.where(work != 0)
    left, right = int(xs.min()), int(xs.max()) + 1
    bottom = int(ys.max()) + 1
    glyph_height = int(ys.max()) - int(ys.min()) + 1
    scale = CAP_HEIGHT / glyph_height
    x_offset = (ADVANCE - (right - left) * scale) / 2

    def unit(point):
        return (
            round(x_offset + (point.x - left) * scale, 2),
            round((bottom - point.y) * scale, 2),
        )

    pen = T2CharStringPen(ADVANCE, None)
    paths = potrace.Bitmap(work == 0).trace(
        turdsize=24, alphamax=0.88, opticurve=True, opttolerance=0.55
    )
    for path in paths:
        pen.moveTo(unit(path.start_point))
        for segment in path:
            if segment.is_corner:
                pen.lineTo(unit(segment.c))
                pen.lineTo(unit(segment.end_point))
            else:
                pen.curveTo(unit(segment.c1), unit(segment.c2), unit(segment.end_point))
        pen.closePath()
    return pen.getCharString()


def build_font(
    output: Path,
    one_reference: Path | None = None,
    family: str = "Narrow Timer Digits",
    postscript: str = "NarrowTimerDigits-Regular",
):
    image = cv2.imdecode(np.fromfile(SOURCE, dtype=np.uint8), cv2.IMREAD_COLOR)
    if image is None or image.shape[:2] != (3136, 5568):
        raise ValueError("Expected 5568 x 3136 source PNG")
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    foreground = np.where(gray > 220, 255, 0).astype(np.uint8)

    glyphs = {".notdef": T2CharStringPen(ADVANCE, None).getCharString()}
    cmap: dict[int, str] = {}
    for digit, box in enumerate(BOXES):
        name = f"digit{digit}"
        glyphs[name] = glyph_from_box(foreground, box)
        cmap[ord(str(digit))] = name

    if one_reference is not None:
        reference = cv2.imdecode(np.fromfile(one_reference, dtype=np.uint8), cv2.IMREAD_COLOR)
        if reference is None or reference.shape[:2] != (2048, 1241):
            raise ValueError("Expected 1241 x 2048 comparison PNG")
        reference_gray = cv2.cvtColor(reference, cv2.COLOR_BGR2GRAY)
        reference_foreground = np.where(reference_gray > 220, 255, 0).astype(np.uint8)
        glyphs["digit1"] = glyph_from_box(reference_foreground, (179, 215, 337, 1664))

    glyphs["space"] = T2CharStringPen(128, None).getCharString()
    colon = T2CharStringPen(125, None)
    circle(colon, 62.5, 515, 27)
    circle(colon, 62.5, 185, 27)
    glyphs["colon"] = colon.getCharString()
    period = T2CharStringPen(125, None)
    circle(period, 62.5, 27, 27)
    glyphs["period"] = period.getCharString()
    hyphen = T2CharStringPen(180, None)
    hyphen.moveTo((25, 320))
    hyphen.lineTo((155, 320))
    hyphen.lineTo((155, 365))
    hyphen.lineTo((25, 365))
    hyphen.closePath()
    glyphs["hyphen"] = hyphen.getCharString()
    cmap.update({32: "space", 45: "hyphen", 46: "period", 58: "colon"})

    order = [".notdef", "space"] + [f"digit{i}" for i in range(10)] + ["colon", "period", "hyphen"]
    metrics = {name: (ADVANCE, 0) for name in order}
    metrics.update({"space": (128, 0), "colon": (125, 0), "period": (125, 0), "hyphen": (180, 0)})
    fb = FontBuilder(1000, isTTF=False)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupCFF(
        postscript,
        {"FullName": f"{family} Regular", "FamilyName": family, "Weight": "Regular"},
        glyphs,
        {},
    )
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
        usWinAscent=800, usWinDescent=200,
        sxHeight=700, sCapHeight=700,
        usWeightClass=400, usWidthClass=3,
    )
    fb.setupPost(isFixedPitch=0)
    fb.save(output)


def make_specimen(font_path: Path, output: Path):
    canvas = Image.new("RGB", (2200, 1050), "#111216")
    draw = ImageDraw.Draw(canvas)
    ui_font = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 50)
    font_big = ImageFont.truetype(str(font_path), 390)
    font_medium = ImageFont.truetype(str(font_path), 270)
    draw.text((70, 35), "NARROW TIMER DIGITS", font=ui_font, fill="#BFC5CE")
    draw.line((70, 125, 2130, 125), fill="#414650", width=2)
    draw.text((65, 140), "0123456789", font=font_big, fill="white")
    draw.text((70, 610), "08:35  16:20", font=font_medium, fill="white")
    canvas.save(output)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    font_path = OUT / "NarrowTimerDigits-Regular.otf"
    build_font(font_path)
    make_specimen(font_path, OUT / "NarrowTimerDigits-Specimen.png")
    print(font_path)
