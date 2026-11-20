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

    // An animal a rider sits on: a model from Quaternius's Ultimate Animated Animals (CC0) with
    // its own clips, in its natural colours. The rider's root rides SeatBone, placed at Seat, so
    // it moves with every gait, and the faces whose centre falls inside Cloth are painted in the
    // team colour. Both are in the mount's own space at its bind pose, which its idle matches.
    public sealed class Mount
    {
        public string Model;            // FBX in Resources/Quaternius
        public float Scale;             // import scale: file units to board units
        public string Idle, Walk, Gallop, Attack, Hit, Death;   // takes, without the rig's "AnimalArmature|"
        public float WalkSpeed, GallopSpeed;   // board units/s a planted hoof slides back at 1x
        public float Impact;            // seconds into Attack (at 1x) where the head drives home
        public float Reach;             // how far its head reaches ahead of its root, in board units
        public string SeatBone;
        public Vector3 Seat;
        public Bounds Cloth;

        public string[] Clips => new[] { Idle, Walk, Gallop, Attack, Hit, Death };
        public bool Loops(string clip) => clip == Idle || clip == Walk || clip == Gallop;
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
        public Mount Mount;             // what a rider sits on
        public Vector3? Crest;          // a team-coloured crest on the helmet, at this point in head-bone space
    }

    // The table every figure is built from. UnitFactory and UnitModel read only this, and the
    // KayKit and Quaternius import postprocessors import exactly the clips named here.
    public static class UnitLooks
    {
        public const string CharacterDir = "KayKit/Characters/", WeaponDir = "KayKit/Weapons/", MountDir = "Quaternius/";
        public const float ImportScale = 0.8f;   // pack units to board units; a figure stands ~1.85 tall on a 2-unit tile

        // Running_A's planted foot slides back 3.52 pack units/s at 1x (sampled from foot.l).
        public const string Run = "Running_A";
        public const float RunGroundSpeed = 3.52f * ImportScale;

        static readonly KitAtlas Knight    = new KitAtlas("knight_texture",    V(0, 1), V(2, 2));                // tabard and cape, the shield's badge
        static readonly KitAtlas Barbarian = new KitAtlas("barbarian_texture", V(0, 1), V(1, 1), V(2, 2));       // vest, sleeves, axe and shield wraps
        static readonly KitAtlas Mage      = new KitAtlas("mage_texture",      V(0, 1), V(1, 1), V(2, 1), V(2, 2)); // robe, hat, cape, trim and staff collar
        static readonly KitAtlas Rogue     = new KitAtlas("rogue_texture",     V(0, 1), V(1, 1), V(1, 2));       // tunic, hood and cape, quiver

        // 0.345 makes the horse 1.98 long at its idle, so it still stands inside a 2-unit tile, and
        // big enough under the chibi knight (0.3 left the rider looking oversized). The horse's own
        // measures are in the file's units times the scale: hoof speeds are the four hooves' stance
        // average (the pack's front and back hooves disagree by a third); the seat puts the knight's
        // crotch (KnightCrotch above its root, which doesn't scale with the horse) on the dip behind
        // the withers, BackHeight up; Torso is the back bone that tilts least at the walk.
        const float HorseScale = 0.345f, BackHeight = 3.487f, KnightCrotch = 0.37f;
        public static readonly Mount Horse = new Mount
        {
            Model = "Horse", Scale = HorseScale,
            Idle = "Idle", Walk = "Walk", Gallop = "Gallop", Attack = "Attack_Headbutt", Hit = "Idle_HitReact_Left", Death = "Death",
            WalkSpeed = 2.7f * HorseScale, GallopSpeed = 9.0f * HorseScale, Impact = 0.3f, Reach = 3.25f * HorseScale,
            SeatBone = "Torso", Seat = new Vector3(0f, BackHeight * HorseScale - KnightCrotch, 0.1f * HorseScale),
            Cloth = new Bounds(new Vector3(0f, 3.3f, -0.283f) * HorseScale, new Vector3(2.667f, 1.4f, 1.767f) * HorseScale),
        };
        public static readonly Mount[] Mounts = { Horse };

        // The knight's helmet top in its head bone's space (the helmet mesh's crown at bind pose):
        // a crest there reads in team colour from the front, where the armour is all steel.
        static readonly Vector3 KnightCrest = new Vector3(0f, 0.98f, 0.02f);

        static readonly Dictionary<string, UnitLook> table = new Dictionary<string, UnitLook>
        {
            ["Fighter"] = new UnitLook
            {
                Model = "Knight", Atlas = Knight, Crest = KnightCrest,
                Props = new[] { new Prop("sword_1handed", "handslot.r", new Vector3(0f, 0.03f, 0f)), new Prop("shield_badge_color", "handslot.l", new Vector3(0f, 0.02f, 0.16f)) },
                Attack = "1H_Melee_Attack_Slice_Diagonal", Impact = 0.40f,
            },
            ["Archer"] = new UnitLook
            {
                Model = "RogueHooded", Atlas = Rogue,
                // the Weapons pack's crossbow lies along +Z; the rig's own 1H_Crossbow, which the shoot
                // clip is posed for, lies along -X from the same grip, so turn it -90 about Y
                Props = new[] { new Prop("crossbow_1handed", "handslot.r", new Vector3(0.105f, -0.01f, 0f), new Vector3(0f, -90f, 0f)),new Prop("quiver", "chest", new Vector3(0.2f, 0.3f, -0.38f), new Vector3(-12f, 0f, -24f)) },
                Attack = "1H_Ranged_Shoot", Impact = 0.34f,
            },
            ["Dragoon"] = new UnitLook
            {
                Model = "Knight", Atlas = Knight, Crest = KnightCrest, Props = new Prop[0],   // no pack prop is a lance: UnitFactory builds it
                Move = null, Seat = "Sit_Chair_Pose", Mount = Horse,
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
