using UnityEngine;

namespace LLTacticalWar
{
    // Environment dressing. No bare planes: the board sits on a walnut war table, obstacles are
    // little tree clumps and rock piles, the enemy holds a brick fort to the north and your camp
    // tents sit to the south.
    public static class Scenery
    {
        public static void BuildBoard(TileMap map)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(map.transform, false);
            float w = map.tileX * map.tileSize, h = map.tileZ * map.tileSize;

            // the table and the meadow beyond it
            Prim.Part("TableTop", PrimitiveType.Cube, root, new Vector3(w / 2f, -0.75f, h / 2f), new Vector3(w + 3f, 0.5f, h + 3f), Palette.Walnut);
            Prim.Part("TableTrim", PrimitiveType.Cube, root, new Vector3(w / 2f, -0.46f, h / 2f), new Vector3(w + 0.7f, 0.08f, h + 0.7f), Palette.Gold);
            Prim.Part("Field", PrimitiveType.Cube, root, new Vector3(w / 2f, -1.1f, h / 2f), new Vector3(w + 80f, 0.2f, h + 80f), Palette.Pine);

            var rng = new System.Random(7);
            foreach (var t in map.tiles)
            {
                char c = map.LayoutAt(t);
                if (c == 'T') TreeClump(root, t.position, rng);
                else if (c == 'R') RockPile(root, t.position, rng);
            }

            Fort(root, new Vector3(w / 2f, 0f, h + 5.5f));
            Camp(root, new Vector3(w / 2f, -1f, -9f));

            // distant tree ring so the horizon isn't empty
            for (int i = 0; i < 46; i++)
            {
                float ang = i / 46f * Mathf.PI * 2f;
                float rad = Mathf.Max(w, h) * 0.85f + (float)rng.NextDouble() * 6f;
                var p = new Vector3(w / 2f + Mathf.Cos(ang) * rad, -1f, h / 2f + Mathf.Sin(ang) * rad * 0.8f);
                Tree(root, p, 1.6f + (float)rng.NextDouble(), rng);
            }
        }

        public static void Tree(Transform parent, Vector3 pos, float scale, System.Random rng)
        {
            var t = new GameObject("Tree").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 6f - 3f);
            t.localScale = Vector3.one * scale;
            Prim.Part("Trunk", PrimitiveType.Cylinder, t, new Vector3(0, 0.3f, 0), new Vector3(0.16f, 0.3f, 0.16f), Palette.Wood);
            Prim.Part("Crown1", PrimitiveType.Sphere, t, new Vector3(0, 0.85f, 0), new Vector3(0.8f, 0.6f, 0.8f), Palette.Pine);
            Prim.Part("Crown2", PrimitiveType.Sphere, t, new Vector3(0.05f, 1.3f, 0.02f), new Vector3(0.55f, 0.5f, 0.55f), Palette.PineDark);
            Prim.Part("Crown3", PrimitiveType.Sphere, t, new Vector3(0, 1.65f, 0), new Vector3(0.3f, 0.32f, 0.3f), Palette.Pine);
        }

        static void TreeClump(Transform parent, Vector3 center, System.Random rng)
        {
            Tree(parent, center + new Vector3(-0.35f, 0, 0.3f), 1.05f, rng);
            Tree(parent, center + new Vector3(0.45f, 0, -0.1f), 0.85f, rng);
            Tree(parent, center + new Vector3(-0.1f, 0, -0.5f), 0.7f, rng);
        }

        static void RockPile(Transform parent, Vector3 center, System.Random rng)
        {
            Prim.Part("Rock", PrimitiveType.Sphere, parent, center + new Vector3(0, 0.3f, 0), new Vector3(1.2f, 0.75f, 1.0f), Palette.Stone, new Vector3(0, (float)rng.NextDouble() * 90f, 8f));
            Prim.Part("Rock", PrimitiveType.Sphere, parent, center + new Vector3(0.5f, 0.2f, 0.4f), new Vector3(0.6f, 0.45f, 0.55f), Palette.StoneDark, new Vector3(0, 30f, -10f));
            Prim.Part("Rock", PrimitiveType.Sphere, parent, center + new Vector3(-0.5f, 0.15f, -0.4f), new Vector3(0.5f, 0.35f, 0.5f), Palette.Stone);
        }

        static void Fort(Transform parent, Vector3 pos)
        {
            var f = new GameObject("EnemyFort").transform;
            f.SetParent(parent, false);
            f.localPosition = pos;
            Prim.Part("Wall", PrimitiveType.Cube, f, new Vector3(0, 1f, 0), new Vector3(12f, 2f, 1.2f), Palette.Brick);
            for (int i = -5; i <= 5; i += 2)
                Prim.Part("Merlon", PrimitiveType.Cube, f, new Vector3(i, 2.25f, 0), new Vector3(0.9f, 0.5f, 1.25f), Palette.Brick);
            foreach (float x in new[] { -6.5f, 6.5f })
            {
                Prim.Part("Tower", PrimitiveType.Cylinder, f, new Vector3(x, 1.6f, 0), new Vector3(2f, 1.6f, 2f), Palette.Brick);
                Prim.Part("Roof", PrimitiveType.Cylinder, f, new Vector3(x, 3.35f, 0), new Vector3(2.3f, 0.15f, 2.3f), Palette.CrimsonDark);
                Prim.Part("Spire", PrimitiveType.Cylinder, f, new Vector3(x, 3.9f, 0), new Vector3(1.2f, 0.45f, 1.2f), Palette.CrimsonDark);
                Prim.Part("FlagPole", PrimitiveType.Cylinder, f, new Vector3(x, 4.9f, 0), new Vector3(0.06f, 0.6f, 0.06f), Palette.Wood);
                Prim.Part("Flag", PrimitiveType.Cube, f, new Vector3(x + 0.45f, 5.25f, 0), new Vector3(0.9f, 0.5f, 0.04f), Palette.Crimson);
            }
            Prim.Part("Gate", PrimitiveType.Cube, f, new Vector3(0, 0.75f, -0.62f), new Vector3(2.2f, 1.5f, 0.1f), Palette.Walnut);
            Prim.Part("GateArch", PrimitiveType.Cube, f, new Vector3(0, 1.6f, -0.64f), new Vector3(2.6f, 0.2f, 0.12f), Palette.StoneDark);
        }

        static void Camp(Transform parent, Vector3 pos)
        {
            var c = new GameObject("PlayerCamp").transform;
            c.SetParent(parent, false);
            c.localPosition = pos;
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;
                var tent = new GameObject("Tent").transform;
                tent.SetParent(c, false);
                tent.localPosition = new Vector3(i * 3.2f, 0, Mathf.Abs(i) * 0.6f);
                tent.localRotation = Quaternion.Euler(0, i * 6f, 0);
                Prim.Part("Canvas", PrimitiveType.Cube, tent, new Vector3(0, 0.75f, 0), new Vector3(1.5f, 1.5f, 2.2f), Palette.Canvas, new Vector3(0, 0, 45f));
                Prim.Part("Stripe", PrimitiveType.Cube, tent, new Vector3(0, 0.75f, 0), new Vector3(1.52f, 0.3f, 2.22f), Palette.Azure, new Vector3(0, 0, 45f));
                Prim.Part("Pole", PrimitiveType.Cylinder, tent, new Vector3(0, 1.4f, 1.15f), new Vector3(0.06f, 0.5f, 0.06f), Palette.Wood);
            }
            Prim.Part("Banner", PrimitiveType.Cube, c, new Vector3(0, 2.4f, 0.6f), new Vector3(1f, 0.6f, 0.04f), Palette.Azure);
            Prim.Part("BannerPole", PrimitiveType.Cylinder, c, new Vector3(-0.55f, 1.5f, 0.6f), new Vector3(0.07f, 1.5f, 0.07f), Palette.Wood);
            Prim.Part("Fire", PrimitiveType.Sphere, c, new Vector3(0, 0.15f, 2.2f), new Vector3(0.5f, 0.3f, 0.5f), Palette.StoneDark);
        }

        // ---------------- battlefield ----------------

        public static GameObject BuildBattlefield(Transform parent, float sizeX, float sizeZ)
        {
            var ground = Prim.Part("Ground", PrimitiveType.Cube, parent, new Vector3(0, -0.5f, 0), new Vector3(sizeX, 1f, sizeZ), Palette.Dirt);
            ground.AddComponent<BoxCollider>();
            Prim.Part("Outskirts", PrimitiveType.Cube, parent, new Vector3(0, -0.62f, 0), new Vector3(sizeX + 120f, 1f, sizeZ + 120f), Palette.Pine);

            var rng = new System.Random(11);
            // worn dirt patches so the field has texture without a texture
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(((float)rng.NextDouble() - 0.5f) * sizeX * 0.95f, 0.005f, ((float)rng.NextDouble() - 0.5f) * sizeZ * 0.95f);
                float s = 1.5f + (float)rng.NextDouble() * 4f;
                Prim.Part("Patch", PrimitiveType.Cylinder, parent, p, new Vector3(s, 0.005f, s * 0.7f), Palette.DirtDark, new Vector3(0, (float)rng.NextDouble() * 180f, 0), false);
            }
            // tree lines along the long edges, rocks in the corners
            for (int i = 0; i < 40; i++)
            {
                float x = -sizeX / 2f + i * (sizeX / 39f) + ((float)rng.NextDouble() - 0.5f) * 1.5f;
                Tree(parent, new Vector3(x, 0, sizeZ / 2f + 2f + (float)rng.NextDouble() * 3f), 1.6f + (float)rng.NextDouble() * 0.8f, rng);
                Tree(parent, new Vector3(x, 0, -sizeZ / 2f - 4f - (float)rng.NextDouble() * 3f), 1.4f + (float)rng.NextDouble() * 0.6f, rng);
            }
            for (int i = 0; i < 10; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var p = new Vector3(side * (sizeX / 2f + 2f + (float)rng.NextDouble() * 3f), 0.3f, ((float)rng.NextDouble() - 0.5f) * sizeZ);
                Prim.Part("Boulder", PrimitiveType.Sphere, parent, p, new Vector3(2.4f, 1.6f, 2f), Palette.Stone, new Vector3(0, (float)rng.NextDouble() * 180f, 0));
            }
            // camp flags behind each army
            Prim.Part("FlagA", PrimitiveType.Cube, parent, new Vector3(-sizeX / 2f - 1f, 2.6f, 0), new Vector3(0.05f, 1f, 1.6f), Palette.Azure);
            Prim.Part("PoleA", PrimitiveType.Cylinder, parent, new Vector3(-sizeX / 2f - 1f, 1.6f, 0.85f), new Vector3(0.1f, 1.6f, 0.1f), Palette.Wood);
            Prim.Part("FlagB", PrimitiveType.Cube, parent, new Vector3(sizeX / 2f + 1f, 2.6f, 0), new Vector3(0.05f, 1f, 1.6f), Palette.Crimson);
            Prim.Part("PoleB", PrimitiveType.Cylinder, parent, new Vector3(sizeX / 2f + 1f, 1.6f, 0.85f), new Vector3(0.1f, 1.6f, 0.1f), Palette.Wood);
            return ground;
        }
    }
}
