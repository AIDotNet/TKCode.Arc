#!/usr/bin/env bash
# Builds the Windows video shim (native/video/win/tkcode_video_mf.c, Media Foundation) with the Zig in
# native/toolchain.json, cross-compiling from macOS or Linux (zig's mingw headers have mfmediaengine.h and d3d11.h).
# Output: artifacts/native/<rid>/tkcode_video.dll, plus the hand-run harness
#         artifacts/native-test/<rid>/tkv_play_test.exe (native/video/win/test/play.c, with a copy of the DLL next to it).
#         Run the harness by hand on Windows: tkv_play_test.exe <path to tests/Video/Media/red-blue-2s.mp4>; it ends with
#         "RESULT: PASS" and exit code 0 when the shim works.
# Then checks that every TKV_API function is exported and that the DLL imports only system libraries.
# Usage: tools/video/build-win.sh [win-x64|win-arm64]   (default win-x64)
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"

rid="${1:-win-x64}"
case "$rid" in
  win-x64)   target=x86_64-windows-gnu;;
  win-arm64) target=aarch64-windows-gnu;;
  *) echo "unknown rid: $rid (win-x64 or win-arm64)" >&2; exit 2;;
esac

zig="${TKCODE_ZIG:-}"
[[ -n "$zig" ]] || zig="$("$root/tools/terminal/install-zig.sh" | tail -1)"
export ZIG_GLOBAL_CACHE_DIR="${ZIG_GLOBAL_CACHE_DIR:-$HOME/.cache/tkcode/zig-global}"

src="$root/native/video/win"
header="$root/native/video/tkcode_video.h"
out="$root/artifacts/native/$rid"
test_out="$root/artifacts/native-test/$rid"
work="$root/artifacts/native-build/video-$rid"
rm -rf "${work:?}"
mkdir -p "$work" "$out" "$test_out"

# Warnings are errors: the shim cannot be run where it is built, so the compiler is the first line of review.
cflags=(-O2 -std=c11 -Wall -Wextra -Werror -fvisibility=hidden)
# mfplat.dll is loaded at run time (N editions lack it); GUIDs come from initguid.h, so neither uuid nor mfuuid is
# linked. d3d11, dxgi, ole32 and oleaut32 exist on every Windows 10.
libs=(-ld3d11 -ldxgi -lole32 -loleaut32)

echo "zig $("$zig" version): cc -target $target ($rid)"
"$zig" cc -target "$target" -shared "${cflags[@]}" -I"$root/native/video" \
  "$src/tkcode_video_mf.c" -o "$work/tkcode_video.dll" "${libs[@]}" -s
"$zig" cc -target "$target" "${cflags[@]}" -Wno-pedantic -I"$root/native/video" \
  "$src/test/play.c" -o "$work/tkv_play_test.exe" -ld3d11 -ldxgi -lole32 -luser32 -s

# Exports and imports, read from the PE tables (zig has no objdump; this works on any host with python3).
pe_tables() {
  python3 - "$1" <<'PY'
import struct, sys
data = open(sys.argv[1], "rb").read()
pe = struct.unpack_from("<I", data, 0x3C)[0]
assert data[pe:pe + 4] == b"PE\0\0"
sections_count = struct.unpack_from("<H", data, pe + 6)[0]
opt_size = struct.unpack_from("<H", data, pe + 20)[0]
opt = pe + 24
magic = struct.unpack_from("<H", data, opt)[0]
dirs = opt + (96 if magic == 0x10B else 112)
sections = []
for i in range(sections_count):
    off = opt + opt_size + 40 * i
    vsize, va, rsize, raw = struct.unpack_from("<IIII", data, off + 8)
    sections.append((va, max(vsize, rsize), raw))
def at(rva):
    for va, size, raw in sections:
        if va <= rva < va + size:
            return rva - va + raw
    raise ValueError(hex(rva))
def cstr(rva):
    o = at(rva)
    return data[o:data.index(b"\0", o)].decode()
exp_rva = struct.unpack_from("<I", data, dirs)[0]
if exp_rva:
    o = at(exp_rva)
    count, names = struct.unpack_from("<I", data, o + 24)[0], struct.unpack_from("<I", data, o + 32)[0]
    for i in range(count):
        print("export", cstr(struct.unpack_from("<I", data, at(names) + 4 * i)[0]))
imp_rva = struct.unpack_from("<I", data, dirs + 8)[0]
o = at(imp_rva)
while True:
    name = struct.unpack_from("<I", data, o + 12)[0]
    if name == 0:
        break
    print("import", cstr(name).lower())
    o += 20
PY
}
tables="$(pe_tables "$work/tkcode_video.dll")"
expected="$(grep -oE 'TKV_API [^(]*\b(tkv_[a-z_]+)\(' "$header" | grep -oE 'tkv_[a-z_]+' | sort -u)"
exports="$(echo "$tables" | awk '$1=="export"{print $2}' | sort -u)"
missing="$(comm -23 <(echo "$expected") <(echo "$exports"))"
extra="$(comm -13 <(echo "$expected") <(echo "$exports"))"
if [[ -n "$missing" ]]; then
  echo "missing exports:" >&2; echo "$missing" >&2; exit 1
fi
if [[ -n "$extra" ]]; then
  echo "unexpected exports:" >&2; echo "$extra" >&2; exit 1
fi
echo "exports: $(echo "$expected" | wc -l | tr -d ' ') TKV_API functions, all present, nothing else"

# Imports: only system DLLs that every Windows 10 has (mfplat.dll is loaded at run time, so it must not appear).
imports="$(echo "$tables" | awk '$1=="import"{print $2}' | sort -u)"
echo "imports: $(echo "$imports" | tr '\n' ' ')"
while read -r dll; do
  case "$dll" in
    kernel32.dll|user32.dll|ole32.dll|oleaut32.dll|d3d11.dll|dxgi.dll|api-ms-win-*.dll|ucrtbase.dll|msvcrt.dll) ;;
    *) echo "unexpected import: $dll" >&2; exit 1;;
  esac
done <<< "$imports"

cp "$work/tkcode_video.dll" "$out/tkcode_video.dll"
cp "$work/tkcode_video.dll" "$test_out/tkcode_video.dll"
cp "$work/tkv_play_test.exe" "$test_out/tkv_play_test.exe"
ls -la "$out/tkcode_video.dll" "$test_out/tkv_play_test.exe"
