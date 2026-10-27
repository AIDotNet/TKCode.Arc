#!/usr/bin/env bash
# 用 curl 预取 Ghostty 构建所需的 Zig 依赖并放入 Zig 全局缓存。
# 背景：Zig 0.16 的 HTTP 客户端经部分 HTTP 代理（CONNECT）时会报 HttpConnectionClosing，
# 而 curl 正常。CI 上通常不需要；本地遇到该错误时运行。
# 用法：prefetch-zig-deps.sh <zig> <ghostty 源码目录>
set -euo pipefail
zig="$1"; src="$2"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
grep -rhoE '\.url = "[^"]+"' --include=build.zig.zon "$src" | sed 's/.url = "//;s/"$//' | sort -u | grep -v '/COMMIT\.' | cat - <(grep -rhoE '\.url = "[^"]+"' --include=build.zig.zon "$src/zig-pkg" 2>/dev/null | sed 's/.url = "//;s/"$//') | sort -u |
while read -r url; do
  file="$tmp/$(basename "${url%%#*}")"
  if [[ "$url" == git+* ]]; then
    # git 依赖：git 取到指定提交后打成 tar，内容哈希与 Zig 直接拉取的结果一致
    repo="${url#git+}"; repo="${repo%%#*}"; repo="${repo%%\?*}"; rev="${url##*#}"
    rm -rf "$tmp/repo" && git init -q "$tmp/repo" &&
      git -C "$tmp/repo" fetch -q --depth 1 "$repo" "$rev" &&
      git -C "$tmp/repo" archive --format=tar --prefix=pkg/ FETCH_HEAD -o "$file.tar" && file="$file.tar" ok=1 || ok=0
  else
    curl -fsSL --retry 3 "$url" -o "$file" && ok=1 || ok=0
  fi
  if [[ $ok == 1 ]]; then
    (cd "$src" && "$zig" fetch "$file" >/dev/null 2>&1) && echo "ok   $url" || echo "FAIL zig fetch $url"
  else
    echo "FAIL curl $url"
  fi
  rm -f "$file"
done
