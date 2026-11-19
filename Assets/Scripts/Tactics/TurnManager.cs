using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 TurnManager: Setup -> PlayerTurn <-> EnemyTurn -> EndGame, with
    // one component per turn state.
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
        public int battlesFought, unitsLost, enemiesRouted;

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
        }

        public Player UnitAt(TileMapInfo t) => t?.occupant;

        public List<Player> Opponents(Player p) => p.team == Team.Player ? enemies : players;

        public List<Player> TargetsInRange(Player p)
        {
            var result = new List<Player>();
            var tile = TileMap.instance.tiles[p.tilePosition];
            foreach (var o in Opponents(p))
                if (o != null && o.isAlive && TileMap.instance.tiles[o.tilePosition].Distance(tile) <= p.playerClass.TileAttack)
                    result.Add(o);
            return result;
        }

        // Back / side attack bonus, the 2015 rule (+10 back, +5 side), measured from the
        // defender's facing so it also works at range 2. The bonus is applied for that one battle
        // only (in 2015 it was added to the unit's attack permanently and stacked).
        public static int FlankBonus(Player attacker, Player defender, out string label)
        {
            var toAttacker = attacker.transform.position - defender.transform.position;
            toAttacker.y = 0f;
            toAttacker.Normalize();
            float dot = Vector3.Dot(defender.Forward, toAttacker);
            if (dot < -0.5f) { label = "BACK ATTACK  +10"; return 10; }
            if (dot < 0.5f) { label = "SIDE ATTACK  +5"; return 5; }
            label = null;
            return 0;
        }

        public void PlaceUnit(Player p, TileMapInfo tile)
        {
            var map = TileMap.instance;
            if (p.tilePosition >= 0 && p.tilePosition < map.tiles.Count && map.tiles[p.tilePosition].occupant == p)
                map.tiles[p.tilePosition].occupant = null;
            p.tilePosition = tile.index;
            tile.occupant = p;
        }

        public void RemoveUnit(Player p)
        {
            if (p == null) return;
            var map = TileMap.instance;
            if (map.tiles[p.tilePosition].occupant == p) map.tiles[p.tilePosition].occupant = null;
            if (p.team == Team.Player) { players.Remove(p); unitsLost++; }
            else { enemies.Remove(p); enemiesRouted++; }
            Fx.I?.TeamBurst(p.transform.position + Vector3.up, Palette.Team(p.team));
            Fx.I?.DeathPuff(p.transform.position);
            AudioBank.I?.Play(Sfx.Death);
            GameUI.I?.FloatText(p.transform.position + Vector3.up * 2.2f, p.DisplayName.ToUpper() + " ROUTED", Palette.Team(p.team), 1.1f);
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
