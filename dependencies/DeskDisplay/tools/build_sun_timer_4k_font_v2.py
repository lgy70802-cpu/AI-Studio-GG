"""Build the second, reference-calibrated Sun Timer 4K Smooth release."""

from __future__ import annotations

from pathlib import Path

import cv2
import numpy as np
import potrace
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.t2CharStringPen import T2CharStringPen

from build_sun_timer_4k_font import (
    ADVANCE,
    CAP_HEIGHT,
    ROOT,
    UPM,
    circle,
    empty_glyph,
    rectangle_glyph,
    specimen,
    detail_specimen,
)

SOURCE = Path(r"C:\Users\Administrator\Downloads\jimeng-2026-09-28-2980-把这张图变成超清4k图.png")
OUT = ROOT / "artifacts" / "sun-timer-4k-font-v2"

# Selected clean instances in the 3776 x 4864 reference image. The background,
# color halo and watermark are not part of the font.
DIGIT_BOXES = {
    "0": (397, 69, 644, 840),
    "1": (489, 2458, 370, 819),
    "2": (2720, 1245, 591, 833),
    "3": (2088, 65, 619, 846),
    "4": (1997, 3631, 666, 815),
    "5": (2784, 78, 609, 832),
    "6": (969, 2446, 638, 843),
    "7": (1152, 1258, 575, 817),
    "8": (1109, 64, 638, 847),
    "9": (2713, 3618, 636, 841),
}


def traced_digit(mask: np.ndarray, box: tuple[int, int, int, int]):
    x, y, w, h = box
    pad = 16
    region = mask[y - pad : y + h + pad, x - pad : x + w + pad]
    factor = 520 / h
    work = cv2.resize(region, None, fx=factor, fy=factor, interpolation=cv2.INTER_AREA)
    _, work = cv2.threshold(work, 127, 255, cv2.THRESH_BINARY)
    # The AI-upscaled reference has small scallops along otherwise smooth
    # strokes. Close pinholes and remove isolated projections before tracing.
    work = cv2.morphologyEx(work, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
    work = cv2.morphologyEx(work, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3)))
    work = cv2.GaussianBlur(work, (0, 0), 2.0)
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
    x_offset = (ADVANCE - (right - left) * scale) / 2

    def point(p):
        return (round(x_offset + (p.x - left) * scale, 2), round((bottom - p.y) * scale, 2))

    pen = T2CharStringPen(ADVANCE, None)
    paths = potrace.Bitmap(clean == 0).trace(
        turdsize=30, alphamax=0.85, opticurve=True, opttolerance=0.65
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


def build_font(font_path: Path) -> None:
    source = cv2.imdecode(np.fromfile(SOURCE, dtype=np.uint8), cv2.IMREAD_COLOR)
    if source is None or source.shape[:2] != (4864, 3776):
        raise ValueError("The expected 3776 x 4864 reference PNG could not be read")
    hsv = cv2.cvtColor(source, cv2.COLOR_BGR2HSV)
    foreground = np.where(
        (hsv[:, :, 1] < 175) & (hsv[:, :, 2] > 240), 255, 0
    ).astype(np.uint8)

    glyphs = {".notdef": rectangle_glyph(ADVANCE, 100, 0, 550, CAP_HEIGHT)}
    cmap: dict[int, str] = {}
    for digit, box in DIGIT_BOXES.items():
        name = f"digit{digit}"
        glyphs[name] = traced_digit(foreground, box)
        cmap[ord(digit)] = name

    # The reference's circular dots are 0.20 cap-heights across. These are
    # true cubic Bezier circles, not auto-traced pixels, so both stay smooth.
    colon = T2CharStringPen(300, None)
    circle(colon, 150, 525, 70)
    circle(colon, 150, 185, 70)
    glyphs["colon"] = colon.getCharString()
    period = T2CharStringPen(300, None)
    circle(period, 150, 70, 70)
    glyphs["period"] = period.getCharString()
    glyphs["space"] = empty_glyph(325)
    glyphs["hyphen"] = rectangle_glyph(400, 60, 315, 340, 385)
    cmap.update({32: "space", 45: "hyphen", 46: "period", 58: "colon", 0xFF1A: "colon"})

    order = [".notdef", "space"] + [f"digit{i}" for i in range(10)] + ["colon", "period", "hyphen"]
    metrics = {name: (ADVANCE, 0) for name in order}
    metrics.update({"space": (325, 0), "colon": (300, 0), "period": (300, 0), "hyphen": (400, 0)})
    family = "Sun Timer 4K Smooth"
    postscript = "SunTimer4KSmooth-Regular"
    fb = FontBuilder(UPM, isTTF=False)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupCFF(
        postscript,
        {"FullName": f"{family} Regular", "FamilyName": family, "Weight": "Regular"},
        glyphs, {},
    )
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=800, descent=-200)
    fb.setupNameTable({
        "familyName": family,
        "styleName": "Regular",
        "uniqueFontIdentifier": f"{family} Regular 2.000",
        "fullName": f"{family} Regular",
        "psName": postscript,
        "version": "Version 2.000",
    })
    fb.setupOS2(
        sTypoAscender=800, sTypoDescender=-200, sTypoLineGap=0,
        usWinAscent=800, usWinDescent=200, sxHeight=700, sCapHeight=700,
        usWeightClass=400, usWidthClass=4,
    )
    fb.setupPost(isFixedPitch=0)
    fb.save(font_path)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    font_path = OUT / "SunTimer4KSmooth-Regular.otf"
    build_font(font_path)
    specimen(font_path, OUT / "SunTimer4KSmooth-Specimen.png")
    detail_specimen(font_path, OUT / "SunTimer4KSmooth-Detail.png")
    print(font_path)
