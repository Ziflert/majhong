using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;

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
