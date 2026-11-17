#!/usr/bin/env bash
# Prepare everything the Android build needs that is not stored in the repo.
#   ENGINES_DIR (default: ../engines) gets carnage3d/ (patched) and android-deps/.
# Usage: ./setup_deps.sh [path/to/perspective-lock-android.patch]
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ENGINES_DIR="${ENGINES_DIR:-$HERE/../engines}"
PATCH="${1:-$HERE/../prototype/carnage3d/perspective-lock-android.patch}"
mkdir -p "$ENGINES_DIR/android-deps"
cd "$ENGINES_DIR"

if [ ! -d carnage3d ]; then
  git clone https://github.com/codenamecpp/carnage3d
  (cd carnage3d && git checkout 3dc03b2 && git submodule update --init --depth 1 && git apply "$PATCH")
fi

cd android-deps
[ -d SDL2 ] || git clone --depth 1 -b release-2.30.9 https://github.com/libsdl-org/SDL SDL2
[ -d openal-soft ] || git clone --depth 1 -b 1.23.1 https://github.com/kcat/openal-soft openal-soft
[ -d glm ] || git clone --depth 1 -b 1.0.1 https://github.com/g-truc/glm glm

# SDL2 Java glue (org.libsdl.app) into the app
mkdir -p "$HERE/app/src/main/java/org/libsdl/app"
cp SDL2/android-project/app/src/main/java/org/libsdl/app/*.java "$HERE/app/src/main/java/org/libsdl/app/"

# Carnage3D engine data (shaders, configs) into APK assets; GTA game data is never bundled
A="$HERE/app/src/main/assets"
rm -rf "$A/gamedata" && mkdir -p "$A" && cp -r "$ENGINES_DIR/carnage3d/gamedata" "$A/gamedata"
cp "$A/gamedata/config/inputs.json.default" "$A/gamedata/config/inputs.json"
sed 's/"r_screenDims":.*/"r_screenDims":\t"1280 720",/; s/"r_fullscreen":.*/"r_fullscreen":\t"true",/; s/"g_gtadata":.*/"g_gtadata":\t"",\n\t"r_aspect":\t"auto"/' \
  "$A/gamedata/config/sys_config.json.default" > "$A/gamedata/config/sys_config.json"

[ -f "$HERE/local.properties" ] || echo "sdk.dir=${ANDROID_HOME:-$HOME/Android/Sdk}" > "$HERE/local.properties"
echo "Ready. Build with: cd $HERE && ./gradlew assembleRelease"
