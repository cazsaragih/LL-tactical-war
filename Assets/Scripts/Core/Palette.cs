using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // The art bible, as code. Every colour in the game reads from here.
    //   Style : toy-soldier war table - chunky flat-shaded primitives on a painted board,
    //           two banner colours that read from across the map.
    //   Mood  : late-morning sun from the south-west, warm key, cool sky bounce.
    public static class Palette
    {
        static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // Board and terrain
        public static readonly Color Meadow     = H("#7E9F5B");
        public static readonly Color MeadowAlt  = H("#75964F");
        public static readonly Color Pine       = H("#3E6A3C");
        public static readonly Color PineDark   = H("#2F5230");
        public static readonly Color Stone      = H("#8A8378");
        public static readonly Color StoneDark  = H("#6A645B");
        public static readonly Color Walnut     = H("#6B5136");
        public static readonly Color Dirt       = H("#7F8C55");   // battlefield steppe grass
        public static readonly Color DirtDark   = H("#8C7A4E");   // trampled earth patches
        public static readonly Color Brick      = H("#A65A3C");
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
        public static readonly Color Horse      = H("#7A4E2E");

        // Shadow, accent, light
        public static readonly Color Ink        = H("#2B3340");
        public static readonly Color Gold       = H("#F2C14E");
        public static readonly Color SunKey     = H("#FFF0D8");
        public static readonly Color SkyTop     = H("#8FB8E0");
        public static readonly Color SkyHorizon = H("#E8DCC0");
        public static readonly Color AmbientSky = H("#9DB7D6");
        public static readonly Color AmbientEq  = H("#A79F8C");
        public static readonly Color AmbientGnd = H("#4A3B2C");

        // UI
        public static readonly Color Panel      = H("#1E2430");
        public static readonly Color PanelLight = H("#2C3444");
        public static readonly Color Parchment  = H("#F3EAD3");
        public static readonly Color Muted      = H("#8E96A3");

        public static Color Team(Team t)      => t == LLTacticalWar.Team.Player ? Azure : Crimson;
        public static Color TeamDark(Team t)  => t == LLTacticalWar.Team.Player ? AzureDark : CrimsonDark;
        public static Color TeamLight(Team t) => t == LLTacticalWar.Team.Player ? AzureLight : CrimsonLight;

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        // ---------- materials (cloned from Resources so they survive player builds) ----------

        static Material litBase, overlayBase, particleBase;
        static readonly Dictionary<Color, Material> litCache = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> glowCache = new Dictionary<Color, Material>();

        static Material LitBase
        {
            get
            {
                if (litBase == null)
                {
                    litBase = Resources.Load<Material>("Materials/BaseLit");
                    if (litBase == null) litBase = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }
                return litBase;
            }
        }

        public static Material Lit(Color c)
        {
            if (litCache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(LitBase) { name = "Lit_" + ColorUtility.ToHtmlStringRGB(c) };
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", 0.12f);
            litCache[c] = m;
            return m;
        }

        // Lit + emission above the bloom threshold: for the one glowing accent per frame.
        public static Material Glow(Color c, float intensity = 2.2f)
        {
            if (glowCache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Lit(c)) { name = "Glow_" + ColorUtility.ToHtmlStringRGB(c) };
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c * intensity);
            glowCache[c] = m;
            return m;
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
}
