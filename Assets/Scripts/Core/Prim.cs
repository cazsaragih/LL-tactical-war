using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // Builds primitive-shaped parts without the colliders GameObject.CreatePrimitive attaches.
    public static class Prim
    {
        static readonly Dictionary<PrimitiveType, Mesh> meshes = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh MeshOf(PrimitiveType type)
        {
            if (meshes.TryGetValue(type, out var m) && m != null) return m;
            var tmp = GameObject.CreatePrimitive(type);
            m = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            m.name = type.ToString();
            Object.DestroyImmediate(tmp);
            OutlineNormals.Bake(m);   // a closed outline hull round the cube's hard edges
            meshes[type] = m;
            return m;
        }

        public static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, Vector3 euler = default, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = MeshOf(type);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Palette.Lit(color);
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject GlowPart(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, Vector3 euler = default)
        {
            var go = Part(name, type, parent, localPos, localScale, color, euler, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = Palette.Glow(color);
            return go;
        }

        public static Transform Pivot(string name, Transform parent, Vector3 localPos, Vector3 euler = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(euler);
            return t;
        }
    }
}
