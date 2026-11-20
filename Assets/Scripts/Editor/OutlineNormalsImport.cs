using UnityEditor;
using UnityEngine;

namespace LLTacticalWar.EditorTools
{
    // Every model the toon shader outlines (the KayKit packs and the Quaternius mounts) gets its
    // smoothed normals baked into TANGENT on import (see OutlineNormals), after the pack's own
    // postprocessor has set the import up.
    public class OutlineNormalsImport : AssetPostprocessor
    {
        static readonly string[] Roots = { "Assets/Resources/KayKit/", "Assets/Resources/" + UnitLooks.MountDir };

        public override uint GetVersion() => 1;
        public override int GetPostprocessOrder() => 100;

        void OnPostprocessModel(GameObject root)
        {
            if (!System.Array.Exists(Roots, assetPath.StartsWith)) return;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) OutlineNormals.Bake(mf.sharedMesh);
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) OutlineNormals.Bake(smr.sharedMesh);
        }
    }
}
