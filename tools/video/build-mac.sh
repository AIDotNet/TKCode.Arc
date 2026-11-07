#!/usr/bin/env bash
# Builds the macOS video shim native/video/mac/tkcode_video_av.m with Xcode clang (zig cannot compile the AVFoundation
# headers: ADR 0036). Needs macOS with Xcode or the Command Line Tools.
# Output: artifacts/native/<rid>/libtkcode_video.dylib for osx-arm64 or osx-x64 (each a single-architecture dylib).
# Usage: tools/video/build-mac.sh [osx-arm64|osx-x64]   (default: the host architecture)
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"

rid="${1:-}"
if [[ -z "$rid" ]]; then
  case "$(uname -m)" in arm64) rid=osx-arm64;; *) rid=osx-x64;; esac
fi
case "$rid" in
  osx-arm64) arch=arm64;;
  osx-x64) arch=x86_64;;
  *) echo "build-mac.sh builds osx-arm64 or osx-x64, not $rid" >&2; exit 2;;
esac
[[ "$(uname -s)" == Darwin ]] || { echo "build-mac.sh needs macOS (Apple SDK)" >&2; exit 1; }

sdk="$(xcrun --sdk macosx --show-sdk-path)"
out="$root/artifacts/native/$rid"
work="$root/artifacts/native-build/video-$rid"
lib=libtkcode_video.dylib
mkdir -p "$out"
rm -rf "${work:?}"
mkdir -p "$work"

echo "$(xcrun clang --version | head -1): $rid"
xcrun clang -arch "$arch" -isysroot "$sdk" -mmacosx-version-min=11.0 \
  -fobjc-arc -fvisibility=hidden -O2 -Wall -Wextra -Wno-unused-parameter -shared \
  -I"$root/native/video" \
  "$root/native/video/mac/tkcode_video_av.m" \
  -framework AVFoundation -framework CoreMedia -framework CoreVideo -framework IOSurface -framework Metal -framework QuartzCore -framework Foundation \
  -Wl,-install_name,@rpath/$lib \
  -o "$work/$lib"
strip -x "$work/$lib"
cp "$work/$lib" "$out/$lib"
ls -la "$out/$lib"
