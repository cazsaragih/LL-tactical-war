# Move a unit

On their turn the player selects one of their units, sees where it can walk (blue) and what it could attack after walking (red fringe), picks a tile and walks there. A menu then offers Attack (only with a foe in range) or Wait. X in the menu undoes the walk. Wait ends the unit's turn: it dims and the cursor jumps to the next ready unit.

## Sub-features

- `pick` hovers a unit to show its card (HP bar, weapon, stat line, BROKEN or DONE chip) and its reach. Z on a ready player unit selects it.
- `move-preview` shows the blue move range, the red fringe of tiles it could attack after moving, and the gold path to the cursor.
- `move-back` returns to Pick with X before moving.
- `move-commit` walks the unit along the path and opens the menu: `Attack` / `Wait`. Choosing its own tile opens the menu without walking.
- `move-undo` puts the unit back with X in the menu ("MOVE UNDONE") and returns to tile selection.
- `wait` ends the unit's action. It dims, shows DONE on its card, and the cursor moves to the next ready unit.

## How to get to it (user POV)

- On the tactics map during YOUR TURN: arrows to the unit, Z, arrows to a blue tile, Z.

## Driving it with verify.sh

Preconditions:

- Baseline, after `title-start`: `playerState: Pick`, cursor on Fighter (4,0). The Fighter has MOV 2 and tiles (4,1) and (4,2) are open.

- **Select.** Press Z on the Fighter with `$S press confirm`. You should see `playerState: Move  unit: Fighter` and `menu: none`.
- **Preview.** `$S nudge 0 2`, then `$S capture move-preview`. You should see `cursor: (4,2)`. The capture shows blue tiles with a red fringe beyond them, a gold path from (4,0) to (4,2), and the hint "ARROWS choose tile  Z move here  X back".
- **Commit.** `$S press confirm`, `$S wait 2`, `$S state`. You should see `Fighter@(4,2)`, `playerState: Menu` and `menu: ContextMenu [FIGHTER] Attack(off) / >Wait`.
- **Undo.** `$S press cancel`, `$S wait 1`, `$S state`. You should see `Fighter@(4,0)` and `playerState: Move` with the cursor still at (4,2).
- **Recommit and Wait.** `$S press confirm`, `$S wait 2`, `$S press confirm` (Wait is preselected when Attack is off). You should see `Fighter@(4,2) ... [done]`, `playerState: Pick` and `cursor: (6,0) ... occupant: Archer`.
- **Proof.** `$S capture done` and `$S errors`. The capture shows the Fighter dimmed at (4,2) and the Archer's card. The errors check shows `0 error entries`.

## Gotchas

- Attack stays `(off)` unless a foe is within the weapon's range from the unit's tile. On the default board nobody is in range on round 1.
- After an undo the game is back in tile selection (`Move`), not the menu. A second `cancel` returns to Pick.
- Z on a tile outside the blue range plays a refusal sound and does nothing. Check `state` before assuming the move happened.
- Trees and rocks block tiles, and so do other units. If a recipe nudges onto one, the move is refused.
- A state ignores presses on the frame it was entered, so one press never moves two states.
