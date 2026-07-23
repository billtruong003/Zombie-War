#!/usr/bin/env python3
"""Add a smooth outline (stroke) around transparent-background icon PNGs.

Method: dilate the alpha channel with MaxFilter, soften with a light Gaussian
blur, use that as the alpha of a solid-color layer, composite the original on
top (per https://stackoverflow.com/q/61405583 — Pillow stroke technique).

Run AFTER regenerating icons in Unity, then let Unity reimport:
    python Tools/outline_icons.py Assets/_Project/UI/Icons/Generated/Weapons
    python Tools/outline_icons.py <folder> --stroke 8 --color "#FFFFFF"

Idempotent-unsafe by design: running twice thickens the outline. Always
regenerate icons in Unity first, then outline exactly once.
"""

import argparse
import sys
from pathlib import Path

try:
    from PIL import Image, ImageFilter
except ImportError:
    sys.exit("Pillow is required: pip install pillow")


def parse_color(value: str):
    value = value.lstrip("#")
    if len(value) == 6:
        value += "FF"
    if len(value) != 8:
        raise argparse.ArgumentTypeError("color must be RRGGBB or RRGGBBAA hex")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4, 6))


def outline(path: Path, stroke: int, color, blur: float) -> bool:
    img = Image.open(path).convert("RGBA")
    alpha = img.getchannel("A")
    if alpha.getextrema() == (255, 255):
        print(f"  skip (no transparency): {path.name}")
        return False

    # MaxFilter kernel must be odd; dilation radius = stroke px.
    dilated = alpha.filter(ImageFilter.MaxFilter(stroke * 2 + 1))
    if blur > 0:
        dilated = dilated.filter(ImageFilter.GaussianBlur(blur))

    stroke_layer = Image.new("RGBA", img.size, color)
    stroke_layer.putalpha(dilated)
    Image.alpha_composite(stroke_layer, img).save(path)
    return True


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path, help="folder containing .png icons")
    parser.add_argument("--stroke", type=int, default=7, help="stroke width in px (default 7 for 512px icons)")
    parser.add_argument("--color", type=parse_color, default=parse_color("FFFFFF"), help="stroke color hex")
    parser.add_argument("--blur", type=float, default=1.0, help="Gaussian blur applied to the stroke mask")
    args = parser.parse_args()

    files = sorted(args.folder.glob("*.png"))
    if not files:
        sys.exit(f"No .png files in {args.folder}")

    done = sum(outline(f, args.stroke, args.color, args.blur) for f in files)
    print(f"Outlined {done}/{len(files)} icons in {args.folder}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
