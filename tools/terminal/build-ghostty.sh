#!/usr/bin/env bash
# 构建 libghostty-vt（native/ghostty/ghostty-upstream.json 中 pin 住的提交 + overlay 补丁）。
# 产物：artifacts/native/<rid>/libghostty-vt.{dylib,so} 或 ghostty-vt.dll，以及 artifacts/native/<rid>/include/。
# 用法：tools/terminal/build-ghostty.sh [rid] [--optimize ReleaseFast|ReleaseSafe]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
manifest="$root/native/ghostty/ghostty-upstream.json"

rid=""; optimize=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --optimize) optimize="$2"; shift 2;;
    *) rid="$1"; shift;;
  esac
done
if [[ -z "$rid" ]]; then
  case "$(uname -s)" in Darwin) os=osx;; Linux) os=linux;; *) os=win;; esac
  case "$(uname -m)" in arm64|aarch64) arch=arm64;; *) arch=x64;; esac
  rid="$os-$arch"
fi

json() { python3 -c "import json,sys; d=json.load(open(sys.argv[1])); print(eval(sys.argv[2]))" "$manifest" "$1"; }
repo="$(json 'd["repo"]')"
commit="$(json 'd["commit"]')"
optimize="${optimize:-$(json 'd["build"]["optimize"]')}"
args="$(python3 -c "import json,sys; d=json.load(open(sys.argv[1])); print(' '.join(d['build']['args'] + d['build']['perRid'][sys.argv[2]]))" "$manifest" "$rid")"

zig="${TKCODE_ZIG:-}"
[[ -n "$zig" ]] || zig="$("$root/tools/terminal/install-zig.sh" | tail -1)"
export ZIG_GLOBAL_CACHE_DIR="${ZIG_GLOBAL_CACHE_DIR:-$HOME/.cache/tkcode/zig-global}"

# 1. 源码：独立的检出目录，按提交号定位；每次构建前恢复为干净状态再打补丁
src="$root/artifacts/src/ghostty-${commit:0:12}"
if [[ ! -d "$src/.git" ]]; then
  git init -q "$src"
  git -C "$src" remote add origin "$repo"
fi
if ! git -C "$src" cat-file -e "$commit^{commit}" 2>/dev/null; then
  git -C "$src" fetch -q --depth 1 origin "$commit"
fi
git -C "$src" checkout -q --force "$commit"
git -C "$src" clean -qfd -e zig-pkg -e .zig-cache
[[ "$(git -C "$src" rev-parse HEAD)" == "$commit" ]] || { echo "commit mismatch" >&2; exit 1; }

# 2. overlay 补丁（按清单顺序；打不上直接失败）
python3 -c "import json,sys; [print(o['file']) for o in json.load(open(sys.argv[1]))['overlays']]" "$manifest" |
while read -r patch; do
  git -C "$src" apply --check "$root/native/ghostty/$patch"
  git -C "$src" apply "$root/native/ghostty/$patch"
  echo "applied $patch"
done

# 3. 构建
prefix="$root/artifacts/native-build/$rid"
rm -rf "$prefix"
echo "zig $("$zig" version): build $args -Doptimize=$optimize"
(cd "$src" && "$zig" build $args -Doptimize="$optimize" --prefix "$prefix")

# 4. 整理产物：统一文件名，供 MSBuild 拾取
out="$root/artifacts/native/$rid"
mkdir -p "$out"
rm -f "$out"/libghostty-vt.* "$out"/ghostty-vt.*
case "$rid" in
  osx-*)
    cp "$(readlink -f "$prefix/lib/libghostty-vt.dylib")" "$out/libghostty-vt.dylib"
    install_name_tool -id "@rpath/libghostty-vt.dylib" "$out/libghostty-vt.dylib";;
  linux-*)
    cp "$(readlink -f "$prefix/lib/libghostty-vt.so")" "$out/libghostty-vt.so"
    # 上游的 lib-vt 共享库不剥离符号（约 10 MB）；调试符号另存一份，发布用剥离后的版本
    strip_tool=""
    for c in llvm-strip /opt/homebrew/opt/llvm/bin/llvm-strip; do command -v "$c" >/dev/null && { strip_tool="$c"; break; }; done
    [[ -z "$strip_tool" && "$(uname -s)" == Linux ]] && strip_tool=strip
    if [[ -n "$strip_tool" ]]; then
      mkdir -p "$root/artifacts/native-symbols/$rid"
      cp "$out/libghostty-vt.so" "$root/artifacts/native-symbols/$rid/libghostty-vt.so.debug"
      "$strip_tool" --strip-unneeded "$out/libghostty-vt.so"
    else
      echo "warning: no ELF strip tool found; libghostty-vt.so left unstripped" >&2
    fi;;
  win-*)
    cp "$prefix/bin/ghostty-vt.dll" "$out/ghostty-vt.dll";;
esac
rm -rf "$out/include" && cp -R "$prefix/include" "$out/include"
git -C "$src" rev-parse HEAD > "$out/ghostty-commit.txt"
ls -la "$out"
