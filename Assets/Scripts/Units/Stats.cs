namespace LLTacticalWar
{
    // A unit's numbers on the 1v1 scale. Each class holds one as its base; each Player copies it.
    [System.Serializable]
    public struct Stats { public int HP, Str, Mag, Skl, Spd, Lck, Def, Res; }
}
