using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LLTacticalWar
{
    // The player's turn as one state machine:
    //   Pick -> Move -> (Walking) -> Menu -> Target -> Battle -> next unit
    // X steps back one state: Move -> Pick, Menu -> Move (undoing the walk), Target -> Menu.
    // A state ignores input on the frame it was entered, so one press never moves two states.
    public class TurnStatePlayerTurn : MonoBehaviour
    {
        public enum PlayerState { None, Pick, Move, Walking, Menu, Target, Battle }
        public PlayerState State { get; private set; } = PlayerState.None;
        int enteredFrame;

        TileMap tMap;
        TurnManager tMan;
        PointerScript pointer;

        Player unit;                 // the unit taking orders
        TileMapInfo origin;          // where it stood before this order, for the undo
        Quaternion originRotation;
        List<TileMapInfo> moveTiles, fringeTiles;
        List<Player> targets = new List<Player>();
        int targetIndex;
        int lastPointerIndex = -1;
        Player lastHover;

        public Player Unit => unit;
        public Player CurrentTarget => State == PlayerState.Target ? targets[targetIndex] : null;

        void Start()
        {
            tMap = TileMap.instance;
            tMan = TurnManager.instance;
            pointer = FindAnyObjectByType<PointerScript>();
        }

        void Go(PlayerState s) { State = s; enteredFrame = Time.frameCount; }

        void PointAt(TileMapInfo t) => pointer.SetTile(t.x, t.z);

        void Update()
        {
            if (!GameManager.IsPlaying || GameManager.I.InBattle || Time.frameCount == enteredFrame) return;
            if (GameInput.Danger && State != PlayerState.None && State != PlayerState.Walking && State != PlayerState.Battle)
            {
                tMan.dangerOn = !tMan.dangerOn;
                tMan.RefreshDanger();
                AudioBank.I.Play(tMan.dangerOn ? Sfx.UiConfirm : Sfx.UiCancel, 0.6f);
            }
            switch (State)
            {
                case PlayerState.Pick: Pick(); break;
                case PlayerState.Move: Move(); break;
                case PlayerState.Target: Target(); break;
            }
        }

        // ---------------- pick ----------------

        public void EnterPickState()
        {
            tMan.onGoingTurn = true;
            foreach (var p in tMan.players) p.SetTurnOver(false);
            GameUI.I.Banner("YOUR TURN", "ROUND " + tMan.round, Palette.Azure);
            GameUI.I.SetTurnInfo("YOUR TURN  -  ROUND " + tMan.round, Palette.AzureLight);
            AudioBank.I.Play(Sfx.Turn);
            var first = tMan.players.FirstOrDefault();
            if (first != null) PointAt(first.Tile);
            EnterPick();
        }

        void EnterPick()
        {
            unit = null;
            lastHover = null;
            lastPointerIndex = -1;
            tMap.ClearHighlights();
            GameUI.I.HideContextMenu();
            GameUI.I.HideForecast();
            pointer.inputEnabled = true;
            tMan.RefreshDanger();
            GameUI.I.SetHint("ARROWS  move cursor     Z  select unit     SHIFT  danger zone     ESC  pause");
            Go(PlayerState.Pick);
        }

        void Pick()
        {
            var tile = pointer.Tile;
            var hover = tile.occupant;
            if (hover != lastHover || tile.index != lastPointerIndex)
            {
                lastHover = hover;
                lastPointerIndex = tile.index;
                tMap.ClearHighlights();
                if (hover != null)
                {
                    GameUI.I.ShowUnitCard(hover);
                    var reach = tMap.MoveRange(hover.Tile, hover.playerClass.TileMove);
                    bool mine = hover.team == Team.Player;
                    tMap.HighlightTiles(Fringe(hover, reach), mine ? Palette.Crimson : Palette.CrimsonDark, 0.4f);
                    tMap.HighlightTiles(reach, mine ? Palette.Azure : Palette.Crimson, 0.3f);
                }
                else GameUI.I.HideUnitCard();
            }

            if (!GameInput.Confirm) return;
            if (hover == null || hover.team != Team.Player || hover.isTurnOver) { AudioBank.I.Play(Sfx.UiCancel, 0.6f); return; }
            AudioBank.I.Play(Sfx.UiConfirm);
            unit = hover;
            tMan.currentTurn = unit;
            origin = unit.Tile;
            originRotation = unit.transform.rotation;
            EnterMove();
        }

        // Tiles the unit could attack after moving anywhere in `reach`, outside `reach` itself.
        List<TileMapInfo> Fringe(Player p, List<TileMapInfo> reach)
        {
            var set = new HashSet<TileMapInfo>();
            foreach (var t in reach) set.UnionWith(tMap.AttackRange(t, p.Weapon.MinRange, p.Weapon.MaxRange));
            set.ExceptWith(reach);
            return set.ToList();
        }

        // ---------------- move ----------------

        void EnterMove()
        {
            GameUI.I.HideContextMenu();
            GameUI.I.ShowUnitCard(unit);
            moveTiles = tMap.MoveRange(unit.Tile, unit.playerClass.TileMove);
            fringeTiles = Fringe(unit, moveTiles);
            tMan.RefreshDanger();
            lastPointerIndex = -1;
            pointer.inputEnabled = true;
            GameUI.I.SetHint("ARROWS  choose tile     Z  move here     X  back");
            Go(PlayerState.Move);
        }

        void Move()
        {
            var tile = pointer.Tile;
            if (tile.index != lastPointerIndex)
            {
                lastPointerIndex = tile.index;
                tMap.ClearHighlights();
                tMap.HighlightTiles(fringeTiles, Palette.Crimson, 0.55f);
                tMap.HighlightTiles(moveTiles, Palette.Azure, 0.45f);
                if (moveTiles.Contains(tile))
                    tMap.HighlightTiles(PathFinder.FindPath(unit.Tile, tile, moveTiles), Palette.Gold, 0.6f);
            }

            if (GameInput.Cancel) { AudioBank.I.Play(Sfx.UiCancel); PointAt(unit.Tile); EnterPick(); return; }
            if (!GameInput.Confirm) return;
            if (!moveTiles.Contains(tile)) { AudioBank.I.Play(Sfx.UiCancel, 0.6f); return; }
            AudioBank.I.Play(Sfx.UiConfirm);
            if (tile == unit.Tile) { OpenMenu(); return; }
            var path = PathFinder.FindPath(unit.Tile, tile, moveTiles);
            if (path.Count >= 2) StartCoroutine(Walk(path));
        }

        IEnumerator Walk(List<TileMapInfo> path)
        {
            Go(PlayerState.Walking);
            pointer.inputEnabled = false;
            GameUI.I.SetHint("");
            tMap.ClearHighlights();
            tMap.HighlightTiles(path, Palette.Gold, 0.45f);
            yield return unit.MoveAlong(path);
            tMan.PlaceUnit(unit, path[path.Count - 1]);
            OpenMenu();
        }

        // ---------------- menu ----------------

        void OpenMenu()
        {
            pointer.inputEnabled = false;
            PointAt(unit.Tile);
            tMan.RefreshDanger();
            targets = tMan.TargetsInRange(unit).OrderBy(t => t.Tile.Distance(unit.Tile)).ThenBy(t => t.Tile.x).ToList();
            tMap.ClearHighlights();
            if (targets.Count > 0)
                tMap.HighlightTiles(tMap.AttackRange(unit.Tile, unit.Weapon.MinRange, unit.Weapon.MaxRange), Palette.Crimson, 0.5f);
            tMap.HighlightTile(unit.Tile, Palette.Gold, 0.45f);
            GameUI.I.HideForecast();
            GameUI.I.ShowUnitCard(unit);

            var menu = new KeyMenu(unit.playerClass.ClassName.ToUpper());
            menu.Add("Attack", EnterTarget, () => targets.Count > 0);
            menu.Add("Wait", FinishUnit);
            menu.onCancel = UndoMove;
            GameUI.I.ShowContextMenu(menu, unit.transform.position);
            GameUI.I.SetHint("UP/DOWN  choose     Z  confirm     X  undo move");
            Go(PlayerState.Menu);
        }

        void UndoMove()
        {
            GameUI.I.HideContextMenu();
            if (unit.Tile != origin)
            {
                tMan.PlaceUnit(unit, origin);
                unit.transform.position = origin.position;
                unit.model.ResetMotionAnchor();
                GameUI.I.FloatText(unit.transform.position + Vector3.up * 2f, "MOVE UNDONE", Palette.Parchment, 0.8f);
            }
            unit.transform.rotation = originRotation;
            EnterMove();
        }

        // ---------------- target ----------------

        void EnterTarget()
        {
            GameUI.I.HideContextMenu();
            GameUI.I.HideUnitCard();
            GameUI.I.SetHint("ARROWS  cycle targets     Z  attack     X  back");
            targetIndex = 0;
            ShowTarget();
            Go(PlayerState.Target);
        }

        void ShowTarget()
        {
            var t = targets[targetIndex];
            PointAt(t.Tile);
            unit.FaceTowards(t.transform.position);
            tMap.ClearHighlights();
            tMap.HighlightTiles(tMap.AttackRange(unit.Tile, unit.Weapon.MinRange, unit.Weapon.MaxRange), Palette.Crimson, 0.45f);
            tMap.HighlightTile(t.Tile, Palette.Crimson, 0.75f);
            GameUI.I.ShowForecast(Combat.Predict(unit, t));
        }

        void Target()
        {
            int step = GameInput.RightPressed || GameInput.DownPressed ? 1 : GameInput.LeftPressed || GameInput.UpPressed ? -1 : 0;
            if (step != 0 && targets.Count > 1)
            {
                targetIndex = (targetIndex + step + targets.Count) % targets.Count;
                AudioBank.I.Play(Sfx.UiMove, 0.4f);
                ShowTarget();
            }
            if (GameInput.Cancel) { AudioBank.I.Play(Sfx.UiCancel); OpenMenu(); return; }
            if (GameInput.Confirm) { AudioBank.I.Play(Sfx.UiConfirm); StartCoroutine(Battle(targets[targetIndex])); }
        }

        IEnumerator Battle(Player target)
        {
            Go(PlayerState.Battle);
            GameUI.I.HideForecast();
            GameUI.I.SetHint("");
            tMap.ClearHighlights();
            yield return tMan.Engage(unit, target);
            if (tMan.CheckTacticResult()) { Go(PlayerState.None); yield break; }
            FinishUnit();
        }

        // ---------------- done ----------------

        void FinishUnit()
        {
            GameUI.I.HideContextMenu();
            if (unit != null && unit.isAlive) unit.SetTurnOver(true);
            var next = tMan.players.FirstOrDefault(p => !p.isTurnOver);
            if (next != null) { PointAt(next.Tile); EnterPick(); return; }

            pointer.inputEnabled = false;
            GameUI.I.HideUnitCard();
            GameUI.I.SetHint("");
            tMap.ClearHighlights();
            Go(PlayerState.None);
            tMan.onGoingTurn = false;
            tMan.NextTurn();
        }
    }
}
