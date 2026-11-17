#!/usr/bin/env bash
# Build and run the native Reperspective unit tests.
# usage: run_tests.sh <path to patched carnage3d/src>
set -euo pipefail
SRC="${1:?path to carnage3d/src}"
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="${TMPDIR:-/tmp}/reperspective_tests"
${CXX:-g++} -std=c++17 -O2 -Wall -Wextra -I"$SRC" "$HERE/reperspective_tests.cpp" -o "$OUT"
"$OUT"
${CXX:-g++} -std=c++11 -Wall -Wextra -I"$SRC" "$HERE/traffic_gap_test.cpp" -o "$OUT.traffic"
"$OUT.traffic"
