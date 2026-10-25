"""Marks components as 已实现 in design/components/overview.md when their control and gallery page exist.

Usage: python3 -I tools/dev/update_status.py [repo-root]
A component counts as implemented when src/TKCode.Arc/<Category>/<Class>/<Class>.cs exists and some file under
gallery/TKCode.Arc.Gallery/Pages registers it with Register("<Class>", ...). Rows already at 已验收 are left alone.
"""
import glob, os, re, sys

root = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()
doc = os.path.join(root, "design/components/overview.md")
registered = set()
for path in glob.glob(os.path.join(root, "gallery/TKCode.Arc.Gallery/Pages/**/*.cs"), recursive=True):
    registered.update(re.findall(r'Register\("([A-Za-z]+)"', open(path, encoding="utf-8").read()))

def implemented(cls):
    return bool(glob.glob(os.path.join(root, f"src/TKCode.Arc/*/{cls}/{cls}.cs"))) and cls in registered

lines = open(doc, encoding="utf-8").read().split("\n")
row = re.compile(r"^\| (\d+) \| `([A-Za-z]+)` \|")
changed = done = 0
for i, line in enumerate(lines):
    m = row.match(line)
    if not m:
        continue
    cells = line.split(" | ")
    status = cells[-1].rstrip(" |")
    if implemented(m.group(2)):
        done += 1
        if status not in ("已实现", "已验收"):
            cells[-1] = "已实现 |"
            lines[i] = " | ".join(cells)
            changed += 1
open(doc, "w", encoding="utf-8").write("\n".join(lines))
print(f"implemented: {done}/111, rows updated: {changed}")
