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
            var def = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultMaterial : null;
            var litShader = def != null ? def.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) { Debug.LogError("[ProjectSetup] URP Lit shader not found."); return; }

            var lit = new Material(litShader) { name = "BaseLit" };
            lit.SetFloat("_Smoothness", 0.12f);
            lit.SetFloat("_EnvironmentReflections", 0f);
            Save(lit, "BaseLit");

            var sprite = Shader.Find("Sprites/Default");
            if (sprite == null) { Debug.LogError("[ProjectSetup] Sprites/Default shader not found."); return; }
            Save(new Material(sprite) { name = "Overlay" }, "Overlay");
            Save(new Material(sprite) { name = "Particle" }, "Particle");
            AssetDatabase.SaveAssets();
        }

        static void Save(Material m, string name)
        {
            string path = MatDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { existing.shader = m.shader; existing.CopyPropertiesFromMaterial(m); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(m, path);
        }

        public static void ConfigurePipeline()
        {
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
                if (asset == null) continue;
                asset.shadowDistance = 90f;
                asset.supportsHDR = true;
                EditorUtility.SetDirty(asset);
            }
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset d)
            {
                d.shadowDistance = 90f;
                d.supportsHDR = true;
                EditorUtility.SetDirty(d);
            }
            AssetDatabase.SaveAssets();
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
