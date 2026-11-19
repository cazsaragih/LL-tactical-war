using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 TurnStatePlayerTurn. Same state machine and the same flow:
    // pick a unit -> Move / Attack / Wait menu -> choose a facing -> next unit. X backs out at
    // each step, and after moving, X in the menu undoes the move.
    public class TurnStatePlayerTurn : MonoBehaviour
    {
        enum PlayerState { None, Pick, Menu, Move, Attack, InBattle, Facing, Animation, End }
        PlayerState playerState = PlayerState.None;

        TileMap tMap;
        TurnManager tMan;
        PointerScript pointer;

        Player unit;
        Player pickedEnemy;
        Vector3 previousPosition;
        Player.Direction previousDirection;
        int previousTile;

        List<TileMapInfo> moveTiles, attackTiles;
        int lastPointerIndex = -1;
        Player lastHover;
        KeyMenu menu;
        Transform facingMarker;

        void Start()
        {
            tMap = TileMap.instance;
            tMan = TurnManager.instance;
            pointer = FindAnyObjectByType<PointerScript>();
            facingMarker = new GameObject("FacingMarker").transform;
            facingMarker.SetParent(GameManager.I.TacticRoot, false);
            for (int i = 0; i < 3; i++)
                Prim.GlowPart("Chevron", PrimitiveType.Cube, facingMarker, new Vector3(0, 0.15f, 0.9f + i * 0.35f), new Vector3(0.5f - i * 0.12f, 0.06f, 0.12f), Palette.Gold);
            facingMarker.gameObject.SetActive(false);
        }

        public void EnterPickState()
        {
            tMan.onGoingTurn = true;
            foreach (var p in tMan.players)
            {
                p.SetTurnOver(false);
                p.moveEnabled = p.attackEnabled = p.waitEnabled = true;
            }
            GameUI.I.Banner("YOUR TURN", "ROUND " + tMan.round, Palette.Azure);
            AudioBank.I.Play(Sfx.Turn);
            var first = tMan.players.FirstOrDefault();
            if (first != null) pointer.SetTile(first.tilePosition % tMap.tileX, first.tilePosition / tMap.tileX);
            EnterPick();
        }

        void EnterPick()
        {
            unit = null;
            lastHover = null;
            lastPointerIndex = -1;
            tMap.ClearHighlights();
            GameUI.I.HideContextMenu();
            pointer.inputEnabled = true;
            GameUI.I.SetHint("ARROWS  move cursor     Z  select unit     ESC  pause");
            GameUI.I.SetTurnInfo("YOUR TURN  -  ROUND " + tMan.round, Palette.AzureLight);
            playerState = PlayerState.Pick;
        }

        void Update()
        {
            if (!GameManager.IsPlaying || GameManager.I.InBattle) return;
            switch (playerState)
            {
                case PlayerState.Pick: PickPlayer(); break;
                case PlayerState.Move: Move(); break;
                case PlayerState.Attack: Attack(); break;
                case PlayerState.Facing: ChooseDirection(); break;
                case PlayerState.End: EndTurn(); break;
            }
        }

        // ---------------- pick ----------------

        void PickPlayer()
        {
            var tile = pointer.Tile;
            var hover = tile?.occupant;
            if (hover != lastHover || tile.index != lastPointerIndex)
            {
                lastHover = hover;
                lastPointerIndex = tile.index;
                tMap.ClearHighlights();
                if (hover != null)
                {
                    GameUI.I.ShowUnitCard(hover);
                    var range = tMap.MoveRange(tMap.tiles[hover.tilePosition], hover.playerClass.TileMove);
                    tMap.HighlightTiles(range, hover.team == Team.Player ? Palette.AzureLight : Palette.CrimsonLight, 0.28f);
                }
                else GameUI.I.HideUnitCard();
            }

            if (GameInput.Confirm)
            {
                if (hover != null && hover.team == Team.Player && !hover.isTurnOver)
                {
                    unit = hover;
                    tMan.currentTurn = unit;
                    AudioBank.I.Play(Sfx.UiConfirm);
                    OpenMenu();
                }
                else AudioBank.I.Play(Sfx.UiCancel, 0.6f);
            }
        }

        // ---------------- menu ----------------

        void OpenMenu()
        {
            pointer.inputEnabled = false;
            pointer.SetTile(unit.tilePosition % tMap.tileX, unit.tilePosition / tMap.tileX);
            tMap.ClearHighlights();
            tMap.HighlightTile(tMap.tiles[unit.tilePosition], Palette.Gold, 0.45f);
            GameUI.I.ShowUnitCard(unit);

            menu = new KeyMenu(unit.playerClass.ClassName.ToUpper());
            menu.Add("Move", () => EnterMove(), () => unit.moveEnabled);
            menu.Add("Attack", () => EnterAttack(), () => unit.attackEnabled && tMan.TargetsInRange(unit).Count > 0);
            menu.Add("Wait", () => EnterFacing());
            menu.onCancel = InMenuCancel;
            GameUI.I.ShowContextMenu(menu, unit.transform.position);
            GameUI.I.SetHint("UP/DOWN  choose     Z  confirm     X  " + (unit.moveEnabled ? "back" : unit.attackEnabled ? "undo move" : "-"));
            playerState = PlayerState.Menu;
        }

        void InMenuCancel()
        {
            // nothing committed yet -> back to picking; moved but not attacked -> undo the move
            if (unit.moveEnabled && unit.attackEnabled) EnterPick();
            else if (!unit.moveEnabled && unit.attackEnabled) CancelMove();
        }

        // ---------------- move ----------------

        void EnterMove()
        {
            GameUI.I.HideContextMenu();
            previousPosition = unit.transform.position;
            previousDirection = unit.playerDirection;
            previousTile = unit.tilePosition;
            moveTiles = tMap.MoveRange(tMap.tiles[unit.tilePosition], unit.playerClass.TileMove);
            tMap.DetermineAvailableTiles(moveTiles);
            lastPointerIndex = -1;
            pointer.inputEnabled = true;
            GameUI.I.SetHint("ARROWS  choose tile     Z  move here     X  back");
            playerState = PlayerState.Move;
        }

        void Move()
        {
            var tile = pointer.Tile;
            if (tile.index != lastPointerIndex)
            {
                lastPointerIndex = tile.index;
                tMap.ClearHighlights();
                tMap.HighlightTiles(moveTiles, Palette.AzureLight, 0.42f);
                if (moveTiles.Contains(tile))
                    tMap.HighlightTiles(PathFinder.FindPath(tMap.tiles[unit.tilePosition], tile, moveTiles), Palette.Gold, 0.55f);
            }

            if (GameInput.Cancel) { AudioBank.I.Play(Sfx.UiCancel); tMap.ResetInRange(); OpenMenu(); return; }
            if (!GameInput.Confirm) return;

            if (!moveTiles.Contains(tile)) { AudioBank.I.Play(Sfx.UiCancel, 0.6f); return; }
            AudioBank.I.Play(Sfx.UiConfirm);
            if (tile.index == unit.tilePosition) { tMap.ResetInRange(); OpenMenu(); return; }

            var path = PathFinder.FindPath(tMap.tiles[unit.tilePosition], tile, moveTiles);
            if (path.Count < 2) return;
            pointer.inputEnabled = false;
            playerState = PlayerState.Animation;
            StartCoroutine(MovingAnimation(path));
        }

        IEnumerator MovingAnimation(List<TileMapInfo> path)
        {
            GameUI.I.SetHint("");
            yield return unit.MoveAlong(path);
            PostMoveCondition(path[path.Count - 1]);
        }

        void PostMoveCondition(TileMapInfo destination)
        {
            unit.moveEnabled = false;
            tMan.PlaceUnit(unit, destination);
            tMap.ResetInRange();
            OpenMenu();
        }

        void CancelMove()
        {
            unit.transform.position = previousPosition;
            unit.Rotate(previousDirection);
            tMan.PlaceUnit(unit, tMap.tiles[previousTile]);
            unit.moveEnabled = true;
            unit.model.ResetMotionAnchor();
            GameUI.I.FloatText(unit.transform.position + Vector3.up * 2f, "MOVE UNDONE", Palette.Parchment, 0.8f);
            EnterMove();
        }

        // ---------------- attack ----------------

        void EnterAttack()
        {
            GameUI.I.HideContextMenu();
            attackTiles = tMap.AttackRange(tMap.tiles[unit.tilePosition], unit.playerClass.TileAttack);
            tMap.DetermineAvailableTiles(attackTiles);
            var targets = tMan.TargetsInRange(unit);
            if (targets.Count > 0) pointer.SetTile(targets[0].tilePosition % tMap.tileX, targets[0].tilePosition / tMap.tileX);
            lastPointerIndex = -1;
            pointer.inputEnabled = true;
            GameUI.I.SetHint("ARROWS  choose target     Z  attack     X  back");
            playerState = PlayerState.Attack;
        }

        void Attack()
        {
            var tile = pointer.Tile;
            if (tile.index != lastPointerIndex)
            {
                lastPointerIndex = tile.index;
                tMap.ClearHighlights();
                tMap.HighlightTiles(attackTiles, Palette.CrimsonLight, 0.4f);
                var enemy = tile.occupant;
                if (enemy != null && enemy.team == Team.Enemy && attackTiles.Contains(tile))
                {
                    tMap.HighlightTile(tile, Palette.Crimson, 0.7f);
                    GameUI.I.ShowUnitCard(enemy);
                    TurnManager.FlankBonus(unit, enemy, out var label);
                    GameUI.I.SetHint((label ?? "FRONT ATTACK") + "     Z  attack     X  back");
                }
                // adjust the unit to look at the cursor, as the 2015 version did
                unit.FaceTowards(tile.position);
            }

            if (GameInput.Cancel) { AudioBank.I.Play(Sfx.UiCancel); tMap.ResetInRange(); OpenMenu(); return; }
            if (!GameInput.Confirm) return;

            var target = tile.occupant;
            if (target == null || target.team != Team.Enemy || !attackTiles.Contains(tile)) { AudioBank.I.Play(Sfx.UiCancel, 0.6f); return; }

            pickedEnemy = target;
            unit.FaceTowards(target.transform.position);
            int bonus = TurnManager.FlankBonus(unit, target, out var flankLabel);
            if (flankLabel != null)
            {
                GameUI.I.FloatText(target.transform.position + Vector3.up * 2.4f, flankLabel, Palette.Gold, 1.2f);
                AudioBank.I.Play(Sfx.Flank);
            }
            tMap.ResetInRange();
            tMap.ClearHighlights();
            pointer.inputEnabled = false;
            GameUI.I.HideUnitCard();
            playerState = PlayerState.InBattle;
            tMan.battlesFought++;
            WarSceneManager.instance.StartBattle(unit, pickedEnemy, Team.Player, bonus, flankLabel, PostAttackCondition);
        }

        void PostAttackCondition()
        {
            unit.attackEnabled = false;
            bool unitDead = !unit.isAlive;
            if (!pickedEnemy.isAlive) tMan.RemoveUnit(pickedEnemy);
            if (unitDead) tMan.RemoveUnit(unit);

            if (tMan.CheckTacticResult()) { playerState = PlayerState.None; return; }

            if (unitDead) { AfterUnitDone(); return; }
            if (!unit.moveEnabled) EnterFacing();
            else OpenMenu();
        }

        // ---------------- wait / facing ----------------

        void EnterFacing()
        {
            GameUI.I.HideContextMenu();
            pointer.inputEnabled = false;
            tMap.ClearHighlights();
            facingMarker.gameObject.SetActive(true);
            GameUI.I.SetHint("ARROWS  choose facing     Z  confirm");
            playerState = PlayerState.Facing;
        }

        void ChooseDirection()
        {
            if (GameInput.UpPressed) unit.Rotate(Player.Direction.North);
            if (GameInput.RightPressed) unit.Rotate(Player.Direction.East);
            if (GameInput.DownPressed) unit.Rotate(Player.Direction.South);
            if (GameInput.LeftPressed) unit.Rotate(Player.Direction.West);
            facingMarker.position = unit.transform.position;
            facingMarker.rotation = unit.transform.rotation;

            if (GameInput.Confirm)
            {
                AudioBank.I.Play(Sfx.UiConfirm);
                facingMarker.gameObject.SetActive(false);
                unit.SetTurnOver(true);
                AfterUnitDone();
            }
        }

        void AfterUnitDone()
        {
            var next = tMan.players.FirstOrDefault(p => p != null && !p.isTurnOver);
            if (next == null) { playerState = PlayerState.End; return; }
            pointer.SetTile(next.tilePosition % tMap.tileX, next.tilePosition / tMap.tileX);
            EnterPick();
        }

        void EndTurn()
        {
            pointer.inputEnabled = false;
            GameUI.I.HideUnitCard();
            GameUI.I.SetHint("");
            tMap.ClearHighlights();
            tMan.onGoingTurn = false;
            tMan.NextTurn();
            playerState = PlayerState.None;
        }
    }
}
