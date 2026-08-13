#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
ARCH="${1:-arm64}"

case "$ARCH" in
  arm64) RID="osx-arm64" ;;
  x64) RID="osx-x64" ;;
  *)
    echo "Usage: $0 [arm64|x64]" >&2
    exit 2
    ;;
esac

APP_NAME="小欧公爵和小耶牧师桌宠"
APP_VERSION="${PETFRIENDS_VERSION:-1.1.1}"
APP_VERSION="${APP_VERSION#v}"
if [[ "${GITHUB_REF_TYPE:-}" == "tag" && "${GITHUB_REF_NAME:-}" == v* ]]; then
  APP_VERSION="${GITHUB_REF_NAME#v}"
fi
BUNDLE_VERSION="$(printf '%s' "$APP_VERSION" | sed -E 's/[^0-9.].*$//')"
PUBLISH_DIR="$SCRIPT_DIR/bin/publish/$RID"
DIST_DIR="$SCRIPT_DIR/dist/$RID"
APP_DIR="$DIST_DIR/$APP_NAME.app"
ICONSET_DIR="$SCRIPT_DIR/obj/AppIcon.iconset"
DMG_WORK_PATH="$SCRIPT_DIR/obj/PetFriends-$RID.dmg"

rm -rf "$PUBLISH_DIR" "$DIST_DIR" "$ICONSET_DIR"
rm -f "$DMG_WORK_PATH"
mkdir -p "$PUBLISH_DIR" "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources" "$ICONSET_DIR"

dotnet publish "$SCRIPT_DIR/PetFriends.Mac.csproj" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  --output "$PUBLISH_DIR" \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=false \
  -p:Version="$APP_VERSION"

cp -R "$PUBLISH_DIR/." "$APP_DIR/Contents/MacOS/"
find "$APP_DIR/Contents/MacOS" -name '*.pdb' -delete
chmod +x "$APP_DIR/Contents/MacOS/PetFriends"
rm -rf "$PUBLISH_DIR"
cp "$SCRIPT_DIR/Info.plist" "$APP_DIR/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $APP_VERSION" "$APP_DIR/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $BUNDLE_VERSION" "$APP_DIR/Contents/Info.plist"

SOURCE_ICON="$ROOT_DIR/Assets/cat.png"
sips -z 16 16 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_16x16.png" >/dev/null
sips -z 32 32 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_16x16@2x.png" >/dev/null
sips -z 32 32 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_32x32.png" >/dev/null
sips -z 64 64 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_32x32@2x.png" >/dev/null
sips -z 128 128 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_128x128.png" >/dev/null
sips -z 256 256 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_128x128@2x.png" >/dev/null
sips -z 256 256 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_256x256.png" >/dev/null
sips -z 512 512 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_256x256@2x.png" >/dev/null
sips -z 512 512 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_512x512.png" >/dev/null
sips -z 1024 1024 "$SOURCE_ICON" --out "$ICONSET_DIR/icon_512x512@2x.png" >/dev/null
iconutil -c icns "$ICONSET_DIR" -o "$APP_DIR/Contents/Resources/AppIcon.icns"

# Ad-hoc signing keeps the local bundle internally consistent. Distribution can
# replace this with a Developer ID signature and notarization later.
codesign --force --deep --sign - "$APP_DIR"
codesign --verify --deep --strict --verbose=2 "$APP_DIR"

DMG_PATH="$DIST_DIR/$APP_NAME-macOS-$ARCH.dmg"
ln -s /Applications "$DIST_DIR/Applications"
hdiutil create \
  -volname "$APP_NAME" \
  -srcfolder "$DIST_DIR" \
  -size 512m \
  -ov \
  -format UDZO \
  "$DMG_WORK_PATH" >/dev/null
rm -f "$DIST_DIR/Applications"
hdiutil verify "$DMG_WORK_PATH" >/dev/null
mv "$DMG_WORK_PATH" "$DMG_PATH"

ZIP_PATH="$DIST_DIR/$APP_NAME-macOS-$ARCH.zip"
ditto -c -k --sequesterRsrc --keepParent "$APP_DIR" "$ZIP_PATH"

echo "Built: $APP_DIR"
echo "Archive: $ZIP_PATH"
echo "Disk image: $DMG_PATH"
