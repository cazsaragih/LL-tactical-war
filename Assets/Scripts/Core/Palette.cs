using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // The art bible, as code. Every colour in the game reads from here.
    //   Style : KayKit chibi figures on a soft-gradient grass board, a field between a blue and a
    //           red castle in the KayKit Medieval world; two banner colours that read from across the map.
    //           Painted-miniature toon: three light bands, cool blue-violet shadows, thin outlines.
    //   Mood  : late-morning sun from the south-west, warm key, cool sky bounce, blue-grey distance haze.
    public static class Palette
    {
        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // Board and terrain (the world around it is the KayKit atlas's own colours)
        public static readonly Color Meadow     = H("#8FB463");   // board checker, light
        public static readonly Color MeadowAlt  = H("#84AA58");   // board checker, dark
        public static readonly Color PineDark   = H("#2F5230");   // the grooves between tiles: the grid lines
        public static readonly Color Field      = H("#6A8F45");   // the meadow around the board, a step below it
        public static readonly Color Soil       = H("#8C7A4E");   // the board's earthen bank, top
        public static readonly Color SoilDark   = H("#6B5136");   // ... and foot
        public static readonly Color Canvas     = H("#E6D8B8");

        // Armies
        public static readonly Color Azure      = H("#2F6FD6");
        public static readonly Color AzureDark  = H("#1F4A93");
        public static readonly Color AzureLight = H("#7FB0FF");
        public static readonly Color Crimson    = H("#D2463A");
        public static readonly Color CrimsonDark= H("#8E2B24");
        public static readonly Color CrimsonLight=H("#FF8A70");

        // Figures
        public static readonly Color Skin       = H("#E7C29A");
        public static readonly Color Steel      = H("#B8BEC6");
        public static readonly Color Wood       = H("#8B5A33");

        // Shadow, accent, light
        public static readonly Color Ink        = H("#2B3340");
        public static readonly Color Gold       = H("#F2C14E");
        public static readonly Color SunKey     = H("#FFF0D8");
        public static readonly Color FillLight  = H("#B9C9E6");   // the shadowless lift from the south-east
        public static readonly Color Shade      = H("#6B73A8");   // the toon shadow band's light: cool blue-violet, never black
        public static readonly Color RimWarm    = H("#FFCF8A");   // the thin rim that lifts figures off the board
        public static readonly Color SkyTop     = H("#5F98DA");   // zenith: a clear late-morning blue
        public static readonly Color Haze       = H("#AFC0D3");   // horizon, fog and far atmosphere: blue-grey, never beige
        public static readonly Color AmbientSky = H("#9DB7D6");
        public static readonly Color AmbientEq  = H("#9C9C98");
        public static readonly Color AmbientGnd = H("#4A3B2C");

        // Grade (Split Toning works around mid grey: hue and saturation tint, value stays near 0.5)
        public static readonly Color GradeShadows    = Color.HSVToRGB(0.64f, 0.32f, 0.56f);   // cool blue-violet
        public static readonly Color GradeHighlights = Color.HSVToRGB(0.09f, 0.30f, 0.56f);   // warm amber

        // UI: the general's paperwork. Parchment cards written in ink, brass fittings, wax seals in
        // the two banner colours (UiKit draws them all).
        public static readonly Color Parchment  = H("#F3EAD3");   // light paper: text lifted over the world
        public static readonly Color Paper      = H("#E9DCBB");   // a card's face
        public static readonly Color PaperDark  = H("#CDB88D");   // its burnt edge, an empty bar's trough
        public static readonly Color InkFaded   = H("#7A6E58");   // labels and second lines on paper
        public static readonly Color Brass      = H("#C3923A");   // corner fittings, the map pin
        public static readonly Color BrassDark  = H("#7C5A21");
        public static readonly Color BrassLight = H("#F1D58A");
        public static readonly Color Muted      = H("#8E96A3");

        public static Color Team(Team t)      => t == LLTacticalWar.Team.Player ? Azure : Crimson;
        public static Color TeamDark(Team t)  => t == LLTacticalWar.Team.Player ? AzureDark : CrimsonDark;
        public static Color TeamLight(Team t) => t == LLTacticalWar.Team.Player ? AzureLight : CrimsonLight;

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        // ---------- materials (cloned from Resources so they survive player builds) ----------

        static Material toonBase, overlayBase, particleBase, sky;
        static readonly Dictionary<(Texture, Color, ToonStyle), Material> toonCache = new Dictionary<(Texture, Color, ToonStyle), Material>();
        static readonly Dictionary<Color, Material> glowCache = new Dictionary<Color, Material>();

        static Material ToonBase => toonBase != null ? toonBase : toonBase = Resources.Load<Material>("Materials/Toon");

        // The toon shader over a texture (a KayKit atlas, a team skin, the board's swatches; null for
        // plain white) times a colour, drawn in a style. One per combination, so like surfaces batch.
        public static Material Toon(Texture tex, Color color, ToonStyle style)
        {
            var key = (tex, color, style);
            if (toonCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(ToonBase) { name = "Toon_" + (tex != null ? tex.name : ColorUtility.ToHtmlStringRGB(color)) };
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetColor("_RimColor", RimWarm);
            m.SetFloat("_RimStrength", style.Rim);
            m.SetFloat("_BandSoft", style.BandSoft);
            m.SetColor("_ShadeColor", Color.Lerp(Shade, Color.white, style.ShadeLift));
            m.SetShaderPassEnabled("Outline", style.Outline);
            toonCache[key] = m;
            return m;
        }

        // A flat colour on a figure or its kit.
        public static Material Lit(Color c) => Toon(null, c, ToonStyle.Figure);

        // Toon + emission above the bloom threshold: for the one glowing accent per frame.
        public static Material Glow(Color c, float intensity = 2.2f)
        {
            if (glowCache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Lit(c)) { name = "Glow_" + ColorUtility.ToHtmlStringRGB(c) };
            m.SetColor("_EmissionColor", c * intensity);
            glowCache[c] = m;
            return m;
        }

        // The gradient sky: zenith to the haze at the horizon, which is also the fog colour.
        public static Material Sky()
        {
            if (sky != null) return sky;
            sky = new Material(Resources.Load<Material>("Materials/Sky")) { name = "Sky" };
            sky.SetColor("_Zenith", SkyTop);
            sky.SetColor("_Horizon", Haze);
            sky.SetColor("_Glow", SunKey);
            return sky;
        }

        // A copy of an 8 x 4 swatch atlas (KayKit's layout: vertical gradients, cells addressed column, row from the top) with the listed cells moved onto `target`'s hue and
        // saturation, each keeping its own gradient's shape, its value lifted `valueLift` of the way
        // toward the target's. The source must be readable.
        public static Texture2D Recolor(Texture2D src, Vector2Int[] cells, Color target, float valueLift)
        {
            var px = System.Array.ConvertAll(src.GetPixels(), c => (Color32)c);   // GetPixels reads compressed formats too
            int w = src.width, h = src.height, cw = w / 8, ch = h / 4;
            Color.RGBToHSV(target, out float th, out float ts, out float tv);
            foreach (var cell in cells)
            {
                int x0 = cell.x * cw, y0 = h - (cell.y + 1) * ch;   // rows count from the top, texture rows from the bottom
                float sumS = 0f, sumV = 0f;
                for (int y = y0; y < y0 + ch; y++)
                    for (int x = x0; x < x0 + cw; x++)
                    {
                        Color.RGBToHSV(px[y * w + x], out _, out float s, out float v);
                        sumS += s; sumV += v;
                    }
                float n = cw * ch;
                float sGain = ts / Mathf.Max(0.05f, sumS / n);
                float vGain = Mathf.Lerp(1f, tv / Mathf.Max(0.05f, sumV / n), valueLift);
                for (int y = y0; y < y0 + ch; y++)
                    for (int x = x0; x < x0 + cw; x++)
                    {
                        int i = y * w + x;
                        Color.RGBToHSV(px[i], out _, out float s, out float v);
                        Color32 c = Color.HSVToRGB(th, Mathf.Clamp01(s * sGain), Mathf.Clamp01(v * vGain));
                        c.a = px[i].a;
                        px[i] = c;
                    }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = src.name + "_recolored", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        public static Material Overlay()
        {
            if (overlayBase == null)
            {
                overlayBase = Resources.Load<Material>("Materials/Overlay");
                if (overlayBase == null) overlayBase = new Material(Shader.Find("Sprites/Default"));
            }
            return new Material(overlayBase);
        }

        // The board's tile washes (Assets/Shaders/InkWash.shader): one instance per overlay layer.
        public static Material InkWash() => new Material(Resources.Load<Material>("Materials/InkWash"));

        public static Material Particle()
        {
            if (particleBase == null)
            {
                particleBase = Resources.Load<Material>("Materials/Particle");
                if (particleBase == null) particleBase = new Material(Shader.Find("Sprites/Default"));
            }
            return new Material(particleBase);
        }
    }

    public enum Team { Player, Enemy }

    // How the toon shader draws a surface (Assets/Shaders/Toon.shader): the warm rim, the softness of
    // the band edges, how far its shadow band is lifted from Palette.Shade toward white, and whether
    // it gets an outline.
    public readonly struct ToonStyle : System.IEquatable<ToonStyle>
    {
        public readonly float Rim, BandSoft, ShadeLift;
        public readonly bool Outline;

        ToonStyle(float rim, float bandSoft, float shadeLift, bool outline) { Rim = rim; BandSoft = bandSoft; ShadeLift = shadeLift; Outline = outline; }

        public static readonly ToonStyle Figure = new ToonStyle(0.7f, 0.03f, 0f, true);    // units and what they carry
        public static readonly ToonStyle Prop   = new ToonStyle(0f, 0.03f, 0f, true);      // scenery near the board
        public static readonly ToonStyle Far    = new ToonStyle(0f, 0.05f, 0f, false);     // the horizon: no lines that far off
        public static readonly ToonStyle Ground = new ToonStyle(0f, 0.2f, 0.3f, false);    // board and meadow: soft bands, light cast shadows, no lines

        public bool Equals(ToonStyle o) => Rim == o.Rim && BandSoft == o.BandSoft && ShadeLift == o.ShadeLift && Outline == o.Outline;
        public override bool Equals(object o) => o is ToonStyle t && Equals(t);
        public override int GetHashCode() => (Rim, BandSoft, ShadeLift, Outline).GetHashCode();
    }
}
