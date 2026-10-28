#!/usr/bin/env bash
# Copies the audio shim for every RID built by tools/audio/build-miniaudio.sh (artifacts/native/<rid>) into
# src/TKCode.Arc.Audio.Native/runtimes/<rid>/native, from where the package picks them up.
# Usage: tools/audio/sync-native.sh [<artifacts/native dir>]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
src="${1:-$root/artifacts/native}"
dst="$root/src/TKCode.Arc.Audio.Native/runtimes"
found=0
for dir in "$src"/*/; do
  rid="$(basename "$dir")"
  for lib in "$dir"libtkcode_audio.dylib "$dir"libtkcode_audio.so "$dir"tkcode_audio.dll; do
    [ -f "$lib" ] || continue
    mkdir -p "$dst/$rid/native"
    cp "$lib" "$dst/$rid/native/"
    echo "$rid: $(basename "$lib")"
    found=1
  done
done
[ "$found" = 1 ] || { echo "no tkcode_audio under $src" >&2; exit 1; }
