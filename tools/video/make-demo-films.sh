#!/usr/bin/env bash
# Makes the Gallery's animated demo clips in gallery/TKCode.Arc.Gallery/Media/Video/ from the Blender Foundation's official
# trailers (CC BY 3.0: the Gallery page credits them):
#   sintel.mp4, sintel-poster.webp               Sintel trailer, (c) Blender Foundation, durian.blender.org
#   big-buck-bunny.mp4, big-buck-bunny-poster.webp   Big Buck Bunny trailer, (c) Blender Foundation, peach.blender.org
# The downloads are checked against the hashes below. Sintel is only remuxed; Big Buck Bunny is re-encoded to a smaller size
# with Apple's system encoders. Needs macOS, curl, ffmpeg and cwebp. The committed files are the reference.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/gallery/TKCode.Arc.Gallery/Media/Video"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

fetch() { # url, file, sha256
  curl -fsSL -o "$work/$2" "$1"
  echo "$3  $work/$2" | shasum -a 256 -c - >/dev/null
}
fetch https://download.blender.org/durian/trailer/sintel_trailer-480p.mp4 sintel.mp4 \
  b670602fa00934ca27c4351bb0efe7ea7a07fae57284e44226025eeed7c51254
fetch https://download.blender.org/peach/trailer/trailer_480p.mov bunny.mov \
  36801b74638c12be9aa587e93cd18edfc9bc51a1c089ab2a19ee42beed9f497d

ffmpeg -hide_banner -loglevel error -y -i "$work/sintel.mp4" -map 0:v -map 0:a -c copy -movflags +faststart "$out/sintel.mp4"
ffmpeg -hide_banner -loglevel error -y -i "$work/bunny.mov" -map 0:v -map 0:a -vf "scale=854:480,setsar=1,format=yuv420p" \
  -c:v h264_videotoolbox -profile:v main -b:v 1100k -g 50 -c:a aac_at -b:a 96k -movflags +faststart "$out/big-buck-bunny.mp4"

poster() { # clip, seconds
  ffmpeg -hide_banner -loglevel error -y -ss "$2" -i "$out/$1.mp4" -frames:v 1 "$work/$1.png"
  cwebp -quiet -q 80 "$work/$1.png" -o "$out/$1-poster.webp"
}
poster sintel "${SINTEL_POSTER_AT:-33}"
poster big-buck-bunny "${BUNNY_POSTER_AT:-16}"
shasum -a 256 "$out/sintel.mp4" "$out/big-buck-bunny.mp4"
ls -la "$out"/sintel* "$out"/big-buck-bunny*
