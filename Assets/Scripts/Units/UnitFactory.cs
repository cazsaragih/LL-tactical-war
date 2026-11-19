using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Builds every figure from palette-tinted primitives. Each class has a silhouette you can
    // read filled black: the Dragoon rides a horse, the Apprentice wears a tall hat, the Brawler
    // is a broad beastman with ears, the Archer is hooded with a bow, the Thief is masked with
    // twin daggers, the Fighter carries sword and shield. Every unit wears a banner on the back.
    public static class UnitFactory
    {
        static readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();
        static Transform templateRoot;

        public static UnitModel Create(BaseClass cls, Team team, Transform parent)
        {
            string key = cls.ClassName + "_" + team;
            if (!templates.TryGetValue(key, out var tpl) || tpl == null)
            {
                if (templateRoot == null)
                {
                    templateRoot = new GameObject("[UnitTemplates]").transform;
                    templateRoot.gameObject.SetActive(false);
                    Object.DontDestroyOnLoad(templateRoot.gameObject);
                }
                tpl = Build(cls, team);
                tpl.transform.SetParent(templateRoot, false);
                templates[key] = tpl;
            }
            var go = Object.Instantiate(tpl, parent, false);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var model = go.GetComponent<UnitModel>();
            model.CacheRenderers();
            model.ResetMotionAnchor();
            return model;
        }

        public static void ClearCache()
        {
            templates.Clear();
            if (templateRoot != null) Object.Destroy(templateRoot.gameObject);
            templateRoot = null;
        }

        static GameObject Build(BaseClass cls, Team team)
        {
            var root = new GameObject("Model_" + cls.ClassName);
            var m = root.AddComponent<UnitModel>();
            Color tc = Palette.Team(team), td = Palette.TeamDark(team);

            float riderLift = 0f;
            if (cls.Mounted)
            {
                BuildHorse(root.transform, tc, td, m);
                riderLift = 0.62f;
            }

            bool robe = cls.ClassName == "Apprentice";
            bool beast = cls.ClassName == "Brawler";
            float width = beast ? 1.25f : 1f;

            // legs (hidden under the robe / horse but still swing)
            if (!cls.Mounted)
            {
                m.legL = Prim.Pivot("LegL", root.transform, new Vector3(-0.11f, 0.55f, 0f));
                Prim.Part("Leg", PrimitiveType.Capsule, m.legL, new Vector3(0, -0.26f, 0), new Vector3(0.16f, 0.26f, 0.16f), td);
                Prim.Part("Boot", PrimitiveType.Sphere, m.legL, new Vector3(0, -0.5f, 0.03f), new Vector3(0.15f, 0.12f, 0.2f), Palette.Walnut);
                m.legR = Prim.Pivot("LegR", root.transform, new Vector3(0.11f, 0.55f, 0f));
                Prim.Part("Leg", PrimitiveType.Capsule, m.legR, new Vector3(0, -0.26f, 0), new Vector3(0.16f, 0.26f, 0.16f), td);
                Prim.Part("Boot", PrimitiveType.Sphere, m.legR, new Vector3(0, -0.5f, 0.03f), new Vector3(0.15f, 0.12f, 0.2f), Palette.Walnut);
            }

            // upper body
            m.body = Prim.Pivot("Body", root.transform, new Vector3(0, 0.55f + riderLift, 0));
            Prim.Part("Torso", PrimitiveType.Capsule, m.body, new Vector3(0, 0.33f, 0), new Vector3(0.46f * width, 0.38f, 0.34f * width), tc);
            Prim.Part("Belt", PrimitiveType.Cylinder, m.body, new Vector3(0, 0.16f, 0), new Vector3(0.47f * width, 0.035f, 0.35f * width), Palette.Walnut);
            if (robe)
                Prim.Part("Robe", PrimitiveType.Cylinder, m.body, new Vector3(0, -0.12f, 0), new Vector3(0.52f, 0.28f, 0.44f), td);
            if (cls.Mounted)
            {
                Prim.Part("RiderLegL", PrimitiveType.Capsule, m.body, new Vector3(-0.24f, -0.02f, 0.06f), new Vector3(0.14f, 0.24f, 0.14f), td, new Vector3(-20f, 0, 12f));
                Prim.Part("RiderLegR", PrimitiveType.Capsule, m.body, new Vector3(0.24f, -0.02f, 0.06f), new Vector3(0.14f, 0.24f, 0.14f), td, new Vector3(-20f, 0, -12f));
            }

            m.head = Prim.Pivot("Head", m.body, new Vector3(0, 0.84f, 0));
            Prim.Part("Face", PrimitiveType.Sphere, m.head, Vector3.zero, Vector3.one * 0.32f, beast ? Palette.Horse : Palette.Skin);
            BuildHeadgear(cls.ClassName, m.head, tc, td);

            // arms
            m.armR = Prim.Pivot("ArmR", m.body, new Vector3(0.29f * width, 0.56f, 0), new Vector3(-18f, 0, 6f));
            Prim.Part("Arm", PrimitiveType.Capsule, m.armR, new Vector3(0, -0.18f, 0), new Vector3(0.12f, 0.2f, 0.12f), tc);
            Prim.Part("Hand", PrimitiveType.Sphere, m.armR, new Vector3(0, -0.38f, 0), Vector3.one * 0.11f, beast ? Palette.Horse : Palette.Skin);
            m.armL = Prim.Pivot("ArmL", m.body, new Vector3(-0.29f * width, 0.56f, 0), new Vector3(-10f, 0, -6f));
            Prim.Part("Arm", PrimitiveType.Capsule, m.armL, new Vector3(0, -0.18f, 0), new Vector3(0.12f, 0.2f, 0.12f), tc);
            Prim.Part("Hand", PrimitiveType.Sphere, m.armL, new Vector3(0, -0.38f, 0), Vector3.one * 0.11f, beast ? Palette.Horse : Palette.Skin);
            if (beast)
            {
                Prim.Part("PadR", PrimitiveType.Sphere, m.body, new Vector3(0.3f, 0.6f, 0), new Vector3(0.26f, 0.18f, 0.26f), td);
                Prim.Part("PadL", PrimitiveType.Sphere, m.body, new Vector3(-0.3f, 0.6f, 0), new Vector3(0.26f, 0.18f, 0.26f), td);
            }

            m.weaponHold = Prim.Pivot("WeaponHold", m.armR, new Vector3(0, -0.4f, 0.02f), new Vector3(90f, 0, 0));
            BuildWeapon(cls.Weapon, m.weaponHold, tc, td);
            BuildOffHand(cls.ClassName, m.armL, m.body, tc, td);

            BuildBanner(m, tc);
            root.transform.localScale = Vector3.one * 1.15f;
            m.maxSpeed = 6f;
            return root;
        }

        static void BuildHorse(Transform root, Color tc, Color td, UnitModel m)
        {
            var horse = Prim.Pivot("Horse", root, Vector3.zero);
            Prim.Part("Barrel", PrimitiveType.Capsule, horse, new Vector3(0, 0.85f, 0), new Vector3(0.42f, 0.55f, 0.45f), Palette.Horse, new Vector3(90f, 0, 0));
            Prim.Part("Caparison", PrimitiveType.Cube, horse, new Vector3(0, 0.82f, -0.05f), new Vector3(0.5f, 0.3f, 0.7f), tc);
            Prim.Part("Trim", PrimitiveType.Cube, horse, new Vector3(0, 0.66f, -0.05f), new Vector3(0.51f, 0.05f, 0.71f), Palette.Gold);
            Prim.Part("Neck", PrimitiveType.Capsule, horse, new Vector3(0, 1.17f, 0.5f), new Vector3(0.2f, 0.3f, 0.22f), Palette.Horse, new Vector3(35f, 0, 0));
            Prim.Part("HorseHead", PrimitiveType.Cube, horse, new Vector3(0, 1.38f, 0.74f), new Vector3(0.17f, 0.2f, 0.42f), Palette.Horse, new Vector3(20f, 0, 0));
            Prim.Part("Mane", PrimitiveType.Cube, horse, new Vector3(0, 1.3f, 0.46f), new Vector3(0.06f, 0.16f, 0.42f), td, new Vector3(35f, 0, 0));
            Prim.Part("EarL", PrimitiveType.Cube, horse, new Vector3(-0.05f, 1.55f, 0.62f), new Vector3(0.04f, 0.1f, 0.04f), Palette.Horse);
            Prim.Part("EarR", PrimitiveType.Cube, horse, new Vector3(0.05f, 1.55f, 0.62f), new Vector3(0.04f, 0.1f, 0.04f), Palette.Horse);
            Prim.Part("Tail", PrimitiveType.Capsule, horse, new Vector3(0, 0.85f, -0.62f), new Vector3(0.08f, 0.25f, 0.08f), td, new Vector3(-40f, 0, 0));
            var legs = new Transform[4];
            Vector3[] at = { new Vector3(-0.16f, 0.62f, 0.36f), new Vector3(0.16f, 0.62f, 0.36f), new Vector3(-0.16f, 0.62f, -0.36f), new Vector3(0.16f, 0.62f, -0.36f) };
            for (int i = 0; i < 4; i++)
            {
                legs[i] = Prim.Pivot("HorseLeg" + i, horse, at[i]);
                Prim.Part("Leg", PrimitiveType.Capsule, legs[i], new Vector3(0, -0.3f, 0), new Vector3(0.11f, 0.3f, 0.11f), Palette.Horse);
                Prim.Part("Hoof", PrimitiveType.Cylinder, legs[i], new Vector3(0, -0.6f, 0), new Vector3(0.12f, 0.04f, 0.12f), Palette.Ink);
            }
            m.horseLegs = legs;
        }

        static void BuildHeadgear(string cls, Transform head, Color tc, Color td)
        {
            switch (cls)
            {
                case "Fighter":
                    Prim.Part("Helmet", PrimitiveType.Sphere, head, new Vector3(0, 0.07f, -0.01f), new Vector3(0.35f, 0.24f, 0.35f), Palette.Steel);
                    Prim.Part("Plume", PrimitiveType.Cube, head, new Vector3(0, 0.2f, -0.03f), new Vector3(0.05f, 0.12f, 0.3f), tc);
                    break;
                case "Dragoon":
                    Prim.Part("Helmet", PrimitiveType.Sphere, head, new Vector3(0, 0.07f, 0f), new Vector3(0.35f, 0.3f, 0.35f), Palette.Steel);
                    Prim.Part("Visor", PrimitiveType.Cube, head, new Vector3(0, 0.02f, 0.15f), new Vector3(0.26f, 0.05f, 0.06f), Palette.Ink);
                    Prim.Part("Plume", PrimitiveType.Cube, head, new Vector3(0, 0.26f, -0.06f), new Vector3(0.06f, 0.18f, 0.34f), tc, new Vector3(-15f, 0, 0));
                    break;
                case "Archer":
                    Prim.Part("Hood", PrimitiveType.Sphere, head, new Vector3(0, 0.04f, -0.04f), new Vector3(0.37f, 0.36f, 0.37f), td);
                    Prim.Part("HoodTip", PrimitiveType.Sphere, head, new Vector3(0, 0.06f, -0.2f), new Vector3(0.14f, 0.14f, 0.18f), td);
                    break;
                case "Apprentice":
                    Prim.Part("Brim", PrimitiveType.Cylinder, head, new Vector3(0, 0.13f, 0), new Vector3(0.52f, 0.02f, 0.52f), td);
                    Prim.Part("Crown", PrimitiveType.Cylinder, head, new Vector3(0, 0.27f, 0), new Vector3(0.24f, 0.13f, 0.24f), td);
                    Prim.Part("Tip", PrimitiveType.Cylinder, head, new Vector3(0, 0.47f, -0.03f), new Vector3(0.13f, 0.1f, 0.13f), td, new Vector3(-12f, 0, 0));
                    Prim.Part("Band", PrimitiveType.Cylinder, head, new Vector3(0, 0.16f, 0), new Vector3(0.25f, 0.025f, 0.25f), Palette.Gold);
                    break;
                case "Brawler":
                    Prim.Part("EarL", PrimitiveType.Cube, head, new Vector3(-0.13f, 0.17f, -0.02f), new Vector3(0.09f, 0.18f, 0.05f), Palette.Horse, new Vector3(0, 0, 16f));
                    Prim.Part("EarR", PrimitiveType.Cube, head, new Vector3(0.13f, 0.17f, -0.02f), new Vector3(0.09f, 0.18f, 0.05f), Palette.Horse, new Vector3(0, 0, -16f));
                    Prim.Part("EarInL", PrimitiveType.Cube, head, new Vector3(-0.13f, 0.16f, 0.005f), new Vector3(0.05f, 0.11f, 0.02f), Palette.Ink, new Vector3(0, 0, 16f));
                    Prim.Part("EarInR", PrimitiveType.Cube, head, new Vector3(0.13f, 0.16f, 0.005f), new Vector3(0.05f, 0.11f, 0.02f), Palette.Ink, new Vector3(0, 0, -16f));
                    Prim.Part("Snout", PrimitiveType.Sphere, head, new Vector3(0, -0.04f, 0.15f), new Vector3(0.16f, 0.12f, 0.14f), Palette.Skin);
                    Prim.Part("Nose", PrimitiveType.Cube, head, new Vector3(0, -0.01f, 0.22f), Vector3.one * 0.045f, Palette.Ink, new Vector3(0, 45f, 0));
                    Prim.Part("Band", PrimitiveType.Cylinder, head, new Vector3(0, 0.08f, 0), new Vector3(0.33f, 0.035f, 0.33f), tc);
                    break;
                case "Thief":
                    Prim.Part("Bandana", PrimitiveType.Sphere, head, new Vector3(0, 0.07f, -0.01f), new Vector3(0.34f, 0.22f, 0.34f), td);
                    Prim.Part("Knot", PrimitiveType.Cube, head, new Vector3(0, 0.06f, -0.2f), new Vector3(0.06f, 0.06f, 0.14f), td, new Vector3(-30f, 0, 0));
                    Prim.Part("Mask", PrimitiveType.Cube, head, new Vector3(0, -0.06f, 0.13f), new Vector3(0.26f, 0.09f, 0.08f), Palette.Ink);
                    break;
            }
            // eyes on every face so the figures have a front
            if (cls != "Thief" && cls != "Dragoon")
            {
                Prim.Part("EyeL", PrimitiveType.Sphere, head, new Vector3(-0.065f, 0.02f, 0.145f), Vector3.one * 0.045f, Palette.Ink, default, false);
                Prim.Part("EyeR", PrimitiveType.Sphere, head, new Vector3(0.065f, 0.02f, 0.145f), Vector3.one * 0.045f, Palette.Ink, default, false);
            }
        }

        static void BuildWeapon(WeaponKind kind, Transform hold, Color tc, Color td)
        {
            switch (kind)
            {
                case WeaponKind.Sword:
                    Prim.Part("Grip", PrimitiveType.Cylinder, hold, new Vector3(0, 0, 0), new Vector3(0.05f, 0.07f, 0.05f), Palette.Walnut);
                    Prim.Part("Guard", PrimitiveType.Cube, hold, new Vector3(0, 0.08f, 0), new Vector3(0.22f, 0.04f, 0.06f), Palette.Gold);
                    Prim.Part("Blade", PrimitiveType.Cube, hold, new Vector3(0, 0.37f, 0), new Vector3(0.07f, 0.54f, 0.025f), Palette.Steel);
                    break;
                case WeaponKind.Dagger:
                    Prim.Part("Guard", PrimitiveType.Cube, hold, new Vector3(0, 0.04f, 0), new Vector3(0.14f, 0.03f, 0.05f), Palette.Gold);
                    Prim.Part("Blade", PrimitiveType.Cube, hold, new Vector3(0, 0.2f, 0), new Vector3(0.06f, 0.28f, 0.02f), Palette.Steel);
                    break;
                case WeaponKind.Spear:
                    Prim.Part("Shaft", PrimitiveType.Cylinder, hold, new Vector3(0, 0.45f, 0), new Vector3(0.045f, 0.95f, 0.045f), Palette.Wood);
                    Prim.Part("Tip", PrimitiveType.Cube, hold, new Vector3(0, 1.45f, 0), new Vector3(0.09f, 0.16f, 0.09f), Palette.Steel, new Vector3(0, 45f, 0));
                    Prim.Part("Pennant", PrimitiveType.Cube, hold, new Vector3(0, 1.2f, 0.09f), new Vector3(0.02f, 0.14f, 0.16f), tc);
                    break;
                case WeaponKind.Staff:
                    hold.localRotation = Quaternion.Euler(25f, 0, 0);
                    Prim.Part("Shaft", PrimitiveType.Cylinder, hold, new Vector3(0, 0.25f, 0), new Vector3(0.045f, 0.62f, 0.045f), Palette.Wood);
                    Prim.Part("Cage", PrimitiveType.Sphere, hold, new Vector3(0, 0.88f, 0), Vector3.one * 0.19f, Palette.Walnut);
                    Prim.GlowPart("Orb", PrimitiveType.Sphere, hold, new Vector3(0, 0.9f, 0), Vector3.one * 0.15f, Palette.Gold);
                    break;
                case WeaponKind.Bow:
                    hold.localRotation = Quaternion.Euler(10f, 0, 0);
                    Prim.Part("Grip", PrimitiveType.Cube, hold, new Vector3(0, 0.05f, 0), new Vector3(0.05f, 0.14f, 0.05f), Palette.Walnut);
                    Prim.Part("LimbUp", PrimitiveType.Cube, hold, new Vector3(0, 0.27f, -0.05f), new Vector3(0.035f, 0.34f, 0.04f), Palette.Wood, new Vector3(-20f, 0, 0));
                    Prim.Part("LimbDn", PrimitiveType.Cube, hold, new Vector3(0, -0.17f, -0.05f), new Vector3(0.035f, 0.34f, 0.04f), Palette.Wood, new Vector3(20f, 0, 0));
                    Prim.Part("String", PrimitiveType.Cube, hold, new Vector3(0, 0.05f, -0.13f), new Vector3(0.01f, 0.72f, 0.01f), Palette.Parchment, default, false);
                    break;
                case WeaponKind.Club:
                    Prim.Part("Handle", PrimitiveType.Cylinder, hold, new Vector3(0, 0.22f, 0), new Vector3(0.08f, 0.26f, 0.08f), Palette.Wood);
                    Prim.Part("Head", PrimitiveType.Sphere, hold, new Vector3(0, 0.58f, 0), new Vector3(0.24f, 0.32f, 0.24f), Palette.Walnut);
                    Prim.Part("Spike", PrimitiveType.Cube, hold, new Vector3(0.1f, 0.62f, 0), Vector3.one * 0.07f, Palette.Steel, new Vector3(0, 0, 45f));
                    Prim.Part("Spike2", PrimitiveType.Cube, hold, new Vector3(-0.1f, 0.55f, 0.04f), Vector3.one * 0.07f, Palette.Steel, new Vector3(0, 0, 45f));
                    break;
            }
        }

        static void BuildOffHand(string cls, Transform armL, Transform body, Color tc, Color td)
        {
            switch (cls)
            {
                case "Fighter":
                    Prim.Part("Shield", PrimitiveType.Cylinder, armL, new Vector3(-0.09f, -0.28f, 0.04f), new Vector3(0.38f, 0.03f, 0.38f), tc, new Vector3(0, 0, 90f));
                    Prim.Part("Rim", PrimitiveType.Cylinder, armL, new Vector3(-0.085f, -0.28f, 0.04f), new Vector3(0.4f, 0.02f, 0.4f), Palette.Steel, new Vector3(0, 0, 90f));
                    Prim.Part("Boss", PrimitiveType.Sphere, armL, new Vector3(-0.12f, -0.28f, 0.04f), Vector3.one * 0.1f, Palette.Gold);
                    break;
                case "Thief":
                    var h = Prim.Pivot("OffHold", armL, new Vector3(0, -0.4f, 0.02f), new Vector3(90f, 0, 0));
                    Prim.Part("Blade", PrimitiveType.Cube, h, new Vector3(0, 0.18f, 0), new Vector3(0.05f, 0.24f, 0.02f), Palette.Steel);
                    Prim.Part("Pouch", PrimitiveType.Sphere, body, new Vector3(0.18f, 0.12f, 0.12f), new Vector3(0.14f, 0.12f, 0.1f), Palette.Walnut);
                    break;
                case "Archer":
                    Prim.Part("Quiver", PrimitiveType.Cylinder, body, new Vector3(0.12f, 0.5f, -0.2f), new Vector3(0.11f, 0.2f, 0.11f), Palette.Walnut, new Vector3(-15f, 0, -20f));
                    Prim.Part("Fletch", PrimitiveType.Cube, body, new Vector3(0.18f, 0.74f, -0.25f), new Vector3(0.1f, 0.06f, 0.06f), Palette.Parchment, new Vector3(-15f, 0, -20f));
                    break;
                case "Apprentice":
                    Prim.Part("Tome", PrimitiveType.Cube, armL, new Vector3(-0.02f, -0.42f, 0.08f), new Vector3(0.16f, 0.05f, 0.2f), td);
                    break;
                case "Dragoon":
                    Prim.Part("Shield", PrimitiveType.Cube, armL, new Vector3(-0.08f, -0.26f, 0.02f), new Vector3(0.03f, 0.34f, 0.26f), tc);
                    Prim.Part("Chevron", PrimitiveType.Cube, armL, new Vector3(-0.1f, -0.24f, 0.02f), new Vector3(0.02f, 0.08f, 0.27f), Palette.Gold);
                    break;
            }
        }

        static void BuildBanner(UnitModel m, Color tc)
        {
            var pole = Prim.Pivot("Banner", m.body, new Vector3(-0.12f, 0.45f, -0.22f));
            Prim.Part("Pole", PrimitiveType.Cylinder, pole, new Vector3(0, 0.6f, 0), new Vector3(0.03f, 0.75f, 0.03f), Palette.Wood);
            Prim.Part("Finial", PrimitiveType.Sphere, pole, new Vector3(0, 1.38f, 0), Vector3.one * 0.08f, Palette.Gold);
            m.flag = Prim.Pivot("Flag", pole, new Vector3(0, 1.12f, 0));
            Prim.Part("Cloth", PrimitiveType.Cube, m.flag, new Vector3(0, 0, -0.21f), new Vector3(0.02f, 0.3f, 0.42f), tc);
            Prim.Part("Stripe", PrimitiveType.Cube, m.flag, new Vector3(0, -0.06f, -0.21f), new Vector3(0.025f, 0.05f, 0.43f), Palette.Gold);
        }
    }
}
