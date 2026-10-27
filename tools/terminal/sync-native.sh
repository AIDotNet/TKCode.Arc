#!/usr/bin/env bash
# Copies libghostty-vt for every RID built by tools/terminal/build-ghostty.sh (artifacts/native/<rid>) into
# src/TKCode.Arc.Terminal/runtimes/<rid>/native, from where the package picks them up.
# Usage: tools/terminal/sync-native.sh [<artifacts/native dir>]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
src="${1:-$root/artifacts/native}"
dst="$root/src/TKCode.Arc.Terminal/runtimes"
found=0
for dir in "$src"/*/; do
  rid="$(basename "$dir")"
  for lib in "$dir"libghostty-vt.dylib "$dir"libghostty-vt.so "$dir"ghostty-vt.dll; do
    [ -f "$lib" ] || continue
    mkdir -p "$dst/$rid/native"
    cp "$lib" "$dst/$rid/native/"
    echo "$rid: $(basename "$lib")"
    found=1
  done
done
[ "$found" = 1 ] || { echo "no libghostty-vt under $src" >&2; exit 1; }
