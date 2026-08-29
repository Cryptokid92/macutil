#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
fsproj="$root/MacUtilGUI/MacUtilGUI.fsproj"
plist="$root/MacUtilGUI/Info.plist"
build_script="$root/MacUtilGUI/build_universal.sh"
deploy_script="$root/MacUtilGUI/deploy_macos.sh"

fsproj_prop() {
  local key="$1"
  sed -n "s/.*<${key}>\\([^<]*\\)<\\/${key}>.*/\\1/p" "$fsproj" | head -n 1
}

plist_string() {
  local key="$1"
  awk -v k="$key" '
    index($0, "<key>" k "</key>") { want = 1; next }
    want && /<string>/ {
      sub(/.*<string>/, "")
      sub(/<\/string>.*/, "")
      print
      exit
    }
  ' "$plist"
}

script_version() {
  local file="$1"
  sed -n 's/^VERSION="\([^"]*\)".*/\1/p' "$file" | head -n 1
}

names=()
values=()

add() {
  local name="$1"
  local value="$2"
  if [ -z "$value" ]; then
    echo "missing version: $name" >&2
    exit 1
  fi
  names+=("$name")
  values+=("$value")
}

add "MacUtilGUI.fsproj CFBundleVersion" "$(fsproj_prop CFBundleVersion)"
add "MacUtilGUI.fsproj CFBundleShortVersionString" "$(fsproj_prop CFBundleShortVersionString)"
add "MacUtilGUI/Info.plist CFBundleVersion" "$(plist_string CFBundleVersion)"
add "MacUtilGUI/Info.plist CFBundleShortVersionString" "$(plist_string CFBundleShortVersionString)"
add "MacUtilGUI/build_universal.sh VERSION" "$(script_version "$build_script")"
add "MacUtilGUI/deploy_macos.sh VERSION" "$(script_version "$deploy_script")"

expected="${values[0]}"
drift=0
i=0
while [ "$i" -lt "${#names[@]}" ]; do
  printf '%s=%s\n' "${names[$i]}" "${values[$i]}"
  if [ "${values[$i]}" != "$expected" ]; then
    drift=1
  fi
  i=$((i + 1))
done

if [ "$drift" -ne 0 ]; then
  echo "version drift: expected $expected from ${names[0]}" >&2
  exit 1
fi

echo "version $expected"
