#!/usr/bin/env bash
# 安装构建 libghostty-vt 所需的精确 Zig 版本（native/toolchain.json），不修改 PATH。
# 输出：安装后的 zig 可执行文件路径（最后一行）。
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
info="$(python3 -c '
import json, platform, sys
z = json.load(open(sys.argv[1]))["zig"]
arch = {"arm64": "aarch64", "aarch64": "aarch64", "x86_64": "x86_64"}[platform.machine()]
os_ = {"Darwin": "macos", "Linux": "linux"}[platform.system()]
a = z["archives"][f"{arch}-{os_}"]
print(z["version"], z["baseUrl"], a["file"], a["sha256"])
' "$root/native/toolchain.json")"
read -r version base file sha <<< "$info"
dest="${XDG_CACHE_HOME:-$HOME/.cache}/tkcode/zig/$version"
if [[ -x "$dest/zig" ]] && [[ "$("$dest/zig" version)" == "$version" ]]; then
  echo "$dest/zig"; exit 0
fi
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
curl -fsSL "$base$file" -o "$tmp/$file"
actual="$(shasum -a 256 "$tmp/$file" | cut -d' ' -f1)"
[[ "$actual" == "$sha" ]] || { echo "SHA-256 mismatch for $file: $actual" >&2; exit 1; }
mkdir -p "$dest"
tar -xJf "$tmp/$file" -C "$dest" --strip-components=1
"$dest/zig" version >&2
echo "$dest/zig"
