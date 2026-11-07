#!/usr/bin/env bash
# Copies the video shim for every RID built by tools/video/build-native.sh (artifacts/native/<rid>) into
# src/TKCode.Arc.Video.Native/runtimes/<rid>/native, from where the package picks them up.
# Usage: tools/video/sync-native.sh [<artifacts/native dir>]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
src="${1:-$root/artifacts/native}"
dst="$root/src/TKCode.Arc.Video.Native/runtimes"
found=0
for dir in "$src"/*/; do
  rid="$(basename "$dir")"
  for lib in "$dir"libtkcode_video.dylib "$dir"libtkcode_video.so "$dir"tkcode_video.dll; do
    [ -f "$lib" ] || continue
    mkdir -p "$dst/$rid/native"
    cp "$lib" "$dst/$rid/native/"
    echo "$rid: $(basename "$lib")"
    found=1
  done
done
[ "$found" = 1 ] || { echo "no tkcode_video under $src" >&2; exit 1; }
