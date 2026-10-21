#!/usr/bin/env python3
"""Prints the source-hash of en-US.md files the way GalleryDocsGenerator computes it (DocPage.HashOf).

    dochash.py <en-US.md>...            print each page's hash
    dochash.py --stamp <translation>... write the hash of the en-US.md next to each translation into its source-hash line
"""
import os
import re
import sys

def normalize(text):
    return text.replace("\r\n", "\n").replace("\r", "\n").rstrip()

def hash_of(text):
    h = 14695981039346656037
    for b in normalize(text).encode("utf-8"):
        h ^= b
        h = (h * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return format(h, "016x")[:12]

def stamp(path):
    source = os.path.join(os.path.dirname(path), "en-US.md")
    value = hash_of(open(source, encoding="utf-8").read())
    text = open(path, encoding="utf-8").read()
    stamped, count = re.subn(r"(?m)^source-hash:.*$", "source-hash: " + value, text, count=1)
    if count == 0:
        raise SystemExit(f"{path}: no source-hash line in the front matter")
    with open(path, "w", encoding="utf-8", newline="") as file:
        file.write(stamped)
    print(value, path)

if __name__ == "__main__":
    if sys.argv[1:2] == ["--stamp"]:
        for path in sys.argv[2:]:
            stamp(path)
    else:
        for path in sys.argv[1:]:
            print(hash_of(open(path, encoding="utf-8").read()), path)
