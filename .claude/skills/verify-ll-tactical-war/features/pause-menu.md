# Pause menu

Esc pauses the game anywhere during play, freezing time and showing a menu: Resume, Restart, Music and Sound volume sliders, Quit to Title and Quit Game.

## Sub-features

- `pause-open` uses Esc to freeze time (timeScale 0) and show the menu.
- `pause-resume` uses Resume or Esc again to continue where the player left off.
- `pause-volume` uses Left/Right on Music or Sound to change the volume in 10% steps. The label shows the percentage.
- `pause-restart` reloads the game. Quit to Title does the same.

## How to get to it (user POV)

- Press Esc on the tactics map or in a battle.

## Driving it with verify.sh

Preconditions:

- `shell: Playing`, at any point after `title-start`.

- **Open.** Run `$S press pause` and `$S state`. You should see `shell: Paused`, `timeScale: 0`, and `menu: ... >Resume / Restart / Music < 100% > / Sound < 100% > / Quit to Title / Quit Game` (labels show the current volumes).
- **Volume.** Run `$S press down` ×2 to reach Music, then `$S press left` and `$S state`. You should see the Music label read 90%. Run `$S press right` to put it back.
- **Resume.** Run `$S press up` until Resume is selected, then `$S press confirm`. You should see `shell: Playing`, `timeScale: 1`, and the same `playerState` and units as before pausing.
- **Restart.** From pause, select Restart and confirm, then `$S wait 3` and `$S state`. You should see `shell: Title` and the default board.
- **Proof.** Capture the pause menu, the state lines before and after, and `$S errors`.

## Gotchas

- Never select **Quit Game** in the Editor. It calls the quit path, which can stop Play mode and invalidate the run.
- Volumes are static and survive a Restart within the same Play-mode session. Put them back to 100% before ending a run.
- Not yet driven by this harness. Promote it to proven after the first successful run.
