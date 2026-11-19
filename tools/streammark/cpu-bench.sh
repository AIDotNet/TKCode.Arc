#!/usr/bin/env bash
# StreamMark streaming CPU benchmark: NativeAOT-publishes tools/streammark/CpuHarness, runs the scenario matrix in a real
# window and appends the results to artifacts/cpu-bench.txt. Compare with docs/streammark/benchmarks.md.
# Usage: tools/streammark/cpu-bench.sh [rid] (default: this machine). Keep the window visible; each scenario takes ~18 s.
set -euo pipefail
cd "$(dirname "$0")/../.."
rid="${1:-$(dotnet --info | awk '/RID:/ {print $2; exit}')}"
out_dir="artifacts/cpu-bench/$rid"
dotnet publish tools/streammark/CpuHarness -c Release -r "$rid" -o "$out_dir" 2>&1 | grep -E "warning IL|error" || true
exe="$out_dir/TKCode.Arc.StreamMark.CpuHarness"
[[ -x "$exe" ]] || exe="$out_dir/TKCode.Arc.StreamMark.CpuHarness.exe"
samples="tools/streammark/CpuHarness/Samples"
result="artifacts/cpu-bench.txt"
echo "# $(date -u +%Y-%m-%dT%H:%M:%SZ) $rid $(git rev-parse --short HEAD)" >> "$result"
run() { SM_OUT="$PWD/$result" SM_LABEL="$1" "${@:2}" "$exe" > /dev/null 2>&1; }
run idle            env SM_IDLE=1 SM_SECONDS=6
run chat            env
run chat-nofade     env SM_FADE_MS=0
run chat-nosmooth   env SM_SMOOTH=0
run chat-region     env SM_REGION=1
run long-paragraph  env SM_SAMPLE="$PWD/$samples/long-paragraph.md"
run long-para-nofade env SM_SAMPLE="$PWD/$samples/long-paragraph.md" SM_FADE_MS=0
run long-code       env SM_SAMPLE="$PWD/$samples/long-code.md"
tail -n 9 "$result"
