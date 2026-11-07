#!/usr/bin/env bash
# Builds the Linux TKCode.Arc.Video.Native shim (native/video/linux/tkcode_video_gst.c) with the Zig in
# native/toolchain.json. GStreamer is loaded at run time with dlopen, so no GStreamer headers or libraries are needed
# and the result links only libc (libdl / libpthread are part of glibc 2.34+, and linked explicitly for 2.28).
# Output: artifacts/native/<rid>/libtkcode_video.so (glibc 2.28 baseline).
# Usage: tools/video/build-linux.sh [linux-x64|linux-arm64]   (default: both)
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"

zig="${TKCODE_ZIG:-}"
[[ -n "$zig" ]] || zig="$("$root/tools/terminal/install-zig.sh" | tail -1)"
export ZIG_GLOBAL_CACHE_DIR="${ZIG_GLOBAL_CACHE_DIR:-$HOME/.cache/tkcode/zig-global}"

rids=("$@")
[[ ${#rids[@]} -gt 0 ]] || rids=(linux-x64 linux-arm64)

for rid in "${rids[@]}"; do
  case "$rid" in
    linux-x64)   target=x86_64-linux-gnu.2.28;;
    linux-arm64) target=aarch64-linux-gnu.2.28;;
    *) echo "unsupported rid: $rid (linux-x64, linux-arm64)" >&2; exit 2;;
  esac
  out="$root/artifacts/native/$rid"
  work="$root/artifacts/native-build/video-$rid"
  rm -rf "${work:?}"
  mkdir -p "$out" "$work"
  echo "zig $("$zig" version): cc -target $target -O2 ($rid)"
  "$zig" cc -target "$target" -shared -fPIC -O2 -fvisibility=hidden -std=c11 -Wall -Wextra -Werror \
    -Wno-unused-parameter \
    "$root/native/video/linux/tkcode_video_gst.c" -o "$work/libtkcode_video.so" \
    -ldl -lpthread -s -Wl,-soname,libtkcode_video.so
  cp "$work/libtkcode_video.so" "$out/libtkcode_video.so"

  # Checks: the tkv_* exports (ABI 2; tkv_player_timeline_sync is optional and not exported: CPU frames only), and no GStreamer / GLib among the DT_NEEDED entries.
  if command -v llvm-readelf >/dev/null 2>&1; then readelf=llvm-readelf
  elif command -v readelf >/dev/null 2>&1; then readelf=readelf
  else readelf=""; fi
  if [[ -n "$readelf" ]]; then
    needed="$("$readelf" -d "$out/libtkcode_video.so" | grep NEEDED || true)"
    exports="$("$readelf" --dyn-syms -W "$out/libtkcode_video.so" | awk '$5=="GLOBAL" && $7!="UND" {print $8}' | sort)"
  else
    needed="$(python3 "$root/native/video/linux/test/elfinfo.py" needed "$out/libtkcode_video.so")"
    exports="$(python3 "$root/native/video/linux/test/elfinfo.py" exports "$out/libtkcode_video.so")"
  fi
  echo "needed: $(echo "$needed" | tr '\n' ' ')"
  if echo "$needed" | grep -Eqi 'gst|glib|gobject'; then
    echo "error: libtkcode_video.so links GStreamer/GLib at load time" >&2; exit 1
  fi
  expected="tkv_can_play tkv_frame_kinds tkv_gpu_handle_type tkv_initialize tkv_player_acquire_frame tkv_player_buffered
tkv_player_create tkv_player_destroy tkv_player_last_error tkv_player_open tkv_player_pause tkv_player_play
tkv_player_position tkv_player_redeliver_frame tkv_player_release_frame tkv_player_seek tkv_player_set_loop tkv_player_set_rate
tkv_player_set_volume tkv_version"
  for sym in $expected; do
    echo "$exports" | grep -qx "$sym" || { echo "error: missing export $sym" >&2; exit 1; }
  done
  extra="$(echo "$exports" | grep -v '^tkv_' || true)"
  [[ -z "$extra" ]] || { echo "error: unexpected exports: $extra" >&2; exit 1; }
  echo "exports: $(echo "$exports" | wc -l | tr -d ' ') tkv_* symbols"
  ls -la "$out/libtkcode_video.so"
done
