using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 TurnStateEnemyBot. It used to pick a random enemy and wander to a
    // random tile, and that do/while could spin forever once no free tile was left. Now each enemy
    // attacks if it can; otherwise it advances to the free tile that puts it in reach of the
    // weakest player unit, or as close as it can get. There are short pauses so you can follow it.
    public class TurnStateEnemyBot : MonoBehaviour
    {
        TileMap tMap;
        TurnManager tMan;
        PointerScript pointer;
        bool battleDone;

        void Start()
        {
            tMap = TileMap.instance;
            tMan = TurnManager.instance;
            pointer = FindAnyObjectByType<PointerScript>();
        }

        public void InitializeAction()
        {
            tMan.onGoingTurn = true;
            foreach (var e in tMan.enemies)
            {
                e.SetTurnOver(false);
                e.moveEnabled = e.attackEnabled = e.waitEnabled = true;
            }
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

            var order = tMan.enemies.OrderBy(e => NearestPlayerDistance(e)).ToList();
            foreach (var enemy in order)
            {
                if (enemy == null || !enemy.isAlive || tMan.turnState == TurnManager.TurnState.EndGame) continue;
                if (tMan.players.Count == 0) break;
                tMan.currentTurn = enemy;
                pointer.SetTile(enemy.tilePosition % tMap.tileX, enemy.tilePosition / tMap.tileX);
                GameUI.I.ShowUnitCard(enemy);
                yield return new WaitForSeconds(0.45f);

                var target = BestTarget(enemy);
                if (target == null && enemy.moveEnabled)
                {
                    var start = tMap.tiles[enemy.tilePosition];
                    var range = tMap.MoveRange(start, enemy.playerClass.TileMove);
                    tMap.HighlightTiles(range, Palette.CrimsonLight, 0.35f);
                    yield return new WaitForSeconds(0.45f);

                    var dest = ChooseDestination(enemy, range);
                    if (dest != null && dest != start)
                    {
                        var path = PathFinder.FindPath(start, dest, range);
                        tMap.HighlightTiles(path, Palette.Gold, 0.5f);
                        if (path.Count >= 2)
                        {
                            yield return enemy.MoveAlong(path);
                            tMan.PlaceUnit(enemy, dest);
                        }
                    }
                    enemy.moveEnabled = false;
                    tMap.ClearHighlights();
                    target = BestTarget(enemy);
                }

                if (target != null && enemy.attackEnabled)
                {
                    tMap.HighlightTiles(tMap.AttackRange(tMap.tiles[enemy.tilePosition], enemy.playerClass.TileAttack), Palette.CrimsonLight, 0.35f);
                    tMap.HighlightTile(tMap.tiles[target.tilePosition], Palette.Crimson, 0.7f);
                    pointer.SetTile(target.tilePosition % tMap.tileX, target.tilePosition / tMap.tileX);
                    enemy.FaceTowards(target.transform.position);
                    int bonus = TurnManager.FlankBonus(enemy, target, out var label);
                    if (label != null)
                    {
                        GameUI.I.FloatText(target.transform.position + Vector3.up * 2.4f, label, Palette.CrimsonLight, 1.2f);
                        AudioBank.I.Play(Sfx.Flank);
                    }
                    yield return new WaitForSeconds(0.7f);
                    tMap.ClearHighlights();
                    GameUI.I.HideUnitCard();

                    battleDone = false;
                    tMan.battlesFought++;
                    WarSceneManager.instance.StartBattle(target, enemy, Team.Enemy, bonus, label, () => battleDone = true);
                    while (!battleDone) yield return null;

                    enemy.attackEnabled = false;
                    bool enemyDead = !enemy.isAlive, targetDead = !target.isAlive;
                    if (targetDead) tMan.RemoveUnit(target);
                    if (enemyDead) tMan.RemoveUnit(enemy);
                    if (tMan.CheckTacticResult()) yield break;
                    if (enemyDead) continue;
                    yield return new WaitForSeconds(0.4f);
                }

                var nearest = NearestPlayer(enemy);
                if (nearest != null) enemy.FaceTowards(nearest.transform.position);
                enemy.SetTurnOver(true);
                yield return new WaitForSeconds(0.25f);
            }

            GameUI.I.HideUnitCard();
            tMap.ClearHighlights();
            tMan.onGoingTurn = false;
            tMan.NextTurn();
        }

        // weakest player unit in reach (lowest HP + troops), so the bot finishes what it starts
        Player BestTarget(Player enemy)
        {
            return tMan.TargetsInRange(enemy).OrderBy(p => p.currentHealth + p.currentUnits * 2).FirstOrDefault();
        }

        TileMapInfo ChooseDestination(Player enemy, List<TileMapInfo> range)
        {
            TileMapInfo best = null;
            float bestScore = float.MaxValue;
            int reach = enemy.playerClass.TileAttack;
            foreach (var t in range)
            {
                float score = float.MaxValue;
                foreach (var p in tMan.players)
                {
                    if (p == null) continue;
                    int d = t.Distance(tMap.tiles[p.tilePosition]);
                    float s = d <= reach ? -1000f + p.currentHealth + p.currentUnits * 2 : d * 10f;
                    // prefer flanking tiles a little
                    if (d <= reach)
                    {
                        var dir = (t.position - p.transform.position).normalized;
                        if (Vector3.Dot(p.Forward, dir) < -0.5f) s -= 60f;
                    }
                    score = Mathf.Min(score, s);
                }
                score += Random.value; // break ties
                if (score < bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        Player NearestPlayer(Player e) =>
            tMan.players.Where(p => p != null).OrderBy(p => tMap.tiles[p.tilePosition].Distance(tMap.tiles[e.tilePosition])).FirstOrDefault();

        float NearestPlayerDistance(Player e)
        {
            var n = NearestPlayer(e);
            return n == null ? 999f : tMap.tiles[n.tilePosition].Distance(tMap.tiles[e.tilePosition]);
        }
    }
}
