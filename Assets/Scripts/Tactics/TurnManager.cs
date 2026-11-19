using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 TurnManager: Setup -> PlayerTurn <-> EnemyTurn -> EndGame, with
    // one component per turn state. Both turns fight through Engage.
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager instance;

        public readonly List<Player> players = new List<Player>();
        public readonly List<Player> enemies = new List<Player>();

        public Player currentTurn;
        public bool onGoingTurn;
        public bool running;

        public enum TurnState { Setup, PlayerTurn, EnemyTurn, EndGame }
        public TurnState turnState = TurnState.Setup;

        public enum FirstTurn { Player, Enemy }
        public FirstTurn firstTurn = FirstTurn.Player;

        public int round = 1;
        public int battlesFought, unitsLost, enemiesDefeated;

        // Danger zone: every tile some enemy could attack next turn. Toggled by the player.
        public bool dangerOn;
        public readonly HashSet<TileMapInfo> dangerTiles = new HashSet<TileMapInfo>();

        // The last exchange, kept for the verify probe.
        public Forecast lastForecast;
        public List<Strike> lastStrikes = new List<Strike>();

        readonly System.Random rng = new System.Random();

        TurnStateStart turnStateStart;
        TurnStatePlayerTurn turnStatePlayerTurn;
        TurnStateEnemyBot turnStateEnemyBot;

        void Awake()
        {
            instance = this;
            turnStateStart = gameObject.AddComponent<TurnStateStart>();
            turnStatePlayerTurn = gameObject.AddComponent<TurnStatePlayerTurn>();
            turnStateEnemyBot = gameObject.AddComponent<TurnStateEnemyBot>();
        }

        public void SpawnArmies() => turnStateStart.Initialize();

        public void Begin()
        {
            running = true;
            turnState = firstTurn == FirstTurn.Player ? TurnState.PlayerTurn : TurnState.EnemyTurn;
            onGoingTurn = false;
        }

        void Update()
        {
            if (!running || !GameManager.IsPlaying) return;
            switch (turnState)
            {
                case TurnState.PlayerTurn:
                    if (!onGoingTurn) turnStatePlayerTurn.EnterPickState();
                    break;
                case TurnState.EnemyTurn:
                    if (!onGoingTurn) turnStateEnemyBot.InitializeAction();
                    break;
            }
        }

        public void NextTurn()
        {
            if (turnState == TurnState.EndGame) return;
            if (turnState == TurnState.PlayerTurn) turnState = TurnState.EnemyTurn;
            else { turnState = TurnState.PlayerTurn; round++; }
            // a broken unit recovers when its own side's phase begins
            foreach (var u in turnState == TurnState.PlayerTurn ? players : enemies) u.broken = false;
            // spent units are greyed only during their own side's phase
            foreach (var u in players) u.SetTurnOver(false);
            foreach (var u in enemies) u.SetTurnOver(false);
        }

        public Player UnitAt(TileMapInfo t) => t?.occupant;

        // From every tile an enemy can walk to, the ring its weapon reaches. Shown only on the
        // player's turn, and only while toggled on.
        public void RefreshDanger()
        {
            var map = TileMap.instance;
            dangerTiles.Clear();
            foreach (var e in enemies)
                foreach (var t in map.MoveRange(e.Tile, e.playerClass.TileMove))
                    dangerTiles.UnionWith(map.AttackRange(t, e.Weapon.MinRange, e.Weapon.MaxRange));
            map.ShowDanger(dangerOn && turnState == TurnState.PlayerTurn ? dangerTiles : null);
        }

        public List<Player> Opponents(Player p) => p.team == Team.Player ? enemies : players;

        // Foes the unit's weapon reaches from `from` (its own tile when null).
        public List<Player> TargetsInRange(Player p, TileMapInfo from = null)
        {
            from ??= p.Tile;
            var result = new List<Player>();
            foreach (var o in Opponents(p))
                if (o != null && o.isAlive && p.Weapon.Covers(o.Tile.Distance(from)))
                    result.Add(o);
            return result;
        }

        // One full exchange: forecast, every roll decided up front, the battle scene plays it,
        // the result is written back, and the fallen leave the board.
        public IEnumerator Engage(Player attacker, Player defender)
        {
            battlesFought++;
            lastForecast = Combat.Predict(attacker, defender);
            lastStrikes = Combat.Resolve(lastForecast, rng);
            yield return BattleScene.instance.Play(lastForecast, lastStrikes);
            Combat.Apply(lastForecast, lastStrikes);
            if (!defender.isAlive) RemoveUnit(defender);
            if (!attacker.isAlive) RemoveUnit(attacker);
        }

        public void PlaceUnit(Player p, TileMapInfo tile)
        {
            var map = TileMap.instance;
            if (p.tilePosition >= 0 && p.tilePosition < map.tiles.Count && map.tiles[p.tilePosition].occupant == p)
                map.tiles[p.tilePosition].occupant = null;
            p.tilePosition = tile.index;
            tile.occupant = p;
        }

        // The unit leaves the board at once; its figure fades out after.
        public void RemoveUnit(Player p)
        {
            if (p == null) return;
            if (p.Tile.occupant == p) p.Tile.occupant = null;
            if (p.team == Team.Player) { players.Remove(p); unitsLost++; }
            else { enemies.Remove(p); enemiesDefeated++; }
            RefreshDanger();
            StartCoroutine(FadeOut(p));
        }

        IEnumerator FadeOut(Player p)
        {
            GameUI.I?.FloatText(p.transform.position + Vector3.up * 2.2f, p.DisplayName.ToUpper() + " DEFEATED", Palette.TeamLight(p.team), 1.1f);
            Fx.I?.DeathPuff(p.transform.position);
            AudioBank.I?.Play(Sfx.Death);
            p.model.SetDimmed(true);
            var s0 = p.transform.localScale;
            var p0 = p.transform.position;
            yield return Ease.Tween(0.7f, k =>
            {
                p.transform.localScale = new Vector3(s0.x * (1f - k * 0.6f), s0.y * (1f - k), s0.z * (1f - k * 0.6f));
                p.transform.position = p0 + Vector3.down * 0.3f * k;
            }, Ease.InOutSine, false);
            Fx.I?.DeathPuff(p0);
            Destroy(p.gameObject);
        }

        public bool CheckTacticResult()
        {
            if (players.Count == 0 || enemies.Count == 0)
            {
                turnState = TurnState.EndGame;
                running = false;
                GameManager.I.EndGame(enemies.Count == 0);
                return true;
            }
            return false;
        }
    }
}
