using System.Collections.Generic;
using UnityEngine;

namespace LLTacticalWar
{
    // The toon outline pushes its hull out along a smoothed normal, so the hull stays closed where a
    // mesh has hard edges or UV seams. Bake writes, for every vertex, the average of the distinct
    // normals that meet at its position into TANGENT xyz with w = 0, which Toon.shader reads as
    // "smoothed normal here". Skinning and static batching carry tangents along like normals. No
    // shader in the game uses real tangents (there are no normal maps).
    public static class OutlineNormals
    {
        public static void Bake(Mesh mesh)
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            if (normals.Length != verts.Length) return;
            var at = new Dictionary<Vector3Int, List<Vector3>>();
            var keys = new Vector3Int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var k = keys[i] = Vector3Int.RoundToInt(verts[i] * 1e4f);
                if (!at.TryGetValue(k, out var list)) at[k] = list = new List<Vector3>();
                if (!list.Exists(n => Vector3.Dot(n, normals[i]) > 0.999f)) list.Add(normals[i]);
            }
            var tangents = new Vector4[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var sum = Vector3.zero;
                foreach (var n in at[keys[i]]) sum += n;
                var s = sum.sqrMagnitude > 1e-8f ? sum.normalized : normals[i];
                tangents[i] = new Vector4(s.x, s.y, s.z, 0f);
            }
            mesh.tangents = tangents;
        }
    }
}
