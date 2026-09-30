"""Turns sandbox clip folders (SkillSandbox.RecordClips) into GIFs.

Usage: python Tools/make_gifs.py <clips root> [frame_ms]
Every sub-folder of JPG frames becomes <clips root>/<name>.gif, plus an overview contact sheet.
"""
import glob
import os
import sys

from PIL import Image, ImageDraw


def main():
    root = sys.argv[1]
    frame_ms = int(sys.argv[2]) if len(sys.argv) > 2 else 80
    firsts = []
    for folder in sorted(d for d in glob.glob(os.path.join(root, "*")) if os.path.isdir(d)):
        frames = [Image.open(f).convert("RGB") for f in sorted(glob.glob(os.path.join(folder, "*.jpg")))]
        if not frames:
            continue
        name = os.path.basename(folder)
        pal = [f.quantize(colors=128, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE) for f in frames]
        pal[0].save(os.path.join(root, name + ".gif"), save_all=True, append_images=pal[1:],
                    duration=frame_ms, loop=0, optimize=True)
        firsts.append((name, frames[len(frames) // 3]))
    if firsts:
        w = 240
        cols = 6
        rows = (len(firsts) + cols - 1) // cols
        sheet = Image.new("RGB", (w * cols, (w + 18) * rows), "white")
        draw = ImageDraw.Draw(sheet)
        for i, (name, im) in enumerate(firsts):
            x, y = (i % cols) * w, (i // cols) * (w + 18)
            draw.text((x + 4, y + 3), name, fill="black")
            sheet.paste(im.resize((w, w)), (x, y + 18))
        sheet.save(os.path.join(root, "overview.jpg"), quality=85)
    print(len(firsts), "gifs in", root)


if __name__ == "__main__":
    main()
