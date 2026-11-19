# Attack and battle

When an enemy is within range, the player chooses Attack, picks the target (a side or rear attack shows a flank bonus) and cuts to a real-time battle. They first pick a formation, then fight with their general while their troops follow. The battle ends when a general falls or after 60 seconds. Surviving HP and troops carry back to the map, and a unit whose general fell is routed.

## Sub-features

- `attack-menu` enables Attack only when a target is in range.
- `attack-target` auto-places the cursor on the first target and shows the enemy card and FRONT / SIDE / BACK ATTACK in the hint.
- `flank-bonus` gives +5 ATK for a side attack and +10 for a rear attack, for that battle only.
- `formation` offers Line, Wedge (ATK +10, DEF −10) and Phalanx (ATK −10, DEF +10), then START BATTLE.
- `battle-control` moves the general with WASD, attacks with J and casts Aura Thrust with K, with a timer and both generals' HUDs.
- `command-menu` opens with Space: Advance / Cover Me / Fall Back / Resume.
- `battle-result` shows a verdict banner, returns to the map, writes back HP and troops, routs a unit whose general fell, and updates the army counts.

## How to get to it (user POV)

- During YOUR TURN, select a unit with an enemy in range and choose Attack. Either move into range first, or attack without moving.
- The enemy also attacks on its turn. That opens the same battle with the enemy as attacker.

## Driving it with verify.sh

Preconditions:

- Baseline after `title-start`. On a fresh board no enemy is in range on round 1. Either march units north over several rounds (the true path) or stage adjacency for the battle under test. Staging snippet: Archer (RANGE 2) two tiles south of the Enemy Thief, then put the cursor on it:
  `$S eval 'var tm=TurnManager.instance; var map=TileMap.instance; var p=tm.players.First(u=>u.playerClass.ClassName=="Archer"); var t=map.TileAt(6,7); tm.PlaceUnit(p,t); p.transform.position=t.position; p.model.ResetMotionAnchor(); return "staged";'`
  then `$S nudge` the cursor to (6,7). Report the staging. It means the approach march was not verified.

- **Open menu.** Select the Archer with `$S press confirm`. You should see `menu: ContextMenu [ARCHER] >Move / Attack / Wait` (Attack enabled).
- **Choose Attack.** Run `$S press down` and `$S press confirm`. You should see `playerState: Attack` and `cursor: (6,9) ... occupant: Enemy Thief`. Capture it to show the red target tile and the enemy card.
- **Engage.** Press Z on the target with `$S press confirm`, then `$S wait 3` and `$S state`. You should see `inBattle: True`, `battle: Pre_Battle`, `stats: battles 1`, and `menu: SideMenu [FORMATION] ... >START BATTLE`.
- **Formation.** Run `$S press up`, `$S press up` and `$S press confirm` to pick Wedge. You should see `formation: Wedge` and player general `atk +10` / `def −10` from the base values.
- **Start.** Run `$S press down`, `$S press down` and `$S press confirm`, then `$S wait 4` and `$S state`. You should see `battle: In_Battle`, `timeLeft` counting down from 60, and alive counts dropping.
- **Fight.** Use `$S hold 0 1 2`, `$S press attack` and `$S press skill`. The player general moves and swings, and Aura Thrust shows its cooldown in the HUD.
- **Command.** Run `$S press command`. You should see `commandMenu: True` and `menu: SideMenu [COMMAND] >Advance / Cover Me / Fall Back / Resume`. `$S press confirm` issues ADVANCE!.
- **Result.** Run `$S wait` until `state` shows `inBattle: False`. HP and troops are written back on the map line. If a general fell, that unit is gone, with `stats: lost` or `routed` incremented.
- **Proof.** Capture the formation menu, mid-battle and the post-battle map, then run `$S errors`.

## Gotchas

- The battle runs in real time between CLI calls (about 3 s each). An unattended general does nothing, and a weak matchup can end within one or two calls. In run `20261119-132939`, a Wedge Archer (DEF 2 → −8) lost all 50 troops and the battle about 12 s in, before `command` was sent. Use Line or Phalanx, or a sturdier unit, when the command menu is the thing under test.
- After a battle ends, the next `confirm` lands on the tactics map, on whatever unit the cursor moved to. Check `state` before pressing.
- `FlankBonus` compares the attacker's position with the defender's facing. Staging a unit behind the enemy (z = 10 is off the board for row-9 enemies) is not possible on the default board, so test BACK ATTACK against a unit that has turned or moved.
- Generals take only 25% damage from troops. HP drops mostly come from the other general.
