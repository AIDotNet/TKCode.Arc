#!/usr/bin/env bash
# Makes the backend test clip tests/Video/Media/red-blue-2s.mp4: 320x240, 30 fps, 0-1 s solid red, 1-2 s solid blue,
# with a 440 Hz stereo tone. H.264 + AAC in MP4, encoded with Apple's system encoders (VideoToolbox, AudioToolbox) so no
# GPL encoder is involved. Needs macOS and ffmpeg (Homebrew). The committed file is the reference; rerun only to change it.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/tests/Video/Media/red-blue-2s.mp4"
mkdir -p "$(dirname "$out")"
ffmpeg -hide_banner -loglevel error -y \
  -f lavfi -i "color=c=0xFF0000:s=320x240:r=30:d=1" \
  -f lavfi -i "color=c=0x0000FF:s=320x240:r=30:d=1" \
  -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=2" \
  -filter_complex "[0:v][1:v]concat=n=2:v=1:a=0,format=yuv420p[v];[2:a]aformat=channel_layouts=stereo[a]" \
  -map "[v]" -map "[a]" \
  -c:v h264_videotoolbox -profile:v main -b:v 400k -g 30 \
  -c:a aac_at -b:a 96k \
  -movflags +faststart "$out"
shasum -a 256 "$out"
