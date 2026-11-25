from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.t2CharStringPen import T2CharStringPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts" / "samsung-time-sans-centered"
OUT = ROOT / "artifacts" / "samsung-time-sans-v1-2"
MANROPE_LIGHT = Path(r"C:\Users\Administrator\Desktop\杂图\找字体\英文字体\新建文件夹\Manrope\Manrope-Light.ttf")
FAMILY = "Samsung  Time  Sans"
STYLES = ("Regular", "Medium", "Bold")


def bounds(glyph, glyphset):
    pen = BoundsPen(glyphset)
    glyph.draw(pen)
    return pen.bounds


def use_manrope_light_one(font: TTFont):
    manrope = TTFont(MANROPE_LIGHT)
    manrope_glyphs = manrope.getGlyphSet()
    font_glyphs = font.getGlyphSet()
    top = font["CFF "].cff.topDictIndex[0]
    old_bounds = bounds(font_glyphs["one"], font_glyphs)
    source_bounds = bounds(manrope_glyphs["one"], manrope_glyphs)
    sy = (old_bounds[3] - old_bounds[1]) / (source_bounds[3] - source_bounds[1])
    baseline = old_bounds[1] - source_bounds[1] * sy

    changed = []
    for target_name, source_name in (("one", "one"), ("one.LP", "one"), ("one.LT", "one.tf")):
        if target_name not in font.getGlyphOrder():
            continue
        source_glyph = manrope_glyphs[source_name]
        xmin, _, xmax, _ = bounds(source_glyph, manrope_glyphs)
        advance = font["hmtx"][target_name][0]
        black_width = (xmax - xmin) * sy
        left = (advance - black_width) / 2
        transform = (sy, 0, 0, sy, left - xmin * sy, baseline)
        pen = T2CharStringPen(advance, manrope_glyphs)
        source_glyph.draw(TransformPen(pen, transform))
        top.CharStrings[target_name] = pen.getCharString(
            private=top.Private, globalSubrs=top.GlobalSubrs
        )
        font["hmtx"].metrics[target_name] = (advance, round(left))
        changed.append(target_name)
    return changed


def set_version(font: TTFont, style: str):
    font["head"].fontRevision = 1.2
    font["CFF "].cff.topDictIndex[0].version = "1.200"
    for record in list(font["name"].names):
        if record.nameID == 3:
            value = f"{FAMILY} {style};1.200"
        elif record.nameID == 5:
            value = "Version 1.200"
        else:
            continue
        font["name"].setName(
            value, record.nameID,
            record.platformID, record.platEncID, record.langID,
        )


def build_weight(style: str):
    font = TTFont(SOURCE / f"SamsungTimeSans-{style}.otf")
    changed = use_manrope_light_one(font) if style == "Regular" else []
    set_version(font, style)
    output = OUT / f"SamsungTimeSans-{style}.otf"
    font.save(output)
    return output, changed


def specimen(paths: dict[str, Path]):
    canvas = Image.new("RGB", (2200, 1450), "#181a20")
    draw = ImageDraw.Draw(canvas)
    ui = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 44)
    title = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 58)
    draw.text((65, 30), "Samsung  Time  Sans — Regular 1 refined", font=title, fill="#E9E7F0")
    draw.text((65, 120), "Regular uses Manrope Light 1; Medium and Bold retain their prior 1", font=ui, fill="#AAB1BD")
    draw.line((65, 205, 2135, 205), fill="#414650", width=2)
    y = 245
    for style in STYLES:
        font = ImageFont.truetype(str(paths[style]), 245)
        draw.text((65, y + 75), style.upper(), font=ui, fill="#AAB1BD")
        draw.text((375, y), "10:21  16:20", font=font, fill="#D6CCE7")
        draw.line((65, y + 335, 2135, y + 335), fill="#414650", width=2)
        y += 395
    canvas.save(OUT / "SamsungTimeSans-v1-2-Specimen.png")

    comparison = Image.new("RGB", (1800, 900), "#181a20")
    draw = ImageDraw.Draw(comparison)
    before = ImageFont.truetype(str(SOURCE / "SamsungTimeSans-Regular.otf"), 570)
    after = ImageFont.truetype(str(paths["Regular"]), 570)
    draw.text((70, 35), "BEFORE — MANROPE REGULAR 1", font=ui, fill="#AAB1BD")
    draw.text((900, 35), "AFTER — MANROPE LIGHT 1", font=ui, fill="#AAB1BD")
    draw.text((60, 70), "10", font=before, fill="#D6CCE7")
    draw.text((890, 70), "10", font=after, fill="#D6CCE7")
    draw.line((860, 100, 860, 800), fill="#414650", width=2)
    comparison.save(OUT / "SamsungTimeSans-Regular-One-Comparison.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    paths = {}
    for style in STYLES:
        paths[style], changed = build_weight(style)
        print(style, paths[style], "modified:", ", ".join(changed) if changed else "none")
    specimen(paths)
