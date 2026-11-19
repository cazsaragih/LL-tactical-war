# Attack and battle

When a foe is within the unit's weapon range, the player chooses Attack, cycles through the valid targets while a combat forecast shows both sides' numbers, and confirms. The map fades to a 1v1 battle scene where the two units trade the strikes the game has already rolled: attacker, defender's counter, then the faster side's follow-up. Hits, misses, crits, Break and kills each have their own moment. The HP left on the map is the HP after the last strike, and a unit at 0 fades off the board.

## Sub-features

- `attack-menu` enables Attack only when a foe is in range from the unit's current tile (weapon min to max range, so a bow can't attack at distance 1).
- `target-cycle` moves the cursor to the first target. Arrows cycle through valid targets only. X returns to the menu.
- `forecast` shows player left (blue) and enemy right (crimson): name, weapon with a triangle marker, HP now and after (if every hit lands), DMG (x2 when doubling), HIT, CRIT, and BREAK on the side that would break the other. A side that can't counter shows `--`.
- `battle-scene` fades to the stage with both HUD panels (name, weapon, HP number and bar with a pale ghost segment, HIT, DMG, CRIT). Melee lunges, bows loose an arcing arrow, Fire sends a bolt.
- `strike-feel`: a hit flashes and knocks back the target, sparks, a short hit-stop, a shake and a rising damage number. A miss sidesteps with MISS. A crit slows, pushes the camera in, flashes and pops CRITICAL. Break stamps BREAK and staggers. A kill collapses the target and holds a beat.
- `battle-speed` plays at 2x while Z is held. X skips to the result. Neither changes the outcome.
- `break` stops the broken unit's counters until its own side's next phase.
- `battle-result` returns to the map with HP, `[broken]` and deaths written back. The attacking unit is `[done]`.

## How to get to it (user POV)

- During YOUR TURN, Z on a ready unit, pick a tile in the blue range (its own tile is fine), then Attack in the menu.
- The enemy attacks on its turn. It shows the forecast for a moment, then the same battle scene.

## Driving it with verify.sh

Preconditions:

- Baseline after `title-start`. On a fresh board no foe is in reach on round 1. Either march units north over a few rounds (the true path) or stage adjacency. Staging snippet: Fighter next to the Enemy Brawler (sword beats club, so a hit breaks it), cursor on the Fighter:
  `$S eval 'var tm=TurnManager.instance; var map=TileMap.instance; var p=tm.players.First(u=>u.playerClass.ClassName=="Fighter"); var t=map.TileAt(4,8); tm.PlaceUnit(p,t); p.transform.position=t.position; p.model.ResetMotionAnchor(); UnityEngine.Object.FindAnyObjectByType<PointerScript>().SetTile(4,8); return "staged";'`
  Report the staging. It means the approach march was not verified.

- **Select and stay.** `$S press confirm` twice (select, then move to its own tile). `state` shows `playerState: Menu` and `menu: ContextMenu [FIGHTER] >Attack / Wait`.
- **Target.** `$S press confirm`. `state` shows `playerState: Target ... target: Enemy Brawler`, a `forecast panel:` line and a `Combat.Predict:` line with the same HP, DMG, HIT, CRIT and BREAK. `$S capture forecast` shows the panel. With two foes in range, `$S press right` cycles and both lines change together.
- **Fight.** Start the press and a burst together: `$S press confirm & $S burst battle 8 0.6; wait`. Then `$S state`. `last strikes:` lists every strike (`A Hit 8 -> hp 24 BREAK`). The captures show the wind-up, the lunge, the impact with its number and the result.
- **Mid-battle state.** For a state line inside the battle, run `$S state` in parallel about 2 s after the press. It shows `battle: Strikes  strike n/m ... hud hp: ...`.
- **Fast and skip.** One eval presses and holds Z: `$S eval 'GameInput.DebugPress("confirm"); GameInput.DebugHold("confirm", 6f); return "attack at 2x";'` then `$S state` shows `fast True` and `timeScale: 2` during strikes. `$S press cancel` during the battle sets `skipped True` and jumps to the result.
- **Result.** `state` shows `battle: Idle`, the target's `hp` equal to the last strike's HP after, `[broken]` if a strike broke it, the attacker `[done]`, and `stats: battles` up by one. A kill drops the unit from its list and raises `lost` or `defeated`.
- **Proof.** Forecast capture, the battle burst, the post-battle map, then `$S errors`.

## Gotchas

- A whole exchange takes 3 to 6 s, so a lone `state` call usually lands after it. Use the parallel recipes above.
- A unit already broken at the start shows `--` for its counter and no BREAK on the attacker's side.
- Bows can't attack or counter at distance 1, and Fire reaches 1 to 2. An Archer next to a foe has `Attack(off)`.
- Crit and kill can't be forced by input. Stage them (for example `p.stats.Skl = 200` for a near-certain crit) and say so.
