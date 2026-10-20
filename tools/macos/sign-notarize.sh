#!/usr/bin/env bash
# Signs a .app with the hardened runtime, packs it into a .dmg, signs the .dmg, then notarizes and staples it.
#
#   tools/macos/sign-notarize.sh <app> <dmg>
#
# Environment:
#   MACOS_SIGNING_IDENTITY   "Developer ID Application: Name (TEAMID)" or its SHA-1.
#                            "-" signs ad hoc (local dry run of this script, no timestamp, no notarization);
#                            empty skips signing and notarization and only builds the .dmg.
#   MACOS_KEYCHAIN           keychain that holds the identity (default: the user's keychain search list)
#   APPLE_ID, APPLE_TEAM_ID, APPLE_APP_SPECIFIC_PASSWORD
#                            notarytool credentials; notarization is skipped unless all three are set.
#
# Every Mach-O file is signed individually, then the bundle itself (inside-out, no --deep signing).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

app="${1:?usage: sign-notarize.sh <app> <dmg>}"
dmg="${2:?usage: sign-notarize.sh <app> <dmg>}"
identity="${MACOS_SIGNING_IDENTITY:-}"
keychain="${MACOS_KEYCHAIN:-}"
entitlements="$HERE/Entitlements.plist"

if [[ ! -d "$app" ]]; then
    echo "error: $app is not a directory" >&2
    exit 1
fi

sign_app() {
    local find_args=(find-identity -v -p codesigning)
    local codesign_args=(--force --sign "$identity")

    if [[ -n "$keychain" ]]; then
        find_args+=("$keychain")
        codesign_args+=(--keychain "$keychain")
    fi
    if [[ "$identity" == "-" ]]; then
        codesign_args+=(--timestamp=none)
    else
        codesign_args+=(--timestamp)
        if ! security "${find_args[@]}" | grep -qF "$identity"; then
            echo "error: signing identity not found or not trusted: $identity" >&2
            echo "available identities:" >&2
            security "${find_args[@]}" >&2 || true
            exit 1
        fi
    fi

    local list
    list="$(mktemp)"
    local f
    while IFS= read -r -d '' f; do
        case "$(file -b "$f")" in
            *Mach-O*) printf '%s\0' "$f" ;;
        esac
    done < <(find "$app" -type f -print0) > "$list"

    echo "Signing $(tr -cd '\0' < "$list" | wc -c | tr -d ' ') Mach-O files"
    xargs -0 -n 8 -P 4 codesign "${codesign_args[@]}" --options runtime --entitlements "$entitlements" < "$list"
    rm -f "$list"

    echo "Signing $app"
    codesign "${codesign_args[@]}" --options runtime --entitlements "$entitlements" "$app"
    codesign --verify --deep --strict --verbose=2 "$app"
}

make_dmg() {
    local stage
    stage="$(mktemp -d)"
    ditto "$app" "$stage/$(basename "$app")"
    ln -s /Applications "$stage/Applications"
    mkdir -p "$(dirname "$dmg")"
    rm -f "$dmg"
    hdiutil create -volname "$(basename "$app" .app)" -srcfolder "$stage" -ov -format UDZO "$dmg"
    rm -rf "$stage"
}

sign_dmg() {
    local codesign_args=(--force --sign "$identity")
    if [[ "$identity" == "-" ]]; then
        codesign_args+=(--timestamp=none)
    else
        codesign_args+=(--timestamp)
    fi
    if [[ -n "$keychain" ]]; then
        codesign_args+=(--keychain "$keychain")
    fi
    codesign "${codesign_args[@]}" "$dmg"
}

notarize_dmg() {
    local notary_args=(--apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" --password "$APPLE_APP_SPECIFIC_PASSWORD")
    local log rc=0 id status
    log="$(mktemp)"

    xcrun notarytool submit "$dmg" "${notary_args[@]}" --wait --timeout 30m 2>&1 | tee "$log" || rc=$?
    id="$(awk '/^ *id:/ { print $2; exit }' "$log")"
    status="$(awk '/^ *status:/ { s = $2 } END { print s }' "$log")"
    rm -f "$log"

    if [[ $rc -ne 0 || "$status" != "Accepted" ]]; then
        echo "error: notarization failed (exit $rc, status: ${status:-unknown}, submission: ${id:-unknown})" >&2
        if [[ -n "$id" ]]; then
            xcrun notarytool log "$id" "${notary_args[@]}" >&2 || true
        fi
        exit 1
    fi

    xcrun stapler staple "$dmg"
    xcrun stapler validate "$dmg"
    spctl --assess --type open --context context:primary-signature --verbose=2 "$dmg" \
        || echo "warning: Gatekeeper assessment of $dmg did not pass" >&2
}

if [[ -z "$identity" ]]; then
    echo "warning: MACOS_SIGNING_IDENTITY is empty; building an UNSIGNED dmg" >&2
    make_dmg
    exit 0
fi

sign_app
make_dmg
sign_dmg

if [[ "$identity" == "-" ]]; then
    echo "Ad-hoc signed; skipping notarization"
elif [[ -z "${APPLE_ID:-}" || -z "${APPLE_TEAM_ID:-}" || -z "${APPLE_APP_SPECIFIC_PASSWORD:-}" ]]; then
    echo "warning: notarization credentials are not set; the dmg is signed but NOT notarized" >&2
else
    notarize_dmg
fi

echo "$dmg"
