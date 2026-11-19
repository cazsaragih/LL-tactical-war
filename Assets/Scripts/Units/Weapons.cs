using System.Collections.Generic;

namespace LLTacticalWar
{
    public sealed class WeaponInfo
    {
        public WeaponKind Kind;
        public string Name;
        public int Might, Hit, Crit, MinRange, MaxRange;
        public bool Magic;

        public bool Covers(int distance) => distance >= MinRange && distance <= MaxRange;
        public string RangeText => MinRange == MaxRange ? MinRange.ToString() : MinRange + "-" + MaxRange;
    }

    // Every weapon number lives in this one table, keyed by the WeaponKind the classes and
    // UnitFactory already use. Range comes from here, not from the class.
    public static class Weapons
    {
        static readonly Dictionary<WeaponKind, WeaponInfo> table = new Dictionary<WeaponKind, WeaponInfo>
        {
            [WeaponKind.Sword]  = W(WeaponKind.Sword,  "Iron Sword",  5, 90, 0,  1, 1),
            [WeaponKind.Spear]  = W(WeaponKind.Spear,  "Iron Lance",  7, 80, 0,  1, 1),
            [WeaponKind.Club]   = W(WeaponKind.Club,   "Iron Club",   8, 70, 0,  1, 1),
            [WeaponKind.Bow]    = W(WeaponKind.Bow,    "Iron Bow",    6, 85, 0,  2, 2),
            [WeaponKind.Dagger] = W(WeaponKind.Dagger, "Iron Dagger", 3, 95, 10, 1, 1),
            [WeaponKind.Staff]  = W(WeaponKind.Staff,  "Fire",        5, 90, 0,  1, 2, true),
        };

        // The triangle: each key beats its value. Bow, Dagger and Staff sit outside it.
        static readonly Dictionary<WeaponKind, WeaponKind> beats = new Dictionary<WeaponKind, WeaponKind>
        {
            [WeaponKind.Sword] = WeaponKind.Club,
            [WeaponKind.Club]  = WeaponKind.Spear,
            [WeaponKind.Spear] = WeaponKind.Sword,
        };

        static WeaponInfo W(WeaponKind k, string name, int mt, int hit, int crit, int min, int max, bool magic = false) =>
            new WeaponInfo { Kind = k, Name = name, Might = mt, Hit = hit, Crit = crit, MinRange = min, MaxRange = max, Magic = magic };

        public static WeaponInfo Of(WeaponKind k) => table[k];

        // +1 when a has the advantage over b, -1 when b has it over a, 0 otherwise.
        public static int Triangle(WeaponKind a, WeaponKind b)
        {
            if (beats.TryGetValue(a, out var x) && x == b) return 1;
            if (beats.TryGetValue(b, out var y) && y == a) return -1;
            return 0;
        }
    }
}
