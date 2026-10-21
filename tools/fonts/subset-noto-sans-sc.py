#!/usr/bin/env python3
"""Builds gallery/TKCode.Arc.Gallery.Browser/wwwroot/fonts/NotoSansSC-Subset.otf, the Chinese font of the WebAssembly host.

A browser has no system fonts for Skia, so Chinese text is boxes unless the app brings a font. This subsets Noto Sans SC
Regular (SIL OFL 1.1) to the characters the repository uses plus GB2312 level 1 (the 3755 common characters, which cover
AtomUI's own Chinese strings and future text): about 1 MB, 0.7 MB over the wire, loaded only when a Chinese character is
first drawn (BrowserFonts.cs). GalleryBrowserFontTests fails when a character of the repository is missing; run this again.

  python3 -m venv /tmp/ftvenv && /tmp/ftvenv/bin/pip install fonttools brotli
  /tmp/ftvenv/bin/python tools/fonts/subset-noto-sans-sc.py [NotoSansSC-Regular.otf]

Without an argument the source font is downloaded from github.com/notofonts/noto-cjk (8 MB).
"""
import os
import re
import sys
import urllib.request

from fontTools import subset

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUTPUT = os.path.join(ROOT, "gallery", "TKCode.Arc.Gallery.Browser", "wwwroot", "fonts", "NotoSansSC-Subset.otf")
SOURCE_URL = "https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/SubsetOTF/SC/NotoSansSC-Regular.otf"
# Characters the gallery can draw: Han, CJK and full-width punctuation, general punctuation.
CJK = re.compile("[‐-‧‰-⁞⺀-⿟　-〿㐀-䶿一-鿿豈-﫿＀-￯]")
SKIPPED = ("/obj/", "/bin/", "/GeneratedFiles/", "/node_modules/", "/.git/")


def repository_characters():
    used = set()
    for folder in ("gallery", "src"):
        for folder_path, _, names in os.walk(os.path.join(ROOT, folder)):
            if any(part in folder_path + "/" for part in SKIPPED):
                continue
            for name in names:
                if not name.endswith((".md", ".xlf", ".cs", ".axaml", ".json")):
                    continue
                try:
                    with open(os.path.join(folder_path, name), encoding="utf-8") as file:
                        used.update(ch for ch in file.read() if CJK.match(ch))
                except (UnicodeDecodeError, OSError):
                    pass
    return used


def gb2312_level_one():
    characters = set()
    for lead in range(0xB0, 0xD8):
        for trail in range(0xA1, 0xFF):
            try:
                characters.add(bytes([lead, trail]).decode("gb2312"))
            except UnicodeDecodeError:
                pass
    return characters


def main():
    source = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(OUTPUT), "..", "..", "..", "..", ".work", "NotoSansSC-Regular.otf")
    if not os.path.exists(source):
        os.makedirs(os.path.dirname(source), exist_ok=True)
        print("downloading", SOURCE_URL)
        urllib.request.urlretrieve(SOURCE_URL, source)
    characters = repository_characters() | gb2312_level_one() | set("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz")
    options = subset.Options()
    options.layout_features = ["*"]
    options.hinting = False
    options.desubroutinize = True
    options.notdef_outline = True
    font = subset.load_font(source, options)
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=[ord(c) for c in characters])
    subsetter.subset(font)
    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
    subset.save_font(font, OUTPUT, options)
    print(f"{len(characters)} characters -> {OUTPUT} ({os.path.getsize(OUTPUT) // 1024} KB)")


if __name__ == "__main__":
    main()
