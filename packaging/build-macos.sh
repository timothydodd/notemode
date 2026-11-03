#!/usr/bin/env bash
# Builds NoteMode for macOS: Native AOT publish, NoteMode.app bundle (Info.plist, icon), ad-hoc code
# signature and a .dmg, in artifacts/.
#
#   packaging/build-macos.sh [--version 1.2.3] [--arch arm64|x64|universal] [--output artifacts]
#
# Needs the .NET 10 SDK and the Xcode Command Line Tools (clang/ld for Native AOT, plus sips,
# iconutil, lipo, codesign and hdiutil). The version defaults to <Version> in NoteMode.csproj.
# "universal" publishes both architectures and joins them with lipo (the bundled Avalonia/Skia
# libraries already contain both).
#
# The signature is ad hoc ("-"), which is enough for Apple Silicon to run the app, but it is not a
# Developer ID signature and the app is not notarized: a downloaded copy is quarantined by Gatekeeper
# (see docs/building.md).
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
version=""
arch="arm64"
output="$root/artifacts"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) version="$2"; shift 2 ;;
    --arch) arch="$2"; shift 2 ;;
    --output) output="$2"; shift 2 ;;
    -h|--help) sed -n '2,13p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

if [[ -z "$version" ]]; then
  version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/src/NoteMode/NoteMode.csproj" | head -1)"
fi
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version must be x.y.z (got '$version')." >&2
  exit 2
fi
case "$arch" in
  arm64|x64) rids=("osx-$arch") ;;
  universal) rids=(osx-arm64 osx-x64) ;;
  *) echo "--arch must be arm64, x64 or universal." >&2; exit 2 ;;
esac

stage="$output/macos-stage"
app="$stage/NoteMode.app"
rm -rf "$stage"
mkdir -p "$output" "$stage"

# --- Publish (Native AOT) ------------------------------------------------------------------------
for rid in "${rids[@]}"; do
  echo "Publishing $rid ($version)..."
  dotnet publish "$root/src/NoteMode" \
    --configuration Release \
    --runtime "$rid" \
    --output "$stage/publish-$rid" \
    -p:Version="$version"
done

# --- App bundle ----------------------------------------------------------------------------------
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
first="$stage/publish-${rids[0]}"
# The native executable and the native libraries it loads from its own folder. Debug symbols
# (.dSYM) and the Windows-only file-type icons (Assets/FileIcons, used for registry associations)
# stay out.
cp "$first"/*.dylib "$app/Contents/MacOS/"
if [[ "${#rids[@]}" -gt 1 ]]; then
  lipo -create -output "$app/Contents/MacOS/NoteMode" \
    "$stage/publish-osx-arm64/NoteMode" "$stage/publish-osx-x64/NoteMode"
else
  cp "$first/NoteMode" "$app/Contents/MacOS/NoteMode"
fi
chmod +x "$app/Contents/MacOS/NoteMode"

# Icon: packaging/macos/AppIcon-1024.png (rendered by tools/NoteMode.UiPreview --mac-icon) at every
# size an .icns holds.
iconset="$stage/NoteMode.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$root/packaging/macos/AppIcon-1024.png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" "$root/packaging/macos/AppIcon-1024.png" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/NoteMode.icns"

sed -e "s/@VERSION@/$version/g" "$root/packaging/macos/Info.plist" > "$app/Contents/Info.plist"
plutil -lint "$app/Contents/Info.plist" >/dev/null

# --- Sign (ad hoc) -------------------------------------------------------------------------------
# Libraries first, then the bundle (sealing its resources). Apple Silicon refuses unsigned code.
codesign --force --sign - "$app"/Contents/MacOS/*.dylib
codesign --force --sign - "$app"
codesign --verify --strict --verbose=2 "$app"

# --- Disk image ----------------------------------------------------------------------------------
dmg="$output/NoteMode-$version-macos-$arch.dmg"
dmgroot="$stage/dmg"
mkdir -p "$dmgroot"
cp -R "$app" "$dmgroot/"
ln -s /Applications "$dmgroot/Applications"
rm -f "$dmg"
hdiutil create -volname "NoteMode $version" -srcfolder "$dmgroot" -fs HFS+ -format UDZO -ov "$dmg" >/dev/null

echo "App:  $app"
echo "DMG:  $dmg"
