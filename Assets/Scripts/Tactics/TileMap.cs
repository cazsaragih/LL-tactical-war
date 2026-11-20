using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Ported from the 2015 TileMap: tile neighbour links and point-filtered textures that
    // highlight tiles; Scenery builds what the board looks like. Ranges are now flood fills that
    // respect the map edges and occupied tiles; the old diamond walk crashed near an edge.
    public class TileMap : MonoBehaviour
    {
        public static TileMap instance;

        public int tileX = 14;
        public int tileZ = 10;
        public float tileSize = 2f;
        public TerrainType terrainType = TerrainType.Plain;

        public readonly List<TileMapInfo> tiles = new List<TileMapInfo>();
        public readonly List<TileMapInfo> inRangeTiles = new List<TileMapInfo>();

        // Two overlay layers: the danger zone underneath, the per-state highlights on top.
        OverlayLayer highlights, danger;

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

        void BuildOverlay()
        {
            danger = new OverlayLayer(this, "DangerZone", 0.04f, 3000);
            highlights = new OverlayLayer(this, "Highlights", 0.05f, 3001);
        }

        // ---------------- highlights ----------------

        public void ClearHighlights() { highlights.Clear(); highlights.Push(); }

        public void HighlightTiles(IEnumerable<TileMapInfo> list, Color color, float alpha = 0.5f)
        {
            foreach (var t in list) highlights.Paint(t, color, alpha, false);
            highlights.Push();
        }

        public void HighlightTile(TileMapInfo t, Color color, float alpha = 0.6f)
        {
            highlights.Paint(t, color, alpha, false);
            highlights.Push();
        }

        // The danger zone: striped so it reads apart from a unit's own attack range.
        public void ShowDanger(IEnumerable<TileMapInfo> list)
        {
            danger.Clear();
            if (list != null) foreach (var t in list) danger.Paint(t, Palette.Crimson, 0.34f, true);
            danger.Push();
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

        // Manhattan ring of attack reach between min and max range, edge-safe.
        public List<TileMapInfo> AttackRange(TileMapInfo start, int minRange, int maxRange)
        {
            var result = new List<TileMapInfo>();
            foreach (var t in tiles)
            {
                int d = t.Distance(start);
                if (d >= minRange && d <= maxRange) result.Add(t);
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

    // One transparent quad over the board, PerTile texels per tile, so a highlighted tile gets
    // a fill and a brighter one-texel rim.
    public sealed class OverlayLayer
    {
        const int PerTile = 12;
        readonly Texture2D tex;
        readonly Color32[] px;
        readonly int width;

        public OverlayLayer(TileMap map, string name, float height, int renderQueue)
        {
            width = map.tileX * PerTile;
            tex = new Texture2D(width, map.tileZ * PerTile, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            px = new Color32[width * map.tileZ * PerTile];
            Push();

            var mesh = new Mesh { name = name };
            float w = map.tileX * map.tileSize, h = map.tileZ * map.tileSize;
            mesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, h), new Vector3(w, 0, h), new Vector3(w, 0, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            var go = new GameObject(name);
            go.transform.SetParent(map.transform, false);
            go.transform.localPosition = new Vector3(0, height, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            var mat = Palette.Overlay();
            mat.mainTexture = tex;
            mat.renderQueue = renderQueue;   // same quad size, so sort by queue, not distance
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public void Clear() => System.Array.Clear(px, 0, px.Length);

        public void Paint(TileMapInfo t, Color color, float alpha, bool striped)
        {
            Color32 fill = Palette.WithAlpha(color, alpha);
            Color32 faint = Palette.WithAlpha(color, alpha * 0.35f);
            Color32 rim = Palette.WithAlpha(Color.Lerp(color, Palette.Parchment, 0.3f), Mathf.Min(1f, alpha * 1.9f));
            for (int y = 0; y < PerTile; y++)
                for (int x = 0; x < PerTile; x++)
                {
                    bool edge = x == 0 || y == 0 || x == PerTile - 1 || y == PerTile - 1;
                    bool gap = striped && (x + y) % 6 >= 3;
                    px[(t.z * PerTile + y) * width + t.x * PerTile + x] = edge ? rim : gap ? faint : fill;
                }
        }

        public void Push() { tex.SetPixels32(px); tex.Apply(); }
    }
}
