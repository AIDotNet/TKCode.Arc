#!/usr/bin/env bash
# Builds libghostty-vt for iOS devices (ios-arm64) and the simulator (iossimulator-arm64, iossimulator-x64) with
# tools/terminal/build-ghostty.sh and joins them into the dynamic framework artifacts/native/ios/ghostty-vt.xcframework
# (an iOS app cannot load a loose .dylib). TKCode.Arc.Terminal packs it under runtimes/ios/native, and GhosttyLibrary loads
# it from the app bundle's Frameworks/ghostty-vt.framework at run time. Needs macOS with Xcode and the iOS SDKs.
# Usage: tools/terminal/build-ghostty-ios.sh [--skip-build]   (--skip-build reuses artifacts/native/<ios rid>/)
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
[[ "$(uname -s)" == Darwin ]] || { echo "build-ghostty-ios.sh needs macOS (Apple SDK)" >&2; exit 1; }

name=ghostty-vt
rids=(ios-arm64 iossimulator-arm64 iossimulator-x64)
if [[ "${1:-}" != --skip-build ]]; then
  for rid in "${rids[@]}"; do "$root/tools/terminal/build-ghostty.sh" "$rid"; done
fi

work="$root/artifacts/native-build/ghostty-ios"
out="$root/artifacts/native/ios"
rm -rf "${work:?}" "$out/$name.xcframework"
mkdir -p "$work" "$out"

# framework <dir> <rid...>: one framework from the dylibs of the given RIDs (a fat binary for the simulator).
framework() {
  local dir="$1"; shift
  local fw="$dir/$name.framework" slices=() rid
  mkdir -p "$fw"
  for rid in "$@"; do
    local lib="$root/artifacts/native/$rid/libghostty-vt.dylib"
    [[ -f "$lib" ]] || { echo "missing $lib (run tools/terminal/build-ghostty.sh $rid)" >&2; exit 1; }
    slices+=("$lib")
  done
  lipo -create "${slices[@]}" -output "$fw/$name"
  install_name_tool -id "@rpath/$name.framework/$name" "$fw/$name"
  strip -x "$fw/$name"
  # The highest deployment target of the slices (arm64 simulators start at iOS 14).
  local min_os
  min_os="$(for rid in "$@"; do vtool -show-build "$root/artifacts/native/$rid/libghostty-vt.dylib" | awk '$1 == "minos" { print $2 }'; done | sort -V | tail -1)"
  cat > "$fw/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleExecutable</key><string>$name</string>
  <key>CFBundleIdentifier</key><string>dev.tkcode.arc.ghostty-vt</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>$name</string>
  <key>CFBundlePackageType</key><string>FMWK</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>MinimumOSVersion</key><string>$min_os</string>
</dict>
</plist>
PLIST
  echo "$(lipo -info "$fw/$name") (iOS $min_os+)"
}

framework "$work/device" ios-arm64
framework "$work/simulator" iossimulator-arm64 iossimulator-x64
xcodebuild -create-xcframework -framework "$work/device/$name.framework" -framework "$work/simulator/$name.framework" \
  -output "$out/$name.xcframework" >/dev/null
cp "$root/artifacts/native/ios-arm64/ghostty-commit.txt" "$out/ghostty-commit.txt"
echo "exports: $(nm -gU "$work/device/$name.framework/$name" | grep -c ' _ghostty_\| _tkcode_ghostty_') ghostty_* symbols"
ls -d "$out/$name.xcframework"
