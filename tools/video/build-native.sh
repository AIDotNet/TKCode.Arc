#!/usr/bin/env bash
# Builds the TKCode.Arc.Video.Native shim (native/video/tkcode_video.h) for one RID by dispatching to the platform script:
# tools/video/build-mac.sh (osx-*, Xcode clang, run on macOS), build-win.sh (win-*), build-linux.sh (linux-*).
# Every platform script takes the RID as $1 and writes artifacts/native/<rid>/<lib>
# (libtkcode_video.dylib, tkcode_video.dll, libtkcode_video.so). Then run tools/video/sync-native.sh.
# Usage: tools/video/build-native.sh [rid]   (default: the host RID)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"

rid="${1:-}"
if [[ -z "$rid" ]]; then
  case "$(uname -s)" in Darwin) os=osx;; Linux) os=linux;; *) os=win;; esac
  case "$(uname -m)" in arm64|aarch64) arch=arm64;; *) arch=x64;; esac
  rid="$os-$arch"
fi

case "$rid" in
  osx-*) script="$here/build-mac.sh";;
  win-*) script="$here/build-win.sh";;
  linux-*) script="$here/build-linux.sh";;
  *) echo "unknown rid: $rid" >&2; exit 2;;
esac
[[ -f "$script" ]] || { echo "missing $script for $rid" >&2; exit 1; }
exec bash "$script" "$rid"
