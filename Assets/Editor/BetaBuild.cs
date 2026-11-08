using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace Mahjong.EditorTools
{
    /// <summary>Builds the standalone Windows beta from the code-generated prototype.</summary>
    public static class BetaBuild
    {
        private const string ScenePath = "Assets/Scenes/BetaBootstrap.unity";
        private const string DefaultOutput = "Builds/Mahjong-v0.1-windows/Mahjong.exe";

        /// <summary>
        /// Unity.exe -batchmode -projectPath . -executeMethod Mahjong.EditorTools.BetaBuild.BuildWindows
        /// Optional output: MAHJONG_BUILD_PATH=Builds/Mahjong-v0.1-windows/Mahjong.exe
        /// </summary>
        public static void BuildWindows()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var scenePath = Path.Combine(projectRoot, ScenePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            EnsureBootstrapScene(scenePath);

            var relativeOutput = Environment.GetEnvironmentVariable("MAHJONG_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(relativeOutput)) relativeOutput = DefaultOutput;
            var output = Path.IsPathRooted(relativeOutput)
                ? relativeOutput
                : Path.Combine(projectRoot, relativeOutput.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            PlayerSettings.productName = "Mahjong";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.companyName = "Ziflert";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.ziflert.mahjong");
            EnsureRuntimeShaders();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Windows beta build failed: {report.summary.result}\n{report.summary.totalErrors} errors");

            Debug.Log($"[beta] v0.1 built: {output} ({report.summary.totalSize / 1024 / 1024} MiB)");
        }

        private static void EnsureRuntimeShaders()
        {
            // Shader.Find works in the Editor even when a shader is not referenced by an asset.
            // Player builds strip such shaders, which used to leave v0.1 on a blank purple screen
            // when BuildHudPlate tried to create its first material.
            var required = new[]
            {
                "Unlit/Color",
                "Unlit/Transparent",
                "Sprites/Default"
            };
            var settingsAsset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/GraphicsSettings.asset");
            if (settingsAsset == null)
                throw new BuildFailedException("GraphicsSettings.asset could not be loaded");

            var serialized = new SerializedObject(settingsAsset);
            var property = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (property == null || !property.isArray)
                throw new BuildFailedException("GraphicsSettings.m_AlwaysIncludedShaders is unavailable");

            foreach (var shaderName in required)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogWarning($"[beta] Runtime shader not found in Editor: {shaderName}");
                    continue;
                }

                var found = false;
                for (var i = 0; i < property.arraySize; i++)
                    found |= property.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (!found)
                {
                    property.arraySize++;
                    property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = shader;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureBootstrapScene(string absolutePath)
        {
            if (File.Exists(absolutePath)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
        }
    }
}
