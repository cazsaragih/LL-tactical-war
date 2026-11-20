using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LLTacticalWar.EditorTools
{
    // Import settings for the KayKit Adventurers pack under Assets/Resources/KayKit. Every
    // reimport runs through here, so the .meta files never need hand edits. Characters import
    // as Generic rigs (the game drives them through Playables) at board scale with only the
    // clips UnitLooks names; weapons as plain meshes at the same scale; atlases stay readable
    // and uncompressed because TeamSkin recolours copies of them at runtime. Materials are
    // built at runtime on URP Lit, so none are imported.
    public class KayKitImport : AssetPostprocessor
    {
        const string Root = "Assets/Resources/KayKit/";
        const string Characters = Root + "Characters/";
        const string Environment = "Assets/Resources/" + SceneryLayout.Dir;   // EnvironmentImport's

        // Bumps whenever the clip list or the scale changes, so Unity reimports the pack.
        public override uint GetVersion()
        {
            uint h = 2166136261;   // FNV-1a over everything this postprocessor decides
            foreach (var c in (string.Join(",", UnitLooks.ClipNames().OrderBy(n => n)) + "|" + UnitLooks.ImportScale + "|v1"))
                h = (h ^ c) * 16777619;
            return h;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root) || assetPath.StartsWith(Environment)) return;
            var m = (ModelImporter)assetImporter;
            bool character = assetPath.StartsWith(Characters);
            m.globalScale = UnitLooks.ImportScale;
            m.useFileScale = true;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importCameras = m.importLights = m.importBlendShapes = m.importVisibility = false;
            m.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            m.avatarSetup = character ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
            m.optimizeGameObjects = false;   // props hang off the handslot and chest bones
            m.importAnimation = character;
            m.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Characters)) return;
            var m = (ModelImporter)assetImporter;
            var wanted = UnitLooks.ClipNames();
            var clips = m.defaultClipAnimations.Where(c => wanted.Contains(c.name)).ToArray();
            foreach (var c in clips) c.loopTime = UnitLooks.Loops(c.name);
            m.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Characters)) return;
            var t = (TextureImporter)assetImporter;
            t.isReadable = true;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.sRGBTexture = true;
            t.mipmapEnabled = true;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
        }
    }
}
