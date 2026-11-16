#!/usr/bin/env bash
# Install + launch the app on the running emulator, then screenshot and save logs.
# usage: emu_test.sh <shot-name> [seconds-to-wait] [extra adb shell command run after launch]
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
NAME="${1:-shot}"; WAIT="${2:-20}"; EXTRA="${3:-}"
PKG=com.burndivision.gtaperspectivelock
DATA="$HERE/../engines/gamedata-working/GTADATA"
mkdir -p "$HERE/shots"

timeout 240 bash -c 'until [ "$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d "\r")" = "1" ]; do sleep 3; done' || { echo "emulator not booted"; exit 1; }
adb install -r "$HERE/app/build/outputs/apk/debug/app-debug.apk" | tail -1
EXT="/sdcard/Android/data/$PKG/files"
if ! adb shell ls "$EXT/GTADATA/NYC.CMP" >/dev/null 2>&1; then
  adb shell mkdir -p "$EXT" && adb push "$DATA" "$EXT/" | tail -1 && adb shell sync
fi
adb shell am force-stop $PKG
adb logcat -c
adb shell am start -n $PKG/.LauncherActivity | tail -1
sleep "$WAIT"
[ -n "$EXTRA" ] && adb shell "$EXTRA" && sleep 3
adb exec-out screencap -p > "$HERE/shots/$NAME.png"
adb logcat -d > "$HERE/shots/$NAME.logcat.txt"
grep -E " (SDL|GTALock|DEBUG|AndroidRuntime)|libmain|Carnage|\[shim\]" "$HERE/shots/$NAME.logcat.txt" | grep -v nativeloader | tail -40
adb shell dumpsys activity activities | grep topResumedActivity
ls -la "$HERE/shots/$NAME.png"
