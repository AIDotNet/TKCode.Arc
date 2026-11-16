#!/usr/bin/env bash
# Copies libghostty-vt for every RID built by tools/terminal/build-ghostty.sh (artifacts/native/<rid>) into
# src/TKCode.Arc.Terminal/runtimes/<rid>/native, from where the package picks them up: the desktop RIDs, android-* and
# the iOS xcframework from tools/terminal/build-ghostty-ios.sh (runtimes/ios/native). The loose iOS dylibs under
# artifacts/native/ios-arm64 and iossimulator-* are only its inputs and are not copied.
# Usage: tools/terminal/sync-native.sh [<artifacts/native dir>]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
src="${1:-$root/artifacts/native}"
dst="$root/src/TKCode.Arc.Terminal/runtimes"
found=0
for dir in "$src"/*/; do
  rid="$(basename "$dir")"
  case "$rid" in
    osx-*) lib="${dir}libghostty-vt.dylib";;
    linux-*|android-*) lib="${dir}libghostty-vt.so";;
    win-*) lib="${dir}ghostty-vt.dll";;
    *) continue;;
  esac
  [ -f "$lib" ] || continue
  mkdir -p "$dst/$rid/native"
  cp "$lib" "$dst/$rid/native/"
  echo "$rid: $(basename "$lib")"
  found=1
done
if [ -d "$src/ios/ghostty-vt.xcframework" ]; then
  rm -rf "$dst/ios/native/ghostty-vt.xcframework"
  mkdir -p "$dst/ios/native"
  cp -R "$src/ios/ghostty-vt.xcframework" "$dst/ios/native/"
  echo "ios: ghostty-vt.xcframework"
  found=1
fi
[ "$found" = 1 ] || { echo "no libghostty-vt under $src" >&2; exit 1; }
