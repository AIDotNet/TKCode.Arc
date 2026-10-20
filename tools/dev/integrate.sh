#!/usr/bin/env bash
# Merges a finished sandbox into the repository and verifies the result.
#
#   tools/dev/integrate.sh <sandbox-name> <path>...
#
# Copies the listed paths from .work/sandboxes/<sandbox-name> (see sandbox.sh merge), then builds the
# solution, runs both test suites, marks implemented components in design/components/overview.md and
# checks the design docs. Stops at the first failing step.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
name="${1:-}"
if [[ -z "$name" || $# -lt 2 ]]; then
    sed -n '2,8p' "$0"
    exit 2
fi
shift
cd "$REPO"
bash tools/dev/sandbox.sh merge ".work/sandboxes/$name" "$@"

echo "== build"
dotnet build TKCode.Arc.slnx -c Debug 2>&1 | grep -E " error |warning CS|AVLN|已成功|失败|Build succeeded|FAILED" | sort -u | head -40
dotnet build TKCode.Arc.slnx -c Debug --no-restore >/dev/null 2>&1 || { echo "build failed"; exit 1; }

echo "== tests"
dotnet test tests/TKCode.Arc.Core.Tests --no-build 2>&1 | grep -E "Passed!|Failed!|\[FAIL\]" | head -20
dotnet test tests/TKCode.Arc.Tests --no-build 2>&1 | grep -E "Passed!|Failed!|\[FAIL\]" | head -40

echo "== status"
python3 -I tools/dev/update_status.py "$REPO"
if [[ -d "${ATOMUI_SRC:-/Users/token/code/AtomUI}" ]]; then
    python3 -I tools/dev/verify_design_docs.py design "${ATOMUI_SRC:-/Users/token/code/AtomUI}" | tail -3
fi
