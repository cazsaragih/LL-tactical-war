using System.Collections.Generic;
using UnityEngine;

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
        const float ValueLift = 0.5f;   // 0 keeps each swatch's brightness, 1 matches the team colour's

        static readonly Dictionary<(KitAtlas, Team), Material> cache = new Dictionary<(KitAtlas, Team), Material>();

        public static Material For(KitAtlas atlas, Team team)
        {
            if (cache.TryGetValue((atlas, team), out var m) && m != null) return m;
            var src = Resources.Load<Texture2D>(UnitLooks.CharacterDir + atlas.Texture);
            var tex = Palette.Recolor(src, atlas.TeamCells, Palette.Team(team), ValueLift);
            tex.name = atlas.Texture + "_" + team;
            m = Palette.Toon(tex, Color.white, ToonStyle.Figure);
            cache[(atlas, team)] = m;
            return m;
        }
    }
}
