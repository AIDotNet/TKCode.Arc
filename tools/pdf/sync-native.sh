#!/usr/bin/env bash
# Downloads the pinned PDFium build for each RID from bblanchon/pdfium-binaries, verifies its SHA-256 and places the library in
# src/TKCode.Arc.Pdf.Native/runtimes/<rid>/native and the license files in src/TKCode.Arc.Pdf.Native/licenses, from where the
# package picks them up. Neither folder is committed.
# Usage: tools/pdf/sync-native.sh [<rid>...]     (default: win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64)
#
# A download is untrusted until its checksum matches, and even then nothing from it is executed: each archive is extracted into
# a new empty directory and only the library and the license texts are copied out. These are the builds without V8 and XFA, so
# PDF JavaScript cannot run. To move to a newer release, change the tag and every checksum below, in one commit.
set -euo pipefail

tag="chromium/8165"
base="https://github.com/bblanchon/pdfium-binaries/releases/download/$tag"
all_rids="win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64"

# The archive and its checksum for a RID. Computed from the release assets on 2026-11-23.
archive_of() {
  case "$1" in
    win-x64)     echo "pdfium-win-x64.tgz ca7338ae7779b8b1b82f4580305c4d762d214f8df1d54b47647fe3cc75989ded" ;;
    win-arm64)   echo "pdfium-win-arm64.tgz d65e405c59cffe6fe36c0935b90d40ddf3ce75ef284f3168a28dcf00d0388131" ;;
    linux-x64)   echo "pdfium-linux-x64.tgz ba7865e16c0271daab75256790be06c26272190afe6e2a70eae0bb39bb99089c" ;;
    linux-arm64) echo "pdfium-linux-arm64.tgz 338f5dcc907a20331812ae8ab01b16ff18966f4a247d790c8ad8fb49029d768b" ;;
    osx-x64)     echo "pdfium-mac-x64.tgz 07285915b564e46a432a9909dbc1f15892885ca6489e3faeefc70dee77ed371a" ;;
    osx-arm64)   echo "pdfium-mac-arm64.tgz 112e74d717f3e0310273b70f3c8780e8a61ff11b34834124465c8305c841d350" ;;
    *) return 1 ;;
  esac
}

library_in_archive() {
  case "$1" in
    win-*)   echo "bin/pdfium.dll" ;;
    linux-*) echo "lib/libpdfium.so" ;;
    osx-*)   echo "lib/libpdfium.dylib" ;;
  esac
}

sha256_of() {
  if command -v sha256sum > /dev/null 2>&1; then
    sha256sum "$1" | cut -d' ' -f1
  else
    shasum -a 256 "$1" | cut -d' ' -f1
  fi
}

# Refuses a link or anything else that is not a plain file: an archive must not be able to point the copy somewhere else.
copy_plain_file() {
  if [ -L "$1" ] || [ ! -f "$1" ]; then
    echo "$1 is not a plain file" >&2
    exit 1
  fi
  cp "$1" "$2"
  chmod 644 "$2"
}

root="$(cd "$(dirname "$0")/../.." && pwd)"
package="$root/src/TKCode.Arc.Pdf.Native"

if [ "$#" -gt 0 ]; then
  rids="$*"
else
  rids="$all_rids"
fi
for rid in $rids; do
  archive_of "$rid" > /dev/null || { echo "unknown RID '$rid' (known: $all_rids)" >&2; exit 2; }
done

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

for rid in $rids; do
  set -- $(archive_of "$rid")
  archive="$1"
  expected="$2"
  library="$(library_in_archive "$rid")"

  download="$work/$rid-download"
  mkdir "$download"
  curl -fsSL --retry 3 --proto '=https' --tlsv1.2 -o "$download/$archive" "$base/$archive"
  actual="$(sha256_of "$download/$archive")"
  if [ "$actual" != "$expected" ]; then
    echo "$archive: SHA-256 mismatch" >&2
    echo "  expected $expected" >&2
    echo "  actual   $actual" >&2
    exit 1
  fi

  extracted="$work/$rid-extracted"
  mkdir "$extracted"
  tar -xzf "$download/$archive" -C "$extracted" "$library" LICENSE licenses args.gn
  grep -q '^pdf_enable_v8 = false' "$extracted/args.gn" || { echo "$archive: not a build without V8" >&2; exit 1; }

  mkdir -p "$package/runtimes/$rid/native"
  copy_plain_file "$extracted/$library" "$package/runtimes/$rid/native/$(basename "$library")"

  # The archives carry the same license texts (only the line endings differ), so the last one wins.
  mkdir -p "$package/licenses"
  copy_plain_file "$extracted/LICENSE" "$package/licenses/pdfium-binaries.txt"
  for license in "$extracted"/licenses/*; do
    copy_plain_file "$license" "$package/licenses/$(basename "$license")"
  done

  rm -rf "$download" "$extracted"
  echo "$rid: $(basename "$library") ($tag, sha256 ok)"
done
