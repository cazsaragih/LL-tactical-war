namespace LLTacticalWar
{
    public enum TerrainType { Plain, Forest, Desert, Mountain, Fort }
    public enum WeaponKind { Dagger, Spear, Staff, Bow, Club, Sword }

    // Ported from the 2015 BaseClass. It was a MonoBehaviour that set its stats in a constructor
    // (which Unity doesn't support for components); it is now plain data, so a general and each of
    // its troops get their own instance.
    public abstract class BaseClass
    {
        public int ClassID { get; protected set; }
        public string ClassName { get; protected set; }
        public string ClassDescription { get; protected set; }
        public string ClassType { get; protected set; }
        public WeaponKind Weapon { get; protected set; }
        public bool Mounted { get; protected set; }

        public int BaseHP { get; set; }
        public int BaseAttack { get; set; }
        public int BaseDefense { get; set; }
        public int TotalUnits { get; set; }

        public int TileMove { get; set; }
        public int TileAttack { get; set; }

        // Battle-scene movement speed multiplier ("high agility" in the class descriptions).
        public float WarSpeed { get; protected set; } = 1f;

        // Per-terrain tile movement. These were commented out in every 2015 class, which left
        // TileMove at 0; they are restored here exactly as written.
        public int plainMove, forestMove, desertMove, mountainMove, fortMove;

        public abstract void BoostStats();
        public abstract BaseClass Fresh();

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
