#!/usr/bin/env bash
# Fetches the A2UI conformance suites at a pinned commit and converts the YAML to JSON for
# tests/TKCode.Arc.A2UI.Tests/Conformance. Needs curl, gh (for the file list) and uv (YAML 1.2 via ruamel.yaml).
set -euo pipefail
COMMIT="${A2UI_COMMIT:-f2c410ae5e78f9693da667e76a11ff6c51f5f9b5}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
WORK="$(mktemp -d)"
DEST="$ROOT/tests/TKCode.Arc.A2UI.Tests/Conformance"
gh api "repos/a2ui-project/a2ui/git/trees/$COMMIT?recursive=1" --jq '.tree[] | select(.type=="blob") | .path' \
  | grep -E '^conformance/(core/.*\.yaml|test_data/catalogs/.*\.json|test_data/node/.*)$' > "$WORK/files.txt"
while read -r f; do
  mkdir -p "$WORK/raw/$(dirname "$f")"
  curl -sSfL --retry 3 -o "$WORK/raw/$f" "https://raw.githubusercontent.com/a2ui-project/a2ui/$COMMIT/$f"
done < "$WORK/files.txt"
rm -rf "$DEST/core" "$DEST/test_data"
uv run --quiet --with ruamel.yaml python "$ROOT/tools/a2ui/yaml_to_json.py" "$WORK/raw/conformance/core" "$DEST/core"
rm -f "$DEST"/core/*v0_8*.json "$DEST"/core/*v0_9*.json
mkdir -p "$DEST/test_data"
cp -R "$WORK/raw/conformance/test_data/"* "$DEST/test_data/"
echo "Synced conformance suites at $COMMIT"
