#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
root=$PWD
source_tree="$root/../engines/carnage3d"
mkdir -p engine/third_party
rsync -a --delete --exclude android "$source_tree/src/" engine/src/
rsync -a --delete "$source_tree/gamedata/" engine/gamedata/
rsync -a --delete "$source_tree/cmake/" engine/cmake/
rsync -a --delete "$source_tree/third_party/Box2D/" engine/third_party/Box2D/
cp "$source_tree/CMakeLists.txt" engine/CMakeLists.txt
[ -s engine.patch ] && patch --batch --forward --fuzz=0 --no-backup-if-mismatch -d engine -p1 < engine.patch
version=$(python3 - <<'VERSION'
from pathlib import Path
import re
print(re.search(r"versionName\s+['\"]([^'\"]+)", Path('../android/app/build.gradle').read_text()).group(1))
VERSION
)
cmake -S engine -B build -DCMAKE_TOOLCHAIN_FILE="$root/toolchain-mingw.cmake" -DCMAKE_BUILD_TYPE=Release -DCMAKE_CXX_STANDARD=17 -DWINDOWS_DEPS="$root/deps" -DWINDOWS_APP_VERSION="$version" -DGLM_HEADERS="$root/../engines/android-deps/glm"
cmake --build build --parallel "${JOBS:-4}"
