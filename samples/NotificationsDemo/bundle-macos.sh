#!/usr/bin/env bash
# samples/NotificationsDemo/bundle-macos.sh
# Lays out NotificationsDemo.app so UNUserNotificationCenter has a bundle identifier, then launches it.
# Usage: ./bundle-macos.sh [osx-arm64|osx-x64]
set -euo pipefail

RID="${1:-osx-arm64}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
OUT="$HERE/bin/bundle"
APP="$OUT/NotificationsDemo.app"
PUBLISH="$OUT/publish"

make -C "$ROOT/src/Hermes.Native.macOS" >/dev/null
dotnet publish "$HERE/NotificationsDemo.csproj" -c Release -r "$RID" --self-contained false -o "$PUBLISH" >/dev/null

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH"/. "$APP/Contents/MacOS/"
cp "$ROOT/src/Hermes.Native.macOS/lib/libHermes.Native.macOS.dylib" "$APP/Contents/MacOS/"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>com.mythetech.hermes.notificationsdemo</string>
  <key>CFBundleName</key><string>NotificationsDemo</string>
  <key>CFBundleDisplayName</key><string>Hermes Notifications Demo</string>
  <key>CFBundleExecutable</key><string>NotificationsDemo</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

codesign --force --deep --sign - "$APP" >/dev/null
echo "Bundled: $APP"
open "$APP"
