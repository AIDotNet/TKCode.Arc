#!/usr/bin/env bash
# Wraps a `dotnet publish -r osx-<arch> --self-contained -p:PublishSingleFile=true` output into an unsigned macOS .app.
#
#   tools/macos/bundle.sh <publish-dir> <output-dir>
#
# Environment (all optional):
#   APP_NAME          bundle name and display name            (default: TKCode Arc Gallery)
#   BUNDLE_ID         CFBundleIdentifier                      (default: ai.routin.tkcode.arc.gallery)
#   EXECUTABLE_NAME   apphost inside <publish-dir>            (default: TKCode.Arc.Gallery.Desktop)
#   ICON              .icns copied to the bundle              (default: gallery/TKCode.Arc.Gallery.Desktop/Assets/AppIcon.icns)
#   APP_VERSION       full version, may carry a -suffix       (default: ArcVersion from build/Versions.props)
#   BUILD_NUMBER      CFBundleVersion                         (default: 1)
#   MIN_MACOS_VERSION LSMinimumSystemVersion                  (default: 12.0, the .NET 10 runtime's minos)
#
# Prints the path of the created .app on the last line.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

publish_dir="${1:?usage: bundle.sh <publish-dir> <output-dir>}"
out_dir="${2:?usage: bundle.sh <publish-dir> <output-dir>}"

app_name="${APP_NAME:-TKCode Arc Gallery}"
bundle_id="${BUNDLE_ID:-ai.routin.tkcode.arc.gallery}"
executable="${EXECUTABLE_NAME:-TKCode.Arc.Gallery.Desktop}"
icon="${ICON:-$REPO/gallery/TKCode.Arc.Gallery.Desktop/Assets/AppIcon.icns}"
build_number="${BUILD_NUMBER:-1}"
min_macos="${MIN_MACOS_VERSION:-12.0}"
version="${APP_VERSION:-$(sed -n 's:.*<ArcVersion>\(.*\)</ArcVersion>.*:\1:p' "$REPO/build/Versions.props")}"
# CFBundleShortVersionString accepts digits and dots only, so 0.1.0-preview.1 becomes 0.1.0.
short_version="${version%%-*}"

if [[ ! -x "$publish_dir/$executable" ]]; then
    echo "error: $publish_dir/$executable not found; publish with -r osx-<arch> --self-contained first" >&2
    exit 1
fi
if [[ -z "$version" ]]; then
    echo "error: could not determine the version; set APP_VERSION" >&2
    exit 1
fi
if [[ ! -f "$icon" ]]; then
    echo "error: icon $icon not found; build it with tools/branding/build_icons.py or set ICON" >&2
    exit 1
fi

app="$out_dir/$app_name.app"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"

# ditto keeps symlinks and permissions intact.
ditto "$publish_dir" "$app/Contents/MacOS"
find "$app/Contents/MacOS" \( -name '*.pdb' -o -name '*.xml' \) -type f -delete

# codesign treats every file in Contents/MacOS as nested code, so anything that is not a Mach-O (managed .dll,
# .json, ...) makes the bundle signature fail with "code object is not signed at all". Publish as a single file.
stray=""
while IFS= read -r -d '' f; do
    case "$(file -b "$f")" in
        *Mach-O*) ;;
        *) stray="$stray  ${f#"$app/Contents/MacOS/"}"$'\n' ;;
    esac
done < <(find "$app/Contents/MacOS" -type f -print0)
if [[ -n "$stray" ]]; then
    printf 'error: Contents/MacOS must contain only Mach-O files (publish with -p:PublishSingleFile=true); found:\n%s' "$stray" >&2
    exit 1
fi

cp "$icon" "$app/Contents/Resources/AppIcon.icns"
printf 'APPL????' > "$app/Contents/PkgInfo"
cat > "$app/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$app_name</string>
    <key>CFBundleDisplayName</key>
    <string>$app_name</string>
    <key>CFBundleIdentifier</key>
    <string>$bundle_id</string>
    <key>CFBundleExecutable</key>
    <string>$executable</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleShortVersionString</key>
    <string>$short_version</string>
    <key>CFBundleVersion</key>
    <string>$build_number</string>
    <key>LSMinimumSystemVersion</key>
    <string>$min_macos</string>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.developer-tools</string>
</dict>
</plist>
EOF
plutil -lint "$app/Contents/Info.plist" >&2

echo "$app"
