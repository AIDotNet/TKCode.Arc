#!/usr/bin/env python3
"""Builds every Arc Gallery icon from the master logo.

    python3 tools/branding/build_icons.py [<source.png>]

With a source image (an icon tile on a flat pure magenta #FF00FF background, as the image generator returns it) the
magenta is keyed out and tools/branding/logo-master.png is rewritten. Without one the existing master is reused.
Writes:
  gallery/TKCode.Arc.Gallery/Assets/logo.png                     top bar brand mark and window icon (256px)
  gallery/TKCode.Arc.Gallery.Desktop/Assets/AppIcon.ico          Windows executable icon
  gallery/TKCode.Arc.Gallery.Desktop/Assets/AppIcon.icns         macOS app bundle icon, with a soft shadow (tools/macos/bundle.sh)
  gallery/TKCode.Arc.Gallery.Browser/wwwroot/favicon.png         browser tab icon (64px)
  gallery/TKCode.Arc.Gallery.Browser/wwwroot/apple-touch-icon.png  home screen icon (180px)
Needs Pillow and numpy.
"""
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

REPO = Path(__file__).resolve().parents[2]
MASTER = REPO / "tools/branding/logo-master.png"
SIZE = 1024


def cut_background(source: Path) -> Image.Image:
    """Keys out the magenta around the icon tile, keeping the anti-aliased edge, and crops to the tile."""
    rgb = np.asarray(Image.open(source).convert("RGB"), dtype=np.float32)
    red, green, blue = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    height, width = green.shape

    # How magenta a pixel is: 1 for the flat background, below 0 for the warm white tile and the dark mark.
    magenta = (np.minimum(red, blue) - green) / 255
    # The outside is the magenta connected to the image border. A pixel inside the tile can never be reached.
    keyed = magenta > 0.7
    outside = np.zeros_like(keyed)
    queue = deque()
    for x in range(width):
        queue.extend([(0, x), (height - 1, x)])
    for y in range(height):
        queue.extend([(y, 0), (y, width - 1)])
    while queue:
        y, x = queue.popleft()
        if 0 <= y < height and 0 <= x < width and keyed[y, x] and not outside[y, x]:
            outside[y, x] = True
            queue.extend([(y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)])

    # Edge pixels (next to the outside) blend the tile with magenta, whose green is 0: alpha = green / tile green.
    near = outside.copy()
    for _ in range(3):
        grown = near.copy()
        grown[1:, :] |= near[:-1, :]
        grown[:-1, :] |= near[1:, :]
        grown[:, 1:] |= near[:, :-1]
        grown[:, :-1] |= near[:, 1:]
        near = grown
    edge = near & ~outside
    tile_green = np.median(green[~near & (green > 200)])
    alpha = np.ones_like(green)
    alpha[outside] = 0
    alpha[edge] = np.clip(green[edge] / tile_green, 0.02, 1)
    # Take the magenta back out of the edge color: pixel = alpha * tile + (1 - alpha) * magenta.
    out = rgb.copy()
    magenta_color = np.array([255, 0, 255], dtype=np.float32)
    out[edge] = (rgb[edge] - (1 - alpha[edge, None]) * magenta_color) / alpha[edge, None]
    rgba = np.dstack([np.clip(out, 0, 255), alpha * 255]).round().astype(np.uint8)
    image = Image.fromarray(rgba, "RGBA")

    box = image.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    image = image.crop(box)
    side = max(image.size)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.paste(image, ((side - image.width) // 2, (side - image.height) // 2))
    return square.resize((SIZE, SIZE), Image.LANCZOS)


def padded(master: Image.Image, scale: float, shadow: bool = False) -> Image.Image:
    """The mark centered on a transparent square, scaled to a fraction of it, optionally with a soft drop shadow."""
    inner = round(SIZE * scale)
    mark = master.resize((inner, inner), Image.LANCZOS)
    origin = ((SIZE - inner) // 2, (SIZE - inner) // 2)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    if shadow:
        mask = Image.new("L", (SIZE, SIZE), 0)
        mask.paste(mark.getchannel("A"), (origin[0], origin[1] + 12))
        mask = mask.filter(ImageFilter.GaussianBlur(14)).point(lambda a: round(a * 0.28))
        canvas.paste(Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 255)), mask=mask)
    canvas.alpha_composite(mark, origin)
    return canvas


def save(image: Image.Image, path: str, size: int) -> None:
    target = REPO / path
    target.parent.mkdir(parents=True, exist_ok=True)
    image.resize((size, size), Image.LANCZOS).save(target, optimize=True)
    print(f"{path} ({size}px)")


def main() -> None:
    if len(sys.argv) > 1:
        master = cut_background(Path(sys.argv[1]))
        master.save(MASTER, optimize=True)
        print(f"{MASTER.relative_to(REPO)} ({SIZE}px)")
    master = Image.open(MASTER).convert("RGBA")

    save(master, "gallery/TKCode.Arc.Gallery/Assets/logo.png", 256)
    save(master, "gallery/TKCode.Arc.Gallery.Browser/wwwroot/favicon.png", 64)
    save(padded(master, 0.92), "gallery/TKCode.Arc.Gallery.Browser/wwwroot/apple-touch-icon.png", 180)

    desktop = REPO / "gallery/TKCode.Arc.Gallery.Desktop/Assets"
    desktop.mkdir(parents=True, exist_ok=True)
    # Windows draws the icon edge to edge.
    master.save(desktop / "AppIcon.ico", sizes=[(s, s) for s in (16, 24, 32, 48, 64, 128, 256)])
    print("gallery/TKCode.Arc.Gallery.Desktop/Assets/AppIcon.ico")
    # macOS icons keep a margin: the artwork fills about 80% of the canvas, like the system's own icons.
    padded(master, 0.8, shadow=True).save(desktop / "AppIcon.icns", sizes=[(s, s) for s in (16, 32, 64, 128, 256, 512, 1024)])
    print("gallery/TKCode.Arc.Gallery.Desktop/Assets/AppIcon.icns")


if __name__ == "__main__":
    main()
