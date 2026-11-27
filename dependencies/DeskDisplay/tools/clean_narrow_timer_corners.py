from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.t2CharStringPen import T2CharStringPen


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts" / "narrow-timer-extended-one" / "NarrowTimerDigitsExtendedOne-Regular.otf"
OUT = ROOT / "artifacts" / "narrow-timer-clean"
FONT = OUT / "NarrowTimerDigitsClean-Regular.otf"
SPECIMEN = OUT / "NarrowTimerDigitsClean-Comparison.png"


def replace_span(operations, start, stop, expected, replacement):
    endpoints = [args[-1] for op, args in operations[start:stop]]
    if endpoints != expected:
        raise ValueError(f"Unexpected source outline at {start}:{stop}: {endpoints}")
    operations[start:stop] = [replacement]


def revised_operations(name: str, operations):
    operations = list(operations)
    if name == "digit2":
        # Two short diagonal cuts on the right end of the baseline become
        # tangent cubic roundovers. Work backwards to keep indices stable.
        replace_span(
            operations, 38, 40, [(225, 2), (222, 0)],
            ("curveTo", ((228, 2), (225, 0), (222, 0))),
        )
        replace_span(
            operations, 35, 37, [(226, 55), (228, 53)],
            ("curveTo", ((226, 58), (228, 56), (228, 53))),
        )
    elif name == "digit3":
        # Smooth the right shoulder as it turns inward toward the center.
        replace_span(
            operations, 40, 43, [(228, 434), (225, 425), (218, 405)],
            ("curveTo", ((228, 465), (226, 421), (218, 405))),
        )
        # The small left-facing middle arm has two conspicuous beveled ends.
        replace_span(
            operations, 23, 25, [(89, 391), (91, 393)],
            ("curveTo", ((86, 391), (88, 393), (91, 393))),
        )
        replace_span(
            operations, 20, 22, [(89, 335), (86, 338)],
            ("curveTo", ((88, 333), (86, 335), (86, 338))),
        )
    return operations


def build_font():
    font = TTFont(SOURCE)
    top = font["CFF "].cff.topDictIndex[0]
    glyphset = font.getGlyphSet()
    for name in ("digit2", "digit3"):
        recording = RecordingPen()
        glyphset[name].draw(recording)
        operations = revised_operations(name, recording.value)
        pen = T2CharStringPen(font["hmtx"][name][0], None)
        for operation, args in operations:
            getattr(pen, operation)(*args)
        top.CharStrings[name] = pen.getCharString(
            private=top.Private, globalSubrs=top.GlobalSubrs
        )

    family = "Narrow Timer Digits Clean"
    ps_name = "NarrowTimerDigitsClean-Regular"
    font["CFF "].cff.fontNames = [ps_name]
    top.FamilyName = family
    top.FullName = f"{family} Regular"
    names = {
        1: family,
        3: f"{family} Regular 1.000",
        4: f"{family} Regular",
        5: "Version 1.000",
        6: ps_name,
    }
    for record in font["name"].names:
        if record.nameID in names:
            font["name"].setName(
                names[record.nameID], record.nameID,
                record.platformID, record.platEncID, record.langID,
            )
    font.save(FONT)


def make_specimen():
    canvas = Image.new("RGB", (2200, 1150), "#0d0e11")
    draw = ImageDraw.Draw(canvas)
    label = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 42)
    original = ImageFont.truetype(str(SOURCE), 760)
    revised = ImageFont.truetype(str(FONT), 760)
    draw.text((70, 30), "BEFORE", font=label, fill="#aeb4bd")
    draw.text((1130, 30), "CLEANED CORNERS", font=label, fill="#aeb4bd")
    draw.line((1080, 85, 1080, 1060), fill="#373b43", width=2)
    draw.text((45, 70), "23", font=original, fill="#c9bfdc")
    draw.text((1105, 70), "23", font=revised, fill="#c9bfdc")
    canvas.save(SPECIMEN)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    build_font()
    make_specimen()
    print(FONT)
