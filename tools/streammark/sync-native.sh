#!/usr/bin/env bash
# Copies libratex_ffi for every RID built by tools/streammark/build-ratex.sh (artifacts/native/<rid>) into
# src/TKCode.Arc.StreamMark.RaTeX/runtimes/<rid>/native, from where the package picks them up.
# Usage: tools/streammark/sync-native.sh [<artifacts/native dir>]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
src="${1:-$root/artifacts/native}"
dst="$root/src/TKCode.Arc.StreamMark.RaTeX/runtimes"
found=0
for dir in "$src"/*/; do
  rid="$(basename "$dir")"
  case "$rid" in
    osx-*) lib="${dir}libratex_ffi.dylib";;
    linux-*) lib="${dir}libratex_ffi.so";;
    win-*) lib="${dir}ratex_ffi.dll";;
    *) continue;;
  esac
  [ -f "$lib" ] || continue
  mkdir -p "$dst/$rid/native"
  cp "$lib" "$dst/$rid/native/"
  echo "$rid: $(basename "$lib")"
  found=1
done
[ "$found" = 1 ] || { echo "no libratex_ffi under $src" >&2; exit 1; }
