using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LLTacticalWar
{
    // Builds the world from SceneryLayout: the board's bevelled grass tiles on an earthen bank, the
    // KayKit trees and rocks on its obstacle tiles, both camps and their wilds, mountains and drifting
    // clouds; and the 1v1 stage with its backdrop. Every KayKit piece draws in toon on the pack's
    // atlas (outlined near the board, plain on the horizon), the tiles and the meadow in soft toon on
    // the Palette's board swatches, and each root is static-batched once it is built.
    public static class Scenery
    {
        static Material kit, kitFar, board;
        static Texture2D atlas;
        static readonly Dictionary<string, (Vector2 centre, float radius)> footprints = new Dictionary<string, (Vector2, float)>();

        // The pack's atlas, its olive grass swatch repainted in the meadow's green so hills and
        // mountain tops sit in the same field as the board.
        static Texture2D Atlas => atlas != null ? atlas : atlas = Palette.Recolor(Resources.Load<Texture2D>(SceneryLayout.Dir + SceneryLayout.Atlas), SceneryLayout.GrassCells, Palette.Field, 0.5f);
        static Material Kit => kit != null ? kit : kit = Palette.Toon(Atlas, Color.white, ToonStyle.Prop);
        static Material KitFar => kitFar != null ? kitFar : kitFar = Palette.Toon(Atlas, Color.white, ToonStyle.Far);
        static Material Board => board != null ? board : board = Palette.Toon(Swatches(), Color.white, ToonStyle.Ground);

        public static void BuildBoard(TileMap map)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(map.transform, false);
            var centre = map.Center;
            var ground = centre + Vector3.up * SceneryLayout.GroundY;

            Plot(root, "Board", map.tileX, map.tileZ, new Vector2(map.tileSize, map.tileSize), Vector3.zero, SceneryLayout.GroundY, new System.Random(SceneryLayout.SurfaceSeed));
            Ground(root, ground);

            foreach (var t in map.tiles)
                if (SceneryLayout.Obstacles.TryGetValue(map.LayoutAt(t), out var looks))
                {
                    var r = new System.Random(SceneryLayout.ObstacleSeed * 7919 + t.index);
                    var look = looks[r.Next(looks.Length)];
                    var turn = Quaternion.Euler(0f, (float)r.NextDouble() * 360f, 0f);
                    foreach (var p in look) Put(root, p, t.position, turn, null);
                }

            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                var turn = Quaternion.Euler(0f, team == Team.Player ? 0f : 180f, 0f);
                var taken = new List<(Vector2, float)>();
                foreach (var p in SceneryLayout.Camp) { Claim(taken, p, true); Put(root, p, ground, turn, Colour(team)); }
                foreach (var s in SceneryLayout.Wilds)
                    foreach (var p in Fill(s, (int)team, taken)) Put(root, p, ground, turn, null);
            }
            foreach (var p in Spread(SceneryLayout.Horizon)) Put(root, p, ground, Quaternion.identity, null, false);
            StaticBatchingUtility.Combine(root.gameObject);

            Clouds(map.transform, ground, SceneryLayout.Clouds);
        }

        // The 1v1 stage: two board squares for the two fighters on a grass plot, in a meadow ringed by
        // trees and hills, with the defender's keep on the horizon. The camera looks from -z, so the
        // dressing sits behind. Returns the call that raises the defender's keep for a battle.
        public static Action<Team> BuildStage(Transform parent)
        {
            var root = new GameObject("Stage").transform;
            root.SetParent(parent, false);
            var ground = Vector3.up * SceneryLayout.StageGroundY;

            Plot(root, "StagePlot", 2, 1, new Vector2(5.2f, 4.8f), new Vector3(-5.2f, 0f, -2.4f), SceneryLayout.StageGroundY, new System.Random(SceneryLayout.SurfaceSeed));
            Ground(root, ground);
            var taken = new List<(Vector2, float)> { (Vector2.zero, 6f) };   // the plot
            foreach (var p in SceneryLayout.Stage) { Claim(taken, p, true); Put(root, p, ground, Quaternion.identity, null); }
            foreach (var s in SceneryLayout.StageWilds)
                foreach (var p in Fill(s, 0, taken)) Put(root, p, ground, Quaternion.identity, null);
            foreach (var p in Spread(SceneryLayout.StageHorizon)) Put(root, p, ground, Quaternion.identity, null, false);

            var keeps = new Dictionary<Team, GameObject>();
            foreach (var team in new[] { Team.Player, Team.Enemy })
            {
                var k = new GameObject("Keep_" + team).transform;
                k.SetParent(root, false);
                foreach (var p in SceneryLayout.StageKeep)
                    Put(k, team == Team.Enemy ? p : new Piece(p.Model, -p.X, p.Z, -p.Yaw, p.Scale, p.Y), ground, Quaternion.identity, Colour(team), false);
                keeps[team] = k.gameObject;
            }
            StaticBatchingUtility.Combine(root.gameObject);

            Clouds(parent, ground, SceneryLayout.StageClouds);
            return defender => { foreach (var kv in keeps) kv.Value.SetActive(kv.Key == defender); };
        }

        static string Colour(Team team) => team == Team.Player ? "blue" : "red";

        // ---------------- pieces ----------------

        // A piece with its footprint centred on its spot: (X, Z) turned by `turn` about `origin`. A far
        // piece (the horizon, clouds, the battle's distant keep) casts no shadow and has no outline.
        static void Put(Transform parent, Piece p, Vector3 origin, Quaternion turn, string colour, bool near = true)
        {
            string model = colour == null ? p.Model : p.Model.Replace("{team}", colour);
            var src = Resources.Load<GameObject>(SceneryLayout.Dir + model);
            var go = Object.Instantiate(src, parent, false);
            go.name = model;
            var rot = turn * Quaternion.Euler(0f, p.Yaw, 0f);
            var fp = Footprint(src);
            go.transform.localScale = src.transform.localScale * p.Scale;
            go.transform.localRotation = rot * src.transform.localRotation;
            go.transform.localPosition = origin + turn * new Vector3(p.X, p.Y, p.Z) - rot * new Vector3(fp.centre.x, 0f, fp.centre.y) * p.Scale;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = new Material[r.GetComponent<MeshFilter>().sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = near ? Kit : KitFar;
                r.sharedMaterials = mats;
                r.shadowCastingMode = near ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        // The model's footprint at import scale, through its root's own rotation and scale: its
        // bounds' centre and half-width on the ground.
        static (Vector2 centre, float radius) Footprint(GameObject src)
        {
            if (footprints.TryGetValue(src.name, out var f)) return f;
            var b = new Bounds();
            bool any = false;
            foreach (var mf in src.GetComponentsInChildren<MeshFilter>())
            {
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = mf.transform.localToWorldMatrix.MultiplyPoint3x4(corner) - src.transform.localPosition;
                    if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
                }
            }
            f = (new Vector2(b.center.x, b.center.z), Mathf.Max(b.extents.x, b.extents.z));
            footprints[src.name] = f;
            return f;
        }

        static float Radius(string model) => Footprint(Resources.Load<GameObject>(SceneryLayout.Dir + model.Replace("{team}", "blue"))).radius;

        // Room on the ground: a piece may lean into its neighbours a little, never stand inside one.
        // An authored piece (`always`) takes its room regardless, so scatters flow around it.
        static bool Claim(List<(Vector2 at, float r)> taken, Piece p, bool always = false)
        {
            var at = new Vector2(p.X, p.Z);
            float r = Radius(p.Model) * p.Scale * 0.8f;
            if (!always)
                foreach (var t in taken)
                    if ((t.at - at).sqrMagnitude < (t.r + r) * (t.r + r)) return false;
            taken.Add((at, r));
            return true;
        }

        static float Between(Vector2 range, System.Random rng) => Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());

        // A scatter's pieces, each on free ground. `salt` gives each side its own draw.
        static List<Piece> Fill(Scatter s, int salt, List<(Vector2, float)> taken)
        {
            var rng = new System.Random(s.Seed * 7919 + salt);
            var list = new List<Piece>();
            for (int tries = 0; list.Count < s.Count && tries < s.Count * 40; tries++)
            {
                var p = new Piece(s.Models[rng.Next(s.Models.Length)],
                                  Mathf.Lerp(s.Area.xMin, s.Area.xMax, (float)rng.NextDouble()),
                                  Mathf.Lerp(s.Area.yMin, s.Area.yMax, (float)rng.NextDouble()),
                                  (float)rng.NextDouble() * 360f, Between(s.Scale, rng));
                if (Claim(taken, p)) list.Add(new Piece(p.Model, p.X, p.Z, p.Yaw, p.Scale, -s.Sink * p.Scale));
            }
            return list;
        }

        // A ring's pieces, one per slot of its arc.
        static List<Piece> Spread(Ring g)
        {
            var rng = new System.Random(g.Seed);
            var list = new List<Piece>();
            for (int i = 0; i < g.Count; i++)
            {
                float a = Mathf.Lerp(g.Arc.x, g.Arc.y, (i + 0.2f + 0.6f * (float)rng.NextDouble()) / g.Count) * Mathf.Deg2Rad;
                float r = Between(g.Radius, rng);
                list.Add(new Piece(g.Models[rng.Next(g.Models.Length)], Mathf.Sin(a) * r, Mathf.Cos(a) * r,
                                   (float)rng.NextDouble() * 360f, Between(g.Scale, rng), Between(g.Height, rng)));
            }
            return list;
        }

        // Clouds turn slowly about the centre, so they stay out of the static batch.
        static void Clouds(Transform parent, Vector3 centre, Ring ring)
        {
            var pivot = new GameObject("Clouds").transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = centre;
            foreach (var p in Spread(ring)) Put(pivot, p, Vector3.zero, Quaternion.identity, null, false);
            pivot.gameObject.AddComponent<Drift>().degreesPerSecond = SceneryLayout.CloudDrift;
        }

        // ---------------- ground and board ----------------

        // The meadow: one wide quad in the Palette's field green, fading into the fog long before its edge.
        static void Ground(Transform parent, Vector3 centre)
        {
            const float half = 200f;
            var uv = new Vector2((FieldColumn + 0.5f) / SwatchW, 0.5f);
            var mesh = new Mesh { name = "Ground" };
            mesh.SetVertices(new[] { centre + new Vector3(-half, 0, -half), centre + new Vector3(-half, 0, half), centre + new Vector3(half, 0, half), centre + new Vector3(half, 0, -half) });
            mesh.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new[] { uv, uv, uv, uv });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            Surface(parent, "Ground", mesh, Board);
        }

        // The board swatches: one soft vertical gradient per column, like a KayKit atlas. Columns
        // 0-3 are the light checker's shades, 4-7 the dark one's, then the groove, the bank and the field.
        const int Shades = 4, GrooveColumn = 2 * Shades, BankColumn = GrooveColumn + 1, FieldColumn = BankColumn + 1, SwatchW = 16;

        static Texture2D Swatches()
        {
            const int h = 32;
            var tex = new Texture2D(SwatchW, h, TextureFormat.RGBA32, false) { name = "BoardSwatches", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[SwatchW * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < SwatchW; x++)
                {
                    float v = y / (h - 1f);
                    Color c;
                    if (x < GrooveColumn)
                        c = (x < Shades ? Palette.Meadow : Palette.MeadowAlt) * (Mathf.Lerp(0.965f, 1.035f, x % Shades / (Shades - 1f)) * Mathf.Lerp(0.84f, 1.06f, v));
                    else if (x == GrooveColumn) c = Palette.PineDark * Mathf.Lerp(0.9f, 1.1f, v);
                    else if (x == BankColumn) c = Color.Lerp(Palette.SoilDark, Palette.Soil, v);
                    else c = Palette.Field;
                    c.a = 1f;
                    px[y * SwatchW + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        // A grass plot of cols x rows bevelled tiles from `origin` (its south-west corner, tile-top
        // level): each tile a soft dome of its checker shade, a chamfer down to the dark grooves that
        // draw the grid, and an earthen bank sloping out from the plot's rim down to groundY.
        static void Plot(Transform parent, string name, int cols, int rows, Vector2 tile, Vector3 origin, float groundY, System.Random rng)
        {
            const float top = 0.02f, floor = -0.07f, groove = 0.035f, bevel = 0.13f, bankRun = 0.45f;
            var b = new MeshBuilder();
            float w = cols * tile.x, d = rows * tile.y;
            for (int z = 0; z < rows; z++)
                for (int x = 0; x < cols; x++)
                {
                    float u = (((x + z) % 2) * Shades + rng.Next(Shades) + 0.5f) / SwatchW;
                    var o0 = new Vector2(x * tile.x + groove, z * tile.y + groove);
                    var o1 = new Vector2((x + 1) * tile.x - groove, (z + 1) * tile.y - groove);
                    var i0 = o0 + Vector2.one * bevel;
                    var i1 = o1 - Vector2.one * bevel;
                    var inner = Corners(i0, i1, top);
                    var outer = Corners(o0, o1, floor);
                    var mid = new Vector3((i0.x + i1.x) / 2f, top, (i0.y + i1.y) / 2f);
                    for (int k = 0; k < 4; k++)
                    {
                        b.Face(Vector3.up, (mid, new Vector2(u, 1f)), (inner[k], new Vector2(u, 0.78f)), (inner[(k + 1) % 4], new Vector2(u, 0.78f)));
                        var outward = (inner[k] + inner[(k + 1) % 4]) / 2f - mid;
                        b.Face(outward, (outer[k], new Vector2(u, 0.3f)), (inner[k], new Vector2(u, 0.78f)), (inner[(k + 1) % 4], new Vector2(u, 0.78f)), (outer[(k + 1) % 4], new Vector2(u, 0.3f)));
                    }
                }
            var gu = (GrooveColumn + 0.5f) / SwatchW;
            var rim = Corners(Vector2.zero, new Vector2(w, d), floor);
            b.Face(Vector3.up, (rim[0], new Vector2(gu, 0.5f)), (rim[1], new Vector2(gu, 0.5f)), (rim[2], new Vector2(gu, 0.5f)), (rim[3], new Vector2(gu, 0.5f)));
            var bu = (BankColumn + 0.5f) / SwatchW;
            var foot = Corners(-Vector2.one * bankRun, new Vector2(w, d) + Vector2.one * bankRun, groundY);
            var plotMid = new Vector3(w / 2f, floor, d / 2f);
            for (int k = 0; k < 4; k++)
            {
                var outward = (rim[k] + rim[(k + 1) % 4]) / 2f - plotMid;
                b.Face(outward, (foot[k], new Vector2(bu, 0f)), (rim[k], new Vector2(bu, 1f)), (rim[(k + 1) % 4], new Vector2(bu, 1f)), (foot[(k + 1) % 4], new Vector2(bu, 0f)));
            }
            var mesh = b.Build(name);
            var go = Surface(parent, name, mesh, Board);
            go.transform.localPosition = origin;
        }

        // Clockwise from above: (x0, z0), (x0, z1), (x1, z1), (x1, z0).
        static Vector3[] Corners(Vector2 a, Vector2 b, float y) =>
            new[] { new Vector3(a.x, y, a.y), new Vector3(a.x, y, b.y), new Vector3(b.x, y, b.y), new Vector3(b.x, y, a.y) };

        static GameObject Surface(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // Flat-shaded polygons: each face gets its own vertices and the normal its winding gives,
        // flipped to agree with the side it should face.
        sealed class MeshBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();

            public void Face(Vector3 facing, params (Vector3 p, Vector2 uv)[] c)
            {
                var n = Vector3.Cross(c[1].p - c[0].p, c[2].p - c[0].p).normalized;
                bool flip = Vector3.Dot(n, facing) < 0f;
                if (flip) n = -n;
                int v = verts.Count;
                foreach (var corner in c) { verts.Add(corner.p); normals.Add(n); uvs.Add(corner.uv); }
                for (int i = 1; i < c.Length - 1; i++)
                {
                    tris.Add(v);
                    tris.Add(v + (flip ? i + 1 : i));
                    tris.Add(v + (flip ? i : i + 1));
                }
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(verts);
                m.SetNormals(normals);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            }
        }
    }

    // Turns its object slowly about its own vertical axis: the clouds' drift.
    public sealed class Drift : MonoBehaviour
    {
        public float degreesPerSecond;
        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f);
    }
}
