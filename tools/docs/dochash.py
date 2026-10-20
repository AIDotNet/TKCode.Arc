#!/usr/bin/env python3
"""Prints the source-hash of en-US.md files the way GalleryDocsGenerator computes it (DocPage.HashOf)."""
import sys

def normalize(text):
    return text.replace("\r\n", "\n").replace("\r", "\n").rstrip()

def hash_of(text):
    h = 14695981039346656037
    for b in normalize(text).encode("utf-8"):
        h ^= b
        h = (h * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return format(h, "016x")[:12]

if __name__ == "__main__":
    for path in sys.argv[1:]:
        print(hash_of(open(path, encoding="utf-8").read()), path)
