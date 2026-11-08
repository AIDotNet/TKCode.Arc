#!/usr/bin/env bash
# Makes the embedded text track test clips from tests/Video/Media/red-blue-2s.mp4 (video and audio copied, not re-encoded):
#   tests/Video/Media/captions-2s.mp4  two MP4 timed text (tx3g / mov_text) tracks: English captions, Chinese subtitles
#   tests/Video/Media/captions-2s.mkv  the same two tracks as Matroska SRT (S_TEXT/UTF8)
# Cues: English 0.2-0.9 s "Hello <b>bold</b>", 1.1-1.8 s "Second <i>cue</i>"; Chinese 0.2-0.9 s "你好", 1.1-1.8 s "第二条".
# Needs ffmpeg. The committed files are the reference; rerun only to change them.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
media="$root/tests/Video/Media"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cat > "$work/en.srt" <<'SRT'
1
00:00:00,200 --> 00:00:00,900
Hello <b>bold</b>

2
00:00:01,100 --> 00:00:01,800
Second <i>cue</i>
SRT
cat > "$work/zh.srt" <<'SRT'
1
00:00:00,200 --> 00:00:00,900
你好

2
00:00:01,100 --> 00:00:01,800
第二条
SRT
common=(-hide_banner -loglevel error -y -i "$media/red-blue-2s.mp4" -i "$work/en.srt" -i "$work/zh.srt"
        -map 0:v -map 0:a -map 1 -map 2 -c:v copy -c:a copy
        -metadata:s:s:0 language=eng -metadata:s:s:0 title=English -disposition:s:0 0
        -metadata:s:s:1 language=chi -metadata:s:s:1 title=中文 -disposition:s:1 0)
ffmpeg "${common[@]}" -c:s mov_text -movflags +faststart "$media/captions-2s.mp4"
ffmpeg "${common[@]}" -c:s srt "$media/captions-2s.mkv"
shasum -a 256 "$media/captions-2s.mp4" "$media/captions-2s.mkv"
