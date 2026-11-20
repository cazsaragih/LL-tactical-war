using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LLTacticalWar.EditorTools
{
    // Import settings for the Quaternius mounts under Assets/Resources/Quaternius, read from
    // UnitLooks.Mounts so a reimport reproduces them. Each imports as a Generic rig (the game
    // drives it through Playables) at its mount's scale with only the clips the mount names,
    // renamed from the rig's "AnimalArmature|Walk" to "Walk". Its flat-colour materials import
    // only so UnitFactory can read their colours onto URP Lit; the mesh stays readable because
    // UnitFactory paints the saddle cloth's faces at runtime.
    public class QuaterniusImport : AssetPostprocessor
    {
        const string Root = "Assets/Resources/Quaternius/";

        static Mount MountAt(string path) =>
            UnitLooks.Mounts.FirstOrDefault(m => path == Root + m.Model + ".fbx");

        // Bumps whenever a mount's clips or scale change, so Unity reimports it.
        public override uint GetVersion()
        {
            uint h = 2166136261;   // FNV-1a over everything this postprocessor decides
            foreach (var m in UnitLooks.Mounts)
                foreach (var c in m.Model + ":" + string.Join(",", m.Clips) + "|" + m.Scale + "|v1")
                    h = (h ^ c) * 16777619;
            return h;
        }

        void OnPreprocessModel()
        {
            var mount = MountAt(assetPath);
            if (mount == null) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = mount.Scale;
            m.useFileScale = true;
            m.isReadable = true;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            m.materialLocation = ModelImporterMaterialLocation.InPrefab;
            m.importCameras = m.importLights = m.importBlendShapes = m.importVisibility = false;
            m.animationType = ModelImporterAnimationType.Generic;
            m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            m.optimizeGameObjects = false;   // the rider rides a bone
            m.importAnimation = true;
            m.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessAnimation()
        {
            var mount = MountAt(assetPath);
            if (mount == null) return;
            var m = (ModelImporter)assetImporter;
            var clips = m.defaultClipAnimations
                .Select(c => { c.name = c.takeName.Substring(c.takeName.LastIndexOf('|') + 1); return c; })
                .Where(c => mount.Clips.Contains(c.name))
                .GroupBy(c => c.name).Select(g => g.First())   // the file carries each take twice
                .ToArray();
            foreach (var c in clips) c.loopTime = mount.Loops(c.name);
            m.clipAnimations = clips;
        }
    }
}
