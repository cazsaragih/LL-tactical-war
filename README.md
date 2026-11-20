# LL Tactical War (2026 revival)

A port of the 2015 Unity 4 project
[cazsaragih/LL-tactical-war](https://github.com/cazsaragih/LL-tactical-war) to Unity 6
(6000.6.9f1, URP). It is a turn-based grid tactics map. The 2015 real-time battle between two
generals and their troops is replaced by genre-standard 1v1 combat: every attack cuts to a short
battle scene between the two units.

The 2015 repo held only `Scripts/`. Its scenes, prefabs, models, animators and NavMesh were
never committed, so the board, scenery and effects are built in code from basic shapes. The units
are the [KayKit Adventurers](https://kaylousberg.com) pack by Kay Lousberg (CC0, licence in
`Assets/Resources/KayKit/`), loaded from Resources and animated through Playables with no
controller asset. The Dragoon rides the Horse from Quaternius's
[Ultimate Animated Animals](https://quaternius.com/packs/ultimateanimatedanimals.html) (CC0,
licence in `Assets/Resources/Quaternius/`), and its lance is code-built because no pack has one.
The scene is `Assets/Scenes/Main.unity`, and one `GameManager` object boots the whole game.
Everything draws with one toon shader (`Assets/Shaders/Toon.shader`): three light bands with cool
blue-violet shadows, a warm rim on figures, and inverted-hull outlines drawn by the renderer's
ToonOutlines feature.

## Controls

| Where | Keys |
|---|---|
| Menus | Up/Down to choose · **Z** or **Enter** to confirm · **X** or **Backspace** to go back · Left/Right to change sliders |
| Tactics map | Arrows or WASD move the cursor · **Z** selects a unit, **Z** again on a blue tile moves it, then Attack / Wait · arrows cycle targets · **X** steps back (and undoes a move) · **Shift** or **C** toggles the danger zone · **Esc** pauses |
| Battle scene | Hold **Z** for double speed · **X** skips to the result |

## Rules

- Six classes from 2015: Apprentice, Archer, Brawler, Dragoon, Fighter, Thief. Each keeps its
  weapon and per-terrain movement. Stats (HP, Str, Mag, Skl, Spd, Lck, Def, Res) live in each
  class; weapon numbers and ranges live in one table (`Units/Weapons.cs`).
- Range comes from the weapon: swords, lances, axes and daggers 1, crossbows 2 (not 1), Fire 1-2.
- Damage is Str (Mag for Fire) plus Might, minus the foe's Def (Res for Fire).
- Hit is weapon Hit + Skl x2 + Lck/2, minus the foe's avoid (Spd x2 + Lck), rolled with two
  averaged random numbers. Crit is weapon Crit + Skl/2, minus the foe's Lck, for triple damage.
- A side with 5 more Spd than its foe strikes twice.
- Order: attacker, the defender's counter (if its weapon reaches and it isn't broken), then the
  faster side's follow-up.
- Triangle: sword beats axe, axe beats lance, lance beats sword. A landed hit with the
  advantage **breaks** the target: it can't counter until its own side's next phase.
- A unit at 0 HP leaves the board. The game ends when one side has no units.

## Where things live

```
Assets/Scripts/
  Core/     GameManager (shell + boot, lights, grade, camera lenses), Palette (art bible and
            toon materials), AudioBank (synthesized audio), Fx (pooled particles), Ease, GameInput,
            Prim, CloudShadows (the sun's drifting cookie), OutlineNormals (smoothed normals for outlines)
  Units/    BaseClass + the six classes, Stats, Weapons (the weapon table and the triangle),
            UnitLooks (each class's KayKit model, props, team swatches and clips),
            UnitFactory (builds figures), UnitModel (Playables animation), TeamSkin (team recolour)
  Tactics/  TileMap, TileMapInfo, PathFinder (A*), PointerScript, TurnManager,
            TurnStateStart / TurnStatePlayerTurn / TurnStateEnemyBot, Player, Scenery,
            Combat (pure forecast / resolve / apply), BattleScene (the 1v1 scene)
  UI/       GameUI (+ GameUI.Combat: forecast and battle HUD), MenuView, KeyMenu, HoverSelect
  Editor/   ProjectSetup (menu: LL Tactical War > Run Setup: materials, shadows, SSAO, outlines),
            KayKitImport / EnvironmentImport / QuaterniusImport (pack import settings),
            OutlineNormalsImport (bakes outline normals on import)
Assets/Shaders/  Toon (every surface), SkyGradient (the sky)
```
