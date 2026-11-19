using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 TurnStateStart: spawns both armies onto their starting tiles.
    // (2015 used hard-coded tiles 208-213 on one row; the armies now face each other across
    // the board.)
    public class TurnStateStart : MonoBehaviour
    {
        static BaseClass[] PlayerRoster() => new BaseClass[]
            { new BaseClass_Fighter(), new BaseClass_Archer(), new BaseClass_Dragoon(), new BaseClass_Apprentice() };

        static BaseClass[] EnemyRoster() => new BaseClass[]
            { new BaseClass_Brawler(), new BaseClass_Thief(), new BaseClass_Fighter(), new BaseClass_Archer() };

        static readonly int[] Columns = { 4, 6, 7, 9 };

        public void Initialize()
        {
            var tm = TileMap.instance;
            var tMan = TurnManager.instance;

            var pr = PlayerRoster();
            for (int i = 0; i < pr.Length; i++)
                tMan.players.Add(Spawn(pr[i], Team.Player, tm.TileAt(Columns[i], 0), Player.Direction.North));

            var er = EnemyRoster();
            for (int i = 0; i < er.Length; i++)
                tMan.enemies.Add(Spawn(er[i], Team.Enemy, tm.TileAt(Columns[i], tm.tileZ - 1), Player.Direction.South));
        }

        Player Spawn(BaseClass cls, Team team, TileMapInfo tile, Player.Direction facing)
        {
            var go = new GameObject((team == Team.Player ? "P_" : "E_") + cls.ClassName);
            go.transform.SetParent(GameManager.I.TacticRoot, false);
            go.transform.position = tile.position;
            var p = go.AddComponent<Player>();
            p.tilePosition = -1;
            p.Init(cls, team);
            p.Rotate(facing);
            TurnManager.instance.PlaceUnit(p, tile);
            GameUI.I?.RegisterUnit(p);
            return p;
        }
    }
}
