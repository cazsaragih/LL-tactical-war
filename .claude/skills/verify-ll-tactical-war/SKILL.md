---
name: verify-ll-tactical-war
description: Drive LL Tactical War (the Unity 6 tactics game in C:\dev\tactical-rpg) in Play mode the way a player does — title screen, tactics map turns, attacks, the real-time battle, pause and end screens — and capture screenshots and state as proof. Use whenever a change to gameplay, UI, input, battles or turn flow needs to be shown working in the running game, not just compiled.
---

# Verify LL Tactical War

The game runs inside this project's Unity Editor in Play mode. You drive it through the Unity CLI (`unity command`, Pipeline package) with `scripts/verify.sh`. Real keys don't reach an unfocused Editor, so presses go through the game's own debug input hooks (`GameInput.DebugPress`, `DebugHold`, `debugCursorNudge`). Those feed the same `GameInput.Confirm`, `Cancel` and similar properties that the keyboard does, so every menu, state machine and battle path you drive is the player's path.

All commands below run from the project root in Git Bash. `S=.claude/skills/verify-ll-tactical-war/scripts/verify.sh`. Each call takes about 2.5–3 s because the Editor round-trips, and the game keeps running between calls.

## Launch

```bash
$S launch
```

- Opens this project's Editor if none is open. It writes `Captures/verify/.launched-editor` so cleanup knows to close it.
- Turns on forced ticking (`editor set_auto_tick`), so the game keeps running while the Editor is unfocused.
- Enters Play mode.
- Waits until the game has booted.
- Ready means it prints `READY  shell=Title  run=<id>  evidence=<dir>`.
- Play mode always boots fresh from `Assets/Scenes/Main.unity`. There is no save data, and there are no migrations or seeds. The board is the same every run: 4 player units on row 0 and 4 enemies on row 9 (see `features/README.md`).

A cold Editor open takes 1–3 minutes, longer after a package or script change because of the import.

## Doctor

```bash
$S doctor
```

Read-only. Run it first, and again whenever anything looks off. A healthy result shows:

- `editor: ready (...)`
- `playing: True  booted: True`
- the scene `Assets/Scenes/Main.unity`
- `console: errors 0 ... compileFailed False`

If you see `compileFailed True`, or the Editor is in Safe Mode, stop and fix the compile errors first. `$S errors` lists console errors.

## Drive

| Command | Effect |
|---|---|
| `$S state` | Read-only snapshot: shell state, turn, `playerState`, cursor tile and occupant, each unit (tile, HP, troops, facing, `[done]`), the top menu with `>` on the selected item and `(off)` on disabled ones, and the battle state, formation, timer, generals and alive counts. **Run it after every action**. It is the main observation. |
| `$S press <action>` | One press, live for exactly one frame. Actions: `confirm` `cancel` `pause` `up` `down` `left` `right` `attack` `skill` `command`. |
| `$S nudge <dx> <dz>` | Move the tactics cursor by whole tiles. `+x` is east (right on screen) and `+z` is north (toward the enemy). The cursor only moves while `cursor ... input: True`. |
| `$S hold <x> <y> <seconds>` | Hold the battle move stick, for example `hold 0 1 2` walks your general forward for 2 s. |
| `$S wait <seconds>` | Let the game run. |
| `$S capture <name>` | Saves the Game view to `Captures/verify/<run>/<name>.png`. Read the PNG to check it. |
| `$S eval '<C#>'` | Raw `script eval`. `LLTacticalWar` is imported, and the code should `return` a string. Use it only to read state or to stage preconditions, never to perform the action under test. |

Key mapping (what the player presses → action): Z/Enter → `confirm`, X/Backspace → `cancel`, Esc → `pause`, arrows/WASD → `up` `down` `left` `right`, J/left click → `attack`, K/right click → `skill`, Space → `command`.

Before driving a feature, read `features/README.md` and that feature's file.

## Evidence

Every proof captures the **action and the resulting state**, not only the final screen:

1. `$S state` before the action, with the relevant lines quoted.
2. The press, nudge or hold you sent.
3. `$S state` after it, showing what changed (unit tile, `playerState`, menu, HP, troops, `[done]`).
4. `$S capture <step>` at the moments a player would see the change. Read the PNG to confirm it shows what the state claims.
5. `$S errors` at the end: `0 error entries`.

Evidence lives in `Captures/verify/<run-id>/` (git-ignored) and survives cleanup. Name captures by step (`03-move-preview`). In the report, name the feature ID and entry point for every claim.

Standards:

- Drive the player path: presses, nudges and holds.
- `eval` may stage a precondition, for example putting a unit next to an enemy so a battle can be tested without 4 rounds of marching. Say so in the report. A staged precondition never proves the path it skipped.
- Mutations here are in-memory only. Leaving Play mode discards them, so there are no side effects to restore.

## Cleanup

```bash
$S cleanup
```

- Leaves Play mode, which resets all game state, and turns off forced ticking.
- Closes the Editor only if `launch` opened it. An Editor that was already open, possibly holding the user's unsaved work, is left open.
- Never kills Unity by process name. Other Unity Editors on this machine belong to other projects: never drive or close them.
- Evidence in `Captures/verify/<run-id>/` is kept. Check that it is still there after cleanup.

## Isolation and gotchas

- One Editor per project, so two verification runs cannot share this project at once. If `doctor` shows Play mode already running and you didn't start it, someone else is driving: stop and ask.
- Don't pass `--project-path` with a Git Bash path (`/c/dev/...`): the CLI fails to match the Editor. `verify.sh` runs from the project root instead.
- CLI flags (`--format`, `--caller`) go **before** `--`, and Editor command parameters go after it. `verify.sh` handles this.
- A press is consumed by whatever state is active on that frame. Send one press per call and check `state` before the next one when the target state matters.
- Banners ("YOUR TURN", "CHARGE!") last about 1.6 s, shorter than one CLI round-trip. To capture one, trigger and capture in parallel, or don't rely on catching it.
- `state` reads a private field (`TurnStatePlayerTurn.playerState`) by reflection. If that field is renamed, update `scripts/probe.cs`.
