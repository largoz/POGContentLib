#!/usr/bin/env bash
# Run POGContentLib in the real game (Steam + the Gale profile) under the runtime driver, then clean up.
#
#   run.sh <out dir> [extra DLL or folder to deploy into Mods/ ...]
#
# Deploys: the library build (../../bin/Release/net6.0/POGContentLib.dll), the driver, the extras, and
# <out dir>/driver.txt as UserData/POGContentLib/driver.txt when it exists. Screenshots the window ~2 s
# after every "[DRIVER] shot <name>" log line, stops at "[DRIVER] done", closes the game and removes
# everything it deployed. Prints the library's and driver's log lines.
# Ask/announce before running: the game takes the screen for a few minutes (see _research/TESTING.md §0).
set -u
PROFILE="${POG_PROFILE:-$HOME/.local/share/com.kesomannen.gale/pit-of-goblin/profiles/Default}"
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
OUT="$1"; shift
LOG="$PROFILE/MelonLoader/Latest.log"
MODS="$PROFILE/Mods"
CFG="$PROFILE/UserData/POGContentLib/driver.txt"
TIMEOUT="${POG_TIMEOUT:-420}"

if pgrep -x 'Pit of Goblin.e' >/dev/null; then echo "game already running (the user may be playing): abort"; exit 1; fi
mkdir -p "$OUT"

deployed=()
deploy() {   # atomic copy into Mods/, remembered for removal
  local src="$1" name; name="$(basename "$src")"
  if [ -d "$src" ]; then cp -r "$src" "$MODS/$name"; else cp "$src" "$MODS/.$name.tmp" && mv -f "$MODS/.$name.tmp" "$MODS/$name"; fi
  deployed+=("$MODS/$name")
}
cleanup() {
  local gp; gp=$(pgrep -x 'Pit of Goblin.e'); [ -n "$gp" ] && kill $gp
  for _ in $(seq 1 20); do pgrep -x 'Pit of Goblin.e' >/dev/null || break; sleep 1; done
  pgrep -x 'Pit of Goblin.e' >/dev/null && echo "game still running" || echo "game closed"
  for f in "${deployed[@]}"; do rm -rf "$f"; done
  rm -f "$CFG"
  for f in "${test_shops[@]}"; do rm -f "$f"; done
  echo "Mods/ now:"; ls "$MODS"
}
trap cleanup EXIT

deploy "$ROOT/bin/Release/net6.0/POGContentLib.dll"
deploy "$HERE/bin/Release/net6.0/POGContentLibRuntimeDriver.dll"
for extra in "$@"; do deploy "$extra"; done
if [ -f "$OUT/driver.txt" ]; then mkdir -p "$(dirname "$CFG")"; cp "$OUT/driver.txt" "$CFG"; fi
# Test shop rules: <out dir>/shops/*.json -> UserData/POGContentLib/Shops/ (removed afterwards).
SHOPS_DIR="$PROFILE/UserData/POGContentLib/Shops"
test_shops=()
if [ -d "$OUT/shops" ]; then mkdir -p "$SHOPS_DIR"; for f in "$OUT"/shops/*.json; do cp "$f" "$SHOPS_DIR/"; test_shops+=("$SHOPS_DIR/$(basename "$f")"); done; fi
sha256sum "$ROOT/bin/Release/net6.0/POGContentLib.dll" "$MODS/POGContentLib.dll" | awk '{print $1}' | uniq -c

T0=$(date +%s)
(setsid steam -applaunch 3253530 --melonloader.basedir "Z:$PROFILE" >/dev/null 2>&1 &)
WIN=""
seen=" "
while true; do
  now=$(date +%s)
  if [ $((now - T0)) -gt "$TIMEOUT" ]; then echo "TIMEOUT after $TIMEOUT s"; break; fi
  if [ -f "$LOG" ] && [ "$(stat -c %Y "$LOG")" -ge "$T0" ]; then
    [ -z "$WIN" ] && WIN=$(xdotool search --name "^Pit of Goblin$" 2>/dev/null | tail -1)
    while read -r name; do
      case "$seen" in *" $name "*) ;; *)
        seen="$seen$name "; sleep 2.2
        [ -n "$WIN" ] && import -silent -window "$WIN" "$OUT/$name.png" && echo "shot $name";;
      esac
    done < <(grep -a -o "\[DRIVER\] shot [A-Za-z0-9_-]*" "$LOG" | awk '{print $3}')
    grep -a -q "\[DRIVER\] done" "$LOG" && break
  fi
  if [ $((now - T0)) -gt 30 ] && ! pgrep -x 'Pit of Goblin.e' >/dev/null; then echo "game process gone"; break; fi
  sleep 1
done
cp "$LOG" "$OUT/Latest.log" 2>/dev/null
grep -a -E "\[DRIVER\]|POGContentLib|\[Shops\]|ERROR|Exception" "$LOG" | cut -c1-300
