from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.t2CharStringPen import T2CharStringPen
from fontTools.ttLib import TTFont


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts" / "samsung-time-sans"
OUT = ROOT / "artifacts" / "samsung-time-sans-centered"
STYLES = ("Regular", "Medium", "Bold")
FAMILY = "Samsung  Time  Sans"

# Measured from the uploaded 669 x 219 reference: the dot centers are at
# y~547 and y~185 in Samsung's 1000-unit coordinate system.
DOT_CENTERS = (185.0, 547.0)


def contours_of(font: TTFont, name: str):
    recording = RecordingPen()
    font.getGlyphSet()[name].draw(recording)
    contours = []
    current = []
    for operation in recording.value:
        current.append(operation)
        if operation[0] in ("closePath", "endPath"):
            contours.append(current)
            current = []
    if current:
        contours.append(current)
    if len(contours) != 2:
        raise ValueError(f"Expected two dots in {name}, found {len(contours)}")
    return contours


def contour_bounds(contour):
    pen = BoundsPen(None)
    for operation, args in contour:
        getattr(pen, operation)(*args)
    return pen.bounds


def centered_colon(font: TTFont, name: str):
    advance = font["hmtx"][name][0]
    contours = sorted(contours_of(font, name), key=lambda c: contour_bounds(c)[1])
    pen = T2CharStringPen(advance, None)
    for target_center, contour in zip(DOT_CENTERS, contours):
        x0, y0, x1, y1 = contour_bounds(contour)
        shift_y = target_center - (y0 + y1) / 2
        for operation, args in contour:
            moved = tuple(tuple((point[0], point[1] + shift_y)) for point in args)
            getattr(pen, operation)(*moved)
    top = font["CFF "].cff.topDictIndex[0]
    return pen.getCharString(private=top.Private, globalSubrs=top.GlobalSubrs)


def build_weight(style: str):
    font = TTFont(SOURCE / f"SamsungTimeSans-{style}.otf")
    top = font["CFF "].cff.topDictIndex[0]
    changed = []
    for name in ("colon", "colon.T"):
        if name in font.getGlyphOrder():
            top.CharStrings[name] = centered_colon(font, name)
            changed.append(name)

    # Both characters deliberately use the narrow time-display spacing.
    for subtable in font["cmap"].tables:
        if subtable.isUnicode():
            subtable.cmap[0xFF1A] = "colon"

    font["head"].fontRevision = 1.1
    top.version = "1.100"
    for record in list(font["name"].names):
        if record.nameID == 3:
            value = f"{FAMILY} {style};1.100"
        elif record.nameID == 5:
            value = "Version 1.100"
        else:
            continue
        font["name"].setName(
            value, record.nameID,
            record.platformID, record.platEncID, record.langID,
        )
    output = OUT / f"SamsungTimeSans-{style}.otf"
    font.save(output)
    return output, changed


def make_specimen(paths: dict[str, Path]):
    canvas = Image.new("RGB", (2400, 1420), "#1a1c22")
    draw = ImageDraw.Draw(canvas)
    ui = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 46)
    title = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 58)
    draw.text((70, 35), "Samsung  Time  Sans — Centered Colon", font=title, fill="#fff35a")
    draw.text((70, 125), "ASCII :    and    U+FF1A", font=ui, fill="#b7bdc7")
    draw.line((70, 205, 2330, 205), fill="#414650", width=2)
    y = 260
    for style in STYLES:
        font = ImageFont.truetype(str(paths[style]), 230)
        draw.text((70, y), style.upper(), font=ui, fill="#b7bdc7")
        draw.text((350, y - 65), "16:20    16：20", font=font, fill="#fff35a")
        draw.line((70, y + 280, 2330, y + 280), fill="#414650", width=2)
        y += 390
    canvas.save(OUT / "SamsungTimeSans-CenteredColon-Specimen.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    outputs = {}
    for style in STYLES:
        outputs[style], modified = build_weight(style)
        print(style, outputs[style], "modified:", ", ".join(modified))
    make_specimen(outputs)
