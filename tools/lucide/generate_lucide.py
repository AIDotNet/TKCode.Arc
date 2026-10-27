#!/usr/bin/env python3
"""Generates Lucide icon geometry for TKCode.Arc from a lucide-static package.

Usage:
  python3 -I tools/lucide/generate_lucide.py <lucide-static-package-dir> <repo-root>

Writes:
  src/TKCode.Arc.Core/Glyphs/ArcIcons.g.cs          curated subset used inside Arc controls
  src/TKCode.Arc.Icons.Lucide/LucideIcons.g.cs       every icon (optional package)

Every SVG element is converted to SVG path data in the 24x24 viewBox. Lucide icons are stroked
(stroke-width 2 by default; Arc draws them with 1.75), round caps and joins, no fill.
"""
import json
import os
import re
import sys

PKG, ROOT = sys.argv[1], sys.argv[2]

CORE_SUBSET = """
arrow-down arrow-left arrow-right arrow-up arrow-up-right at-sign bell bell-off calendar check check-check
chevron-down chevron-left chevron-right chevron-up chevrons-left chevrons-right chevrons-up-down circle
circle-alert circle-check circle-help circle-x clock copy corner-down-left download ellipsis ellipsis-vertical
external-link eye eye-off file file-text filter folder globe grip-vertical hash heart image info keyboard
link loader-circle lock log-out mail menu message-circle mic minus moon paperclip pause pencil phone pipette
play plus rotate-ccw search send settings share smile smile-plus square star sun trash-2 triangle-alert
undo-2 upload user users x zoom-in zoom-out archive bookmark command copy-check list
bold calendar-days chevrons-down-up clock-3 code cookie corner-down-right eraser file-archive file-code-2
file-image file-play folder-open heading-1 heading-2 heading-3 italic link-2 list-ordered monitor pilcrow quote
redo-2 rotate-cw square-code strikethrough sun-moon unlink house inbox layout-grid sparkles zap trending-up
trending-down refresh-cw maximize-2 minimize-2 sliders-horizontal shield credit-card tag flag thumbs-up
message-square arrow-up-down music volume-2 camera cloud cloud-upload database server terminal git-branch box
package layers palette type underline highlighter list-checks circle-dot circle-plus circle-minus file-plus
send-horizontal reply share-2 chart-line chart-bar chart-pie activity percent dollar-sign euro calendar-check
calendar-range timer hourglass laptop smartphone wifi battery ban lock-open key fingerprint expand shrink move
grip-horizontal panel-left panel-right columns-2 rows-2 table filter-x arrow-down-up arrow-down-wide-narrow
arrow-up-narrow-wide list-filter
gauge layers-3 brain file-code file-spreadsheet languages lightbulb wrench clipboard-paste square-pen pen-line bug
trophy equal thumbs-down git-compare-arrows arrow-up-from-line scan-text text-select square-dashed-text
panel-right-close shield-check cpu book-open list-todo layout-template
heading-4 heading-5 heading-6 superscript subscript replace replace-all a-large-small
""".split()


def num(v):
    f = float(v)
    s = ("%.4f" % f).rstrip("0").rstrip(".")
    return s if s not in ("-0", "") else "0"


def element_to_path(tag, a):
    if tag == "path":
        return a["d"]
    if tag == "circle":
        cx, cy, r = float(a["cx"]), float(a["cy"]), float(a["r"])
        return f"M{num(cx - r)} {num(cy)}a{num(r)} {num(r)} 0 1 0 {num(2 * r)} 0a{num(r)} {num(r)} 0 1 0 {num(-2 * r)} 0z"
    if tag == "ellipse":
        cx, cy, rx, ry = float(a["cx"]), float(a["cy"]), float(a["rx"]), float(a["ry"])
        return f"M{num(cx - rx)} {num(cy)}a{num(rx)} {num(ry)} 0 1 0 {num(2 * rx)} 0a{num(rx)} {num(ry)} 0 1 0 {num(-2 * rx)} 0z"
    if tag == "rect":
        x, y = float(a.get("x", 0)), float(a.get("y", 0))
        w, h = float(a["width"]), float(a["height"])
        rx = float(a.get("rx", a.get("ry", 0)))
        ry = float(a.get("ry", rx))
        rx, ry = min(rx, w / 2), min(ry, h / 2)
        if rx <= 0:
            return f"M{num(x)} {num(y)}h{num(w)}v{num(h)}h{num(-w)}z"
        return (f"M{num(x + rx)} {num(y)}h{num(w - 2 * rx)}a{num(rx)} {num(ry)} 0 0 1 {num(rx)} {num(ry)}"
                f"v{num(h - 2 * ry)}a{num(rx)} {num(ry)} 0 0 1 {num(-rx)} {num(ry)}h{num(-(w - 2 * rx))}"
                f"a{num(rx)} {num(ry)} 0 0 1 {num(-rx)} {num(-ry)}v{num(-(h - 2 * ry))}a{num(rx)} {num(ry)} 0 0 1 {num(rx)} {num(-ry)}z")
    if tag == "line":
        return f"M{num(a['x1'])} {num(a['y1'])}L{num(a['x2'])} {num(a['y2'])}"
    if tag in ("polyline", "polygon"):
        pts = [p for p in re.split(r"[\s,]+", a["points"].strip()) if p]
        pairs = [(pts[i], pts[i + 1]) for i in range(0, len(pts) - 1, 2)]
        d = "M" + " L".join(f"{num(x)} {num(y)}" for x, y in pairs)
        return d + ("z" if tag == "polygon" else "")
    raise ValueError(f"unsupported element {tag}")


def pascal(name):
    parts = re.split(r"[-_]", name)
    out = "".join(p[:1].upper() + p[1:] for p in parts)
    if out[0].isdigit():
        out = "Icon" + out
    return out


import xml.etree.ElementTree as ET


def absolute_start(d):
    """A <path>'s leading 'm' is absolute; joined after another path it would turn relative. Make it M + l."""
    d = d.strip()
    m = re.match(r"m\s*(-?[\d.]+)[\s,]*(-?[\d.]+)(.*)", d, re.S)
    if not m:
        return d
    x, y, rest = m.groups()
    rest = rest.strip()
    return f"M{x} {y}" + (f"l{rest}" if rest and rest[0] in "-.0123456789" else rest)


icons = {}
icon_dir = os.path.join(PKG, "icons")
for fn in sorted(os.listdir(icon_dir)):
    if not fn.endswith(".svg"):
        continue
    root = ET.parse(os.path.join(icon_dir, fn)).getroot()
    paths = []
    for el in root.iter():
        tag = el.tag.split("}")[-1]
        if tag in ("path", "circle", "ellipse", "rect", "line", "polyline", "polygon"):
            paths.append(element_to_path(tag, el.attrib))
    icons[fn[:-4]] = " ".join(absolute_start(p) for p in paths)

version = json.load(open(os.path.join(PKG, "package.json")))["version"]
header = f"// <auto-generated />\n// Generated by tools/lucide/generate_lucide.py from lucide-static {version} (ISC license, Copyright (c) Lucide Contributors).\n"

missing = [n for n in CORE_SUBSET if n not in icons]
if missing:
    print("missing in lucide:", missing)

core_lines = [header, "using Avalonia.Media;", "", "namespace TKCode.Arc;", "",
              "/// <summary>Lucide glyphs used inside Arc controls (24x24 viewBox, stroked).</summary>",
              "public static partial class ArcIcons", "{"]
for name in sorted(n for n in CORE_SUBSET if n in icons):
    core_lines.append(f"    public static Geometry {pascal(name)} => Get(\"{name}\", \"{icons[name]}\");")
core_lines.append("}")
os.makedirs(os.path.join(ROOT, "src/TKCode.Arc.Core/Glyphs"), exist_ok=True)
open(os.path.join(ROOT, "src/TKCode.Arc.Core/Glyphs/ArcIcons.g.cs"), "w", encoding="utf-8").write("\n".join(core_lines) + "\n")

full_lines = [header, "namespace TKCode.Arc.Icons.Lucide;", "",
              "/// <summary>Every Lucide icon as SVG path data in a 24x24 viewBox.</summary>",
              "public enum LucideIconKind", "{"]
# Lucide ships aliases whose PascalCase names collide (arrow-down-0-1 / arrow-down-01): keep the first.
names = []
seen = set()
for n in sorted(icons):
    if pascal(n) in seen:
        continue
    seen.add(pascal(n))
    names.append(n)
full_lines += [f"    {pascal(n)}," for n in names]
full_lines += ["}", "", "public static partial class LucideIconData", "{",
               "    public static string Get(LucideIconKind kind) => kind switch", "    {"]
full_lines += [f"        LucideIconKind.{pascal(n)} => \"{icons[n]}\"," for n in names]
full_lines += ["        _ => string.Empty", "    };", "}"]
os.makedirs(os.path.join(ROOT, "src/TKCode.Arc.Icons.Lucide"), exist_ok=True)
open(os.path.join(ROOT, "src/TKCode.Arc.Icons.Lucide/LucideIcons.g.cs"), "w", encoding="utf-8").write("\n".join(full_lines) + "\n")
print(f"core icons: {len([n for n in CORE_SUBSET if n in icons])}, all icons: {len(names)}")
