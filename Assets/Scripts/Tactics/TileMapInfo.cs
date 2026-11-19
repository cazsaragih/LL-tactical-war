using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 TileMapInfo. The A* bookkeeping (G/H/F, parent) now lives inside
    // PathFinder, so a failed search can't leave stale state on the tiles.
    public class TileMapInfo
    {
        public Vector3 position;
        public int index, x, z;

        public bool blocked;          // scenery: trees, rocks
        public Player occupant;       // the unit standing here, if any
        public bool inRange;

        public TileMapInfo north, east, south, west;

        public bool Reachable => !blocked && occupant == null;

        public int Distance(TileMapInfo other) => Mathf.Abs(x - other.x) + Mathf.Abs(z - other.z);

        public TileMapInfo[] Neighbours => new[] { north, east, south, west };
    }
}
