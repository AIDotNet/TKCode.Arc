#!/usr/bin/env python3
"""Builds the gallery (net10.0, no incremental) and collects the GDOC diagnostics.

Writes <out>/api-keys.json  : [{"key","source"}]  every API description that lacks a zh-CN or zh-TW translation
                  (GDOC006), or whose translation is outdated (GDOC005)
       <out>/summaries.txt  : GDOC004 lines (members without an XML <summary>)
       <out>/other.txt      : every other GDOC / ATOMUI warning or error
"""
import json, os, re, subprocess, sys

repo, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
proc = subprocess.run(["dotnet", "build", "gallery/TKCode.Arc.Gallery/TKCode.Arc.Gallery.csproj", "-f", "net10.0", "--no-incremental"],
                      cwd=repo, capture_output=True, text=True)
log = proc.stdout + proc.stderr
open(os.path.join(out, "build.log"), "w", encoding="utf-8").write(log)

tail = re.compile(r"\s\[/[^\]]*\.csproj[^\]]*\]\s*$")
keys, summaries, other = {}, set(), set()
for line in log.splitlines():
    line = tail.sub("", line.strip())
    m = re.search(r"GDOC006: The (\S+) page shows (\S+) but Docs/Api/\S+\.xlf has no translation for it: (.*)$", line)
    if m:
        keys[m.group(2)] = m.group(3)
        continue
    m = re.search(r"GDOC005: The (\S+) translation of (\S+) was made from different English text; the summary is now: (.*)$", line)
    if m:
        keys[m.group(2)] = m.group(3)
        continue
    m = re.search(r"GDOC004: (\S+) has no XML", line)
    if m:
        summaries.add(m.group(1))
        continue
    if re.search(r"(GDOC\d+|ATOMUI\w+|error CS|error )", line):
        other.add(line)

json.dump([{"key": k, "source": keys[k]} for k in sorted(keys)], open(os.path.join(out, "api-keys.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
open(os.path.join(out, "summaries.txt"), "w", encoding="utf-8").write("\n".join(sorted(summaries)) + "\n")
open(os.path.join(out, "other.txt"), "w", encoding="utf-8").write("\n".join(sorted(other)) + "\n")
print(f"build exit {proc.returncode}: {len(keys)} descriptions to translate, {len(summaries)} members without a summary, {len(other)} other diagnostics")
