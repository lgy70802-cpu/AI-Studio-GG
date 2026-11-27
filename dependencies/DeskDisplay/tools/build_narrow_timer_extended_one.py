from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

import build_narrow_timer_font as base


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "narrow-timer-extended-one"
REFERENCE = OUT / "one-reference.png"
FONT = OUT / "NarrowTimerDigitsExtendedOne-Regular.otf"
OLD_FONT = ROOT / "artifacts" / "narrow-timer-font" / "NarrowTimerDigits-Regular.otf"


def specimen(output: Path):
    canvas = Image.new("RGB", (1800, 1050), "#101115")
    draw = ImageDraw.Draw(canvas)
    label = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 40)
    old_big = ImageFont.truetype(str(OLD_FONT), 660)
    new_big = ImageFont.truetype(str(FONT), 660)
    new_medium = ImageFont.truetype(str(FONT), 260)
    draw.text((80, 35), "ORIGINAL 1", font=label, fill="#aeb5bf")
    draw.text((500, 35), "EXTENDED LEFT SHOULDER", font=label, fill="#aeb5bf")
    draw.text((90, 70), "1", font=old_big, fill="white")
    draw.text((550, 70), "1", font=new_big, fill="white")
    draw.line((420, 90, 420, 670), fill="#353941", width=2)
    draw.line((80, 700, 1720, 700), fill="#353941", width=2)
    draw.text((80, 710), "0101  1234  16:20", font=new_medium, fill="white")
    canvas.save(output)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    base.build_font(
        FONT,
        one_reference=REFERENCE,
        family="Narrow Timer Digits Extended One",
        postscript="NarrowTimerDigitsExtendedOne-Regular",
    )
    specimen(OUT / "NarrowTimerDigitsExtendedOne-Specimen.png")
    print(FONT)
