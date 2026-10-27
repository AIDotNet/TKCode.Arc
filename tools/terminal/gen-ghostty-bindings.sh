#!/usr/bin/env bash
# 重新生成 libghostty-vt 的 C# 绑定（02 §2.2 第 4 步）。需要先构建本机 RID 的库（tools/terminal/build-ghostty.sh）。
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
case "$(uname -s)" in Darwin) os=osx; ext=dylib;; *) os=linux; ext=so;; esac
case "$(uname -m)" in arm64|aarch64) arch=arm64;; *) arch=x64;; esac
native="$root/artifacts/native/$os-$arch"
python3 "$root/tools/terminal/ghostty_bindgen.py" generate \
  --lib "$native/libghostty-vt.$ext" \
  --include "$native/include/ghostty" --include "$root/native/ghostty/include" \
  --out "$root/src/TKCode.Arc.Terminal/Core/Interop/Generated"
