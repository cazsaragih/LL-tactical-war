using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LLTacticalWar
{
    // The general's paperwork, drawn in code at startup: one atlas holding a parchment card (ink border,
    // a 2 px inner rule, brass corner fittings), a small slip, a soft shadow, an ink brush stroke, a
    // rubber-stamp frame, a bar's trough and wash, a scroll's roller, a brass pointer and the two wax
    // seals (a tower for blue, crossed swords for crimson). Everything shares one texture, so the HUD
    // batches. The faces: Noto Serif Bold for headers (OFL, Assets/Resources/Fonts) and TMP's
    // Liberation Sans for numbers and body text, whose digits are already tabular.
    public static class UiKit
    {
        public static Sprite Card { get; private set; }      // Tiled: grain repeats, corners stay put
        public static Sprite Slip { get; private set; }      // Tiled
        public static Sprite Shadow { get; private set; }    // Sliced
        public static Sprite Stroke { get; private set; }    // Filled: an underline that draws itself
        public static Sprite Stamp { get; private set; }     // Sliced, white: tint it with the ink
        public static Sprite Trough { get; private set; }    // Sliced
        public static Sprite Wash { get; private set; }      // white: tint it with the team colour
        public static Sprite Rod { get; private set; }       // Sliced vertically
        public static Sprite Pointer { get; private set; }
        static readonly Dictionary<Team, Sprite> seals = new Dictionary<Team, Sprite>();
        public static Sprite Seal(Team t) => seals[t];

        public static TMP_FontAsset Display { get; private set; }
        public static TMP_FontAsset Body { get; private set; }
        // ink on paper (no outline), and lifted (ink outline and a soft shadow) for text over the world
        public static Material DisplayInk { get; private set; }
        public static Material DisplayLifted { get; private set; }
        public static Material BodyInk { get; private set; }
        public static Material BodyLifted { get; private set; }

        const int Size = 512;
        static Texture2D atlas;
        static int cx, cy, rowH;

        public static void Build()
        {
            if (atlas != null) return;
            atlas = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "UiKit", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            atlas.SetPixels32(new Color32[Size * Size]);
            cx = cy = rowH = 0;
            Card = Add("Card", 192, 192, PaperFace(192, 40, 3f, 9f, true, 3f), new Vector4(40, 40, 40, 40));
            seals[Team.Player] = Add("SealBlue", 128, 128, WaxSeal(Palette.Azure, Palette.AzureDark, Tower), Vector4.zero);
            seals[Team.Enemy] = Add("SealRed", 128, 128, WaxSeal(Palette.Crimson, Palette.CrimsonDark, Swords), Vector4.zero);
            Slip = Add("Slip", 64, 64, PaperFace(64, 14, 2f, -1f, false, 3f), new Vector4(14, 14, 14, 14));
            Shadow = Add("Shadow", 96, 96, SoftShadow(96, 24), new Vector4(40, 40, 40, 40));
            Stamp = Add("Stamp", 128, 64, StampFrame(128, 64), new Vector4(20, 20, 20, 20));
            Rod = Add("Rod", 32, 128, Roller(32, 128), new Vector4(0, 26, 0, 26));
            Trough = Add("Trough", 48, 24, BarTrough(48, 24), new Vector4(10, 10, 10, 10));
            Wash = Add("Wash", 32, 16, BarWash(32, 16), Vector4.zero);
            Pointer = Add("Pointer", 24, 24, BrassDiamond(24), Vector4.zero);
            Stroke = Add("Stroke", 256, 20, BrushStroke(256, 20), Vector4.zero);
            atlas.Apply(false, true);
            BuildFonts();
        }

        static Sprite Add(string name, int w, int h, Color[] px, Vector4 border)
        {
            if (cx + w > Size) { cx = 0; cy += rowH + 2; rowH = 0; }
            atlas.SetPixels(cx, cy, w, h, px);
            var s = Sprite.Create(atlas, new Rect(cx, cy, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = name;
            cx += w + 2;
            rowH = Mathf.Max(rowH, h);
            return s;
        }

        static void BuildFonts()
        {
            Body = TMP_Settings.defaultFontAsset;
            var ttf = Resources.Load<Font>("Fonts/NotoSerif-Bold");
            Display = ttf != null ? TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024) : null;
            if (Display == null) { Debug.LogError("[UiKit] Fonts/NotoSerif-Bold missing: headers fall back to the body face."); Display = Body; }
            else
            {
                Display.name = "NotoSerif-Bold SDF (runtime)";
                Display.fallbackFontAssetTable = new List<TMP_FontAsset> { Body };   // arrows and triangles
            }
            DisplayInk = Ink(Display, 0.04f);
            DisplayLifted = Lifted(Display);
            BodyInk = Ink(Body, 0.12f);
            BodyLifted = Lifted(Body);
        }

        static Material Ink(TMP_FontAsset f, float dilate)
        {
            var m = new Material(f.material) { name = f.name + " Ink" };
            m.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            return m;
        }

        static Material Lifted(TMP_FontAsset f)
        {
            var m = new Material(f.material) { name = f.name + " Lifted" };
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            m.SetColor(ShaderUtilities.ID_OutlineColor, Palette.Ink);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.14f);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, Palette.WithAlpha(Palette.Ink, 0.7f));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.7f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.4f);
            return m;
        }

        // ---------------- shaping helpers ----------------

        static float S(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
        static float Band(float d, float a, float b) => S(a - 0.5f, a + 0.5f, d) * (1f - S(b - 0.5f, b + 0.5f, d));
        static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);

        static float SdBox(Vector2 p, Vector2 centre, Vector2 half)
        {
            var q = new Vector2(Mathf.Abs(p.x - centre.x) - half.x, Mathf.Abs(p.y - centre.y) - half.y);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        }

        static float SdCircle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        static Vector2 Rot(Vector2 p, float deg) { float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y); }

        // Distance in from the edge of a w x h rounded rectangle (positive inside).
        static float Inside(float px, float py, float w, float h, float radius)
        {
            float dx = Mathf.Min(px, w - px), dy = Mathf.Min(py, h - py);
            if (dx < radius && dy < radius) return radius - new Vector2(radius - dx, radius - dy).magnitude;
            return Mathf.Min(dx, dy);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        // Value noise that repeats every `period` px: `cells` lattice cells per period.
        static float Noise(float x, float y, int cells, float period, int seed)
        {
            float cs = period / cells, fx = x / cs, fy = y / cs;
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
            float tx = fx - ix, ty = fy - iy;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            int W(int i) => ((i % cells) + cells) % cells;
            float a = Hash(W(ix), W(iy), seed), b = Hash(W(ix + 1), W(iy), seed), c = Hash(W(ix), W(iy + 1), seed), d = Hash(W(ix + 1), W(iy + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static Color[] Draw(int w, int h, System.Func<float, float, Color> f)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f(x + 0.5f, y + 0.5f);
            return px;
        }

        // ---------------- paper ----------------

        // A square paper face `n` px with `b` px slices: grain that repeats every n - 2b px (so tiled
        // centres and edges meet without seams), a burnt edge, an ink border `ink` px wide, an inner
        // rule 2 px wide `rule` px in (none below 0), and brass corner fittings.
        static Color[] PaperFace(int n, int b, float ink, float rule, bool brass, float radius)
        {
            float period = n - 2 * b;
            return Draw(n, n, (x, y) =>
            {
                float d = Inside(x, y, n, n, radius);
                float gx = Mod(x - b, period), gy = Mod(y - b, period);
                float grain = 0.6f * Noise(gx, gy, Mathf.Max(2, (int)(period / 14f)), period, 3) + 0.4f * Noise(gx, gy, Mathf.Max(4, (int)(period / 4f)), period, 7);
                var c = Palette.Paper * (0.955f + 0.09f * grain);
                c = Color.Lerp(c, Palette.PaperDark, 0.6f * (1f - S(3f, 20f, d)));
                if (rule > 0f) c = Color.Lerp(c, Palette.Ink, 0.5f * Band(d, rule, rule + 2f));
                c = Color.Lerp(c, Palette.Ink, 1f - S(ink - 0.5f, ink + 0.5f, d));
                c.a = S(-0.5f, 0.5f, d);
                return brass ? Fitting(x, y, n, c) : c;
            });
        }

        // An L-shaped brass bracket over each corner, a polished ridge along its arms and a rivet where
        // they meet, outlined in ink.
        static Color Fitting(float x, float y, int n, Color under)
        {
            const float arm = 30f, thick = 9f;
            var p = new Vector2(Mathf.Min(x, n - x), Mathf.Min(y, n - y));
            float sd = Mathf.Min(SdBox(p, new Vector2(arm / 2f, thick / 2f), new Vector2(arm / 2f, thick / 2f)),
                                 SdBox(p, new Vector2(thick / 2f, arm / 2f), new Vector2(thick / 2f, arm / 2f)));
            float inside = 1f - S(-0.6f, 0.6f, sd), rim = 1f - S(0.6f, 1.6f, sd);
            if (rim <= 0f) return under;
            float depth = -sd;
            var brass = Color.Lerp(Palette.BrassDark, Palette.Brass, S(0f, 2.2f, depth));
            brass = Color.Lerp(brass, Palette.BrassLight, 0.6f * Mathf.Clamp01(1f - Mathf.Abs(depth - 3.4f) / 1.3f));
            float rivet = SdCircle(p, new Vector2(thick * 0.5f, thick * 0.5f), 2.6f);
            brass = Color.Lerp(brass, Palette.BrassDark, 1f - S(0f, 1f, Mathf.Abs(rivet)));
            brass = Color.Lerp(brass, Palette.BrassLight, 1f - S(-1.6f, -0.6f, rivet));
            var c = Color.Lerp(under, Palette.Ink, rim * (1f - inside));
            c = Color.Lerp(c, brass, inside);
            c.a = Mathf.Max(under.a, rim);
            return c;
        }

        static Color[] SoftShadow(int n, float spread) => Draw(n, n, (x, y) =>
        {
            float d = Inside(x, y, n, n, 12f) - spread;   // positive inside the shadow's core
            return new Color(1f, 1f, 1f, S(-spread, 6f, d));
        });

        // A rubber stamp's double-ruled frame, its ink mottled where the stamp didn't take.
        static Color[] StampFrame(int w, int h) => Draw(w, h, (x, y) =>
        {
            float d = Inside(x, y, w, h, 8f);
            float ink = Mathf.Max(Band(d, 1.5f, 5f), Band(d, 8f, 9.6f));
            float take = S(0.18f, 0.5f, 0.6f * Noise(x, y, 32, 128f, 11) + 0.4f * Noise(x, y, 64, 128f, 13));
            return new Color(1f, 1f, 1f, ink * (0.45f + 0.55f * take));
        });

        // The trough an HP bar drains into: darker paper ruled in ink.
        static Color[] BarTrough(int w, int h) => Draw(w, h, (x, y) =>
        {
            float d = Inside(x, y, w, h, 5f);
            var c = Color.Lerp(Palette.PaperDark * 0.88f, Palette.Ink, 1f - S(1.5f, 2.5f, d));
            c.a = S(-0.5f, 0.5f, d);
            return c;
        });

        // A painted wash: lighter along its top, faint brush streaks.
        static Color[] BarWash(int w, int h) => Draw(w, h, (x, y) =>
        {
            float v = Mathf.Lerp(0.8f, 1f, y / h) * (0.94f + 0.06f * Noise(x, y * 4f, 4, 32f, 17));
            return new Color(v, v, v, 1f);
        });

        // A small brass diamond with a lit upper-left face: the menu's pointer.
        static Color[] BrassDiamond(int n) => Draw(n, n, (x, y) =>
        {
            var p = new Vector2(x - n / 2f, y - n / 2f);
            float sd = (Mathf.Abs(p.x) + Mathf.Abs(p.y) - n * 0.36f) * 0.7071f;
            float inside = 1f - S(-0.6f, 0.6f, sd), rim = 1f - S(0.4f, 1.4f, sd);
            var brass = p.y > -p.x ? Palette.BrassLight : Palette.Brass;
            brass = Color.Lerp(Palette.BrassDark, brass, S(0f, 1.6f, -sd));
            var c = Color.Lerp(Palette.Ink, brass, inside);
            c.a = rim;
            return c;
        });

        // A scroll's wooden roller, round in section, with a brass knob at each end.
        static Color[] Roller(int w, int h) => Draw(w, h, (x, y) =>
        {
            float u = x / w * 2f - 1f;
            float knobT = Mathf.Min(y, h - y);                       // px from the nearer end
            bool knob = knobT < 22f;
            float half = knob ? 0.95f * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow((knobT - 11f) / 11.5f, 2f))) : 0.6f;
            float sd = (Mathf.Abs(u) - half) * w / 2f;
            if (half <= 0f) return Color.clear;
            float round = Mathf.Sqrt(Mathf.Clamp01(1f - (u / half) * (u / half)));
            float lit = 0.55f + 0.45f * round + 0.25f * Mathf.Exp(-Mathf.Pow((u / half + 0.35f) / 0.18f, 2f));
            var baseC = knob ? Palette.Brass : Palette.Wood;
            var c = Color.Lerp(knob ? Palette.BrassDark : Palette.Wood * 0.55f, baseC, Mathf.Clamp01(lit - 0.3f)) * Mathf.Min(1.25f, lit + 0.2f);
            c = Color.Lerp(c, Palette.Ink, 1f - S(-1.6f, -0.6f, sd));
            c.a = 1f - S(-0.5f, 0.5f, sd);
            return c;
        });

        // One stroke of a broad nib: tapered ends, a slight wave, dry patches. Drawn white.
        static Color[] BrushStroke(int w, int h) => Draw(w, h, (x, y) =>
        {
            float taper = S(0f, 26f, x) * S(w, w - 60f, x);
            float thick = 2.7f * (0.5f + 0.5f * taper) * (1f + 0.12f * Mathf.Sin(x * 0.09f));
            float centre = h / 2f + 0.8f * Mathf.Sin(x * 0.035f + 1f);
            float a = 1f - S(thick - 0.7f, thick + 0.7f, Mathf.Abs(y - centre));
            float dry = 0.82f + 0.18f * Noise(x, y, 64, 256f, 19);
            return new Color(1f, 1f, 1f, a * dry * (0.7f + 0.3f * taper));
        });

        // ---------------- wax seals ----------------

        // A blob of wax with a wavy edge, pressed in the middle by a die: a ring groove, a raised rim,
        // the emblem standing proud. Lit from the upper left, with a soft shadow under it.
        static Color[] WaxSeal(Color wax, Color waxDark, System.Func<Vector2, float> emblem)
        {
            const int n = 128;
            var centre = new Vector2(63f, 66f);
            float Edge(Vector2 p)
            {
                var q = p - centre;
                float th = Mathf.Atan2(q.y, q.x);
                float r = 49f + 2.6f * Mathf.Sin(5f * th + 0.7f) + 1.6f * Mathf.Sin(8f * th + 2.1f) + 1.1f * Mathf.Sin(13f * th + 4f);
                return r - q.magnitude;   // positive inside
            }
            var height = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float r = (p - centre).magnitude, e = Edge(p);
                    const float die = 36f;
                    float hgt = S(0f, 7f, e);
                    hgt -= 0.55f * (1f - S(die - 1.2f, die + 1.2f, r));
                    hgt += 0.35f * Mathf.Exp(-Mathf.Pow((r - (die + 6f)) / 4f, 2f));
                    hgt += 0.22f * Band(r, die - 5.5f, die - 4f);
                    hgt += 0.5f * (1f - S(-0.7f, 0.7f, emblem(p - centre)));
                    height[y * n + x] = hgt;
                }
            var light = new Vector3(-0.5f, 0.6f, 0.62f).normalized;
            return Draw(n, n, (x, y) =>
            {
                int ix = (int)x, iy = (int)y;
                float H(int i, int j) => height[Mathf.Clamp(j, 0, n - 1) * n + Mathf.Clamp(i, 0, n - 1)];
                var normal = new Vector3(-(H(ix + 1, iy) - H(ix - 1, iy)) * 2.4f, -(H(ix, iy + 1) - H(ix, iy - 1)) * 2.4f, 1f).normalized;
                float diff = Mathf.Clamp01(Vector3.Dot(normal, light));
                float spec = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(Vector3.Reflect(-light, normal), Vector3.forward)), 18f);
                var p = new Vector2(x, y);
                float e = Edge(p);
                var body = Color.Lerp(waxDark, wax, 0.35f + 0.3f * height[iy * n + ix]) * (0.62f + 0.55f * diff) + Color.white * (0.32f * spec);
                body.a = 1f;
                float shadow = 0.5f * S(-4f, 3f, Edge(p + new Vector2(-3f, 4f)));
                float cover = S(-0.8f, 0.8f, e);
                var c = Color.Lerp(Palette.WithAlpha(Palette.Ink, shadow), body, cover);
                c.a = Mathf.Max(shadow, cover);
                return c;
            });
        }

        // Signed distance (px, negative inside) of a crenellated tower with a door and a window slit.
        static float Tower(Vector2 p)
        {
            float d = SdBox(p, new Vector2(0f, -3f), new Vector2(10f, 12f));
            d = Mathf.Min(d, SdBox(p, new Vector2(0f, -17f), new Vector2(13f, 2.5f)));
            for (int i = -1; i <= 1; i++) d = Mathf.Min(d, SdBox(p, new Vector2(i * 7.5f, 12.5f), new Vector2(2.6f, 3.6f)));
            float door = Mathf.Min(SdBox(p, new Vector2(0f, -11.5f), new Vector2(3.6f, 4f)), SdCircle(p, new Vector2(0f, -7.5f), 3.6f));
            float slit = SdBox(p, new Vector2(0f, 2.5f), new Vector2(1.4f, 3.6f));
            return Mathf.Max(d, -Mathf.Min(door, slit));
        }

        // Two swords crossed over each other, points up.
        static float Swords(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (float a in new[] { 40f, -40f })
            {
                var q = Rot(p, a);
                float s = SdBox(q, new Vector2(0f, 4f), new Vector2(2.2f, 15f));
                s = Mathf.Min(s, SdBox(Rot(q - new Vector2(0f, 19.5f), 45f), Vector2.zero, new Vector2(1.6f, 1.6f)));
                s = Mathf.Min(s, SdBox(q, new Vector2(0f, -11.5f), new Vector2(8f, 1.8f)));
                s = Mathf.Min(s, SdBox(q, new Vector2(0f, -16.5f), new Vector2(1.6f, 4f)));
                s = Mathf.Min(s, SdCircle(q, new Vector2(0f, -22f), 2.8f));
                d = Mathf.Min(d, s);
            }
            return d;
        }
    }
}
