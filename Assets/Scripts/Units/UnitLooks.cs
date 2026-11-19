using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // One KayKit atlas and the swatches on it that carry the team colour. Swatches are addressed
    // (column, row from the top) on the atlas's grid of 8 x 4 vertical gradients, 128 x 256 px each.
    public sealed class KitAtlas
    {
        public readonly string Texture;
        public readonly Vector2Int[] TeamCells;

        public KitAtlas(string texture, params Vector2Int[] teamCells) { Texture = texture; TeamCells = teamCells; }
    }

    // A weapon or kit piece from KayKit/Weapons, parented to a bone of the character's rig.
    // Pos is in the pack's own units (UnitLooks.ImportScale converts); hand props use the grip
    // offsets of the pack's built-in props they replace.
    public readonly struct Prop
    {
        public readonly string Fbx, Bone;
        public readonly Vector3 Pos, Euler;

        public Prop(string fbx, string bone, Vector3 pos = default, Vector3 euler = default) { Fbx = fbx; Bone = bone; Pos = pos; Euler = euler; }
    }

    // How a class looks and moves: its KayKit Adventurers model (CC0, Kay Lousberg), what it
    // carries, which atlas swatches take the team colour, and which clips it plays.
    public sealed class UnitLook
    {
        public string Model;            // FBX in Resources/KayKit/Characters
        public KitAtlas Atlas;
        public Prop[] Props;
        public string Idle = "Idle", Move = UnitLooks.Run, Attack, Hit = "Hit_A", HeavyHit = "Hit_B",
                      Dodge = "Dodge_Backward", Death = "Death_A", Cheer = "Cheer";
        public float Impact;            // seconds into Attack (at 1x) where the blow lands or the shot leaves
        public string Seat;             // a pose clip whose legs are held over everything (a rider)
    }

    // The table every figure is built from. UnitFactory and UnitModel read only this, and the
    // KayKit import postprocessor imports exactly the clips named here.
    public static class UnitLooks
    {
        public const string CharacterDir = "KayKit/Characters/", WeaponDir = "KayKit/Weapons/";
        public const float ImportScale = 0.8f;   // pack units to board units; a figure stands ~1.85 tall on a 2-unit tile

        // Running_A's planted foot slides back 3.52 pack units/s at 1x (sampled from foot.l).
        public const string Run = "Running_A";
        public const float RunGroundSpeed = 3.52f * ImportScale;

        static readonly KitAtlas Knight    = new KitAtlas("knight_texture",    V(0, 1));                         // tabard and cape
        static readonly KitAtlas Barbarian = new KitAtlas("barbarian_texture", V(0, 1), V(1, 1), V(2, 2));       // vest, sleeves, axe and shield wraps
        static readonly KitAtlas Mage      = new KitAtlas("mage_texture",      V(0, 1), V(1, 1), V(2, 1), V(2, 2)); // robe, hat, cape, trim and staff collar
        static readonly KitAtlas Rogue     = new KitAtlas("rogue_texture",     V(0, 1), V(1, 1), V(1, 2));       // tunic, hood and cape, quiver

        static readonly Dictionary<string, UnitLook> table = new Dictionary<string, UnitLook>
        {
            ["Fighter"] = new UnitLook
            {
                Model = "Knight", Atlas = Knight,
                Props = new[] { new Prop("sword_1handed", "handslot.r", new Vector3(0f, 0.03f, 0f)), new Prop("shield_round", "handslot.l", new Vector3(0f, 0.02f, 0.16f)) },
                Attack = "1H_Melee_Attack_Slice_Diagonal", Impact = 0.40f,
            },
            ["Archer"] = new UnitLook
            {
                Model = "RogueHooded", Atlas = Rogue,
                // the 2-handed crossbow juts sideways in the idle pose; the 1-handed one hangs at the hip
                Props = new[] { new Prop("crossbow_1handed", "handslot.r", new Vector3(0.10f, -0.01f, 0f)), new Prop("quiver", "chest", new Vector3(0.2f, 0.3f, -0.38f), new Vector3(-12f, 0f, -24f)) },
                Attack = "1H_Ranged_Shoot", Impact = 0.34f,
            },
            ["Dragoon"] = new UnitLook
            {
                Model = "Knight", Atlas = Knight, Props = new Prop[0],   // the lance and horse are code-built (UnitFactory)
                Move = null, Seat = "Sit_Chair_Pose",
                Attack = "1H_Melee_Attack_Stab", Impact = 0.44f,
            },
            ["Apprentice"] = new UnitLook
            {
                Model = "Mage", Atlas = Mage,
                Props = new[] { new Prop("staff", "handslot.r") },
                Attack = "Spellcast_Shoot", Impact = 0.26f,
            },
            ["Brawler"] = new UnitLook
            {
                Model = "Barbarian", Atlas = Barbarian,
                Props = new[] { new Prop("axe_1handed", "handslot.r", new Vector3(0f, 0.03f, 0f)), new Prop("shield_round_barbarian", "handslot.l", new Vector3(0f, 0.02f, 0.16f)) },
                Attack = "1H_Melee_Attack_Chop", Impact = 0.60f,
            },
            ["Thief"] = new UnitLook
            {
                Model = "Rogue", Atlas = Rogue,
                Props = new[] { new Prop("dagger", "handslot.r"), new Prop("dagger", "handslot.l") },
                Attack = "Dualwield_Melee_Attack_Stab", Impact = 0.44f,
            },
        };

        static Vector2Int V(int column, int row) => new Vector2Int(column, row);

        public static UnitLook Of(BaseClass cls) => table[cls.ClassName];

        // Every clip any look plays, for the importer.
        public static HashSet<string> ClipNames()
        {
            var names = new HashSet<string>();
            foreach (var l in table.Values)
                foreach (var n in new[] { l.Idle, l.Move, l.Attack, l.Hit, l.HeavyHit, l.Dodge, l.Death, l.Cheer, l.Seat })
                    if (!string.IsNullOrEmpty(n)) names.Add(n);
            return names;
        }

        public static bool Loops(string clip)
        {
            foreach (var l in table.Values)
                if (clip == l.Idle || clip == l.Move) return true;
            return false;
        }
    }
}
