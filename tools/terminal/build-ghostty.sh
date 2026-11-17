#!/usr/bin/env bash
# 构建 libghostty-vt（native/ghostty/ghostty-upstream.json 中 pin 住的提交 + overlay 补丁）。
# 产物：artifacts/native/<rid>/libghostty-vt.{dylib,so} 或 ghostty-vt.dll，以及 artifacts/native/<rid>/include/。
# 移动端 RID：android-*（要 Android NDK，版本见 native/toolchain.json；ANDROID_NDK_HOME，或 Android SDK 下的 ndk/<版本>）
# 与 ios-arm64、iossimulator-*（要 macOS + Xcode；tools/terminal/build-ghostty-ios.sh 把它们合成 xcframework）。
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

# Android：固定 NDK 版本（ghostty 的构建在没有 ANDROID_NDK_HOME 时会取 SDK 里最新的一个）
case "$rid" in
  android-*)
    ndk_version="$(python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['androidNdk']['version'])" "$root/native/toolchain.json")"
    if [[ -z "${ANDROID_NDK_HOME:-}" ]]; then
      for sdk in "${ANDROID_HOME:-}" "${ANDROID_SDK_ROOT:-}" "$HOME/Library/Android/sdk" "$HOME/Android/Sdk"; do
        [[ -n "$sdk" && -d "$sdk/ndk/$ndk_version" ]] && { ANDROID_NDK_HOME="$sdk/ndk/$ndk_version"; break; }
      done
    fi
    [[ -n "${ANDROID_NDK_HOME:-}" && -d "$ANDROID_NDK_HOME" ]] || {
      echo "Android NDK $ndk_version not found: set ANDROID_NDK_HOME, or run sdkmanager --install \"ndk;$ndk_version\"" >&2; exit 1; }
    ndk_found="$(sed -n 's/^Pkg.Revision *= *//p' "$ANDROID_NDK_HOME/source.properties")"
    [[ "$ndk_found" == "$ndk_version" ]] || echo "warning: Android NDK $ndk_found at $ANDROID_NDK_HOME, expected $ndk_version" >&2
    export ANDROID_NDK_HOME
    echo "Android NDK $ndk_found: $ANDROID_NDK_HOME";;
  ios-*|iossimulator-*)
    [[ "$(uname -s)" == Darwin ]] || { echo "$rid needs macOS with Xcode (iOS SDK)" >&2; exit 1; };;
esac

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
  ios-*|iossimulator-*)
    # 原样的 dylib；install name 与签名由 build-ghostty-ios.sh 打包成框架时设置
    cp "$(readlink -f "$prefix/lib/libghostty-vt.dylib")" "$out/libghostty-vt.dylib";;
  linux-*|android-*)
    cp "$(readlink -f "$prefix/lib/libghostty-vt.so")" "$out/libghostty-vt.so"
    # 上游的 lib-vt 共享库不剥离符号（约 10 MB）；调试符号另存一份，发布用剥离后的版本
    strip_tool=""
    if [[ "$rid" == android-* ]]; then
      for c in "$ANDROID_NDK_HOME"/toolchains/llvm/prebuilt/*/bin/llvm-strip; do [[ -x "$c" ]] && { strip_tool="$c"; break; }; done
    fi
    [[ -n "$strip_tool" ]] || for c in llvm-strip /opt/homebrew/opt/llvm/bin/llvm-strip; do command -v "$c" >/dev/null && { strip_tool="$c"; break; }; done
    [[ -z "$strip_tool" && "$(uname -s)" == Linux && "$rid" == linux-* ]] && strip_tool=strip
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
