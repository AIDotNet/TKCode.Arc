#!/usr/bin/env python3
"""Read-only verification of the TKCode.Arc design/ documents.

Usage: python3 -I verify_design_docs.py <design-dir> <atomui-src-dir>
Exit code 0 = no errors (warnings allowed), 1 = errors found.
"""
import os
import re
import sys
import unicodedata

DESIGN = os.path.abspath(sys.argv[1])
ATOMUI = os.path.abspath(sys.argv[2])

errors, warnings = [], []


def err(msg):
    errors.append(msg)


def warn(msg):
    warnings.append(msg)


def read(rel):
    with open(os.path.join(DESIGN, rel), encoding="utf-8") as f:
        return f.read()


EXPECTED_CATEGORIES = [
    ("Buttons", "buttons", 11), ("Gestures", "gestures", 3), ("Menus", "menus", 3),
    ("TextFields", "text-fields", 7), ("SpecialInputs", "special-inputs", 7), ("Selects", "selects", 5),
    ("Toggles", "toggles", 6), ("Sliders", "sliders", 2), ("Pickers", "pickers", 5), ("Editors", "editors", 3),
    ("Navigation", "navigation", 3), ("Expand", "expand", 5), ("Overlays", "overlays", 6),
    ("Messages", "messages", 5), ("Progress", "progress", 5), ("Avatars", "avatars", 3), ("Cards", "cards", 3),
    ("Charts", "charts", 13), ("Tables", "tables", 7), ("Activity", "activity", 3), ("Media", "media", 3),
    ("TextEffects", "text-effects", 4),
]
EXPECTED_PHASES = [2, 6, 7, 19, 8, 14, 17, 17, 12, 10]

# ---------------------------------------------------------------- 1. inventory
inv = read("components/overview.md")
rows = []  # dict(order, cls, slug, phase, deps, prims, category)
current_cat = None
for line in inv.splitlines():
    m = re.match(r"^### 3\.\d+ .*（`(\w+)`，(\d+)）", line)
    if m:
        current_cat = m.group(1)
        continue
    m = re.match(r"^\| (\d+) \| `(\w+)` \| \[[^\]]+\]\(https://uiarc\.dev/components/([a-z0-9-]+)\) \| P(\d) \| ([^|]+) \| [^|]+ \| ([^|]+) \|", line)
    if m and current_cat:
        deps = re.findall(r"`(\w+)`", m.group(5))
        prims = [int(x) for x in re.findall(r"P(\d+)", m.group(6))]
        rows.append(dict(order=int(m.group(1)), cls=m.group(2), slug=m.group(3), phase=int(m.group(4)),
                         deps=deps, prims=prims, category=current_cat))

if len(rows) != 112:
    err(f"inventory: expected 112 rows, found {len(rows)}")
if len({r['cls'] for r in rows}) != len(rows):
    err("inventory: duplicate class names")
if len({r['slug'] for r in rows}) != len(rows):
    err("inventory: duplicate slugs")
cat_counts = [sum(1 for r in rows if r["category"] == c) for c, _, _ in EXPECTED_CATEGORIES]
if cat_counts != [n for _, _, n in EXPECTED_CATEGORIES]:
    err(f"inventory: category counts {cat_counts} != expected")
phase_counts = [sum(1 for r in rows if r["phase"] == p) for p in range(10)]
if phase_counts != EXPECTED_PHASES:
    err(f"inventory: phase counts {phase_counts} != {EXPECTED_PHASES}")
by_cls = {r["cls"]: r for r in rows}
for r in rows:
    for d in r["deps"]:
        if d not in by_cls:
            err(f"inventory: {r['cls']} depends on unknown {d}")
        elif by_cls[d]["phase"] >= r["phase"]:
            err(f"inventory: {r['cls']} (P{r['phase']}) depends on {d} (P{by_cls[d]['phase']}) not earlier")

# ---------------------------------------------------------------- 2. roadmap phases
road = read("roadmap/overview.md")
for line in road.splitlines():
    m = re.match(r"^\| P(\d) \| [^|]+ \| (\d+) \| ([^|]+) \|", line)
    if m:
        p, n = int(m.group(1)), int(m.group(2))
        listed = [x.strip() for x in m.group(3).split("、") if x.strip()]
        want = {r["cls"] for r in rows if r["phase"] == p}
        if set(listed) != want:
            err(f"roadmap P{p}: listed {sorted(set(listed) ^ want)} differ from inventory")
        if n != len(want):
            err(f"roadmap P{p}: count {n} != {len(want)}")

# ---------------------------------------------------------------- 3. primitives
cat = read("architecture/motion/primitives-catalog.md")
seen_p = set()
for line in cat.splitlines():
    m = re.match(r"^\| P(\d+) \| [^|]+ \| [^|]+ \| [^|]+ \| [^|]+ \| (\d+) \| P(\d) \| ([^|]+) \|", line)
    if m:
        p, n, fp = int(m.group(1)), int(m.group(2)), int(m.group(3))
        seen_p.add(p)
        users = [r for r in rows if p in r["prims"]]
        if not users:
            err(f"primitives: P{p} has no users in inventory")
            continue
        real_fp = min(u["phase"] for u in users)
        if n != len(users):
            err(f"primitives: P{p} count {n} != {len(users)}")
        if fp != real_fp:
            err(f"primitives: P{p} lands P{fp} but first user is P{real_fp}")
if seen_p != set(range(1, 31)):
    err(f"primitives: missing {sorted(set(range(1, 31)) - seen_p)}")
seen_s = set(int(x) for x in re.findall(r"^\| S(\d+) \|", cat, re.M))
if seen_s != set(range(1, 13)):
    err(f"services: missing {sorted(set(range(1, 13)) - seen_s)}")

# ---------------------------------------------------------------- 4. tokens
tok = read("architecture/theming/token-catalog.md")
token_names = []
for line in tok.splitlines():
    if not line.startswith("| `"):
        continue
    first = line.split("|")[1]
    token_names += re.findall(r"`([A-Z][A-Za-z0-9]*)`", first)
dups = {t for t in token_names if token_names.count(t) > 1}
if dups:
    err(f"tokens: duplicates {sorted(dups)}")
global_names = set()
defs = os.path.join(ATOMUI, "src/AtomUI.Core/Theme/DesignTokens/Definitions")
for fn in os.listdir(defs):
    with open(os.path.join(defs, fn), encoding="utf-8") as f:
        global_names |= set(re.findall(r"public [A-Za-z<>?,. ]+ ([A-Z][A-Za-z0-9]+) \{ get; set; \}", f.read()))
clash = sorted(set(token_names) & global_names)
if clash:
    err(f"tokens: clash with AtomUI global tokens {clash}")
if len(global_names) < 200:
    warn(f"tokens: only {len(global_names)} AtomUI global names parsed")

# ---------------------------------------------------------------- 5. structure & links
def slugify(text):
    text = text.strip().lower()
    out = []
    for ch in text:
        cat_ = unicodedata.category(ch)
        if ch in " -_" or cat_[0] in ("L", "N"):
            out.append(ch)
    return "".join(out).replace(" ", "-")


md_files = []
for root, dirs, files in os.walk(DESIGN):
    for fn in files:
        if fn.endswith(".md") and not (root == DESIGN and fn == "index.md"):
            md_files.append(os.path.relpath(os.path.join(root, fn), DESIGN))
    if "index.md" in files:
        where = os.path.relpath(root, DESIGN)
        if where == ".":
            warn("structure: design/index.md exists (copy of the approved plan, not created by the docs); entry file is overview.md")
        else:
            err(f"structure: index.md found in {where}")
    if root != DESIGN and "overview.md" not in files:
        err(f"structure: no overview.md in {os.path.relpath(root, DESIGN)}")

anchors = {}
for rel in md_files:
    txt = read(rel)
    in_code = False
    hs = set()
    for line in txt.splitlines():
        if line.startswith("```"):
            in_code = not in_code
        if not in_code:
            m = re.match(r"^#{1,6} (.+)$", line)
            if m:
                hs.add(slugify(m.group(1)))
    anchors[rel] = hs

link_re = re.compile(r"\]\(([^)\s]+)\)")
for rel in md_files:
    txt = read(rel)
    stripped = re.sub(r"```.*?```", "", txt, flags=re.S)
    for target in link_re.findall(stripped):
        if re.match(r"^[a-z]+://", target) or target.startswith("mailto:"):
            continue
        path, _, anchor = target.partition("#")
        dest = os.path.normpath(os.path.join(os.path.dirname(rel), path)) if path else rel
        if path and not os.path.exists(os.path.join(DESIGN, dest)):
            err(f"link: {rel} -> {target} (missing file)")
            continue
        if anchor and dest.endswith(".md") and anchor not in anchors.get(dest, set()):
            err(f"link: {rel} -> {target} (missing anchor)")

for rel in md_files:
    if os.path.basename(rel) != "overview.md":
        continue
    d = os.path.dirname(rel)
    txt = read(rel)
    for entry in sorted(os.listdir(os.path.join(DESIGN, d))):
        full = os.path.join(DESIGN, d, entry)
        if entry == "overview.md" or (d == "" and entry == "index.md"):
            continue
        child = entry if entry.endswith(".md") else (entry + "/overview.md" if os.path.isdir(full) else None)
        if child and child not in txt:
            err(f"structure: {rel} does not link child {child}")

for rel in md_files:
    n = read(rel).count("\n")
    if n > 400:
        warn(f"length: {rel} has {n} lines (> 400)")

# ---------------------------------------------------------------- 6. ADRs
adr_dir = os.path.join(DESIGN, "decisions")
adr_index = read("decisions/overview.md")
for fn in sorted(os.listdir(adr_dir)):
    if not re.match(r"^\d{4}-.*\.md$", fn):
        continue
    txt = read("decisions/" + fn)
    for key in ("- 状态：", "- 日期：", "## 背景", "## 决策", "## 备选方案", "## 后果"):
        if key not in txt:
            err(f"adr: {fn} missing '{key}'")
    if fn not in adr_index:
        err(f"adr: {fn} not in index")

# ---------------------------------------------------------------- 7. category overviews
for code, doc, n in EXPECTED_CATEGORIES:
    path = f"components/{doc}/overview.md"
    if not os.path.exists(os.path.join(DESIGN, path)):
        err(f"category: {path} missing")
        continue
    txt = read(path)
    want = [r for r in rows if r["category"] == code]
    for r in want:
        if not re.search(rf"^### 4\.\d+ {r['cls']}（", txt, re.M):
            err(f"category: {path} lacks section for {r['cls']}")
        m = re.search(rf"^### 4\.\d+ {r['cls']}（.*\n+`slug: ([a-z0-9-]+)` · 阶段 P(\d)", txt, re.M)
        if m and (m.group(1) != r["slug"] or int(m.group(2)) != r["phase"]):
            err(f"category: {path} {r['cls']} meta slug/phase {m.group(1)}/P{m.group(2)} != {r['slug']}/P{r['phase']}")
        elif not m:
            warn(f"category: {path} {r['cls']} metadata line not in expected format")
    extra = set(re.findall(r"^### 4\.\d+ (\w+)（", txt, re.M)) - {r["cls"] for r in want}
    if extra:
        err(f"category: {path} has unexpected sections {sorted(extra)}")
    # primitives column of the section-2 table must match the inventory
    for r in want:
        found = False
        for line in txt.splitlines():
            cells = line.split("|")
            if not line.startswith("|") or len(cells) < 9:
                continue
            if any(c.strip().strip("`") == r["cls"] for c in cells[1:3]):
                listed = sorted(int(x) for x in re.findall(r"P(\d+)", cells[-2]))
                if listed != sorted(r["prims"]):
                    err(f"category: {path} {r['cls']} primitives {listed} != inventory {sorted(r['prims'])}")
                found = True
                break
        if not found:
            err(f"category: {path} {r['cls']} missing from section-2 table")

# ---------------------------------------------------------------- report
print(f"files: {len(md_files)}, inventory rows: {len(rows)}, tokens: {len(token_names)}")
for w in warnings:
    print("WARN ", w)
for e in errors:
    print("ERROR", e)
print("RESULT:", "OK" if not errors else f"{len(errors)} error(s)")
sys.exit(1 if errors else 0)
