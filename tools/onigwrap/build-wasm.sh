#!/usr/bin/env bash
# Builds gallery/TKCode.Arc.Gallery.Browser/native/onigwrap/<emscripten>/st/libonigwrap.a, the Oniguruma regular expressions
# that TextMateSharp (code highlighting) calls in the WebAssembly host.
#
# The Onigwrap package ships WebAssembly archives up to Emscripten 3.1.56 (.NET 9 and 10). .NET 11 links with Emscripten 6, which
# cannot link them, so the Browser project points OnigwrapWasmLibraryPath at the archive this script makes. It compiles the same
# sources as the package (Onigwrap v1.0.11, Oniguruma v6.9.10, both pinned by commit) with the Emscripten of the installed .NET
# WebAssembly workload. Run it again after a .NET update moves the workload to another Emscripten version (the publish fails with
# "Could not find NativeFileReference .../native/onigwrap/<version>/st/libonigwrap.a").
#
#   tools/onigwrap/build-wasm.sh [<emscripten version>]
#
# Without an argument it uses the newest Emscripten pack under ~/.dotnet/packs (or $DOTNET_ROOT/packs). Needs git and network access.
set -euo pipefail

ONIGWRAP_COMMIT=dd8a1cbbe3190c18033b96f2922b95c1761ae793   # v1.0.11
ONIGURUMA_COMMIT=4ef89209a239c1aea328cf13c05a2807e5c146d1  # v6.9.10

REPO=$(cd "$(dirname "$0")/../.." && pwd)
PACKS=${DOTNET_ROOT:-$HOME/.dotnet}/packs

# The workload's Emscripten: four packs (Sdk, Cache, Node, Python) named after the Emscripten release they track.
sdk_pack=$(ls -d "$PACKS"/Microsoft.NET.Runtime.Emscripten.${1:-*}.Sdk.*/*/ 2>/dev/null | sort -V | tail -1)
if [ -z "$sdk_pack" ]; then
    echo "No Emscripten pack under $PACKS; install the workload first (dotnet workload install wasm-tools)." >&2
    exit 1
fi
pack_dir=$(dirname "$sdk_pack")
pack_name=$(basename "$pack_dir")
pack_version=$(basename "$sdk_pack")
pack_of() { echo "$PACKS/${pack_name/.Sdk./.$1.}/$pack_version/tools"; }
TOOLS=${sdk_pack}tools
EMSCRIPTEN_VERSION=$(tr -d '"\n' < "$TOOLS/emscripten/emscripten-version.txt")

# The environment the .NET SDK gives emcc (Microsoft.NET.Runtime.WebAssembly.Sdk, _SetupEmscripten).
export DOTNET_EMSCRIPTEN_LLVM_ROOT=$TOOLS/bin
export DOTNET_EMSCRIPTEN_BINARYEN_ROOT=$TOOLS
export DOTNET_EMSCRIPTEN_NODE_JS=$(pack_of Node)/bin/node
export EM_CACHE=$(pack_of Cache)/emscripten/cache
export EM_FROZEN_CACHE=1
export PYTHONUTF8=1
export PATH=$TOOLS/bin:$TOOLS/emscripten:$(pack_of Python)/bin:$PATH

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
fetch() {
    git -C "$WORK" init -q "$1"
    git -C "$WORK/$1" fetch -q --depth 1 "$2" "$3"
    git -C "$WORK/$1" checkout -q FETCH_HEAD
}
fetch onigwrap https://github.com/aikawayataro/Onigwrap.git "$ONIGWRAP_COMMIT"
fetch oniguruma https://github.com/kkos/oniguruma.git "$ONIGURUMA_COMMIT"

# Oniguruma's config.h for wasm32 (what its configure script finds there).
cat > "$WORK/oniguruma/src/config.h" <<'EOF'
#define HAVE_ALLOCA 1
#define HAVE_ALLOCA_H 1
#define HAVE_STDINT_H 1
#define HAVE_SYS_TIMES_H 1
#define HAVE_SYS_TIME_H 1
#define HAVE_SYS_TYPES_H 1
#define HAVE_UNISTD_H 1
#define HAVE_INTTYPES_H 1
#define SIZEOF_INT 4
#define SIZEOF_LONG 4
#define SIZEOF_LONG_LONG 8
#define SIZEOF_VOIDP 4
#define PACKAGE "onig"
#define PACKAGE_VERSION "6.9.10"
#define VERSION "6.9.10"
EOF

# The library sources of Oniguruma's CMakeLists.txt (without the POSIX API), then Onigwrap's wrapper.
cd "$WORK/oniguruma/src"
SOURCES="regerror.c regparse.c regext.c regcomp.c regexec.c reggnu.c regenc.c regsyntax.c regtrav.c regversion.c st.c onig_init.c
    unicode.c ascii.c utf8.c utf16_be.c utf16_le.c utf32_be.c utf32_le.c euc_jp.c sjis.c iso8859_1.c iso8859_2.c iso8859_3.c
    iso8859_4.c iso8859_5.c iso8859_6.c iso8859_7.c iso8859_8.c iso8859_9.c iso8859_10.c iso8859_11.c iso8859_13.c iso8859_14.c
    iso8859_15.c iso8859_16.c euc_tw.c euc_kr.c big5.c gb18030.c koi8_r.c cp1251.c euc_jp_prop.c sjis_prop.c unicode_unfold_key.c
    unicode_fold1_key.c unicode_fold2_key.c unicode_fold3_key.c"
OBJECTS=()
for source in $SOURCES; do
    emcc -O2 -c "$source" -o "${source%.c}.o"
    OBJECTS+=("${source%.c}.o")
done
emcc -O2 -c "$WORK/onigwrap/onigwrap/onigwrap.c" -I. -o onigwrap.o
OBJECTS+=(onigwrap.o)

OUT=$REPO/gallery/TKCode.Arc.Gallery.Browser/native/onigwrap/$EMSCRIPTEN_VERSION/st
mkdir -p "$OUT"
rm -f "$OUT/libonigwrap.a"
emar rcs "$OUT/libonigwrap.a" "${OBJECTS[@]}"
echo "Built $OUT/libonigwrap.a with Emscripten $EMSCRIPTEN_VERSION ($(wc -c < "$OUT/libonigwrap.a" | tr -d ' ') bytes)"
