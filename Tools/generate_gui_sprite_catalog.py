#!/usr/bin/env python3
"""Build visual contact sheets for the Layer Lab GUI Pro-SuperCasual pack.

The vendor directory is read-only. Generated evidence is written under
Assets/Screenshots/UIAudit/GUIProSuperCasual.
"""

from __future__ import annotations

import csv
import math
import textwrap
from collections import defaultdict
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


PROJECT_ROOT = Path(__file__).resolve().parents[1]
SPRITE_ROOT = PROJECT_ROOT / "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites"
OUTPUT_ROOT = PROJECT_ROOT / "Assets/Screenshots/UIAudit/GUIProSuperCasual"
ASSET_PREFIX = "Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/ResourcesData/Sprites"

PAGE_SIZE = 120
PAGE_COLUMNS = 8
TILE_W = 340
TILE_H = 252
PREVIEW_BOX = (300, 154)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    name = "arialbd.ttf" if bold else "arial.ttf"
    try:
        return ImageFont.truetype(str(Path("C:/Windows/Fonts") / name), size)
    except OSError:
        return ImageFont.load_default()


FONT_TITLE = font(28, True)
FONT_NAME = font(15, True)
FONT_PATH = font(11)
FONT_INDEX = font(11, True)


def asset_path(path: Path) -> str:
    return path.relative_to(PROJECT_ROOT).as_posix()


def category(path: Path) -> str:
    parts = path.relative_to(SPRITE_ROOT).parts
    return "/".join(parts[:2]) if len(parts) > 1 else parts[0]


def checker(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], cell: int = 12) -> None:
    x0, y0, x1, y1 = box
    for y in range(y0, y1, cell):
        for x in range(x0, x1, cell):
            tone = 48 if ((x - x0) // cell + (y - y0) // cell) % 2 else 58
            draw.rectangle((x, y, min(x + cell, x1), min(y + cell, y1)), fill=(tone, tone + 4, tone + 12, 255))


def load_preview(path: Path, max_size: tuple[int, int]) -> Image.Image | None:
    try:
        with Image.open(path) as source:
            image = source.convert("RGBA")
    except Exception:
        return None
    image.thumbnail(max_size, Image.Resampling.LANCZOS)
    return image


def draw_tile(canvas: Image.Image, path: Path, index: int, x: int, y: int) -> None:
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((x + 4, y + 4, x + TILE_W - 4, y + TILE_H - 4), 14,
                           fill=(24, 32, 52, 255), outline=(75, 96, 132, 255), width=2)
    preview_box = (x + 20, y + 18, x + TILE_W - 20, y + 172)
    checker(draw, preview_box)
    preview = load_preview(path, PREVIEW_BOX)
    if preview is not None:
        px = x + (TILE_W - preview.width) // 2
        py = y + 18 + (154 - preview.height) // 2
        canvas.alpha_composite(preview, (px, py))
    else:
        draw.text((x + TILE_W // 2, y + 90), "UNITY/PSD", font=FONT_NAME,
                  anchor="mm", fill=(220, 224, 235, 255))

    draw.text((x + 16, y + 180), f"#{index:04d}  {path.stem}", font=FONT_NAME,
              fill=(255, 255, 255, 255))
    rel = asset_path(path)
    lines = textwrap.wrap(rel, width=52, break_long_words=True)[:3]
    draw.multiline_text((x + 16, y + 201), "\n".join(lines), font=FONT_PATH,
                        fill=(171, 190, 218, 255), spacing=1)


def write_category_pages(entries: list[tuple[int, Path]]) -> list[str]:
    generated: list[str] = []
    grouped: dict[str, list[tuple[int, Path]]] = defaultdict(list)
    for entry in entries:
        grouped[category(entry[1])].append(entry)

    for group_name, group_entries in sorted(grouped.items()):
        safe_name = group_name.replace("/", "_").replace(" ", "_")
        page_count = math.ceil(len(group_entries) / PAGE_SIZE)
        for page_index in range(page_count):
            page = group_entries[page_index * PAGE_SIZE:(page_index + 1) * PAGE_SIZE]
            rows = math.ceil(len(page) / PAGE_COLUMNS)
            header_h = 76
            canvas = Image.new("RGBA", (PAGE_COLUMNS * TILE_W, header_h + rows * TILE_H), (14, 21, 37, 255))
            draw = ImageDraw.Draw(canvas)
            draw.text((24, 18), f"GUI Pro-SuperCasual — {group_name}", font=FONT_TITLE, fill=(255, 255, 255, 255))
            draw.text((24, 51), f"Page {page_index + 1}/{page_count} · {len(group_entries)} sprites · name + Unity asset path",
                      font=FONT_INDEX, fill=(113, 205, 255, 255))
            for tile_index, (catalog_index, path) in enumerate(page):
                col = tile_index % PAGE_COLUMNS
                row = tile_index // PAGE_COLUMNS
                draw_tile(canvas, path, catalog_index, col * TILE_W, header_h + row * TILE_H)
            filename = f"Catalog_{safe_name}_{page_index + 1:02d}.png"
            canvas.convert("RGB").save(OUTPUT_ROOT / filename, optimize=True)
            generated.append(filename)
    return generated


def write_master(entries: list[tuple[int, Path]]) -> str:
    columns = 49
    tile_w, tile_h = 150, 150
    rows = math.ceil(len(entries) / columns)
    source = Image.new("RGBA", (columns * tile_w, rows * tile_h + 60), (14, 21, 37, 255))
    draw = ImageDraw.Draw(source)
    draw.text((18, 13), f"GUI Pro-SuperCasual — {len(entries)} sprites — overview (see CSV/category sheets for full paths)",
              font=FONT_TITLE, fill=(255, 255, 255))
    mini_name = font(10, True)
    for slot, (catalog_index, path) in enumerate(entries):
        x = (slot % columns) * tile_w
        y = 60 + (slot // columns) * tile_h
        draw.rectangle((x + 2, y + 2, x + tile_w - 2, y + tile_h - 2), fill=(27, 36, 58), outline=(60, 76, 104))
        preview = load_preview(path, (126, 104))
        if preview is not None:
            source.alpha_composite(preview, (x + (tile_w - preview.width) // 2, y + 8 + (104 - preview.height) // 2))
        label = f"{catalog_index:04d} {path.stem}"
        draw.text((x + 6, y + 118), textwrap.shorten(label, width=24, placeholder="…"), font=mini_name, fill=(235, 240, 248))

    target_w = 4096
    target_h = round(source.height * target_w / source.width)
    master = source.convert("RGB").resize((target_w, target_h), Image.Resampling.LANCZOS)
    filename = "GUIProSuperCasual_AllSprites_Overview.png"
    master.save(OUTPUT_ROOT / filename, optimize=True)
    return filename


def write_manifest(entries: list[tuple[int, Path]], page_files: list[str], master_file: str) -> None:
    with (OUTPUT_ROOT / "GUIProSuperCasual_SpriteManifest.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.writer(stream)
        writer.writerow(("catalog_index", "sprite_name", "category", "asset_path", "source_type"))
        for index, path in entries:
            writer.writerow((index, path.stem, category(path), asset_path(path), path.suffix.lower().lstrip(".")))

    counts: dict[str, int] = defaultdict(int)
    for _, path in entries:
        counts[category(path)] += 1
    lines = [
        "# GUI Pro-SuperCasual sprite catalog",
        "",
        f"- Source: `{ASSET_PREFIX}`",
        f"- Total Unity sprite source assets: **{len(entries)}**",
        f"- Master overview: `{master_file}`",
        "- Exact sprite names and Unity asset paths: `GUIProSuperCasual_SpriteManifest.csv`",
        f"- Readable category sheets: **{len(page_files)}** PNG files",
        "- Vendor files are read-only; this folder contains generated audit evidence only.",
        "",
        "## Category counts",
        "",
        "| Category | Sprites |",
        "|---|---:|",
    ]
    lines.extend(f"| `{name}` | {count} |" for name, count in sorted(counts.items()))
    lines.extend(("", "## Category sheets", ""))
    lines.extend(f"- `{name}`" for name in page_files)
    (OUTPUT_ROOT / "README.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    paths = sorted(
        (path for path in SPRITE_ROOT.rglob("*") if path.is_file() and path.suffix.lower() in {".png", ".psd"}),
        key=lambda path: asset_path(path).casefold(),
    )
    entries = list(enumerate(paths, start=1))
    pages = write_category_pages(entries)
    master = write_master(entries)
    write_manifest(entries, pages, master)
    print(f"Generated {master}, {len(pages)} category sheets, and manifest for {len(entries)} sprites in {OUTPUT_ROOT}")


if __name__ == "__main__":
    main()
