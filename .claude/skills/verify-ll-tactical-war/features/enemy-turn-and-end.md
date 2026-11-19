# Enemy turn and game end

Once every player unit is done, the enemy takes its turn behind an "ENEMY TURN" banner. Each enemy moves toward the player and attacks when in range, which starts a battle the player still fights. Then the next round begins. When one side has no units left, the game shows a victory or defeat screen with the battle stats and Play Again / Quit Game.

## Sub-features

- `end-player-turn` hands over to the enemy after the last unit's facing is confirmed.
- `enemy-bot` moves each enemy in turn and may attack, which starts a battle with the enemy as attacker.
- `next-round` returns to YOUR TURN with the round incremented and all player units reset.
- `end-screen` shows victory or defeat with stats (battles, units lost, enemies routed) and Play Again / Quit Game.

## How to get to it (user POV)

- Wait (or act) with every unit on YOUR TURN.
- Win or lose every unit of one side.

## Driving it with verify.sh

Preconditions:

- Baseline after `title-start`.

- **End turn.** For each of the 4 units, run `$S press confirm`, `$S press down`, `$S press down`, `$S press confirm` (Wait), then `$S press confirm` (keep facing). Check `state` between units, because the cursor moves itself. After the fourth, `state` shows `turn: EnemyTurn`.
- **Enemy acts.** Run `$S wait 5` and `$S state` repeatedly. Enemy tiles change. If an enemy attacks, you see `inBattle: True` and the formation menu, and you drive it as in [attack-and-battle.md](./attack-and-battle.md).
- **Next round.** `state` shows `turn: PlayerTurn  round: 2`, and no player unit is `[done]`.
- **End screen.** Reaching it on the true path takes many rounds. Run `state` until `shell: Ended`, then `$S capture end`. The menu reads `>Play Again / Quit Game`, and the capture shows VICTORY or DEFEAT with stats.
- **Proof.** Capture the enemy turn, the round-2 state and the end screen, then run `$S errors`.

## Gotchas

- During the enemy turn, presses are ignored until a battle opens. Don't queue presses blindly.
- An enemy-initiated battle starts with the formation menu just like the player's. Leaving it unattended blocks the turn.
- Never select Quit Game in the Editor (see [pause-menu.md](./pause-menu.md)).
- Not yet driven by this harness. Promote it to proven after the first successful run.
