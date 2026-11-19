using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LLTacticalWar
{
    // Team colours on the KayKit figures. Both armies use the same models, so each atlas is
    // copied once per team with its team swatches (KitAtlas.TeamCells) moved onto the team hue.
    // A swatch keeps its own vertical gradient: hue becomes the team's, saturation is rescaled
    // so the swatch averages the team colour's, and value keeps its shape, lifted part of the
    // way toward the team colour so dark cloth still reads as blue or crimson from the map
    // camera. Skin, hair, metal and wood swatches are never listed, so never touched.
    public static class TeamSkin
    {
        const int Columns = 8, Rows = 4;
        const float ValueLift = 0.5f;   // 0 keeps each swatch's brightness, 1 matches the team colour's

        static readonly Dictionary<(KitAtlas, Team), Material> cache = new Dictionary<(KitAtlas, Team), Material>();

        public static Material For(KitAtlas atlas, Team team)
        {
            if (cache.TryGetValue((atlas, team), out var m) && m != null) return m;
            var src = Resources.Load<Texture2D>(UnitLooks.CharacterDir + atlas.Texture);
            m = new Material(LitBase()) { name = atlas.Texture + "_" + team };
            m.SetTexture("_BaseMap", Recolor(src, atlas.TeamCells, Palette.Team(team)));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.1f);
            m.EnableKeyword("_EMISSION");   // UnitModel's hit flash writes _EmissionColor
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", Color.black);
            cache[(atlas, team)] = m;
            return m;
        }

        // The pipeline's own default Lit material. Player builds may not carry it, so they fall
        // back to the BaseLit material ProjectSetup made from the same shader.
        static Material LitBase()
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            return rp != null && rp.defaultMaterial != null ? rp.defaultMaterial : Resources.Load<Material>("Materials/BaseLit");
        }

        static Texture2D Recolor(Texture2D src, Vector2Int[] cells, Color team)
        {
            var px = src.GetPixels32();
            int w = src.width, h = src.height, cw = w / Columns, ch = h / Rows;
            Color.RGBToHSV(team, out float th, out float ts, out float tv);
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
                float vGain = Mathf.Lerp(1f, tv / Mathf.Max(0.05f, sumV / n), ValueLift);
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
                name = src.name + "_team", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}
