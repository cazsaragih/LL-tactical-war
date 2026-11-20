using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // One KayKit Medieval Hexagon piece (CC0, Kay Lousberg): a model in Resources/KayKit/Environment,
    // with "{team}" in its name standing for blue or red. It is set down with its footprint centred
    // on (X, Z), Y above the ground, turned Yaw degrees and scaled Scale times its import size. The
    // table that holds a piece says what (X, Z) is measured from.
    public readonly struct Piece
    {
        public readonly string Model;
        public readonly float X, Z, Yaw, Scale, Y;

        public Piece(string model, float x, float z, float yaw, float scale, float y = 0f)
        { Model = model; X = x; Z = z; Yaw = yaw; Scale = scale; Y = y; }
    }

    // A seeded fill of a rectangle (x, z): Count pieces drawn from Models, each turned at random and
    // scaled within Scale, none overlapping another piece already set down on its side. Each sinks
    // Sink import units (times its scale) into the ground.
    public readonly struct Scatter
    {
        public readonly string[] Models;
        public readonly Rect Area;
        public readonly int Count, Seed;
        public readonly Vector2 Scale;
        public readonly float Sink;

        public Scatter(string[] models, Rect area, int count, Vector2 scale, int seed, float sink = 0f)
        { Models = models; Area = area; Count = count; Scale = scale; Seed = seed; Sink = sink; }
    }

    // A seeded ring around a centre: Count pieces from Models spread across Arc (degrees, 0 is +z,
    // clockwise), each jittered inside its slot, at a radius within Radius and a height within Height.
    public readonly struct Ring
    {
        public readonly string[] Models;
        public readonly Vector2 Arc, Radius, Height, Scale;
        public readonly int Count, Seed;

        public Ring(string[] models, Vector2 arc, Vector2 radius, Vector2 height, Vector2 scale, int count, int seed)
        { Models = models; Arc = arc; Radius = radius; Height = height; Scale = scale; Count = count; Seed = seed; }
    }

    // The table the world is built from. Scenery reads only this, and EnvironmentImport imports the
    // pack at ImportScale.
    public static class SceneryLayout
    {
        public const string Dir = "KayKit/Environment/", Atlas = "hexagons_medieval";
        public static readonly Vector2Int[] GrassCells = { new Vector2Int(0, 2) };   // the atlas's olive grass: hill and mountain tops
        public const float ImportScale = 1f;   // the pack's hex is 2 units across, a board tile's size
        public const float GroundY = -0.75f;   // the meadow around the board, whose tile tops sit at 0: a bank that reads from the map camera
        public const float StageGroundY = -0.9f;
        public const int SurfaceSeed = 3;        // which shade each board tile takes

        // Set scales over the import size. On the board pieces stay tile-sized, so nothing hides a
        // figure; the camps are at life size, the figures' own scale (a door is about a figure tall); the wilds
        // read as full-size trees and hills; the horizon is far and big.
        const float Life = 2.5f;

        static Piece P(string model, float x, float z, float yaw, float scale, float y = 0f) => new Piece(model, x, z, yaw, scale, y);
        static Rect R(float xMin, float zMin, float xMax, float zMax) => Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        static Vector2 V(float a, float b) => new Vector2(a, b);

        // ---------------- the board ----------------

        // Obstacle looks by TileMap layout letter (T trees, R rocks), as offsets from the tile centre.
        // Each obstacle tile picks one look and a yaw from its own seed. Every look fits inside its
        // tile at any yaw and stands under 1.45 units, so a figure on the tile behind shows over it.
        public const int ObstacleSeed = 7;
        public static readonly Dictionary<char, Piece[][]> Obstacles = new Dictionary<char, Piece[][]>
        {
            ['T'] = new[]
            {
                new[] { P("trees_A_small", 0f, 0f, 0f, 1.15f) },
                new[] { P("trees_B_small", 0f, 0f, 0f, 1.2f) },
                new[] { P("tree_single_B", -0.3f, 0.25f, 0f, 1.2f), P("tree_single_A", 0.38f, -0.3f, 40f, 1.15f), P("tree_single_A_cut", 0.4f, 0.45f, 0f, 1.8f) },
            },
            ['R'] = new[]
            {
                new[] { P("mountain_A", 0f, 0f, 0f, 0.72f) },
                new[] { P("mountain_C", 0f, 0f, 0f, 0.68f) },
                new[] { P("rock_single_E", -0.15f, 0.15f, 0f, 3f), P("rock_single_C", 0.36f, -0.3f, 60f, 2.6f), P("rock_single_B", -0.36f, -0.4f, 20f, 2.4f) },
            },
        };

        // One side's camp, in board-centre space as the player's blue side has it: +z runs toward
        // the enemy and the player's edge is at z = -10. The enemy's camp is this one in red, turned
        // half a circle about the board centre. Between x -14 and 14 nothing may rise above a sight
        // line from the map camera to the board's edge, about 1.48 x its distance behind the edge.
        public static readonly Piece[] Camp =
        {
            // the keep, flanking towers and a curtain wall, far enough back to clear every sight line
            P("building_castle_{team}", 0f, -23.5f, 180f, Life),
            P("building_tower_B_{team}", -6.6f, -22f, 180f, Life),
            P("building_tower_B_{team}", 6.6f, -22f, 180f, Life),
            P("wall_straight", -10.5f, -22f, 0f, Life),
            P("wall_straight", 10.5f, -22f, 0f, Life),
            P("building_tower_A_{team}", -14.6f, -22f, 180f, Life),
            P("building_tower_A_{team}", 14.6f, -22f, 180f, Life),
            // the town behind the wall
            P("building_home_A_{team}", -9f, -27f, 160f, Life),
            P("building_home_B_{team}", -5f, -29.5f, 200f, Life),
            P("building_church_{team}", 9.5f, -28f, 190f, Life),
            P("building_home_A_{team}", 5.5f, -30f, 170f, Life),
            P("building_windmill_{team}", -20f, -26f, 150f, Life),
            // the field camp between the wall and the board: buildings, then tents, the camp's kit
            // gathered by the building it serves, and its banners along the edge of the field
            P("building_barracks_{team}", -9.4f, -17.6f, 180f, Life),
            P("building_archeryrange_{team}", 9.8f, -17.8f, 180f, Life),
            P("building_well_{team}", 0.3f, -17.6f, 180f, Life),
            P("tent", -4.8f, -15.6f, 10f, Life),
            P("tent", -2.1f, -15.2f, -8f, Life),
            P("tent", 2.6f, -15.4f, 4f, Life),
            P("tent", 5.2f, -15.8f, -12f, Life),
            // the armoury beside the barracks
            P("weaponrack", -13.6f, -16.4f, 90f, Life),
            P("resource_lumber", -13.8f, -18.6f, 0f, Life),
            P("wheelbarrow", -12.4f, -19.6f, 150f, Life),
            // stores beside the well
            P("barrel", 2.3f, -17.9f, 0f, Life),
            P("barrel", 2.9f, -18.5f, 30f, Life),
            P("crate_A_big", -1.9f, -18.2f, 15f, Life),
            P("crate_B_small", -1.6f, -17.3f, 40f, Life),
            P("sack", -2.6f, -17.4f, 70f, Life),
            // butts beside the archery range
            P("target", 13.6f, -15.4f, 180f, Life),
            P("target", 13.6f, -17.2f, 180f, Life),
            P("bucket_arrows", 12.6f, -16.3f, 0f, Life),
            // banners along the edge, watchtowers beside the corners
            P("flag_{team}", -12f, -10.9f, 90f, Life),
            P("flag_{team}", -6f, -10.9f, 90f, Life),
            P("flag_{team}", 6f, -10.9f, 90f, Life),
            P("flag_{team}", 12f, -10.9f, 90f, Life),
            P("building_tower_A_{team}", -17.6f, -12.2f, 135f, 2.2f),
            P("building_tower_A_{team}", 17.6f, -12.2f, 225f, 2.2f),
            // the field's dry-stone side walls, this side's half, open in the middle
            P("fence_stone_straight", -15.6f, -8.56f, 0f, Life),
            P("fence_stone_straight", -15.6f, -5.69f, 0f, Life),
            P("fence_stone_straight", -15.6f, -2.82f, 0f, Life),
            P("fence_stone_straight", 15.6f, -8.56f, 0f, Life),
            P("fence_stone_straight", 15.6f, -5.69f, 0f, Life),
            P("fence_stone_straight", 15.6f, -2.82f, 0f, Life),
        };

        static readonly string[] TreeClumps = { "trees_A_large", "trees_B_large", "trees_A_medium", "trees_B_medium" };
        // The pack's hills stand on earthen cliffs; sunk by HillSink only their wooded tops show.
        // hills_C's cliff rises to 0.55, past any sink that keeps its trees, so only hills_B is used.
        static readonly string[] Hills = { "hills_B_trees" };
        const float HillSink = 0.2f;
        static readonly string[] Rocks = { "rock_single_A", "rock_single_B", "rock_single_C", "rock_single_D", "rock_single_E" };

        // The wilds on this side's half, in the same space as Camp, filled after it.
        public static readonly Scatter[] Wilds =
        {
            new Scatter(Hills, R(18f, -30f, 36f, -1f), 3, V(2.6f, 3.2f), 11, HillSink),
            new Scatter(Hills, R(-36f, -30f, -18f, -1f), 3, V(2.6f, 3.2f), 12, HillSink),
            new Scatter(TreeClumps, R(17.5f, -26f, 32f, 0f), 6, V(2.1f, 2.7f), 13),
            new Scatter(TreeClumps, R(-32f, -26f, -17.5f, 0f), 6, V(2.1f, 2.7f), 14),
            new Scatter(Rocks, R(17f, -22f, 28f, 0f), 4, V(3.5f, 5f), 15),
            new Scatter(Rocks, R(-28f, -22f, -17f, 0f), 4, V(3.5f, 5f), 16),
            new Scatter(TreeClumps, R(-40f, -48f, 40f, -33f), 10, V(2.6f, 3.4f), 17),
            new Scatter(Hills, R(-60f, -55f, 60f, -30f), 6, V(3.5f, 4.5f), 18, HillSink),
            new Scatter(TreeClumps, R(36f, -40f, 60f, 0f), 6, V(2.8f, 3.6f), 19),
            new Scatter(TreeClumps, R(-60f, -40f, -36f, 0f), 6, V(2.8f, 3.6f), 20),
        };

        static readonly string[] Mountains = { "mountain_A_grass_trees", "mountain_B_grass_trees", "mountain_C_grass_trees", "mountain_B_grass", "mountain_C" };
        static readonly string[] CloudModels = { "cloud_big", "cloud_small" };

        // Around the board centre: mountains on the horizon, and the clouds that drift above it. The
        // mountains stay under 15 m so the title's orbit (46 m out, 15 m up) frames their peaks.
        public static readonly Ring Horizon = new Ring(Mountains, V(0f, 360f), V(66f, 92f), V(0f, 0f), V(4f, 6f), 22, 5);
        public static readonly Ring Clouds = new Ring(CloudModels, V(0f, 360f), V(45f, 90f), V(7f, 13f), V(3f, 5f), 12, 9);
        public const float CloudDrift = 0.6f;   // degrees a second about the centre

        // ---------------- the battle stage ----------------

        // Stage space: the fighters stand on z = 0, the camera looks along +z from z = -9.
        public static readonly Piece[] Stage =
        {
            P("rock_single_E", -6.6f, 3.4f, 30f, 6f),
            P("rock_single_C", 7.6f, 4.2f, 70f, 5f),
            P("rock_single_B", 6.4f, 3.3f, 10f, 4f),
        };

        public static readonly Scatter[] StageWilds =
        {
            new Scatter(TreeClumps, R(-18f, 7f, 18f, 13f), 9, V(2.4f, 3f), 21),
            new Scatter(TreeClumps, R(-16f, -3f, -8.5f, 6f), 4, V(2.2f, 2.8f), 22),
            new Scatter(TreeClumps, R(8.5f, -3f, 16f, 6f), 4, V(2.2f, 2.8f), 23),
            new Scatter(Hills, R(-40f, 16f, 40f, 34f), 7, V(4f, 5f), 24, HillSink),
        };

        // The defender's keep on the horizon, behind the defender's half: as listed for the enemy
        // (on the right), mirrored across x = 0 for the player.
        public static readonly Piece[] StageKeep =
        {
            P("building_castle_{team}", 12f, 58f, 180f, 2.6f),
            P("building_tower_B_{team}", 7.5f, 57f, 180f, 2.6f),
            P("building_tower_B_{team}", 16.5f, 57f, 180f, 2.6f),
            P("building_windmill_{team}", 23f, 54f, 200f, 2.6f),
        };

        public static readonly Ring StageHorizon = new Ring(Mountains, V(-55f, 55f), V(100f, 140f), V(0f, 0f), V(5f, 7f), 11, 25);
        public static readonly Ring StageClouds = new Ring(CloudModels, V(-60f, 60f), V(50f, 95f), V(16f, 28f), V(4f, 6f), 8, 26);
    }
}
