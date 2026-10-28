#!/usr/bin/env bash
# Builds the TKCode.Arc.Audio.Native shim (native/miniaudio/tkcode_audio.c + the miniaudio header pinned in
# native/miniaudio/miniaudio-upstream.json) with the Zig in native/toolchain.json.
# Output: artifacts/native/<rid>/libtkcode_audio.{dylib,so} or tkcode_audio.dll.
# Linux hosts cross-compile the Linux and Windows targets (miniaudio loads ALSA/PulseAudio at run time, so no system
# headers are needed); the macOS targets need the Apple SDK (CoreAudio headers), so build them on macOS.
# Usage: tools/audio/build-miniaudio.sh [rid]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
manifest="$root/native/miniaudio/miniaudio-upstream.json"

rid="${1:-}"
if [[ -z "$rid" ]]; then
  case "$(uname -s)" in Darwin) os=osx;; Linux) os=linux;; *) os=win;; esac
  case "$(uname -m)" in arm64|aarch64) arch=arm64;; *) arch=x64;; esac
  rid="$os-$arch"
fi

json() { python3 -c "import json,sys; d=json.load(open(sys.argv[1])); print(eval(sys.argv[2]))" "$manifest" "$1"; }
url="$(json 'd["header"]["url"]')"
sha="$(json 'd["header"]["sha256"]')"
version="$(json 'd["version"]')"
target="$(json "d['build']['perRid']['$rid']['target']")"
lib="$(json "d['build']['perRid']['$rid']['lib']")"
optimize="$(json 'd["build"]["optimize"]')"
defines="$(json '" ".join("-D" + x for x in d["build"]["defines"])')"

zig="${TKCODE_ZIG:-}"
[[ -n "$zig" ]] || zig="$("$root/tools/terminal/install-zig.sh" | tail -1)"
export ZIG_GLOBAL_CACHE_DIR="${ZIG_GLOBAL_CACHE_DIR:-$HOME/.cache/tkcode/zig-global}"

# 1. The pinned header, checked against its SHA-256.
src="$root/artifacts/src/miniaudio-$version"
mkdir -p "$src"
if [[ ! -f "$src/miniaudio.h" ]] || [[ "$(shasum -a 256 "$src/miniaudio.h" | cut -d' ' -f1)" != "$sha" ]]; then
  curl -fsSL "$url" -o "$src/miniaudio.h.part"
  actual="$(shasum -a 256 "$src/miniaudio.h.part" | cut -d' ' -f1)"
  [[ "$actual" == "$sha" ]] || { echo "SHA-256 mismatch for miniaudio.h: $actual" >&2; exit 1; }
  mv "$src/miniaudio.h.part" "$src/miniaudio.h"
fi

# 2. Build into a fresh directory, then copy the library out.
out="$root/artifacts/native/$rid"
work="$root/artifacts/native-build/audio-$rid"
mkdir -p "$out"
rm -rf "${work:?}"
mkdir -p "$work"
extra=()
case "$rid" in
  osx-*)
    sdk="$(xcrun --sdk macosx --show-sdk-path)"
    extra=(--sysroot "$sdk" -I"$sdk/usr/include" -F"$sdk/System/Library/Frameworks" -L"$sdk/usr/lib"
           -framework CoreFoundation -framework CoreAudio -framework AudioToolbox
           -Wno-availability -Wno-nullability-completeness -Wno-nullability-extension
           -Wl,-install_name,@rpath/libtkcode_audio.dylib);;
  linux-*)
    extra=(-lpthread -lm -ldl -s);;
  win-*)
    extra=(-s);;
esac
echo "zig $("$zig" version): cc -target $target $optimize ($rid)"
"$zig" cc -target "$target" -shared $optimize -fvisibility=hidden $defines \
  -I"$src" -I"$root/native/miniaudio" \
  "$root/native/miniaudio/tkcode_audio.c" -o "$work/$lib" "${extra[@]}"
cp "$work/$lib" "$out/$lib"
echo "$version" > "$out/miniaudio-version.txt"
ls -la "$out/$lib"
