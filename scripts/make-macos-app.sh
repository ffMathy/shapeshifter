#!/usr/bin/env bash
# Wraps the single-file macOS build in Shapeshifter.app and zips it, as it is released.
#
#   scripts/make-macos-app.sh <executable> <version> <zip to create>
#
# A bare executable downloaded with a browser loses its executable bit, and Finder opens such a file in TextEdit. In
# an app bundle zipped with ditto, Finder's Archive Utility restores the bundle and its permissions, and a double-click
# starts it. The bundle is signed ad hoc (Apple silicon runs no unsigned code); the app installs it to
# ~/Applications on first start (src/Shapeshifter.Core/Updates/SelfInstaller.cs) and updates it from the same zip
# (Updater.AssetName).
set -euo pipefail

executable="${1:?executable}"
version="${2:?version}"
zip="${3:?zip to create}"
repo="$(cd "$(dirname "$0")/.." && pwd)"

staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
app="$staging/Shapeshifter.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp "$executable" "$app/Contents/MacOS/Shapeshifter"
chmod +x "$app/Contents/MacOS/Shapeshifter"
printf 'APPL????' > "$app/Contents/PkgInfo"

# The same keys as the bundle the installer writes around a bare executable (DesktopIntegration.BuildInfoPlist),
# plus the icon. Without the installer's marker comment, the installer leaves this file as it is.
cat > "$app/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>Shapeshifter</string>
  <key>CFBundleDisplayName</key>
  <string>Shapeshifter</string>
  <key>CFBundleIdentifier</key>
  <string>app.shapeshifter.Shapeshifter</string>
  <key>CFBundleExecutable</key>
  <string>Shapeshifter</string>
  <key>CFBundleIconFile</key>
  <string>Shapeshifter</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$version</string>
  <key>CFBundleVersion</key>
  <string>$version</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>LSUIElement</key>
  <true/>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF
plutil -lint "$app/Contents/Info.plist"

iconset="$staging/Shapeshifter.iconset"
mkdir -p "$iconset"
png="$repo/src/Shapeshifter.Desktop/Assets/MacOS/Application.png"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$png" --out "$iconset/icon_${size}x${size}.png" > /dev/null
  sips -z $((size * 2)) $((size * 2)) "$png" --out "$iconset/icon_${size}x${size}@2x.png" > /dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/Shapeshifter.icns"

codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"

mkdir -p "$(dirname "$zip")"
rm -f "$zip"
ditto -c -k --keepParent "$app" "$zip"
echo "Created $zip"
