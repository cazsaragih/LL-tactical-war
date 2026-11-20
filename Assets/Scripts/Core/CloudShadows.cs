using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LLTacticalWar
{
    // Slow cloud shadows: a soft, tiling cookie on the sun, drawn in code (a few wrapped blobs) and
    // slid across the world so the light on the board breathes. Under a cloud the key keeps
    // `Floor` of its strength; the toon shader dims its lit band, the bands stay where they are.
    public sealed class CloudShadows : MonoBehaviour
    {
        const int Size = 128, Blobs = 9, Seed = 21;
        const float Floor = 0.7f;
        const float Tile = 70f;                                    // world metres per cookie repeat
        static readonly Vector2 Wind = new Vector2(0.55f, 0.3f);   // metres per second

        UniversalAdditionalLightData data;

        public static void Attach(Light sun)
        {
            sun.cookie = Cookie();
            var shadows = sun.gameObject.AddComponent<CloudShadows>();
            shadows.data = sun.GetUniversalAdditionalLightData();
            shadows.data.lightCookieSize = new Vector2(Tile, Tile);
        }

        void Update() => data.lightCookieOffset = Wind * Time.time;

        static Texture2D Cookie()
        {
            var rng = new System.Random(Seed);
            var blobs = new (Vector2 at, float r, float k)[Blobs];
            for (int i = 0; i < Blobs; i++)
                blobs[i] = (new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()), 0.08f + 0.1f * (float)rng.NextDouble(), 0.6f + 0.4f * (float)rng.NextDouble());
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);
                    float cover = 0f;
                    foreach (var b in blobs)
                    {
                        var d = p - b.at;
                        d.x -= Mathf.Round(d.x); d.y -= Mathf.Round(d.y);   // wrap, so the cookie tiles
                        cover = Mathf.Max(cover, b.k * Mathf.SmoothStep(1f, 0f, d.magnitude / b.r));
                    }
                    byte v = (byte)(255f * Mathf.Lerp(1f, Floor, cover));
                    px[y * Size + x] = new Color32(v, v, v, 255);
                }
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "CloudShadows", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
