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
            var f = typeof(TurnStatePlayerTurn).GetField("playerState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sb.AppendLine("playerState: " + f?.GetValue(pt));
        }
        var ptr = UnityEngine.Object.FindAnyObjectByType<PointerScript>();
        if (ptr != null) sb.AppendLine("cursor: (" + ptr.tileXIndex + "," + ptr.tileZIndex + ")  input: " + ptr.inputEnabled + "  occupant: " + (ptr.Tile?.occupant != null ? ptr.Tile.occupant.DisplayName : "-"));
        var map = TileMap.instance;
        System.Func<Player, string> row = p => p.DisplayName + "@(" + (p.tilePosition % map.tileX) + "," + (p.tilePosition / map.tileX) + ") hp " + p.currentHealth + "/" + p.maxHealth + " troops " + p.currentUnits + "/" + p.startUnits + " facing " + p.playerDirection + (p.isTurnOver ? " [done]" : "");
        sb.AppendLine("players (" + tm.players.Count + "): " + string.Join(" | ", tm.players.Select(row)));
        sb.AppendLine("enemies (" + tm.enemies.Count + "): " + string.Join(" | ", tm.enemies.Select(row)));
        sb.AppendLine("stats: battles " + tm.battlesFought + "  lost " + tm.unitsLost + "  routed " + tm.enemiesRouted);
    }

    var top = GameUI.I != null ? GameUI.I.TopMenu : null;
    if (top != null && top.menu != null)
    {
        var m = top.menu;
        var labels = m.items.Select((it, i) => (i == m.selected ? ">" : "") + (it.label != null ? it.label() : "?") + (m.IsEnabled(i) ? "" : "(off)"));
        sb.AppendLine("menu: " + top.name + " [" + m.title + "] " + string.Join(" / ", labels));
    }
    else sb.AppendLine("menu: none");

    var war = WarSceneManager.instance;
    if (war != null && war.battleState != WarSceneManager.BattleState.Idle)
    {
        sb.AppendLine("battle: " + war.battleState + "  formation: " + war.formation + "  timeLeft: " + war.TimeLeft.ToString("0.0") + "  commandMenu: " + war.CommandMenuOpen);
        System.Func<Soldier, string> g = s => s == null ? "-" : (s.isAlive ? "alive" : "down") + " hp " + s.currentHealth + "/" + s.maxHealth + " atk " + s.modifiedAttack + " def " + s.modifiedDefense;
        sb.AppendLine("generals: player " + g(war.playerGeneral) + "  enemy " + g(war.enemyGeneral));
        sb.AppendLine("alive (incl. general): player " + war.playerTroops.Count + "  enemy " + war.enemyTroops.Count);
    }
    return sb.ToString();
}

Probe()
