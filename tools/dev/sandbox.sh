#!/usr/bin/env bash
# Isolated working copies for parallel component work (the repository is not a git repository).
#
#   tools/dev/sandbox.sh create <dir>            copy the repository (sources only) to <dir>
#   tools/dev/sandbox.sh merge <dir> <path>...   copy <path>s (relative to the repository root) from <dir> back
#
# Merge copies only the listed paths, never deletes, and skips build outputs. Each sandbox owns its
# category directories; shared files are integrated by hand.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
EXCLUDES=(--exclude '.work/' --exclude 'bin/' --exclude 'obj/' --exclude 'GeneratedFiles/' --exclude '.vs/' --exclude '.idea/' --exclude '*.user')

cmd="${1:-}"
dir="${2:-}"
if [[ -z "$cmd" || -z "$dir" ]]; then
    sed -n '2,9p' "$0"
    exit 2
fi

case "$cmd" in
    create)
        mkdir -p "$dir"
        rsync -a "${EXCLUDES[@]}" "$REPO/" "$dir/"
        echo "sandbox ready: $dir"
        ;;
    merge)
        shift 2
        if [[ $# -eq 0 ]]; then
            echo "merge: no paths given" >&2
            exit 2
        fi
        for path in "$@"; do
            case "$path" in
                demo | demo/* | src/AtomUI.Arc* | tests/AtomUI.Arc* | gallery/AtomUI.Arc*)
                    # A stale path would silently recreate an old folder next to the renamed one, outside the solution.
                    echo "merge: '$path' is the old layout (AtomUI.Arc -> TKCode.Arc, demo/ -> gallery/)." >&2
                    echo "       Run: python3 -I tools/dev/port_sandbox.py <sandbox> <old paths...>, then merge the printed paths." >&2
                    exit 2
                    ;;
            esac
            src="$dir/$path"
            if [[ ! -e "$src" ]]; then
                echo "skip (missing in sandbox): $path" >&2
                continue
            fi
            if [[ -d "$src" ]]; then
                mkdir -p "$REPO/$path"
                rsync -a "${EXCLUDES[@]}" "$src/" "$REPO/$path/"
            else
                mkdir -p "$(dirname "$REPO/$path")"
                rsync -a "$src" "$REPO/$path"
            fi
            echo "merged: $path"
        done
        ;;
    *)
        echo "unknown command: $cmd" >&2
        exit 2
        ;;
esac
