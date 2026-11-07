#!/usr/bin/env bash
# Tests the Linux video shim (native/video/linux/tkcode_video_gst.c) for real, in Docker, for the Docker host's
# architecture (linux-arm64 on Apple silicon, linux-x64 elsewhere).
#
#   tools/video/test-linux.sh            build the shim + the C harness (native/video/linux/test/play.c) with Zig, then
#                                        1. run the harness against tests/Video/Media/red-blue-2s.mp4 in Debian
#                                           bookworm with GStreamer (plugins base, good, bad, libav);
#                                        1b. again without pulling frames while playing (TKV_NOPULL=1);
#                                        2. run it again under valgrind (memcheck; fails on memory errors, prints the
#                                           leak summary: GStreamer/GLib/OpenAL keep one-time allocations from plugin
#                                           loading, which do not grow with TKV_CYCLES; TKV_VALGRIND=0 skips);
#                                        3. run it in plain bookworm-slim without GStreamer: expects TKV_E_UNAVAILABLE.
#   tools/video/test-linux.sh dotnet [cmd...]
#                                        build the shim, then run cmd (default: dotnet test of
#                                        tests/TKCode.Arc.Video.Native.Tests) in an image with the .NET 10 SDK, the
#                                        SDK pinned by global.json and the same GStreamer packages. The workspace is
#                                        mounted read-only and copied to /src inside, so host bin/obj stay untouched.
# Images: tkcode-video-gst:bookworm, tkcode-video-dotnet:10 (built on first use; TKV_REBUILD=1 rebuilds).
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
mode="${1:-gst}"
[[ $# -gt 0 ]] && shift

case "$(docker info --format '{{.Architecture}}')" in
  aarch64|arm64) rid=linux-arm64; target=aarch64-linux-gnu.2.28;;
  x86_64|amd64)  rid=linux-x64;   target=x86_64-linux-gnu.2.28;;
  *) echo "unsupported docker architecture" >&2; exit 2;;
esac

gst_packages="gstreamer1.0-plugins-base gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-libav gstreamer1.0-tools"

image() { # name, Dockerfile text, [build args...]
  local name="$1" text="$2"; shift 2
  if [[ "${TKV_REBUILD:-0}" == "1" ]] || ! docker image inspect "$name" >/dev/null 2>&1; then
    echo "== docker build $name"
    printf '%s\n' "$text" | docker build -t "$name" "$@" -
  fi
}

"$root/tools/video/build-linux.sh" "$rid"
lib="$root/artifacts/native/$rid/libtkcode_video.so"

if [[ "$mode" == "dotnet" ]]; then
  sdk="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["sdk"]["version"])' "$root/global.json")"
  image tkcode-video-dotnet:10 "FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends $gst_packages curl ca-certificates \\
    && rm -rf /var/lib/apt/lists/*
ARG SDK_VERSION
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \\
    && bash /tmp/dotnet-install.sh --version \"\$SDK_VERSION\" --install-dir /usr/share/dotnet \\
    && rm /tmp/dotnet-install.sh && dotnet --list-sdks
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1" --build-arg "SDK_VERSION=$sdk"
  if [[ $# -eq 0 ]]; then
    set -- dotnet test tests/TKCode.Arc.Video.Native.Tests -f net10.0
  fi
  cmd="$(printf '%q ' "$@")"
  echo "== $cmd (in tkcode-video-dotnet:10)"
  exec docker run --rm -v "$root:/work:ro" -v tkcode-video-nuget:/root/.nuget/packages tkcode-video-dotnet:10 bash -c "
    set -euo pipefail
    mkdir -p /src && cd /work
    tar --exclude=./.git --exclude=./.work --exclude='*/bin' --exclude='*/obj' --exclude=./artifacts/native-build -cf - . | tar -xf - -C /src
    cd /src && $cmd"
fi

[[ "$mode" == "gst" ]] || { echo "usage: $0 [dotnet [cmd...]]" >&2; exit 2; }

# The harness links the shim directly and finds it next to itself ($ORIGIN).
zig="${TKCODE_ZIG:-}"
[[ -n "$zig" ]] || zig="$("$root/tools/terminal/install-zig.sh" | tail -1)"
export ZIG_GLOBAL_CACHE_DIR="${ZIG_GLOBAL_CACHE_DIR:-$HOME/.cache/tkcode/zig-global}"
bin="$root/artifacts/native-build/video-test-$rid"
rm -rf "${bin:?}"; mkdir -p "$bin"
cp "$lib" "$bin/"
"$zig" cc -target "$target" -O1 -g -std=c11 -Wall -Wextra -Werror "$root/native/video/linux/test/play.c" \
  -o "$bin/play" -L"$bin" -ltkcode_video -lpthread -lm -Wl,-rpath,'$ORIGIN'

image tkcode-video-gst:bookworm "FROM debian:bookworm-slim
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends $gst_packages valgrind \\
    && rm -rf /var/lib/apt/lists/*"

clip=/work/tests/Video/Media/red-blue-2s.mp4
play=/work/artifacts/native-build/video-test-$rid/play
status=0

echo "== 1. harness with GStreamer ($rid, debian:bookworm-slim)"
docker run --rm -v "$root:/work:ro" tkcode-video-gst:bookworm bash -c \
  "gst-inspect-1.0 --version | head -2; $play $clip" || status=1

echo "== 1b. harness without pulling frames while playing (TKV_NOPULL=1)"
docker run --rm -e TKV_NOPULL=1 -v "$root:/work:ro" tkcode-video-gst:bookworm bash -c "$play $clip 2>/dev/null | tail -n 1" \
  | tee /dev/stderr | grep -q '^PASS' || status=1

if [[ "${TKV_VALGRIND:-1}" == "1" ]]; then
  echo "== 2. harness under valgrind (memcheck)"
  docker run --rm -v "$root:/work:ro" -e TKV_SLOW=12 tkcode-video-gst:bookworm bash -c \
    "valgrind --error-exitcode=99 --leak-check=full --show-leak-kinds=definite --errors-for-leak-kinds=none \
       --num-callers=30 $play $clip > /tmp/out.txt 2> /tmp/vg.txt; rc=\$?; \
     tail -n 3 /tmp/out.txt; \
     grep -E 'ERROR SUMMARY|definitely lost:|indirectly lost:|possibly lost:|still reachable:' /tmp/vg.txt; \
     echo valgrind exit \$rc; exit \$rc" || status=1
fi

echo "== 3. harness without GStreamer (debian:bookworm-slim)"
docker run --rm -v "$root:/work:ro" debian:bookworm-slim "$play" --unavailable || status=1

[[ $status -eq 0 ]] && echo "ALL PASSED" || echo "SOME CHECKS FAILED"
exit $status
