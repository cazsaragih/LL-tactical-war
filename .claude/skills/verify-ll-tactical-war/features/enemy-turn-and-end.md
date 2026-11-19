# Enemy turn and game end

Once every player unit is done, the enemy takes its turn behind an "ENEMY TURN" banner. Each enemy, nearest to the player first, weighs every tile it can reach and every target from there: a likely kill first, then the most damage dealt for the least taken. It walks there, shows the forecast for a moment, and fights in the same 1v1 battle scene. With no one in reach it marches toward the nearest player unit. Then the next round begins. On the player turn, Shift toggles a danger zone over every tile an enemy could attack next turn. When one side has no units left, the game shows VICTORY or DEFEAT with the match stats and Play Again / Quit Game.

## Sub-features

- `end-player-turn` hands over to the enemy after the last unit's Wait or attack.
- `enemy-bot` moves each enemy and attacks when it can, through the forecast and the battle scene.
- `danger-zone` toggles a striped red overlay on the player turn (Shift or C). It covers every tile any enemy could attack next turn, given where units stand.
- `next-round` returns to YOUR TURN with the round incremented, all player units ready, and their BROKEN cleared.
- `end-screen` shows VICTORY or DEFEAT with ROUNDS, BATTLES, ENEMIES DEFEATED and UNITS LOST, and Play Again / Quit Game.

## How to get to it (user POV)

- Wait (or act) with every unit on YOUR TURN.
- Press Shift on the map during YOUR TURN for the danger zone.
- Defeat every unit of one side.

## Driving it with verify.sh

Preconditions:

- Baseline after `title-start`.

- **Danger zone.** `$S press danger`, then `$S state`. You should see `danger: on  tiles N  players in reach: ...` and a capture shows striped red tiles around the enemy line. `$S press danger` again turns it off.
- **End turn.** For each ready unit: `$S press confirm` (select), `$S press confirm` (stay), `$S press confirm` (Wait is preselected when Attack is off). Check `state` between units, because the cursor moves itself. After the last one, `state` shows `turn: EnemyTurn`.
- **Enemy acts.** Start a burst right away: `$S burst enemy 12 1`, plus `$S state` in parallel a few times. Enemy tiles change. An attack shows `battle: Intro` or `Strikes` with the enemy as attacker, and the captures show its forecast on the map and then the battle scene.
- **Danger check.** Every player unit an enemy attacked must have been listed in `players in reach:` at the end of your turn.
- **Next round.** `state` shows `turn: PlayerTurn  round: 2`, and no player unit is `[done]`.
- **End screen.** Play until one side is gone. `state` shows `shell: Ended`, the menu reads `>Play Again / Quit Game`, and `$S capture end` shows VICTORY or DEFEAT with the four stats.
- **Proof.** Capture the danger zone, an enemy battle, the round-2 state and the end screen, then run `$S errors`.

## Gotchas

- During the enemy turn, presses are ignored except in the battle scene (Z held for 2x, X to skip).
- The enemy turn of four units runs about 10 to 25 s, depending on how many attack.
- Never select Quit Game in the Editor (see [pause-menu.md](./pause-menu.md)).
