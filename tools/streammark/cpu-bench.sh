#!/usr/bin/env bash
# 流式渲染 CPU 基准：NativeAOT 发布 tools/streammark/CpuHarness，在真实窗口中把场景矩阵交错跑 ROUNDS 轮（默认 3），
# 每次运行一行追加到 artifacts/cpu-bench.txt，最后按场景输出有效轮次的中位数。
# 用法：[ROUNDS=n] tools/streammark/cpu-bench.sh [rid]（默认按当前平台）。运行期间会打开窗口，请保持其可见、不要切换桌面；每个场景约 20 秒。
# 窗口被遮挡时系统停止渲染，CPU 会掉到正常值的几分之一：这样的运行被标为 INVALID，不计入中位数。
# 跨轮次、跨机器比较优先看 cpu/frame（每渲染一帧的进程 CPU），CPU 百分比还取决于实际帧率。
set -euo pipefail
cd "$(dirname "$0")/../.."
rid="${1:-$(dotnet --info | awk '/RID:/ {print $2; exit}')}"
rounds="${ROUNDS:-3}"
out_dir="artifacts/cpu-bench/$rid"
dotnet publish tools/streammark/CpuHarness -c Release -r "$rid" -o "$out_dir" 2>&1 | grep -E "warning IL|error" || true
exe="$out_dir/TKCode.Arc.StreamMark.CpuHarness"
[[ -x "$exe" ]] || exe="$out_dir/TKCode.Arc.StreamMark.CpuHarness.exe"
samples="tools/streammark/CpuHarness/Samples"
result="artifacts/cpu-bench.txt"
runs="$(mktemp)"
trap 'rm -f "$runs"' EXIT
echo "# $(date -u +%Y-%m-%dT%H:%M:%SZ) $rid $(git rev-parse --short HEAD)$(git diff --quiet HEAD -- || echo '+dirty') rounds=$rounds" >> "$result"
run() { SM_OUT="$PWD/$runs" SM_LABEL="$1" "${@:2}" "$exe" > /dev/null 2>&1; }
for ((round = 1; round <= rounds; round++)); do
    run idle             env SM_IDLE=1 SM_SECONDS=6
    run chat             env
    run chat-nofade      env SM_FADE_MS=0
    run chat-nosmooth    env SM_SMOOTH=0
    run chat-region      env SM_REGION=1
    run long-paragraph   env SM_SAMPLE="$PWD/$samples/long-paragraph.md"
    run long-para-nofade env SM_SAMPLE="$PWD/$samples/long-paragraph.md" SM_FADE_MS=0
    run long-code        env SM_SAMPLE="$PWD/$samples/long-code.md"
done
cat "$runs" >> "$result"

# 每个场景：有效轮次的 cpu / ui / fps / cpu/frame / alloc 中位数
summary="$(awk '
    function num(field,    v) { v = field; sub(/^[^=]*=/, "", v); gsub(/[%a-zA-Z\/]+$/, "", v); return v + 0 }
    function median(list, n,    a, i, j, t) {
        split(list, a, " ")
        for (i = 2; i <= n; i++) { t = a[i]; for (j = i - 1; j >= 1 && a[j] > t; j--) a[j + 1] = a[j]; a[j + 1] = t }
        return n % 2 ? a[(n + 1) / 2] : (a[n / 2] + a[n / 2 + 1]) / 2
    }
    {
        label = $1
        if (!(label in seen)) { seen[label] = 1; order[++count] = label }
        if ($2 == "INVALID") { invalid[label]++; next }
        n[label]++
        for (i = 2; i <= NF; i++) {
            split($i, kv, "=")
            if (kv[1] == "cpu" || kv[1] == "ui" || kv[1] == "fps" || kv[1] == "cpu/frame" || kv[1] == "alloc") {
                v = (kv[2] == "" && i < NF) ? num($(i + 1)) : num($i)
                values[label, kv[1]] = values[label, kv[1]] " " v
            }
        }
    }
    END {
        for (k = 1; k <= count; k++) {
            l = order[k]; m = n[l] + 0
            if (m == 0) { printf "%-18s 有效 0/%d 轮\n", l, invalid[l] + 0; continue }
            printf "%-18s cpu=%5.1f%%  ui=%5.1f%%  fps=%5.1f  cpu/frame=%5.2fms  alloc=%4.1fMB/s  有效 %d/%d 轮\n", l,
                median(values[l, "cpu"], m), median(values[l, "ui"], m), median(values[l, "fps"], m),
                median(values[l, "cpu/frame"], m), median(values[l, "alloc"], m), m, m + invalid[l]
        }
    }' "$runs")"
printf '%s\n' "$summary" | sed 's/^/## median /' >> "$result"
cat "$runs"
echo
echo "中位数（只计有效轮次）："
printf '%s\n' "$summary"
