#!/usr/bin/env bash
# Builds the iOS video shim from the same source as macOS (native/video/mac/tkcode_video_av.m, AVFoundation) with Xcode
# clang, as a dynamic framework for devices (ios-arm64) and the simulator (arm64 and x86_64), joined into
# artifacts/native/ios/tkcode_video.xcframework. TKCode.Arc.Video.Native's iOS target embeds it (NativeReference).
# Needs macOS with Xcode and the iOS SDKs.
# Usage: tools/video/build-ios.sh
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
[[ "$(uname -s)" == Darwin ]] || { echo "build-ios.sh needs macOS (Apple SDK)" >&2; exit 1; }

min_ios=15.0
name=tkcode_video
work="$root/artifacts/native-build/video-ios"
out="$root/artifacts/native/ios"
rm -rf "${work:?}" "$out/$name.xcframework"
mkdir -p "$work" "$out"

# framework <dir> <sdk> <target triples...>
framework() {
  local dir="$1" sdk="$2"; shift 2
  local sysroot; sysroot="$(xcrun --sdk "$sdk" --show-sdk-path)"
  local fw="$dir/$name.framework" slices=()
  mkdir -p "$fw"
  for target in "$@"; do
    local slice="$dir/$target.dylib"
    xcrun clang -target "$target" -isysroot "$sysroot" \
      -fobjc-arc -fvisibility=hidden -O2 -Wall -Wextra -Wno-unused-parameter -dynamiclib \
      -I"$root/native/video" "$root/native/video/mac/tkcode_video_av.m" \
      -framework Accelerate -framework AVFoundation -framework CoreMedia -framework CoreVideo -framework IOSurface \
      -framework Metal -framework QuartzCore -framework Foundation \
      -install_name "@rpath/$name.framework/$name" -o "$slice"
    slices+=("$slice")
  done
  lipo -create "${slices[@]}" -output "$fw/$name"
  strip -x "$fw/$name"
  cat > "$fw/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>en</string>
  <key>CFBundleExecutable</key><string>$name</string>
  <key>CFBundleIdentifier</key><string>dev.tkcode.arc.video</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>$name</string>
  <key>CFBundlePackageType</key><string>FMWK</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>MinimumOSVersion</key><string>$min_ios</string>
</dict>
</plist>
PLIST
  echo "$(lipo -info "$fw/$name")"
}

echo "$(xcrun clang --version | head -1): ios"
framework "$work/device" iphoneos "arm64-apple-ios$min_ios"
framework "$work/simulator" iphonesimulator "arm64-apple-ios$min_ios-simulator" "x86_64-apple-ios$min_ios-simulator"
xcodebuild -create-xcframework -framework "$work/device/$name.framework" -framework "$work/simulator/$name.framework" \
  -output "$out/$name.xcframework" >/dev/null
echo "exports: $(nm -gU "$work/device/$name.framework/$name" | grep -c ' _tkv_') tkv_* symbols"
ls -d "$out/$name.xcframework"
