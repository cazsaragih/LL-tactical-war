# LL Tactical War (2026 revival)

A port of the 2015 Unity 4 project
[cazsaragih/LL-tactical-war](https://github.com/cazsaragih/LL-tactical-war) to Unity 6
(6000.6.9f1, URP). Its design is unchanged: a turn-based grid tactics map where every attack
cuts to a real-time battle between two generals and their troops.

The 2015 repo held only `Scripts/`. Its scenes, prefabs, models, animators and NavMesh were
never committed, so everything you see is now built in code from basic shapes. The scene is
`Assets/Scenes/Main.unity`, and one `GameManager` object boots the whole game.

## Controls

| Where | Keys |
|---|---|
| Menus | Up/Down to choose · **Z** or **Enter** to confirm · **X** or **Backspace** to go back · Left/Right to change sliders |
| Tactics map | Arrows or WASD move the cursor · **Z** selects a unit, then Move / Attack / Wait · after Wait, the arrows pick a facing · **X** undoes a move · **Esc** pauses |
| Battle | **WASD** moves your general · **left click** or **J** attacks · **right click** or **K** casts Aura Thrust · **Space** opens the command menu (Advance / Cover Me / Fall Back) |

## Rules carried over from 2015

- Six classes: Apprentice, Archer, Brawler, Dragoon, Fighter, Thief. Each keeps its 2015 troop
  count, attack range and per-terrain movement.
- Each unit is a general boosted by +150 HP and +10 ATK, leading troops at the class's base stats.
- Attacking from behind gives +10 ATK and from the side +5, for that one battle.
- Formations: Line (balanced), Wedge (ATK +10, DEF −10), Phalanx (ATK −10, DEF +10).
- A battle ends when either general falls or after 60 seconds. Surviving HP and troops carry
  back to the map, and a unit whose general falls is removed.

## Where things live

```
Assets/Scripts/
  Core/     GameManager (shell + boot), Palette (art bible), AudioBank (synthesized audio),
            Fx (pooled particles), Ease, GameInput, Prim
  Units/    BaseClass + the six classes, UnitFactory (primitive figures), UnitModel (motion)
  Tactics/  TileMap, TileMapInfo, PathFinder (A*), PointerScript, TurnManager,
            TurnStateStart / TurnStatePlayerTurn / TurnStateEnemyBot, Player, Scenery
  War/      WarSceneManager, Soldier, TroopAI, PlayerController, BaseWeapon (+ subclasses),
            BaseSkill / BaseSkill_AuraThrust / AuraThrust_Projectile, ArrowTracer
  UI/       GameUI, MenuView, KeyMenu, HoverSelect
  Editor/   ProjectSetup (menu: LL Tactical War > Run Setup)
```
