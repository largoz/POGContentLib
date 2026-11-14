#!/usr/bin/env bash
# Check a log against expectations. Exit status = number of failures (0 = all passed).
#
#   expect.sh <log> [<expect file> [<Unity Player.log>]]
#
# Expect file: one extended regex per line ('#' comments, blank lines skipped):
#   ERE       must appear
#   !ERE      must not appear
#   =N ERE    must appear on exactly N lines
# A line starting with "player:" is checked against the Unity Player.log instead (missing log = failure).
# Every "[DRIVER] FAIL" line in the log is a failure too (the driver's own checks).
set -u
LOG="$1"; EXPECT="${2:-}"; PLOG="${3:-}"
[ -f "$LOG" ] || { echo "no log at $LOG"; exit 1; }
count() { grep -a -c -E -- "$1" "$LOG"; }
# grep -c prints 0 and exits 1 on no match: test the file first, never chain "|| echo" after grep.
pcount() { if [ -n "$PLOG" ] && [ -f "$PLOG" ]; then grep -a -c -E -- "$1" "$PLOG"; else echo missing; fi; }

fails=0
if [ -n "$EXPECT" ] && [ -f "$EXPECT" ]; then
  echo "== expectations ($EXPECT)"
  while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in ''|'#'*) continue;; esac
    if [ "${line#player:}" != "$line" ]; then
      body="${line#player:}"; body="${body# }"
      if [ "${body#!}" != "$body" ]; then
        re="${body#!}"; n=$(pcount "$re"); [ "$n" = 0 ] && r=PASS || r=FAIL; what="Player.log absent (found $n)"
      else
        re="$body"; n=$(pcount "$re"); [ "$n" != missing ] && [ "$n" -gt 0 ] && r=PASS || r=FAIL; what="Player.log present (found $n)"
      fi
    elif [ "${line#!}" != "$line" ]; then
      re="${line#!}"; n=$(count "$re"); [ "$n" -eq 0 ] && r=PASS || r=FAIL; what="absent (found $n)"
    elif [ "${line#=}" != "$line" ]; then
      want="${line#=}"; want="${want%% *}"; re="${line#=* }"
      n=$(count "$re"); [ "$n" -eq "$want" ] && r=PASS || r=FAIL; what="x$want (found $n)"
    else
      re="$line"; n=$(count "$re"); [ "$n" -gt 0 ] && r=PASS || r=FAIL; what="present (found $n)"
    fi
    [ "$r" = FAIL ] && fails=$((fails + 1))
    echo "$r $what: $re"
  done < "$EXPECT"
fi
dfails=$(count "\[DRIVER\] FAIL")
grep -a "\[DRIVER\] FAIL" "$LOG" | cut -c1-300
fails=$((fails + dfails))
# A driver that never reached its end (the game crashed, hung or was closed) is a failure in itself.
if [ "$(count "\[DRIVER\] done")" -eq 0 ]; then echo "FAIL the driver never reached \"done\" (game crashed, hung or timed out)"; fails=$((fails + 1)); fi
echo "== $fails failure(s) ($dfails from the driver's own checks)"
exit "$fails"
