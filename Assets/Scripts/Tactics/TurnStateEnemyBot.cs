using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LLTacticalWar
{
    // The enemy phase. Each enemy, nearest to the player first, scores every (tile, target) pair
    // it can reach with Combat's odds: a likely kill first, then expected damage dealt minus
    // expected damage taken. With nobody in reach it marches toward the nearest player unit by
    // walking distance. Its attacks play the same battle scene as the player's, after a short
    // look at the forecast. There are pauses so you can follow it.
    public class TurnStateEnemyBot : MonoBehaviour
    {
        TileMap tMap;
        TurnManager tMan;
        PointerScript pointer;

        struct Plan { public TileMapInfo tile; public Player target; public float score; }

        void Start()
        {
            tMap = TileMap.instance;
            tMan = TurnManager.instance;
            pointer = FindAnyObjectByType<PointerScript>();
        }

        void PointAt(TileMapInfo t) => pointer.SetTile(t.x, t.z);

        public void InitializeAction()
        {
            tMan.onGoingTurn = true;
            foreach (var e in tMan.enemies) e.SetTurnOver(false);
            tMan.RefreshDanger();
            GameUI.I.Banner("ENEMY TURN", "ROUND " + tMan.round, Palette.Crimson);
            GameUI.I.SetTurnInfo("ENEMY TURN  -  ROUND " + tMan.round, Palette.CrimsonLight);
            GameUI.I.SetHint("The enemy is moving...     ESC  pause");
            AudioBank.I.Play(Sfx.Turn);
            pointer.inputEnabled = false;
            StartCoroutine(RunTurn());
        }

        IEnumerator RunTurn()
        {
            yield return new WaitForSeconds(1.0f);

            var order = tMan.enemies.OrderBy(NearestPlayerDistance).ToList();
            foreach (var enemy in order)
            {
                if (enemy == null || !enemy.isAlive || tMan.turnState == TurnManager.TurnState.EndGame) continue;
                if (tMan.players.Count == 0) break;
                tMan.currentTurn = enemy;
                PointAt(enemy.Tile);
                GameUI.I.ShowUnitCard(enemy);
                yield return new WaitForSeconds(0.4f);

                var start = enemy.Tile;
                var reach = tMap.MoveRange(start, enemy.playerClass.TileMove);
                var plan = BestAttack(enemy, reach);
                var dest = plan.target != null ? plan.tile : Advance(reach);

                if (dest != start)
                {
                    tMap.HighlightTiles(reach, Palette.Crimson, 0.38f);
                    yield return new WaitForSeconds(0.4f);
                    var path = PathFinder.FindPath(start, dest, reach);
                    tMap.HighlightTiles(path, Palette.Gold, 0.5f);
                    if (path.Count >= 2)
                    {
                        yield return enemy.MoveAlong(path);
                        tMan.PlaceUnit(enemy, dest);
                    }
                    tMap.ClearHighlights();
                }

                if (plan.target != null)
                {
                    var target = plan.target;
                    tMap.HighlightTiles(tMap.AttackRange(enemy.Tile, enemy.Weapon.MinRange, enemy.Weapon.MaxRange), Palette.Crimson, 0.38f);
                    tMap.HighlightTile(target.Tile, Palette.Crimson, 0.75f);
                    PointAt(target.Tile);
                    enemy.FaceTowards(target.transform.position);
                    GameUI.I.HideUnitCard();
                    GameUI.I.ShowForecast(Combat.Predict(enemy, target));
                    yield return new WaitForSeconds(1.1f);
                    GameUI.I.HideForecast();
                    tMap.ClearHighlights();

                    yield return tMan.Engage(enemy, target);
                    if (tMan.CheckTacticResult()) yield break;
                    if (!enemy.isAlive) continue;
                    yield return new WaitForSeconds(0.3f);
                }
                else
                {
                    var nearest = NearestPlayer(enemy);
                    if (nearest != null) enemy.FaceTowards(nearest.transform.position);
                }
                enemy.SetTurnOver(true);
                yield return new WaitForSeconds(0.2f);
            }

            GameUI.I.HideUnitCard();
            tMap.ClearHighlights();
            tMan.onGoingTurn = false;
            tMan.NextTurn();
        }

        // Every (tile, target) pair in reach, scored on the exchange's odds. Ties go to the
        // shorter move.
        Plan BestAttack(Player enemy, List<TileMapInfo> reach)
        {
            var best = new Plan { score = float.MinValue };
            foreach (var tile in reach)
                foreach (var target in tMan.TargetsInRange(enemy, tile))
                {
                    var odds = Combat.Expect(Combat.Predict(enemy, target, tile.Distance(target.Tile)));
                    float score = odds.kill * 1000f + odds.dealt - odds.taken - tile.Distance(enemy.Tile) * 0.01f;
                    if (score > best.score) best = new Plan { tile = tile, target = target, score = score };
                }
            return best;
        }

        // The reachable tile with the shortest walk to any player unit (trees and rocks block;
        // units don't, since they move).
        TileMapInfo Advance(List<TileMapInfo> reach)
        {
            var field = new Dictionary<TileMapInfo, int>();
            var q = new Queue<TileMapInfo>();
            foreach (var p in tMan.players) { field[p.Tile] = 0; q.Enqueue(p.Tile); }
            while (q.Count > 0)
            {
                var t = q.Dequeue();
                foreach (var n in t.Neighbours)
                    if (n != null && !n.blocked && !field.ContainsKey(n)) { field[n] = field[t] + 1; q.Enqueue(n); }
            }
            return reach.OrderBy(t => field.TryGetValue(t, out var d) ? d : 999).ThenBy(_ => Random.value).First();
        }

        Player NearestPlayer(Player e) => tMan.players.OrderBy(p => p.Tile.Distance(e.Tile)).FirstOrDefault();

        float NearestPlayerDistance(Player e)
        {
            var n = NearestPlayer(e);
            return n == null ? 999f : n.Tile.Distance(e.Tile);
        }
    }
}
