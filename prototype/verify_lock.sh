#!/usr/bin/env bash
# Visual verification of the perspective lock.
# Spins the player at 45 deg/s (test-only env var) and grabs 3 frames 1 s apart,
# with the lock ON and OFF. Lock ON: player sprite stays pointing up, world turns.
# Lock OFF: world stays fixed, player sprite turns.
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
SHOTS="$HERE/shots"; mkdir -p "$SHOTS"
export DISPLAY=:0
export LD_LIBRARY_PATH="$HOME/.local/c3d-deps/root/usr/lib/x86_64-linux-gnu"
for mode in on off; do
  lock=1; [ "$mode" = off ] && lock=0
  ( cd "$HERE/bin" && C3D_LOCK=$lock C3D_TEST_SPIN=45 timeout 16 ./carnage3d > "$HERE/run_$mode.log" 2>&1 ) &
  sleep 8
  for i in 1 2 3; do
    ffmpeg -loglevel error -y -f x11grab -i :0 -frames:v 1 "$SHOTS/full_${mode}_$i.png"
    sleep 1
  done
  wait
  ffmpeg -loglevel error -y -i "$SHOTS/full_${mode}_1.png" -i "$SHOTS/full_${mode}_2.png" -i "$SHOTS/full_${mode}_3.png" \
    -filter_complex "[0]crop=400:400:160:300[a];[1]crop=400:400:160:300[b];[2]crop=400:400:160:300[c];[a][b][c]hstack=3" \
    "$SHOTS/strip_lock_${mode}.png"
done
echo done
