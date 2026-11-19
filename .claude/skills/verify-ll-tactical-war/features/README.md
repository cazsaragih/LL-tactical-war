# LL Tactical War verification map

This directory is the maintained source for verifying what a player can do in LL Tactical War. Read this index before driving the game, then use the matching feature file as the recipe. `S` is `.claude/skills/verify-ll-tactical-war/scripts/verify.sh`, run from the project root.

## Baseline preconditions

- `$S launch` printed `READY  shell=Title`, and `$S doctor` shows `playing: True  booted: True`, scene `Assets/Scenes/Main.unity`, and `errors 0`.
- The board is fresh. Play mode always boots the same layout:
  - Players on row 0: Fighter (4,0), Archer (6,0), Dragoon (7,0), Apprentice (9,0).
  - Enemies on row 9: Brawler (4,9), Thief (6,9), Fighter (7,9), Archer (9,9).
  - All face their opponents, at full HP, none broken.
- From the title, `$S press confirm` starts the game. `state` then shows `shell: Playing`, `turn: PlayerTurn  round: 1`, `playerState: Pick`, and the cursor on the Fighter at (4,0).
- Only drive the Editor that `launch` or `doctor` reported for `C:\dev\tactical-rpg`.

## Driving conventions

- One press per CLI call, then `$S state` to see the result. Menus show `>` on the selected item and `(off)` on disabled ones.
- Menus wrap. Choose an item by counting `up` or `down` presses from the `>` position, then `confirm`.
- Cursor coordinates are (x, z). `nudge +1 0` is one tile east and `nudge 0 +1` is one tile north (toward the enemy).
- Unit stats come from `state`: `hp a/b`, the stat line, `[broken]` (can't counter until its side's next phase) and `[done]` (finished acting this turn).
- A precondition the default board can't reach quickly, such as adjacency to an enemy, may be staged with `$S eval`. Use the snippet in [attack-and-battle.md](./attack-and-battle.md) and say so in the report.
- Restart between features with `$S press pause` → Restart, or with `$S cleanup` and `$S launch`.

## Proof and skip reporting

- Capture the action and the resulting state: `state` before and after, and a capture at the visible moment.
- Proof of a mutation means `state` shows the stored change: unit tile, HP, `[broken]`, `[done]`, `round`, `danger:`, or the `stats:` counters.
- Finish with `$S errors` showing `0 error entries`.
- Record the feature ID, the entry point, and any staged precondition with every artifact.
- Report an unreachable path with the command you tried and the unmet precondition. Never report a skipped entry point as verified through a different one.

## Feature entry contract

Each feature file starts with an H1 and one paragraph on the player-visible behavior. It then has exactly four H2s, in this order: `Sub-features`, `How to get to it (user POV)`, `Driving it with verify.sh` (starting with `Preconditions:`), and `Gotchas`. Keep implementation details out of the map.

## Features

- [Title and start](./title-and-start.md): title screen, starting a game, and the first turn banner.
- [Move a unit](./move-unit.md): select, move preview with the attack fringe, move, undo, Wait, and handing off to the next unit.
- [Attack and battle](./attack-and-battle.md): Attack menu, target cycling, the combat forecast, the 1v1 battle scene (hit, miss, crit, Break, kill, 2x, skip), and results carried back to the map.
- [Pause menu](./pause-menu.md): pause, resume, volume sliders, restart and quit to title.
- [Enemy turn and game end](./enemy-turn-and-end.md): the danger zone, ending the player turn, the enemy bot's turn, the next round, and the victory or defeat screen.
