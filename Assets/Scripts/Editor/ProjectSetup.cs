using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LLTacticalWar.EditorTools
{
    // One-time project setup. Safe to run again. Menu: LL Tactical War > Run Setup.
    public static class ProjectSetup
    {
        const string MatDir = "Assets/Resources/Materials";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("LL Tactical War/Run Setup")]
        public static void Run()
        {
            ImportTmpEssentials();
            CreateMaterials();
            ConfigurePipeline();
            CreateMainScene();
            Debug.Log("[ProjectSetup] Done.");
        }

        [MenuItem("LL Tactical War/Open Main Scene")]
        public static void OpenMain() => EditorSceneManager.OpenScene(ScenePath);

        public static void ImportTmpEssentials()
        {
            if (Resources.Load("TMP Settings") != null) { Debug.Log("[ProjectSetup] TMP Essentials already present."); return; }
            var type = Type.GetType("TMPro.TMP_PackageResourceImporter, Unity.TextMeshPro.Editor");
            var method = type?.GetMethod("ImportResources", new[] { typeof(bool), typeof(bool), typeof(bool) });
            if (method == null) { Debug.LogError("[ProjectSetup] TMP_PackageResourceImporter.ImportResources not found."); return; }
            method.Invoke(null, new object[] { true, false, false });
            Debug.Log("[ProjectSetup] TMP Essentials import requested.");
        }

        public static void CreateMaterials()
        {
            Directory.CreateDirectory(MatDir);
            var toon = Shader.Find("LLTacticalWar/Toon");
            var sky = Shader.Find("LLTacticalWar/SkyGradient");
            var sprite = Shader.Find("Sprites/Default");
            var wash = Shader.Find("LLTacticalWar/InkWash");
            if (toon == null || sky == null || sprite == null || wash == null) { Debug.LogError("[ProjectSetup] a shader is missing: Toon, SkyGradient, InkWash or Sprites/Default."); return; }
            Save(new Material(toon) { name = "Toon" }, "Toon");
            Save(new Material(sky) { name = "Sky" }, "Sky");
            Save(new Material(sprite) { name = "Overlay" }, "Overlay");
            Save(new Material(sprite) { name = "Particle" }, "Particle");
            Save(new Material(wash) { name = "InkWash" }, "InkWash");
            AssetDatabase.SaveAssets();
        }

        static void Save(Material m, string name)
        {
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.shader = m.shader; existing.CopyPropertiesFromMaterial(m); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(m, path);
        }

        // Shadows sized to the map camera: it sees the ground from about 26 to 38 m, so the second
        // cascade spans 12.6-39 m and holds the whole view; the battle camera's actors, 9-10 m off,
        // sit in the first. No SSAO: its soft smudges fight the cel tones, and the hard cast shadows
        // already ground every figure and prop. Every renderer draws the toon outlines
        // (Toon.shader's "Outline" pass) in one sweep after the opaques.
        public static void ConfigurePipeline()
        {
            var assets = Enumerable.Range(0, QualitySettings.names.Length).Select(QualitySettings.GetRenderPipelineAssetAt)
                .Append(GraphicsSettings.defaultRenderPipeline).OfType<UniversalRenderPipelineAsset>().Distinct();
            foreach (var asset in assets)
            {
                asset.shadowDistance = 70f;
                if (asset.shadowCascadeCount == 4) asset.cascade4Split = new Vector3(0.18f, 0.56f, 0.8f);   // the PC tier; Mobile keeps one cascade
                asset.supportsHDR = true;
                EditorUtility.SetDirty(asset);
                var renderers = new SerializedObject(asset).FindProperty("m_RendererDataList");
                for (int i = 0; i < renderers.arraySize; i++)
                {
                    if (!(renderers.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData data)) continue;
                    AddOutlines(data);
                    foreach (var feature in data.rendererFeatures.Where(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion").ToList())
                        RemoveFeature(data, feature);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static void RemoveFeature(ScriptableRendererData data, ScriptableRendererFeature feature)
        {
            int i = data.rendererFeatures.IndexOf(feature);
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            features.GetArrayElementAtIndex(i).objectReferenceValue = null;
            features.DeleteArrayElementAtIndex(i);
            so.FindProperty("m_RendererFeatureMap").DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.RemoveObjectFromAsset(feature);
            UnityEngine.Object.DestroyImmediate(feature, true);
            EditorUtility.SetDirty(data);
        }

        static void AddOutlines(ScriptableRendererData data)
        {
            if (data.rendererFeatures.Any(f => f is RenderObjects && f.name == "ToonOutlines")) return;
            var outlines = ScriptableObject.CreateInstance<RenderObjects>();
            outlines.name = "ToonOutlines";
            outlines.settings.passTag = "ToonOutlines";
            outlines.settings.Event = RenderPassEvent.AfterRenderingOpaques;
            outlines.settings.filterSettings = new RenderObjects.FilterSettings { RenderQueueType = RenderQueueType.Opaque, LayerMask = ~0, PassNames = new[] { "Outline" } };
            AssetDatabase.AddObjectToAsset(outlines, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(outlines, out _, out long localId);
            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = outlines;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }

        public static void CreateMainScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameManager").AddComponent<GameManager>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
