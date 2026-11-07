#!/usr/bin/env bash
# Makes the Gallery's video demo media in gallery/TKCode.Arc.Gallery/Media/Video/:
#   aurora.mp4         12 s, 1280x720, 30 fps: three 4 s chapters of generated moving gradients joined by crossfades, with a
#                      synthesized chord per chapter. H.264 + AAC, encoded with Apple's system encoders (VideoToolbox,
#                      AudioToolbox), so no GPL encoder and no third-party footage is involved.
#   aurora-poster.webp the first chapter's frame at 1 s (lossless WebP; needs cwebp, else PNG via ffmpeg).
# Captions and chapters (aurora.*.vtt) are hand-written next to it. Needs macOS and ffmpeg (Homebrew).
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/gallery/TKCode.Arc.Gallery/Media/Video"
mkdir -p "$out"
size=1280x720
ffmpeg -hide_banner -loglevel error -y \
  -f lavfi -i "gradients=s=$size:r=30:d=4.5:type=linear:speed=0.012:n=4:c0=0x1d4ed8:c1=0x7c3aed:c2=0x0ea5e9:c3=0x22d3ee:seed=7" \
  -f lavfi -i "gradients=s=$size:r=30:d=4.5:type=spiral:speed=0.010:n=3:c0=0xdb2777:c1=0xf59e0b:c2=0x7c3aed:seed=11" \
  -f lavfi -i "gradients=s=$size:r=30:d=4:type=circular:speed=0.014:n=3:c0=0x059669:c1=0x0ea5e9:c2=0x1e293b:seed=23" \
  -f lavfi -i "aevalsrc=exprs='0.10*sin(2*PI*220*t)+0.07*sin(2*PI*277.18*t)+0.06*sin(2*PI*329.63*t)':s=48000:d=4" \
  -f lavfi -i "aevalsrc=exprs='0.10*sin(2*PI*196*t)+0.07*sin(2*PI*246.94*t)+0.06*sin(2*PI*293.66*t)':s=48000:d=4" \
  -f lavfi -i "aevalsrc=exprs='0.10*sin(2*PI*174.61*t)+0.07*sin(2*PI*220*t)+0.06*sin(2*PI*261.63*t)':s=48000:d=4" \
  -filter_complex "[0:v][1:v]xfade=transition=fade:duration=0.5:offset=4[v01];[v01][2:v]xfade=transition=fade:duration=0.5:offset=8,format=yuv420p[v];\
[3:a]afade=t=in:d=0.4,afade=t=out:st=3.6:d=0.4[a0];[4:a]afade=t=in:d=0.4,afade=t=out:st=3.6:d=0.4[a1];[5:a]afade=t=in:d=0.4,afade=t=out:st=3.4:d=0.6[a2];\
[a0][a1][a2]concat=n=3:v=0:a=1,aformat=channel_layouts=stereo[a]" \
  -map "[v]" -map "[a]" -c:v h264_videotoolbox -profile:v main -b:v 1800k -g 60 -c:a aac_at -b:a 96k \
  -movflags +faststart "$out/aurora.mp4"
if command -v cwebp >/dev/null; then
  ffmpeg -hide_banner -loglevel error -y -ss 1 -i "$out/aurora.mp4" -frames:v 1 "$out/aurora-poster.png"
  cwebp -quiet -lossless "$out/aurora-poster.png" -o "$out/aurora-poster.webp" && rm "$out/aurora-poster.png"
else
  ffmpeg -hide_banner -loglevel error -y -ss 1 -i "$out/aurora.mp4" -frames:v 1 -c:v libwebp -lossless 0 -quality 85 "$out/aurora-poster.webp"
fi
shasum -a 256 "$out/aurora.mp4" "$out/aurora-poster.webp"
