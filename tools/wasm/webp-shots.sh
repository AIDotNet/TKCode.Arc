#!/bin/sh
# Turns the PNG screenshots of a published WebAssembly site into lossless WebP, in place:
#   sh tools/wasm/webp-shots.sh <publish folder>/wwwroot/shots
# The pixels are identical and a screenshot shrinks to about a quarter (UI captures are flat color and text). The Browser
# project runs this after a publish; the site's HttpShotSource asks for .webp first and falls back to .png, so a site
# whose PNGs were left alone still works. Needs cwebp (brew install webp, apt install webp).
set -eu
dir="$1"
if ! command -v cwebp >/dev/null 2>&1; then
    echo "webp-shots: cwebp is not installed" >&2
    exit 3
fi
jobs="$(getconf _NPROCESSORS_ONLN 2>/dev/null || echo 4)"
# The SDK also leaves a .br and a .gz next to each PNG; the WebP is not compressible, so none is made for it.
find "$dir" -name '*.png' -print0 \
    | xargs -0 -n 1 -P "$jobs" sh -c 'cwebp -quiet -lossless -z 9 "$0" -o "${0%.png}.webp" && rm -f "$0" "$0.br" "$0.gz"'
