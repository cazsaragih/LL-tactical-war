namespace LLTacticalWar
{
    public enum TerrainType { Plain, Forest, Desert, Mountain, Fort }
    public enum WeaponKind { Dagger, Spear, Staff, Bow, Club, Sword }

    // Ported from the 2015 BaseClass. It was a MonoBehaviour that set its stats in a constructor
    // (which Unity doesn't support for components); it is now plain data: identity, base stats,
    // weapon and per-terrain movement. Attack range comes from the weapon (see Weapons).
    public abstract class BaseClass
    {
        public int ClassID { get; protected set; }
        public string ClassName { get; protected set; }
        public string ClassDescription { get; protected set; }
        public string ClassType { get; protected set; }
        public WeaponKind Weapon { get; protected set; }
        public bool Mounted { get; protected set; }

        public Stats Base { get; protected set; }

        public int TileMove { get; set; }

        // Per-terrain tile movement. These were commented out in every 2015 class, which left
        // TileMove at 0; they are restored here exactly as written.
        public int plainMove, forestMove, desertMove, mountainMove, fortMove;

        public void TerrainEffect(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plain:    TileMove = plainMove; break;
                case TerrainType.Forest:   TileMove = forestMove; break;
                case TerrainType.Desert:   TileMove = desertMove; break;
                case TerrainType.Mountain: TileMove = mountainMove; break;
                case TerrainType.Fort:     TileMove = fortMove; break;
            }
        }
    }
}
