// Read-only state snapshot for verify-ll-tactical-war. Run with:  scripts/verify.sh state
// Prints one line per field. Changes nothing in the game.
using System.Linq;
using LLTacticalWar;

string Probe()
{
    var sb = new System.Text.StringBuilder();
    var gm = GameManager.I;
    sb.AppendLine("playing_in_editor: " + UnityEngine.Application.isPlaying);
    if (gm == null) { sb.AppendLine("game: not booted (GameManager.I is null)"); return sb.ToString(); }
    sb.AppendLine("frame: " + UnityEngine.Time.frameCount + "  timeScale: " + UnityEngine.Time.timeScale);
    sb.AppendLine("shell: " + gm.State + "  inBattle: " + gm.InBattle);

    var tm = TurnManager.instance;
    if (tm != null)
    {
        sb.AppendLine("turn: " + tm.turnState + "  round: " + tm.round + "  current: " + (tm.currentTurn != null ? tm.currentTurn.DisplayName : "-"));
        var pt = UnityEngine.Object.FindAnyObjectByType<TurnStatePlayerTurn>();
        if (pt != null)
        {
            sb.AppendLine("playerState: " + pt.State + "  unit: " + (pt.Unit != null ? pt.Unit.DisplayName : "-") + "  target: " + (pt.CurrentTarget != null ? pt.CurrentTarget.DisplayName : "-"));
            var panel = GameUI.I != null ? GameUI.I.ForecastText : "";
            if (!string.IsNullOrEmpty(panel))
            {
                sb.AppendLine("forecast panel: " + System.Text.RegularExpressions.Regex.Replace(panel, "<[^>]+>", ""));
                if (pt.Unit != null && pt.CurrentTarget != null)
                {
                    var fc = Combat.Predict(pt.Unit, pt.CurrentTarget);
                    var hp = Combat.HpIfAllHit(fc);
                    System.Func<SideForecast, int, string> num = (s, after) => s.unit.DisplayName + " hp " + s.unit.currentHealth + "->" + after + " dmg " + (s.canCounter ? s.damage + (s.strikes > 1 ? " x2" : "") : "--") + " hit " + (s.canCounter ? s.hit.ToString() : "--") + " crit " + (s.canCounter ? s.crit.ToString() : "--") + (s.canCounter && s.triangleAdvantage && !(s.unit == fc.attacker.unit ? fc.defender.unit.broken : fc.attacker.unit.broken) ? " BREAK" : "");
                    sb.AppendLine("Combat.Predict: " + num(fc.attacker, hp.attacker) + "  ||  " + num(fc.defender, hp.defender));
                }
            }
        }
        var ptr = UnityEngine.Object.FindAnyObjectByType<PointerScript>();
        if (ptr != null) sb.AppendLine("cursor: (" + ptr.tileXIndex + "," + ptr.tileZIndex + ")  input: " + ptr.inputEnabled + "  occupant: " + (ptr.Tile?.occupant != null ? ptr.Tile.occupant.DisplayName : "-"));
        var map = TileMap.instance;
        System.Func<Player, string> row = p => p.DisplayName + "@(" + p.Tile.x + "," + p.Tile.z + ") hp " + p.currentHealth + "/" + p.MaxHP
            + " " + (p.Weapon.Magic ? "mag " + p.stats.Mag : "str " + p.stats.Str) + " skl " + p.stats.Skl + " spd " + p.stats.Spd + " lck " + p.stats.Lck + " def " + p.stats.Def + " res " + p.stats.Res
            + (p.broken ? " [broken]" : "") + (p.isTurnOver ? " [done]" : "");
        sb.AppendLine("players (" + tm.players.Count + "): " + string.Join(" | ", tm.players.Select(row)));
        sb.AppendLine("enemies (" + tm.enemies.Count + "): " + string.Join(" | ", tm.enemies.Select(row)));
        sb.AppendLine("danger: " + (tm.dangerOn ? "on" : "off") + "  tiles " + tm.dangerTiles.Count + "  players in reach: " + string.Join(", ", tm.players.Where(p => tm.dangerTiles.Contains(p.Tile)).Select(p => p.DisplayName)));
        sb.AppendLine("stats: battles " + tm.battlesFought + "  lost " + tm.unitsLost + "  defeated " + tm.enemiesDefeated);
        if (tm.battlesFought > 0)
        {
            var lf = tm.lastForecast;
            System.Func<SideForecast, string> side = s => (s.unit != null ? s.unit.DisplayName : "(fallen)") + " dmg " + s.damage + " x" + s.strikes + " hit " + s.hit + " crit " + s.crit + (s.triangleAdvantage ? " adv" : "") + (s.canCounter ? "" : " no-counter");
            sb.AppendLine("last forecast: " + side(lf.attacker) + "  ||  " + side(lf.defender));
            sb.AppendLine("last strikes: " + string.Join(" | ", tm.lastStrikes.Select(s => (s.byAttacker ? "A " : "D ") + s.result + " " + s.damage + " -> hp " + s.targetHpAfter + (s.breaks ? " BREAK" : "") + (s.kills ? " KILL" : ""))));
        }
    }

    var bs = BattleScene.instance;
    if (bs != null && bs.CurrentPhase != BattleScene.Phase.Idle)
        sb.AppendLine("battle: " + bs.CurrentPhase + "  strike " + (bs.StrikeIndex + 1) + "/" + bs.StrikeCount + "  fast " + bs.Fast + "  skipped " + bs.Skipped + "  hud hp: " + GameUI.I.BattleHudText);
    else sb.AppendLine("battle: Idle");

    var top = GameUI.I != null ? GameUI.I.TopMenu : null;
    if (top != null && top.menu != null)
    {
        var m = top.menu;
        var labels = m.items.Select((it, i) => (i == m.selected ? ">" : "") + (it.label != null ? it.label() : "?") + (m.IsEnabled(i) ? "" : "(off)"));
        sb.AppendLine("menu: " + top.name + " [" + m.title + "] " + string.Join(" / ", labels));
    }
    else sb.AppendLine("menu: none");
    return sb.ToString();
}

Probe()
