#!/usr/bin/env bash
# Builds libratex_ffi (StreamMark's math engine) from the commit pinned in native/ratex/ratex-upstream.json, with the
# patches it lists, into artifacts/native/<rid>/.
#   tools/streammark/build-ratex.sh            the machine's own RID with plain cargo (local work and tests)
#   tools/streammark/build-ratex.sh <rid>      a release build for that RID: osx-* with cargo --target, linux-* and win-*
#                                              with cargo-zigbuild (glibc 2.28 floor on Linux); needs the rustup target
# Needs a Rust toolchain. cargo-zigbuild finds Zig through CARGO_ZIGBUILD_ZIG_PATH, TKCODE_ZIG, a zig on PATH, or else the
# version pinned in native/toolchain.json (tools/terminal/install-zig.sh). On Windows use build-ratex.ps1.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
manifest="$root/native/ratex/ratex-upstream.json"
json() { python3 -c "import json,sys; d=json.load(open(sys.argv[1])); print(eval(sys.argv[2]))" "$manifest" "$1"; }

rid="${1:-}"
host=0
if [[ -z "$rid" ]]; then
  host=1
  case "$(uname -s)" in Darwin) os=osx;; Linux) os=linux;; *) echo "use tools/streammark/build-ratex.ps1 on Windows" >&2; exit 1;; esac
  case "$(uname -m)" in arm64|aarch64) arch=arm64;; x86_64) arch=x64;; *) echo "unsupported arch $(uname -m)" >&2; exit 1;; esac
  rid="$os-$arch"
fi
repo="$(json 'd["repo"]')"
commit="$(json 'd["commit"]')"
crate="$(json 'd["crate"]')"

# 1. Source: its own checkout, by commit; reset to a clean tree before the patches go on.
src="$root/artifacts/src/ratex-${commit:0:12}"
if [[ ! -d "$src/.git" ]]; then
  git init -q "$src"
  git -C "$src" remote add origin "$repo"
fi
if ! git -C "$src" cat-file -e "$commit^{commit}" 2>/dev/null; then
  git -C "$src" fetch -q --depth 1 origin "$commit"
fi
git -C "$src" checkout -q --force "$commit"
git -C "$src" clean -qfd
[[ "$(git -C "$src" rev-parse HEAD)" == "$commit" ]] || { echo "commit mismatch" >&2; exit 1; }

# 2. Patches, in manifest order; one that does not apply fails the build.
python3 -c "import json,sys; [print(p['file']) for p in json.load(open(sys.argv[1]))['patches']]" "$manifest" |
while read -r patch; do
  git -C "$src" apply --check "$root/native/ratex/$patch"
  git -C "$src" apply "$root/native/ratex/$patch"
  echo "applied $patch"
done

# 3. Build.
export CARGO_TARGET_DIR="$root/artifacts/cargo"
out="$root/artifacts/native/$rid"
mkdir -p "$out"
if [[ "$host" == 1 ]]; then
  cargo build --manifest-path "$src/Cargo.toml" -p "$crate" --release
  dir="$CARGO_TARGET_DIR/release"
  lib="$(python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['build']['perRid'][sys.argv[2]]['lib'])" "$manifest" "$rid")"
else
  read -r target tool lib glibc < <(python3 -c "
import json,sys
r=json.load(open(sys.argv[1]))['build']['perRid'][sys.argv[2]]
print(r['target'], r['tool'], r['lib'], r.get('glibc', '-'))" "$manifest" "$rid")
  if [[ "$tool" == zigbuild ]]; then
    if [[ -z "${CARGO_ZIGBUILD_ZIG_PATH:-}" ]]; then
      if [[ -n "${TKCODE_ZIG:-}" ]]; then
        export CARGO_ZIGBUILD_ZIG_PATH="$TKCODE_ZIG"
      elif ! command -v zig > /dev/null; then
        export CARGO_ZIGBUILD_ZIG_PATH="$("$root/tools/terminal/install-zig.sh" | tail -1)"
      fi
    fi
    spec="$target"; [[ "$glibc" == - ]] || spec="$target.$glibc"
    cargo zigbuild --manifest-path "$src/Cargo.toml" -p "$crate" --release --target "$spec"
  else
    cargo build --manifest-path "$src/Cargo.toml" -p "$crate" --release --target "$target"
  fi
  dir="$CARGO_TARGET_DIR/$target/release"
fi
cp "$dir/$lib" "$out/"
echo "$commit" > "$out/ratex-commit.txt"
ls -la "$out/$lib"
