#!/usr/bin/env python3
"""Merges translations into Docs/Api/<lang>.xlf (XLIFF 1.2), keeping existing units and sorting by id.

usage: xlf_merge.py <Docs/Api dir> <translations.json> [...]
Each json file: {"<key>": {"source": "<english summary>", "zh-CN": "...", "zh-TW": "..."}, ...}
"""
import json, os, sys
from xml.sax.saxutils import escape
import xml.etree.ElementTree as ET

NS = "urn:oasis:names:tc:xliff:document:1.2"
HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!-- API reference descriptions for the gallery's Code tab. id = Type or DeclaringType.Member; <source> is the English
     XML summary the translation was made from (the build warns when the summary changes). -->
<xliff version="1.2" xmlns="urn:oasis:names:tc:xliff:document:1.2">
  <file source-language="en-US" target-language="{lang}" datatype="plaintext" original="TKCode.Arc">
    <body>
"""
FOOTER = """    </body>
  </file>
</xliff>
"""

def load(path):
    units = {}
    if os.path.exists(path):
        for unit in ET.parse(path).getroot().iter("{%s}trans-unit" % NS):
            units[unit.get("id")] = (unit.findtext("{%s}source" % NS) or "", unit.findtext("{%s}target" % NS) or "")
    return units

api_dir = sys.argv[1]
merged = {}
for path in sys.argv[2:]:
    merged.update(json.load(open(path, encoding="utf-8")))

for lang in ("zh-CN", "zh-TW"):
    target = os.path.join(api_dir, lang + ".xlf")
    units = load(target)
    added = 0
    for key, entry in merged.items():
        text = (entry.get(lang) or "").strip()
        if not text or not entry.get("source"):
            continue
        units[key] = (entry["source"], text)
        added += 1
    with open(target, "w", encoding="utf-8") as f:
        f.write(HEADER.format(lang=lang))
        for key in sorted(units):
            source, text = units[key]
            f.write(f'      <trans-unit id="{escape(key)}">\n        <source>{escape(source)}</source>\n        <target>{escape(text)}</target>\n      </trans-unit>\n')
        f.write(FOOTER)
    print(f"{lang}: {added} merged, {len(units)} units")
