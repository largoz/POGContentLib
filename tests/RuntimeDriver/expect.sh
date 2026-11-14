#!/usr/bin/env bash
# Check a log against expectations. Exit status = number of failures (0 = all passed).
#
#   expect.sh <log> [<expect file>]
#
# Expect file: one extended regex per line ('#' comments, blank lines skipped):
#   ERE       must appear
#   !ERE      must not appear
#   =N ERE    must appear on exactly N lines
# Every "[DRIVER] FAIL" line in the log is a failure too (the driver's own checks).
set -u
LOG="$1"; EXPECT="${2:-}"
[ -f "$LOG" ] || { echo "no log at $LOG"; exit 1; }
count() { grep -a -c -E -- "$1" "$LOG"; }

fails=0
if [ -n "$EXPECT" ] && [ -f "$EXPECT" ]; then
  echo "== expectations ($EXPECT)"
  while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in ''|'#'*) continue;; esac
    if [ "${line#!}" != "$line" ]; then
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
