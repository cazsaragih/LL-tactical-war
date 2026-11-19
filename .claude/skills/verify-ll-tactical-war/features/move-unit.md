# Move a unit

On their turn the player selects one of their units, chooses Move, previews the reachable tiles and path, moves, and then either undoes the move or ends the unit's action with Wait and a facing. When a unit is done, the cursor jumps to the next unit that hasn't acted.

## Sub-features

- `pick` hovers a unit to show its card and range, then selects it to open the Move / Attack / Wait menu.
- `move-preview` shows reachable tiles in blue and the path to the cursor in gold.
- `move-commit` walks the unit along the path. The menu reopens with Move disabled.
- `move-undo` puts the unit back with X in the menu after a move ("MOVE UNDONE"). It returns to tile selection.
- `move-back` backs out of tile selection to the menu with X, before moving.
- `wait-facing` uses Wait, then arrows to choose a facing and Z to confirm. The unit is marked done.
- `next-unit` puts the cursor on the next unit that hasn't acted.

## How to get to it (user POV)

- On the tactics map during YOUR TURN: arrows to the unit, Z, then Move.
- After moving: the menu reopens on Wait. X undoes the move.

## Driving it with verify.sh

Preconditions:

- Baseline, after `title-start`: `playerState: Pick`, cursor on Fighter (4,0). The Fighter has MOVE 2 and tiles (4,1) and (4,2) are open.

- **Select.** Press Z on the Fighter with `$S press confirm`. You should see `playerState: Menu` and `menu: ContextMenu [FIGHTER] >Move / Attack(off) / Wait`.
- **Enter move.** Choose Move with `$S press confirm`. You should see `playerState: Move` and `menu: none`.
- **Preview.** Move the cursor two tiles north with `$S nudge 0 2`, then `$S capture move-preview`. You should see `cursor: (4,2)`. The capture shows blue reachable tiles, a gold path from (4,0) to (4,2), and the hint "ARROWS choose tile  Z move here  X back".
- **Commit.** Press Z with `$S press confirm`, then `$S wait 2` and `$S state`. You should see `Fighter@(4,2)`, `playerState: Menu`, and `menu: ... Move(off) / Attack(off) / >Wait`.
- **Undo.** Press X in the menu with `$S press cancel`, then `$S wait 1` and `$S state`. You should see `Fighter@(4,0)` and `playerState: Move` (back in tile selection, cursor still at (4,2)).
- **Recommit and Wait.** Run `$S press confirm`, `$S wait 2`, then `$S press confirm` (Wait is preselected). You should see `playerState: Facing`.
- **Facing.** Run `$S press right`, `$S capture facing`, `$S press confirm`, `$S wait 1` and `$S state`. You should see `Fighter@(4,2) ... facing East [done]`, `playerState: Pick`, and `cursor: (6,0) ... occupant: Archer`.
- **Proof.** Run `$S capture done` and `$S errors`. The capture shows the Fighter darkened at (4,2) and the Archer's card. The errors check shows `0 error entries`.

## Gotchas

- Attack stays `(off)` unless an enemy is within the unit's RANGE. On the default board nobody is in range on round 1.
- After an undo the game is back in tile selection (`Move`), not the menu. A second `cancel` is needed to reach the menu.
- Z on a tile outside the blue range plays a refusal sound and does nothing. Check `state` before assuming the move happened.
- Trees and rocks block tiles. If a recipe nudges onto one, the move is refused.
- Last proven 2026-11-19, run `20261119-132939`.
