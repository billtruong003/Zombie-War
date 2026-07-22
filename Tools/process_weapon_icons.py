"""Turn UIThumbnailGenerator weapon renders into real icons.

The generator outputs 256x256 PNGs of the gun on the solid dark-navy studio
background. This script, for every PNG in the folder:

  1. removes the background (flood fill from the borders, so navy pixels INSIDE
     the gun are never eaten);
  2. draws an outline around the gun silhouette;
  3. recrops so the gun fills the frame instead of floating tiny in the middle.

In-place by default so Unity .meta GUIDs (and every sprite reference) survive.
Re-running is safe: already-transparent images are skipped unless --force.
If the generator is ever re-run, its output overwrites these files - just run
this script again afterwards.

Usage:
    python Tools/process_weapon_icons.py                     # default folder, in place
    python Tools/process_weapon_icons.py --dir <folder> --outline-width 5
    python Tools/process_weapon_icons.py --outline-color "#FFCF66" --force
"""

from __future__ import annotations

import argparse
import sys
from collections import deque
from pathlib import Path

from PIL import Image

DEFAULT_DIR = Path(__file__).resolve().parent.parent / "Assets/_Project/UI/Icons/Generated/Weapons"
# The generator's studio bg is one exact colour (navy 27,33,48), so the tolerance only needs to
# absorb edge anti-aliasing. Dark gun metal is neutral grey ~12-17 away on the blue channel -
# anything much above 12 starts eating black guns whole.
BG_TOLERANCE = 12
PAD_FRACTION = 0.10        # breathing room around the gun after recrop


def parse_color(text: str) -> tuple[int, int, int, int]:
    text = text.lstrip("#")
    if len(text) == 6:
        text += "FF"
    if len(text) != 8:
        raise argparse.ArgumentTypeError(f"expected RRGGBB or RRGGBBAA, got '{text}'")
    return tuple(int(text[i:i + 2], 16) for i in (0, 2, 4, 6))  # type: ignore[return-value]


def is_background(pixel: tuple[int, ...], bg: tuple[int, ...]) -> bool:
    return all(abs(pixel[i] - bg[i]) <= BG_TOLERANCE for i in range(3))


def flood_background_mask(img: Image.Image) -> list[bool] | None:
    """True per pixel reachable from the border through background-coloured pixels.
    Returns None when the border is not a uniform colour (nothing to key out)."""
    w, h = img.size
    data = list(img.getdata())

    corners = [data[0], data[w - 1], data[(h - 1) * w], data[h * w - 1]]
    bg = tuple(sorted(c[i] for c in corners)[1] for i in range(3))  # per-channel median-ish
    if not all(is_background(c, bg) for c in corners):
        return None

    mask = [False] * (w * h)
    queue: deque[int] = deque()
    for x in range(w):
        queue.append(x)
        queue.append((h - 1) * w + x)
    for y in range(h):
        queue.append(y * w)
        queue.append(y * w + w - 1)

    while queue:
        idx = queue.popleft()
        if mask[idx] or not is_background(data[idx], bg):
            continue
        mask[idx] = True
        x, y = idx % w, idx // w
        if x > 0: queue.append(idx - 1)
        if x < w - 1: queue.append(idx + 1)
        if y > 0: queue.append(idx - w)
        if y < h - 1: queue.append(idx + w)
    return mask


def dilate(mask: list[bool], w: int, h: int, rounds: int) -> list[bool]:
    current = mask
    for _ in range(rounds):
        grown = current[:]
        for idx, on in enumerate(current):
            if not on:
                continue
            x, y = idx % w, idx // w
            if x > 0: grown[idx - 1] = True
            if x < w - 1: grown[idx + 1] = True
            if y > 0: grown[idx - w] = True
            if y < h - 1: grown[idx + w] = True
            if x > 0 and y > 0: grown[idx - w - 1] = True
            if x < w - 1 and y > 0: grown[idx - w + 1] = True
            if x > 0 and y < h - 1: grown[idx + w - 1] = True
            if x < w - 1 and y < h - 1: grown[idx + w + 1] = True
        current = grown
    return current


def process(path: Path, outline_color: tuple[int, int, int, int], outline_width: int,
            force: bool) -> str:
    img = Image.open(path).convert("RGBA")
    w, h = img.size

    if not force and img.getpixel((0, 0))[3] == 0:
        return "skip (already transparent)"

    bg_mask = flood_background_mask(img)
    if bg_mask is None:
        return "skip (border is not a solid colour)"

    data = list(img.getdata())
    object_mask = [not bg for bg in bg_mask]
    if not any(object_mask):
        return "skip (nothing left after keying)"

    keyed = [(r, g, b, 0) if bg_mask[i] else (r, g, b, a)
             for i, (r, g, b, a) in enumerate(data)]

    ring = dilate(object_mask, w, h, outline_width)
    for i, on in enumerate(ring):
        if on and not object_mask[i]:
            keyed[i] = outline_color

    out = Image.new("RGBA", (w, h))
    out.putdata(keyed)

    bbox = out.getbbox()
    if bbox:
        content = out.crop(bbox)
        side = max(content.size)
        pad = int(side * PAD_FRACTION)
        canvas = Image.new("RGBA", (side + 2 * pad,) * 2, (0, 0, 0, 0))
        canvas.paste(content, ((canvas.width - content.width) // 2,
                               (canvas.height - content.height) // 2))
        out = canvas.resize((w, h), Image.LANCZOS)

    out.save(path)
    return "ok"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--dir", type=Path, default=DEFAULT_DIR)
    parser.add_argument("--outline-color", type=parse_color, default=parse_color("FFFFFF"),
                        help="outline colour, RRGGBB or RRGGBBAA (default white)")
    parser.add_argument("--outline-width", type=int, default=4, help="outline thickness in px")
    parser.add_argument("--force", action="store_true",
                        help="re-process files that already have transparency")
    args = parser.parse_args()

    files = sorted(args.dir.glob("*.png"))
    if not files:
        print(f"No PNGs found in {args.dir}", file=sys.stderr)
        return 1

    counts: dict[str, int] = {}
    for path in files:
        result = process(path, args.outline_color, args.outline_width, args.force)
        counts[result] = counts.get(result, 0) + 1
        print(f"  {path.name}: {result}")

    print(f"\n{len(files)} files - " + ", ".join(f"{v} {k}" for k, v in counts.items()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
