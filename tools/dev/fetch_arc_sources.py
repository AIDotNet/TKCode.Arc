"""Downloads the MIT-licensed source of Arc's free components (only the slugs in the gallery catalog) and unpacks it.

Usage: python3 -I tools/dev/fetch_arc_sources.py <repo-root> <out-dir>
Writes <out-dir>/<slug>/<file> plus <out-dir>/<slug>/_meta.json (title, description, docs). Uses curl.
Pro components are never requested: the catalog lists only the 111 free components.
"""
import json, os, re, subprocess, sys

root, out = sys.argv[1], sys.argv[2]
catalog = open(os.path.join(root, "gallery/TKCode.Arc.Gallery/Catalog/GalleryCatalog.g.cs"), encoding="utf-8").read()
slugs = re.findall(r'new\("[A-Za-z]+", "[^"]*", "([a-z0-9-]+)", \d+,', catalog)
os.makedirs(out, exist_ok=True)
failed = []
for slug in slugs:
    target = os.path.join(out, slug)
    if os.path.isfile(os.path.join(target, "_meta.json")):
        continue
    result = subprocess.run(["curl", "-sfL", "--max-time", "30", "-A", "atomui-arc-port", f"https://uiarc.dev/r/{slug}.json"],
                            capture_output=True)
    if result.returncode != 0:
        failed.append(slug)
        continue
    data = json.loads(result.stdout.decode("utf-8"))
    os.makedirs(target, exist_ok=True)
    for f in data.get("files", []):
        with open(os.path.join(target, os.path.basename(f["path"])), "w", encoding="utf-8") as handle:
            handle.write(f.get("content", ""))
    meta = {k: data.get(k) for k in ("title", "description", "dependencies", "registryDependencies", "docs", "meta")}
    with open(os.path.join(target, "_meta.json"), "w", encoding="utf-8") as handle:
        json.dump(meta, handle, ensure_ascii=False, indent=1)
print(f"slugs: {len(slugs)}, failed: {failed}")
