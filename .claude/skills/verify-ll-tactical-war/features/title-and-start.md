# Title and start

The game opens on a title screen over an orbiting camera. Pressing start fades to the tactics map, where the player's first turn begins with a "Your Turn · Round 1" scroll that unrolls to a horn call.

## Sub-features

- `title-show` shows the title screen on boot.
- `title-start` starts the game from the title.
- `turn-banner` shows the YOUR TURN banner and puts the cursor on the first player unit.

## How to get to it (user POV)

- Launch the game, which lands on the title.
- Press Z, Enter or Space on the title.
- Choose "Quit to Title" in the pause menu, or "Play Again" on the end screen. Both reload the game to the title.

## Driving it with verify.sh

Preconditions:

- `$S launch` printed `READY  shell=Title`.

- **Title shows.** Run `$S state` and `$S capture title`. You should see `shell: Title`, `turn: Setup`, `menu: none`, and a capture showing the title over the board.
- **Start.** Press start with `$S press confirm`, then `$S wait 3` and `$S state`. You should see `shell: Playing`, `turn: PlayerTurn  round: 1`, `playerState: Pick`, and `cursor: (4,0) ... occupant: Fighter`.
- **Proof.** Run `$S capture your-turn`. It shows the turn plate "Your Turn · Round 1" (set in small caps), the army plate with a blue and a red seal and the counts 4 and 4, and the hint slip "ARROWS move cursor  Z select unit  SHIFT danger zone  ESC pause".

## Gotchas

- The banner lasts about 1.6 s. A capture taken after `state` usually misses it, so the turn label in the top bar is the durable proof.
- `AnyStart` also accepts Space, but `verify.sh` only sends `confirm`. Space on the title is unverified by this harness.
