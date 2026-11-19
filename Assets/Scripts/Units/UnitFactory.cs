using UnityEngine;

namespace LLTacticalWar
{
    // Builds every figure: the class's KayKit model from UnitLooks, its props on the rig's
    // bones, one team-coloured material from TeamSkin, and a team banner on the back. The pack
    // has no horse or lance, so the Dragoon's mount and lance stay code-built, in flat colours
    // and chunky toy proportions that sit next to the KayKit figures.
    public static class UnitFactory
    {
        public static UnitModel Create(BaseClass cls, Team team, Transform parent)
        {
            var look = UnitLooks.Of(cls);
            var root = new GameObject("Model");
            root.transform.SetParent(parent, false);
            var model = root.AddComponent<UnitModel>();
            var seat = cls.Mounted ? BuildHorse(root.transform, team, model) : root.transform;

            var body = Object.Instantiate(Resources.Load<GameObject>(UnitLooks.CharacterDir + look.Model), seat, false);
            body.name = look.Model;
            // the pack's own props hang off the hand slots; the look's props replace them
            foreach (var slot in new[] { "handslot.l", "handslot.r" })
            {
                var t = Bone(body.transform, slot);
                for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
            }
            foreach (var p in look.Props)
            {
                var prop = Object.Instantiate(Resources.Load<GameObject>(UnitLooks.WeaponDir + p.Fbx), Bone(body.transform, p.Bone), false);
                prop.transform.localPosition = p.Pos * UnitLooks.ImportScale;
                prop.transform.localRotation = Quaternion.Euler(p.Euler);
            }
            var skin = TeamSkin.For(look.Atlas, team);
            foreach (var r in body.GetComponentsInChildren<Renderer>()) r.sharedMaterial = skin;

            // a rider's team blanket already flies its colours; a back banner would poke into its tag
            if (cls.Mounted) BuildLance(Bone(body.transform, "handslot.r"), team);
            else BuildBanner(model, Bone(body.transform, "chest"), team);
            model.Init(look, body.GetComponent<Animator>(), root.transform.InverseTransformPoint(seat.position).y);
            return model;
        }

        public static Transform Bone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // A stubby toy horse in a team blanket, built to the KayKit figures' chibi proportions:
        // round barrel, short thick legs, a big head. Returns the saddle, where the rider's root sits.
        static Transform BuildHorse(Transform root, Team team, UnitModel m)
        {
            Color coat = Palette.Horse, mane = Palette.HorseDark;
            var horse = Prim.Pivot("Horse", root, Vector3.zero);
            Prim.Part("Barrel", PrimitiveType.Capsule, horse, new Vector3(0, 0.68f, -0.02f), new Vector3(0.6f, 0.6f, 0.58f), coat, new Vector3(90f, 0, 0));
            Prim.Part("Blanket", PrimitiveType.Capsule, horse, new Vector3(0, 0.72f, -0.06f), new Vector3(0.64f, 0.34f, 0.62f), Palette.Team(team), new Vector3(90f, 0, 0));
            Prim.Part("Saddle", PrimitiveType.Cube, horse, new Vector3(0, 0.99f, -0.08f), new Vector3(0.4f, 0.09f, 0.42f), Palette.Walnut);
            Prim.Part("Cantle", PrimitiveType.Cube, horse, new Vector3(0, 1.05f, -0.27f), new Vector3(0.32f, 0.1f, 0.06f), Palette.Walnut);
            Prim.Part("Neck", PrimitiveType.Capsule, horse, new Vector3(0, 1.0f, 0.44f), new Vector3(0.34f, 0.3f, 0.34f), coat, new Vector3(28f, 0, 0));
            Prim.Part("Head", PrimitiveType.Capsule, horse, new Vector3(0, 1.26f, 0.66f), new Vector3(0.36f, 0.3f, 0.36f), coat, new Vector3(72f, 0, 0));
            Prim.Part("Muzzle", PrimitiveType.Sphere, horse, new Vector3(0, 1.18f, 0.9f), new Vector3(0.3f, 0.26f, 0.28f), Palette.Skin);
            Prim.Part("EyeL", PrimitiveType.Sphere, horse, new Vector3(-0.16f, 1.34f, 0.7f), Vector3.one * 0.07f, Palette.Ink, default, false);
            Prim.Part("EyeR", PrimitiveType.Sphere, horse, new Vector3(0.16f, 1.34f, 0.7f), Vector3.one * 0.07f, Palette.Ink, default, false);
            Prim.Part("EarL", PrimitiveType.Capsule, horse, new Vector3(-0.1f, 1.5f, 0.56f), new Vector3(0.08f, 0.1f, 0.08f), coat, new Vector3(-10f, 0, 14f));
            Prim.Part("EarR", PrimitiveType.Capsule, horse, new Vector3(0.1f, 1.5f, 0.56f), new Vector3(0.08f, 0.1f, 0.08f), coat, new Vector3(-10f, 0, -14f));
            Prim.Part("Mane", PrimitiveType.Cube, horse, new Vector3(0, 1.18f, 0.4f), new Vector3(0.1f, 0.22f, 0.48f), mane, new Vector3(28f, 0, 0));
            Prim.Part("Forelock", PrimitiveType.Cube, horse, new Vector3(0, 1.44f, 0.7f), new Vector3(0.12f, 0.08f, 0.14f), mane, new Vector3(-20f, 0, 0));
            Prim.Part("Tail", PrimitiveType.Capsule, horse, new Vector3(0, 0.74f, -0.7f), new Vector3(0.14f, 0.24f, 0.14f), mane, new Vector3(-30f, 0, 0));
            var legs = new Transform[4];
            Vector3[] at = { new Vector3(-0.17f, 0.46f, 0.36f), new Vector3(0.17f, 0.46f, 0.36f), new Vector3(-0.17f, 0.46f, -0.38f), new Vector3(0.17f, 0.46f, -0.38f) };
            for (int i = 0; i < 4; i++)
            {
                legs[i] = Prim.Pivot("HorseLeg" + i, horse, at[i]);
                Prim.Part("Leg", PrimitiveType.Capsule, legs[i], new Vector3(0, -0.2f, 0), new Vector3(0.2f, 0.22f, 0.2f), coat);
                Prim.Part("Hoof", PrimitiveType.Cylinder, legs[i], new Vector3(0, -0.4f, 0), new Vector3(0.22f, 0.045f, 0.22f), mane);
            }
            m.horse = horse;
            m.horseLegs = legs;
            return Prim.Pivot("Seat", horse, new Vector3(0, 0.77f, -0.08f));
        }

        // Along the hand slot's +Y, the axis every KayKit weapon is modelled on.
        static void BuildLance(Transform hand, Team team)
        {
            Prim.Part("Shaft", PrimitiveType.Cylinder, hand, new Vector3(0, 0.85f, 0), new Vector3(0.06f, 1.25f, 0.06f), Palette.Wood);
            Prim.Part("Vamplate", PrimitiveType.Cylinder, hand, new Vector3(0, 0.14f, 0), new Vector3(0.2f, 0.06f, 0.2f), Palette.Steel);
            Prim.Part("Tip", PrimitiveType.Sphere, hand, new Vector3(0, 2.22f, 0), new Vector3(0.1f, 0.36f, 0.1f), Palette.Steel);
            Prim.Part("Pennant", PrimitiveType.Cube, hand, new Vector3(0, 1.86f, 0.12f), new Vector3(0.02f, 0.16f, 0.22f), Palette.Team(team));
        }

        // The team banner a foot unit wears on its back, riding the chest bone: a tall pole that
        // clears the big KayKit head, with the cloth hung square to the board so it reads from
        // the front and the back.
        static void BuildBanner(UnitModel m, Transform chest, Team team)
        {
            var pole = Prim.Pivot("Banner", chest, new Vector3(-0.2f, -0.12f, -0.3f));
            Prim.Part("Pole", PrimitiveType.Cylinder, pole, new Vector3(0, 0.8f, 0), new Vector3(0.04f, 0.8f, 0.04f), Palette.Wood);
            Prim.Part("Crossbar", PrimitiveType.Cube, pole, new Vector3(0, 1.56f, 0), new Vector3(0.38f, 0.035f, 0.035f), Palette.Wood);
            Prim.Part("Finial", PrimitiveType.Sphere, pole, new Vector3(0, 1.64f, 0), Vector3.one * 0.09f, Palette.Gold);
            m.flag = Prim.Pivot("Flag", pole, new Vector3(0, 1.56f, 0));
            Prim.Part("Cloth", PrimitiveType.Cube, m.flag, new Vector3(0, -0.23f, 0), new Vector3(0.34f, 0.46f, 0.02f), Palette.Team(team));
            Prim.Part("Stripe", PrimitiveType.Cube, m.flag, new Vector3(0, -0.36f, 0), new Vector3(0.345f, 0.05f, 0.025f), Palette.Gold);
        }
    }
}
