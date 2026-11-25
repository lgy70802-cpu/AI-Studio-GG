from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.t2CharStringPen import T2CharStringPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "samsung-time-sans"
SAMSUNG = Path(r"C:\Users\Administrator\AppData\Local\Microsoft\Windows\Fonts")
MANROPE = Path(r"C:\Users\Administrator\Desktop\杂图\找字体\英文字体\新建文件夹\Manrope")
FAMILY = "Samsung  Time  Sans"
POSTSCRIPT_FAMILY = "SamsungTimeSans"

SOURCES = {
    "Regular": (SAMSUNG / "Samsung-Sharp-Sans-Regular.ttf", MANROPE / "Manrope-Regular.ttf", 400),
    "Medium": (SAMSUNG / "Samsung-Sharp-Sans-Medium-2.ttf", MANROPE / "Manrope-Medium.ttf", 500),
    "Bold": (SAMSUNG / "Samsung-Sharp-Sans-Bold-2.ttf", MANROPE / "Manrope-Bold.ttf", 700),
}


def cubic_glyph(glyph, width: int, glyphset, transform=None):
    # T2CharStringPen converts quadratic segments to exact cubic equivalents
    # and expands TrueType composite glyphs through the supplied glyphset.
    pen = T2CharStringPen(width, glyphset)
    target = TransformPen(pen, transform) if transform is not None else pen
    glyph.draw(target)
    return pen.getCharString()


def glyph_bounds(glyph, glyphset):
    pen = BoundsPen(glyphset)
    glyph.draw(pen)
    return pen.bounds


def replace_one(
    glyphs: dict,
    metrics: dict,
    samsung_font: TTFont,
    manrope_font: TTFont,
):
    samsung_glyphs = samsung_font.getGlyphSet()
    manrope_glyphs = manrope_font.getGlyphSet()
    one = samsung_font.getBestCmap()[ord("1")]
    old_bounds = glyph_bounds(samsung_glyphs[one], samsung_glyphs)
    manrope_bounds = glyph_bounds(manrope_glyphs["one"], manrope_glyphs)
    sy = (old_bounds[3] - old_bounds[1]) / (manrope_bounds[3] - manrope_bounds[1])
    base_y = old_bounds[1] - manrope_bounds[1] * sy

    candidates = [(one, "one")]
    if "one.LP" in glyphs:
        candidates.append(("one.LP", "one"))
    if "one.LT" in glyphs:
        candidates.append(("one.LT", "one.tf"))

    for target_name, source_name in candidates:
        source = manrope_glyphs[source_name]
        source_bounds = glyph_bounds(source, manrope_glyphs)
        advance = metrics[target_name][0]
        black_width = (source_bounds[2] - source_bounds[0]) * sy
        left = (advance - black_width) / 2
        dx = left - source_bounds[0] * sy
        transform = (sy, 0, 0, sy, dx, base_y)
        glyphs[target_name] = cubic_glyph(source, advance, manrope_glyphs, transform)
        metrics[target_name] = (advance, round(left))
    return [name for name, _ in candidates]


def set_family_names(font: TTFont, style: str):
    postscript = f"{POSTSCRIPT_FAMILY}-{style}"
    full_name = f"{FAMILY} {style}"
    values = {
        1: FAMILY,
        2: style,
        3: f"{full_name};1.000",
        4: full_name,
        5: "Version 1.000",
        6: postscript,
        16: FAMILY,
        17: style,
    }
    names = font["name"]
    for record in list(names.names):
        if record.nameID in values:
            names.setName(
                values[record.nameID], record.nameID,
                record.platformID, record.platEncID, record.langID,
            )
    for name_id, value in values.items():
        names.setName(value, name_id, 3, 1, 0x409)
        names.setName(value, name_id, 1, 0, 0)
    return postscript, full_name


def build_weight(style: str):
    samsung_path, manrope_path, weight = SOURCES[style]
    font = TTFont(samsung_path)
    manrope = TTFont(manrope_path)
    glyph_order = font.getGlyphOrder()
    glyphset = font.getGlyphSet()
    metrics = dict(font["hmtx"].metrics)
    upm = font["head"].unitsPerEm

    glyphs = {
        name: cubic_glyph(glyphset[name], metrics[name][0], glyphset)
        for name in glyph_order
    }
    replaced = replace_one(glyphs, metrics, font, manrope)

    postscript, full_name = set_family_names(font, style)
    info = {
        "FullName": full_name,
        "FamilyName": FAMILY,
        "Weight": style,
    }
    cff_builder = FontBuilder(upm, isTTF=False)
    cff_builder.setupGlyphOrder(glyph_order)
    cff_builder.setupCFF(postscript, info, glyphs, {})
    font["CFF "] = cff_builder.font["CFF "]
    font.sfntVersion = "OTTO"

    for table in ("glyf", "loca", "fpgm", "prep", "cvt ", "DSIG"):
        if table in font:
            del font[table]
    font["maxp"].tableVersion = 0x00005000
    font["post"].formatType = 3.0
    font["head"].indexToLocFormat = 0
    font["head"].glyphDataFormat = 0
    font["head"].macStyle = (font["head"].macStyle & ~1) | (1 if style == "Bold" else 0)
    os2 = font["OS/2"]
    os2.usWeightClass = weight
    os2.fsSelection = (os2.fsSelection & ~0x61) | (0x20 if style == "Bold" else 0x40 if style == "Regular" else 0)
    font["hmtx"].metrics.update(metrics)

    output = OUT / f"{postscript}.otf"
    font.save(output)
    return output, replaced


def make_specimen(paths: dict[str, Path]):
    canvas = Image.new("RGB", (2400, 1600), "#111318")
    draw = ImageDraw.Draw(canvas)
    ui = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 46)
    title = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 58)
    draw.text((80, 42), "Samsung  Time  Sans", font=title, fill="#F5F5F5")
    draw.text((80, 122), "Samsung Sharp Sans + Manrope 1", font=ui, fill="#ACB5C3")
    draw.line((80, 210, 2320, 210), fill="#3A3F49", width=2)
    y = 250
    for style in ("Regular", "Medium", "Bold"):
        font = ImageFont.truetype(str(paths[style]), 190)
        draw.text((80, y), style.upper(), font=ui, fill="#ACB5C3")
        draw.text((350, y - 40), "0123456789  10:21", font=font, fill="#F5F5F5")
        draw.text((350, y + 190), "Samsung Time Sans  ABC abc !?@#$%", font=ImageFont.truetype(str(paths[style]), 72), fill="#D3D7DE")
        draw.line((80, y + 300, 2320, y + 300), fill="#3A3F49", width=2)
        y += 430
    canvas.save(OUT / "SamsungTimeSans-Specimen.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    outputs = {}
    for name in ("Regular", "Medium", "Bold"):
        outputs[name], modified = build_weight(name)
        print(name, outputs[name], "modified:", ", ".join(modified))
    make_specimen(outputs)
