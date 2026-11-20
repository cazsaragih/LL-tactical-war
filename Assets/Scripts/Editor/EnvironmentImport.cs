using UnityEditor;
using UnityEngine;

namespace LLTacticalWar.EditorTools
{
    // Import settings for the KayKit Medieval Hexagon pieces under Assets/Resources/KayKit/Environment,
    // read from SceneryLayout so a reimport reproduces them. Every piece imports at the one scale that
    // fits the pack's hex onto a board tile, as a plain mesh: no cameras, lights, rig or materials
    // (Scenery puts them on the toon material over the shared atlas). Meshes stay readable because
    // Scenery static-batches each scenery root at runtime; the atlas does because Scenery recolours a copy.
    public class EnvironmentImport : AssetPostprocessor
    {
        const string Root = "Assets/Resources/" + SceneryLayout.Dir;

        // Bumps whenever the scale changes, so Unity reimports the pack.
        public override uint GetVersion()
        {
            uint h = 2166136261;   // FNV-1a over everything this postprocessor decides
            foreach (var c in SceneryLayout.ImportScale + "|v2")
                h = (h ^ c) * 16777619;
            return h;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = SceneryLayout.ImportScale;
            m.useFileScale = true;
            m.isReadable = true;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importCameras = m.importLights = m.importBlendShapes = m.importVisibility = false;
            m.animationType = ModelImporterAnimationType.None;
            m.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            m.importAnimation = false;
            m.generateSecondaryUV = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var t = (TextureImporter)assetImporter;
            t.isReadable = true;   // Scenery repaints the grass swatch on a copy
            t.sRGBTexture = true;
            t.mipmapEnabled = true;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
