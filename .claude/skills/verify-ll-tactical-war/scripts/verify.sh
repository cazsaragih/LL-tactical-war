#!/usr/bin/env bash
# verify-ll-tactical-war harness. Drives the game in this project's own Unity Editor (Play mode)
# through the Unity CLI's Pipeline commands. Run from anywhere; it cd's to the project.
#
#   verify.sh launch            open this project's Editor if needed, enter Play mode, keep it ticking
#   verify.sh doctor            read-only: is the instance worth driving?
#   verify.sh state             read-only snapshot (shell, turn, cursor, units, menu, battle)
#   verify.sh press <action>    one simulated button press: confirm cancel pause up down left right attack skill command
#   verify.sh nudge <dx> <dz>   move the tactics cursor by whole tiles (+x east, +z north)
#   verify.sh hold <x> <y> <s>  hold the battle move stick at (x,y) for s seconds
#   verify.sh wait <seconds>    let the game run
#   verify.sh capture <name>    save the Game view to the run's evidence folder as <name>.png
#   verify.sh errors            Editor console errors and exceptions
#   verify.sh eval '<C#>'       raw script eval (namespace LLTacticalWar is imported)
#   verify.sh cleanup           leave Play mode, stop forced ticking, close the Editor only if launch opened it
#
# Evidence goes to Captures/verify/<RUN_ID>/ (git-ignored). RUN_ID defaults to the id launch wrote.
set -uo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
cd "$PROJECT" || exit 1
VDIR="$PROJECT/Captures/verify"
mkdir -p "$VDIR"
MARK="$VDIR/.launched-editor"
RUNFILE="$VDIR/.current-run"
RUN_ID="${RUN_ID:-$(cat "$RUNFILE" 2>/dev/null || date +%Y%m%d-%H%M%S)}"
RUN_DIR="$VDIR/$RUN_ID"
SKILL=(--caller plugin --skill verify-ll-tactical-war)

# CLI flags must come before a `--`; everything after it goes to the Editor command.
ucmd() {
  local pre=() post=() seen=0
  for a in "$@"; do
    if [[ $seen == 0 && "$a" == "--" ]]; then seen=1; continue; fi
    if [[ $seen == 0 ]]; then pre+=("$a"); else post+=("$a"); fi
  done
  if [[ $seen == 1 ]]; then unity command "${pre[@]}" "${SKILL[@]}" --no-pager -- "${post[@]}"
  else unity command "${pre[@]}" "${SKILL[@]}" --no-pager; fi
}

# Run C# in the Editor and print just its return value (or the error).
ev() {
  local tmp; tmp="$(mktemp "$VDIR/.eval-XXXX.cs")"
  printf 'using LLTacticalWar;\n%s\n' "$1" > "$tmp"
  ucmd script eval --file "$tmp" --format json 2>&1 | python -c '
import sys, json
raw = sys.stdin.read()
try: d = json.loads(raw)
except Exception: print(raw); sys.exit(1)
if not d.get("success"):
    print("EVAL FAILED:", json.dumps(d.get("errors") or d.get("data"), indent=1)); sys.exit(1)
r = d.get("data")
if isinstance(r, dict):
    r = r.get("result", r.get("returnValue", r))
print(r if isinstance(r, str) else json.dumps(r, indent=1))'
  local rc=$?; rm -f "$tmp"; return $rc
}

ready() { unity status --format json 2>/dev/null | python -c '
import sys, json, os
d = json.load(sys.stdin); p = os.path.normcase(sys.argv[1])
ok = any(os.path.normcase(i.get("project","")) == p and i.get("state") == "ready" for i in d.get("data",{}).get("instances",[]))
sys.exit(0 if ok else 1)' "$(cygpath -w "$PROJECT" 2>/dev/null || echo "$PROJECT")"; }

case "${1:-}" in
  launch)
    echo "$RUN_ID" > "$RUNFILE"; mkdir -p "$RUN_DIR"
    if ready; then echo "Editor already open for $PROJECT (not ours; cleanup will leave it open)."
    else
      echo "Opening this project's Editor..."
      unity open "$PROJECT" --format json >/dev/null || { echo "unity open failed"; exit 1; }
      touch "$MARK"
      unity status --until-ready --timeout 300 >/dev/null || { echo "Editor never reached ready"; exit 1; }
    fi
    ucmd editor set_auto_tick -- --enabled true >/dev/null
    ucmd editor.playmode start >/dev/null
    for _ in $(seq 1 30); do
      s="$(ev 'return GameManager.I != null ? GameManager.I.State.ToString() : "booting";' 2>/dev/null)"
      [[ "$s" == Title || "$s" == Playing || "$s" == Paused || "$s" == Ended ]] && { echo "READY  shell=$s  run=$RUN_ID  evidence=$RUN_DIR"; exit 0; }
      sleep 1
    done
    echo "Play mode did not boot the game (last: $s)"; exit 1 ;;

  doctor)
    if ready; then echo "editor: ready ($PROJECT)"; else echo "editor: NOT ready for $PROJECT"; exit 1; fi
    ev 'return "playing: " + UnityEngine.Application.isPlaying + "  booted: " + (GameManager.I != null) + "  unity: " + UnityEngine.Application.unityVersion + "  scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path + "  frame: " + UnityEngine.Time.frameCount;'
    ucmd console status --format json 2>/dev/null | python -c '
import sys, json
g = json.load(sys.stdin).get("data", {}).get("result", {}).get("groundTruth", {})
print("console: errors", g.get("consoleErrors"), " warnings", g.get("consoleWarnings"), " compileFailed", g.get("compilationFailed"))' ;;

  state)   ucmd script eval --file "$PROJECT/.claude/skills/verify-ll-tactical-war/scripts/probe.cs" --format json | python -c '
import sys, json; d = json.load(sys.stdin); r = d.get("data")
r = r.get("result", r) if isinstance(r, dict) else r
print(r if d.get("success") else d.get("errors"))' ;;
  press)   ev "GameInput.DebugPress(\"$2\"); return \"pressed $2 at frame \" + UnityEngine.Time.frameCount;" ;;
  nudge)   ev "GameInput.debugCursorNudge = new UnityEngine.Vector2Int($2, $3); return \"nudge ($2,$3)\";" ;;
  hold)    ev "GameInput.DebugHold(new UnityEngine.Vector2($2f, $3f), $4f); return \"hold ($2,$3) for $4s\";" ;;
  wait)    sleep "$2"; echo "waited $2s" ;;
  capture) mkdir -p "$RUN_DIR"
           ucmd capture game_view -- --output-path "$(cygpath -w "$RUN_DIR/$2.png" 2>/dev/null || echo "$RUN_DIR/$2.png")" >/dev/null && echo "$RUN_DIR/$2.png" ;;
  errors)  ucmd console get --format json -- --level error --tail 30 2>&1 | python -c '
import sys, json
d = json.load(sys.stdin); r = d.get("data", {}).get("result", {})
es = r.get("entries", [])
print(len(es), "error entries")
for e in es: print("-", (e.get("message") or e.get("condition") or str(e))[:400])' ;;
  eval)    ev "$2" ;;

  cleanup)
    ucmd editor.playmode stop >/dev/null 2>&1
    ucmd editor set_auto_tick -- --enabled false >/dev/null 2>&1
    if [[ -f "$MARK" ]]; then
      ucmd editor quit >/dev/null 2>&1 && echo "closed the Editor this run opened" || echo "Editor quit refused (unsaved changes?); left open"
      rm -f "$MARK"
    else echo "left the Editor open (it was open before launch)"; fi
    rm -f "$RUNFILE" "$VDIR"/.eval-*.cs
    echo "evidence kept in $RUN_DIR" ;;

  *) sed -n '2,17p' "$0"; exit 2 ;;
esac
