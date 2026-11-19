using System;
using System.Collections.Generic;

namespace LLTacticalWar
{
    public struct SideForecast
    {
        public Player unit;
        public WeaponInfo weapon;
        public int damage, hit, crit, strikes;
        public bool canCounter, triangleAdvantage;
    }

    public struct Forecast { public SideForecast attacker, defender; }

    public enum StrikeResult { Miss, Hit, Crit }

    public struct Strike
    {
        public bool byAttacker;
        public StrikeResult result;
        public int damage;
        public bool breaks;
        public int targetHpAfter;
        public bool kills;
    }

    // Expected outcome of one exchange, for the enemy AI: chances over every roll, not one roll.
    public struct Odds { public float kill, death, dealt, taken; }

    // The 1v1 exchange as pure functions. Predict reads the two units, Resolve decides every
    // strike up front, Apply writes the result back. The battle scene only plays the list, so
    // skipping the animation can never change the outcome.
    public static class Combat
    {
        public const int DoublingGap = 5;
        public const int CritMultiplier = 3;

        public static Forecast Predict(Player attacker, Player defender) =>
            Predict(attacker, defender, attacker.Tile.Distance(defender.Tile));

        // The distance is a parameter so the enemy AI can score tiles it has not moved to yet.
        public static Forecast Predict(Player attacker, Player defender, int distance)
        {
            bool counter = defender.isAlive && !defender.broken && defender.Weapon.Covers(distance);
            var a = Side(attacker, defender, true);
            var d = Side(defender, attacker, counter);
            if (a.unit.stats.Spd >= d.unit.stats.Spd + DoublingGap) a.strikes = 2;
            else if (counter && d.unit.stats.Spd >= a.unit.stats.Spd + DoublingGap) d.strikes = 2;
            return new Forecast { attacker = a, defender = d };
        }

        static SideForecast Side(Player self, Player foe, bool canStrike)
        {
            var w = self.Weapon;
            var s = self.stats;
            var f = foe.stats;
            int atk = (w.Magic ? s.Mag : s.Str) + w.Might;
            return new SideForecast
            {
                unit = self,
                weapon = w,
                damage = Math.Max(0, atk - (w.Magic ? f.Res : f.Def)),
                hit = Clamp(w.Hit + s.Skl * 2 + s.Lck / 2 - (f.Spd * 2 + f.Lck)),
                crit = Clamp(w.Crit + s.Skl / 2 - f.Lck),
                strikes = canStrike ? 1 : 0,
                canCounter = canStrike,
                triangleAdvantage = Weapons.Triangle(w.Kind, foe.Weapon.Kind) > 0,
            };
        }

        static int Clamp(int v) => Math.Max(0, Math.Min(100, v));

        // ---------------- the exchange ----------------

        struct State { public int hpA, hpD; public bool brokenA, brokenD; }

        static State Start(Forecast f) => new State
        {
            hpA = f.attacker.unit.currentHealth, hpD = f.defender.unit.currentHealth,
            brokenA = f.attacker.unit.broken, brokenD = f.defender.unit.broken,
        };

        // Strike slots in order: attacker, defender's counter, then the faster side's follow-up.
        static bool[] Order(Forecast f)
        {
            var o = new List<bool> { true };
            if (f.defender.strikes > 0) o.Add(false);
            if (f.attacker.strikes > 1) o.Add(true);
            else if (f.defender.strikes > 1) o.Add(false);
            return o.ToArray();
        }

        // Nobody strikes once either side is down, and a side broken mid-exchange strikes no more.
        static bool CanStrike(State s, bool byAttacker) => s.hpA > 0 && s.hpD > 0 && !(byAttacker ? s.brokenA : s.brokenD);

        static Strike Land(ref State s, Forecast f, bool byAttacker, StrikeResult r)
        {
            var side = byAttacker ? f.attacker : f.defender;
            int dmg = r == StrikeResult.Miss ? 0 : r == StrikeResult.Crit ? side.damage * CritMultiplier : side.damage;
            int hp;
            bool targetBroken;
            if (byAttacker) { s.hpD = Math.Max(0, s.hpD - dmg); hp = s.hpD; targetBroken = s.brokenD; }
            else { s.hpA = Math.Max(0, s.hpA - dmg); hp = s.hpA; targetBroken = s.brokenA; }
            bool breaks = r != StrikeResult.Miss && side.triangleAdvantage && !targetBroken && hp > 0;
            if (breaks) { if (byAttacker) s.brokenD = true; else s.brokenA = true; }
            return new Strike { byAttacker = byAttacker, result = r, damage = dmg, breaks = breaks, targetHpAfter = hp, kills = hp == 0 };
        }

        static List<Strike> Run(Forecast f, Func<SideForecast, StrikeResult> roll)
        {
            var s = Start(f);
            var list = new List<Strike>();
            foreach (bool byA in Order(f))
                if (CanStrike(s, byA)) list.Add(Land(ref s, f, byA, roll(byA ? f.attacker : f.defender)));
            return list;
        }

        public static List<Strike> Resolve(Forecast f, Random rng) => Run(f, side => Roll(side, rng));

        // Two random numbers averaged ("true hit"): high rates land more often than shown, low ones less.
        static StrikeResult Roll(SideForecast side, Random rng)
        {
            if (rng.Next(100) + rng.Next(100) >= side.hit * 2) return StrikeResult.Miss;
            return rng.Next(100) < side.crit ? StrikeResult.Crit : StrikeResult.Hit;
        }

        // HP each side ends on if every strike lands (no crits): the forecast's "HP after".
        public static (int attacker, int defender) HpIfAllHit(Forecast f)
        {
            int a = f.attacker.unit.currentHealth, d = f.defender.unit.currentHealth;
            foreach (var s in Run(f, _ => StrikeResult.Hit))
                if (s.byAttacker) d = s.targetHpAfter; else a = s.targetHpAfter;
            return (a, d);
        }

        public static void Apply(Forecast f, List<Strike> strikes)
        {
            foreach (var s in strikes)
            {
                var target = s.byAttacker ? f.defender.unit : f.attacker.unit;
                target.currentHealth = s.targetHpAfter;
                if (s.breaks) target.broken = true;
            }
            f.attacker.unit.isAlive = f.attacker.unit.currentHealth > 0;
            f.defender.unit.isAlive = f.defender.unit.currentHealth > 0;
        }

        // ---------------- odds (enemy AI) ----------------

        // P(two averaged RNs land) for each displayed hit, from the triangular sum distribution.
        static readonly float[] trueHit = BuildTrueHit();

        static float[] BuildTrueHit()
        {
            var t = new float[101];
            int below = 0;
            for (int h = 1; h <= 100; h++)
            {
                for (int sum = 2 * h - 2; sum < 2 * h; sum++) below += sum <= 99 ? sum + 1 : 199 - sum;
                t[h] = below / 10000f;
            }
            return t;
        }

        public static float TrueHit(int hit) => trueHit[Math.Max(0, Math.Min(100, hit))];

        // Walks every miss / hit / crit branch of the exchange (at most 3^3 leaves).
        public static Odds Expect(Forecast f)
        {
            var o = new Odds();
            var start = Start(f);
            Walk(f, Order(f), 0, start, 1f, start, ref o);
            return o;
        }

        static void Walk(Forecast f, bool[] order, int i, State s, float p, State start, ref Odds o)
        {
            while (i < order.Length && !CanStrike(s, order[i])) i++;
            if (i == order.Length)
            {
                o.dealt += p * (start.hpD - s.hpD);
                o.taken += p * (start.hpA - s.hpA);
                if (s.hpD == 0) o.kill += p;
                if (s.hpA == 0) o.death += p;
                return;
            }
            var side = order[i] ? f.attacker : f.defender;
            float ph = TrueHit(side.hit), pc = ph * side.crit / 100f;
            Branch(f, order, i, s, p * (1f - ph), StrikeResult.Miss, start, ref o);
            Branch(f, order, i, s, p * (ph - pc), StrikeResult.Hit, start, ref o);
            Branch(f, order, i, s, p * pc, StrikeResult.Crit, start, ref o);
        }

        static void Branch(Forecast f, bool[] order, int i, State s, float p, StrikeResult r, State start, ref Odds o)
        {
            if (p <= 0f) return;
            Land(ref s, f, order[i], r);
            Walk(f, order, i + 1, s, p, start, ref o);
        }
    }
}
