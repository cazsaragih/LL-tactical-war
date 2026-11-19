using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 TileMap: a generated grid mesh, tile neighbour links, and a
    // point-filtered texture that highlights tiles. Ranges are now flood fills that respect the
    // map edges and occupied tiles; the old diamond walk crashed near an edge.
    public class TileMap : MonoBehaviour
    {
        public static TileMap instance;

        public int tileX = 14;
        public int tileZ = 10;
        public float tileSize = 2f;
        public TerrainType terrainType = TerrainType.Plain;

        public readonly List<TileMapInfo> tiles = new List<TileMapInfo>();
        public readonly List<TileMapInfo> inRangeTiles = new List<TileMapInfo>();

        Texture2D overlay;
        static readonly Color Clear = new Color(0, 0, 0, 0);

        // Obstacle layout, top row is z = tileZ-1. T = trees, R = rocks.
        static readonly string[] Layout =
        {
            "..............",
            "...T......R...",
            "......TT......",
            ".R..........T.",
            "....T....R....",
            "....R....T....",
            ".T..........R.",
            "......TT......",
            "...R......T...",
            "..............",
        };

        public Vector3 Center => new Vector3(tileX * tileSize / 2f, 0f, tileZ * tileSize / 2f);

        void Awake()
        {
            instance = this;
            BuildTiles();
            BuildBoardMesh();
            BuildOverlay();
            Scenery.BuildBoard(this);
        }

        void BuildTiles()
        {
            tiles.Clear();
            for (int z = 0; z < tileZ; z++)
                for (int x = 0; x < tileX; x++)
                {
                    var t = new TileMapInfo
                    {
                        index = z * tileX + x, x = x, z = z,
                        position = new Vector3((x + 0.5f) * tileSize, 0f, (z + 0.5f) * tileSize)
                    };
                    char c = Layout[tileZ - 1 - z][x];
                    t.blocked = c == 'T' || c == 'R';
                    tiles.Add(t);
                }
            foreach (var t in tiles)
            {
                t.north = t.z < tileZ - 1 ? tiles[t.index + tileX] : null;
                t.south = t.z > 0 ? tiles[t.index - tileX] : null;
                t.east = t.x < tileX - 1 ? tiles[t.index + 1] : null;
                t.west = t.x > 0 ? tiles[t.index - 1] : null;
            }
        }

        public char LayoutAt(TileMapInfo t) => Layout[tileZ - 1 - t.z][t.x];

        // Checkerboard of slightly inset tile tops over a dark board, two submeshes so each
        // checker colour gets its own palette material.
        void BuildBoardMesh()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var a = new List<int>();
            var b = new List<int>();
            float inset = 0.05f;
            foreach (var t in tiles)
            {
                float x0 = t.x * tileSize + inset, x1 = (t.x + 1) * tileSize - inset;
                float z0 = t.z * tileSize + inset, z1 = (t.z + 1) * tileSize - inset;
                int v = verts.Count;
                verts.Add(new Vector3(x0, 0, z0)); verts.Add(new Vector3(x0, 0, z1));
                verts.Add(new Vector3(x1, 0, z1)); verts.Add(new Vector3(x1, 0, z0));
                for (int i = 0; i < 4; i++) normals.Add(Vector3.up);
                var list = ((t.x + t.z) % 2 == 0) ? a : b;
                list.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = "TileBoard" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(a, 0);
            mesh.SetTriangles(b, 1);
            mesh.RecalculateBounds();

            var go = new GameObject("TileTops");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0.02f, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { Palette.Lit(Palette.Meadow), Palette.Lit(Palette.MeadowAlt) };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // board slab + collider so the cursor ray still has something to hit
            var slab = Prim.Part("BoardSlab", PrimitiveType.Cube, transform,
                new Vector3(tileX * tileSize / 2f, -0.3f, tileZ * tileSize / 2f),
                new Vector3(tileX * tileSize + 0.1f, 0.6f, tileZ * tileSize + 0.1f), Palette.PineDark);
            slab.AddComponent<BoxCollider>();
        }

        void BuildOverlay()
        {
            overlay = new Texture2D(tileX, tileZ, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            ClearHighlights();

            var mesh = new Mesh { name = "Overlay" };
            float w = tileX * tileSize, h = tileZ * tileSize;
            mesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, h), new Vector3(w, 0, h), new Vector3(w, 0, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            var go = new GameObject("Highlights");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0.05f, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            var mat = Palette.Overlay();
            mat.mainTexture = overlay;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ---------------- highlights ----------------

        public void ClearHighlights()
        {
            for (int z = 0; z < tileZ; z++)
                for (int x = 0; x < tileX; x++)
                    overlay.SetPixel(x, z, Clear);
            overlay.Apply();
        }

        public void HighlightTiles(IEnumerable<TileMapInfo> list, Color color, float alpha = 0.5f)
        {
            color.a = alpha;
            foreach (var t in list) overlay.SetPixel(t.x, t.z, color);
            overlay.Apply();
        }

        public void HighlightTile(TileMapInfo t, Color color, float alpha = 0.6f)
        {
            color.a = alpha;
            overlay.SetPixel(t.x, t.z, color);
            overlay.Apply();
        }

        // ---------------- ranges ----------------

        // Tiles a unit can walk to within `steps`, never through blocked or occupied tiles.
        // The unit's own tile is included (so "stay here" is a valid move).
        public List<TileMapInfo> MoveRange(TileMapInfo start, int steps)
        {
            var result = new List<TileMapInfo> { start };
            var dist = new Dictionary<TileMapInfo, int> { [start] = 0 };
            var q = new Queue<TileMapInfo>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var t = q.Dequeue();
                if (dist[t] >= steps) continue;
                foreach (var n in t.Neighbours)
                {
                    if (n == null || dist.ContainsKey(n) || !n.Reachable) continue;
                    dist[n] = dist[t] + 1;
                    result.Add(n);
                    q.Enqueue(n);
                }
            }
            return result;
        }

        // Manhattan diamond of attack reach, edge-safe, excluding the attacker's tile.
        public List<TileMapInfo> AttackRange(TileMapInfo start, int range)
        {
            var result = new List<TileMapInfo>();
            foreach (var t in tiles)
            {
                int d = t.Distance(start);
                if (d >= 1 && d <= range) result.Add(t);
            }
            return result;
        }

        public void DetermineAvailableTiles(List<TileMapInfo> list)
        {
            ResetInRange();
            foreach (var t in list) { t.inRange = true; inRangeTiles.Add(t); }
        }

        public void ResetInRange()
        {
            foreach (var t in inRangeTiles) t.inRange = false;
            inRangeTiles.Clear();
        }

        // ---------------- conversions ----------------

        public TileMapInfo TileAt(int x, int z)
        {
            if (x < 0 || z < 0 || x >= tileX || z >= tileZ) return null;
            return tiles[z * tileX + x];
        }

        public int WorldToTilePoint(Vector3 location)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(location.x / tileSize), 0, tileX - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(location.z / tileSize), 0, tileZ - 1);
            return x + tileX * z;
        }
    }
}
