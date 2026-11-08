#!/usr/bin/env bash
# Downloads the pinned pdfjs-dist tarball from the npm registry, verifies its SHA-256 and copies the files the viewer needs to
# src/TKCode.Arc.Pdf.Browser/wwwroot/pdfjs, from where the package picks them up. That folder is not committed.
# Usage: tools/pdf/sync-pdfjs.sh
#
# A download is untrusted until its checksum matches, and even then nothing from it is executed: npm is not involved, no lifecycle
# script runs, the archive is extracted into a new empty directory and only named data files are copied out. To move to a newer
# release, change the version and the checksum below in one commit, and read the license files of the new tarball again.
#
# Left out on purpose:
#   standard_fonts/LiberationSans-*.ttf  GPL-2.0 with font exceptions (LICENSE_LIBERATION), unlike everything else here; without
#                                        them pdf.js substitutes the local Helvetica or Arial, or else the browser's sans-serif.
#   wasm/quickjs-eval.*, pdf.sandbox.*   the JavaScript sandbox for scripts in documents, which this viewer never runs.
#   wasm/*_nowasm_fallback.js            decoders for browsers without WebAssembly; a .NET WebAssembly app does not start on those.
set -euo pipefail

version="6.4.299"
expected="86269b40170eb41740ea05ad2102d71d33de4f122b34eb2f478be0ea7779c415"
url="https://registry.npmjs.org/pdfjs-dist/-/pdfjs-dist-$version.tgz"

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
assets="$root/src/TKCode.Arc.Pdf.Browser/wwwroot/pdfjs"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

mkdir "$work/download"
curl -fsSL --retry 3 --proto '=https' --tlsv1.2 -o "$work/download/pdfjs-dist.tgz" "$url"
actual="$(sha256_of "$work/download/pdfjs-dist.tgz")"
if [ "$actual" != "$expected" ]; then
  echo "pdfjs-dist-$version.tgz: SHA-256 mismatch" >&2
  echo "  expected $expected" >&2
  echo "  actual   $actual" >&2
  exit 1
fi

mkdir "$work/extracted"
tar -xzf "$work/download/pdfjs-dist.tgz" -C "$work/extracted" \
  package/LICENSE package/build/pdf.min.mjs package/build/pdf.worker.min.mjs \
  package/cmaps package/standard_fonts package/wasm package/iccs
package="$work/extracted/package"

# A fresh folder, so that nothing of an earlier version stays behind.
rm -rf "$assets"
mkdir -p "$assets/cmaps" "$assets/standard_fonts" "$assets/wasm" "$assets/iccs"

copy_plain_file "$package/LICENSE" "$assets/LICENSE"
copy_plain_file "$package/build/pdf.min.mjs" "$assets/pdf.min.mjs"
copy_plain_file "$package/build/pdf.worker.min.mjs" "$assets/pdf.worker.min.mjs"

for file in "$package"/cmaps/*.bcmap "$package/cmaps/LICENSE"; do
  copy_plain_file "$file" "$assets/cmaps/$(basename "$file")"
done
for file in "$package"/standard_fonts/Foxit*.pfb "$package/standard_fonts/LICENSE_FOXIT"; do
  copy_plain_file "$file" "$assets/standard_fonts/$(basename "$file")"
done
for name in jbig2.wasm openjpeg.wasm qcms_bg.wasm LICENSE_JBIG2 LICENSE_OPENJPEG LICENSE_QCMS LICENSE_PDFJS_JBIG2 LICENSE_PDFJS_OPENJPEG LICENSE_PDFJS_QCMS; do
  copy_plain_file "$package/wasm/$name" "$assets/wasm/$name"
done
for file in "$package"/iccs/*.icc "$package/iccs/LICENSE"; do
  copy_plain_file "$file" "$assets/iccs/$(basename "$file")"
done

echo "pdfjs-dist $version (sha256 ok): $(find "$assets" -type f | wc -l | tr -d ' ') files, $(du -sh "$assets" | cut -f1)"
