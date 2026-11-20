using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Builds every figure: the class's KayKit model from UnitLooks, its props on the rig's
    // bones, one team-coloured material from TeamSkin, and a team banner on the back. A rider
    // sits on its look's Mount instead, and carries a code-built lance (no pack prop is one).
    public static class UnitFactory
    {
        public static UnitModel Create(BaseClass cls, Team team, Transform parent)
        {
            var look = UnitLooks.Of(cls);
            var root = new GameObject("Model");
            root.transform.SetParent(parent, false);
            var model = root.AddComponent<UnitModel>();

            var body = Object.Instantiate(Resources.Load<GameObject>(UnitLooks.CharacterDir + look.Model), root.transform, false);
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

            // a rider's saddle cloth already flies its colours; a back banner would poke into its tag
            Animator mount = null;
            if (look.Mount != null)
            {
                mount = BuildMount(look.Mount, root.transform, body.transform, team);
                BuildLance(Bone(body.transform, "handslot.r"), team);
            }
            else BuildBanner(model, Bone(body.transform, "chest"), team);
            if (look.Crest is Vector3 crest) BuildCrest(Bone(body.transform, "head"), crest, team);
            model.Init(look, body.GetComponent<Animator>(), mount);
            return model;
        }

        public static Transform Bone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // The mount in its own colours on URP Lit, its saddle faces in the team's, and the rider's
        // root on the seat bone. A fresh instance stands in its bind pose, where Seat and Cloth live.
        static Animator BuildMount(Mount m, Transform root, Transform rider, Team team)
        {
            var mount = Object.Instantiate(Resources.Load<GameObject>(UnitLooks.MountDir + m.Model), root, false);
            mount.name = m.Model;
            foreach (var r in mount.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var own = r.sharedMaterials;
                r.sharedMesh = WithCloth(r, mount.transform, m.Cloth);
                var mats = new Material[own.Length + 1];
                for (int i = 0; i < own.Length; i++) mats[i] = Palette.Lit(own[i].GetColor("_BaseColor"));
                mats[own.Length] = Palette.Lit(Palette.Team(team));
                r.sharedMaterials = mats;
            }
            rider.SetParent(mount.transform, false);
            rider.localPosition = m.Seat;
            rider.SetParent(Bone(mount.transform, m.SeatBone), true);
            return mount.GetComponent<Animator>();
        }

        static readonly Dictionary<Mesh, Mesh> clothed = new Dictionary<Mesh, Mesh>();

        // A copy of the mount's mesh with the faces centred inside the cloth box moved to one
        // extra submesh. Skinned like the rest, the cloth bends with every gait and never clips.
        static Mesh WithCloth(SkinnedMeshRenderer r, Transform mount, Bounds cloth)
        {
            var src = r.sharedMesh;
            if (clothed.TryGetValue(src, out var mesh) && mesh != null) return mesh;
            var toMount = mount.worldToLocalMatrix * r.transform.localToWorldMatrix;
            var verts = src.vertices;
            var cut = new List<int>();
            mesh = Object.Instantiate(src);
            mesh.name = src.name + "_Cloth";
            mesh.subMeshCount = src.subMeshCount + 1;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tris = src.GetTriangles(s);
                var kept = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var centre = (verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f;
                    var into = cloth.Contains(toMount.MultiplyPoint3x4(centre)) ? cut : kept;
                    into.Add(tris[i]); into.Add(tris[i + 1]); into.Add(tris[i + 2]);
                }
                mesh.SetTriangles(kept, s);
            }
            mesh.SetTriangles(cut, src.subMeshCount);
            clothed[src] = mesh;
            return mesh;
        }

        // Along the hand slot's +Y, the axis every KayKit weapon is modelled on.
        static void BuildLance(Transform hand, Team team)
        {
            Prim.Part("Shaft", PrimitiveType.Cylinder, hand, new Vector3(0, 0.85f, 0), new Vector3(0.06f, 1.25f, 0.06f), Palette.Wood);
            Prim.Part("Vamplate", PrimitiveType.Cylinder, hand, new Vector3(0, 0.14f, 0), new Vector3(0.2f, 0.06f, 0.2f), Palette.Steel);
            Prim.Part("Tip", PrimitiveType.Sphere, hand, new Vector3(0, 2.22f, 0), new Vector3(0.1f, 0.36f, 0.1f), Palette.Steel);
            Prim.Part("Pennant", PrimitiveType.Cube, hand, new Vector3(0, 1.86f, 0.12f), new Vector3(0.02f, 0.16f, 0.22f), Palette.Team(team));
        }

        // A plume on the helmet's crown, leaning back, in the team colour: it reads from the front,
        // from the side in a battle and from above on the map.
        static void BuildCrest(Transform head, Vector3 at, Team team)
        {
            Prim.Part("Crest", PrimitiveType.Sphere, head, at + new Vector3(0f, 0.17f, -0.1f), new Vector3(0.2f, 0.5f, 0.26f), Palette.Team(team), new Vector3(-22f, 0f, 0f));
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
