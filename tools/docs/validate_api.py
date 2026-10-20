#!/usr/bin/env python3
"""validate_api.py <chunk-in.json> <chunk-out.json>: checks a translated chunk. Prints OK or the problems (exit 1)."""
import json, re, sys
src = {e["key"]: e["source"] for e in json.load(open(sys.argv[1], encoding="utf-8"))}
try:
    out = json.load(open(sys.argv[2], encoding="utf-8"))
except Exception as ex:
    print("PROBLEM: not valid JSON:", ex); sys.exit(1)
problems = []
for k in src:
    if k not in out: problems.append(f"{k}: missing"); continue
    for lang in ("zh-CN", "zh-TW"):
        t = (out[k].get(lang) or "").strip() if isinstance(out[k], dict) else ""
        if not t: problems.append(f"{k}: {lang} is empty"); continue
        if sorted(re.findall(r"`[^`]*`", src[k])) != sorted(re.findall(r"`[^`]*`", t)):
            problems.append(f"{k}: {lang} must keep the backtick spans exactly: {re.findall(chr(96)+'[^'+chr(96)+']*'+chr(96), src[k])}")
        if sorted(re.findall(r"\{\d+\}", src[k])) != sorted(re.findall(r"\{\d+\}", t)):
            problems.append(f"{k}: {lang} changed a {{n}} placeholder")
        words = re.findall(r"[A-Za-z]{3,}", re.sub(r"`[^`]*`", "", src[k]))
        if len(words) >= 3 and not re.search(r"[一-鿿]", t):
            problems.append(f"{k}: {lang} is not Chinese")
        if t == src[k]: problems.append(f"{k}: {lang} equals the English")
for k in out:
    if k not in src: problems.append(f"{k}: not in the chunk (remove it)")
if problems:
    print("\n".join(problems[:40])); print(f"{len(problems)} problem(s)"); sys.exit(1)
print(f"OK {len(src)} entries")
