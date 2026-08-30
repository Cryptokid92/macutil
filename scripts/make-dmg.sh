#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"

fsproj="$root/MacUtilGUI/MacUtilGUI.fsproj"
version="$(sed -n 's/.*<CFBundleVersion>\([^<]*\)<\/CFBundleVersion>.*/\1/p' "$fsproj" | head -n 1)"

if [ -z "$version" ]; then
  echo "missing CFBundleVersion in $fsproj" >&2
  exit 1
fi

echo "version $version"

plist="$root/MacUtilGUI/Info.plist"
icon="$root/MacUtilGUI/MacUtilGUI.icns"
app="$root/dist/MacUtil.app"
dmg="$root/dist/MacUtil-${version}.dmg"
x64_out="$root/dist/publish/osx-x64"
arm_out="$root/dist/publish/osx-arm64"

stage="$(mktemp -d)"
intel_stage="$(mktemp -d)"
arm_stage="$(mktemp -d)"
trap 'rm -rf "$stage" "$intel_stage" "$arm_stage"' EXIT

publish() {
  local rid="$1"
  local out="$2"
  echo "publishing $rid"
  rm -rf "$out"
  mkdir -p "$out"
  dotnet publish MacUtilGUI/MacUtilGUI.fsproj \
    --configuration Release \
    --runtime "$rid" \
    --self-contained true \
    --output "$out" \
    -p:UseAppHost=true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=false
  mkdir -p "$out/config"
  rsync -a "$root/config/" "$out/config/"
  test -f "$out/MacUtilGUI"
  test -f "$out/config/tweaks.json"
}

bundle_app() {
  local src="$1"
  local dest="$2"
  rm -rf "$dest"
  mkdir -p "$dest/Contents/MacOS" "$dest/Contents/Resources"
  rsync -a "$src/" "$dest/Contents/MacOS/"
  chmod +x "$dest/Contents/MacOS/MacUtilGUI"
  cp "$plist" "$dest/Contents/Info.plist"
  cp "$icon" "$dest/Contents/Resources/MacUtilGUI.icns"
  test -f "$dest/Contents/MacOS/config/tweaks.json"
}

publish osx-x64 "$x64_out"
publish osx-arm64 "$arm_out"

echo "bundling MacUtil.app"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
rsync -a --exclude="MacUtilGUI" "$arm_out/" "$app/Contents/MacOS/"
lipo -create \
  "$x64_out/MacUtilGUI" \
  "$arm_out/MacUtilGUI" \
  -output "$app/Contents/MacOS/MacUtilGUI"
chmod +x "$app/Contents/MacOS/MacUtilGUI"
cp "$plist" "$app/Contents/Info.plist"
cp "$icon" "$app/Contents/Resources/MacUtilGUI.icns"
test -f "$app/Contents/MacOS/config/tweaks.json"

lipo -info "$app/Contents/MacOS/MacUtilGUI"
lipo -info "$app/Contents/MacOS/MacUtilGUI" | grep x86_64
lipo -info "$app/Contents/MacOS/MacUtilGUI" | grep arm64

if [ -e /Volumes/MacUtil ]; then
  hdiutil detach /Volumes/MacUtil || true
fi

ditto "$app" "$stage/MacUtil.app"
ln -s /Applications "$stage/Applications"

echo "creating $dmg"
rm -f "$dmg"
hdiutil create -volname MacUtil -srcfolder "$stage" -ov -format UDZO "$dmg"

bundle_app "$x64_out" "$intel_stage/MacUtil.app"
bundle_app "$arm_out" "$arm_stage/MacUtil.app"

rm -f "$root/dist/MacUtil-Universal.zip"
rm -f "$root/dist/MacUtil-macos-x64.zip"
rm -f "$root/dist/MacUtil-macos-arm64.zip"
ditto -c -k --keepParent "$app" "$root/dist/MacUtil-Universal.zip"
ditto -c -k --keepParent "$intel_stage/MacUtil.app" "$root/dist/MacUtil-macos-x64.zip"
ditto -c -k --keepParent "$arm_stage/MacUtil.app" "$root/dist/MacUtil-macos-arm64.zip"

echo "wrote $app"
echo "wrote $dmg"
echo "wrote $root/dist/MacUtil-Universal.zip"
echo "wrote $root/dist/MacUtil-macos-x64.zip"
echo "wrote $root/dist/MacUtil-macos-arm64.zip"
