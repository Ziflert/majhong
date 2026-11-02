using System;
using System.IO;
using Mahjong.Controls;
using Mahjong.Rules;
using UnityEditor;
using UnityEngine;

namespace Mahjong.EditorTools
{
    /// <summary>
    /// Screenshot tool for the action bar: enters Play mode, freezes the generated table, puts a
    /// sample prompt on the bar and saves a Game view shot. Needed because caption sizes and button
    /// positions can only be judged on a real render, and a real claim window is rare in a short run.
    ///
    /// Unity.exe -projectPath &lt;project&gt; -executeMethod Mahjong.EditorTools.ActionBarPreview.CaptureAndExit
    /// Screenshot directory comes from the MAHJONG_SHOT_DIR environment variable.
    /// </summary>
    public static class ActionBarPreview
    {
        private const string ArmedKey = "Mahjong.BarPreview.Armed";
        private const string DirectoryKey = "Mahjong.BarPreview.Directory";
        private const string StartKey = "Mahjong.BarPreview.StartedAt";
        private const double ShotAt = 3.0;
        private const double ShutdownAt = 6.0;

        public static void CaptureAndExit()
        {
            var directory = Environment.GetEnvironmentVariable("MAHJONG_SHOT_DIR") ?? "Screenshots";
            Directory.CreateDirectory(directory);

            SessionState.SetString(DirectoryKey, directory);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(ArmedKey, true);

            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void ArmAfterDomainReload()
        {
            if (SessionState.GetBool(ArmedKey, false)) EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            var elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f);
            if (elapsed < 1.0) return;

            var game = UnityEngine.Object.FindAnyObjectByType<Unity.MahjongGame>();
            if (game == null) return;

            if (!SessionState.GetBool(ArmedKey + ".Shown", false))
            {
                // stop the game loop so the sample prompt is not replaced by the real one
                game.enabled = false;
                game.Bar.Show(SamplePrompt());
                game.Bar.SetHint("Open kong of 5c - pick a button with the mouse or press 1-5");
                SessionState.SetBool(ArmedKey + ".Shown", true);
                return;
            }

            if (elapsed >= ShotAt && !SessionState.GetBool(ArmedKey + ".Shot", false))
            {
                var path = Path.GetFullPath(Path.Combine(SessionState.GetString(DirectoryKey, "Screenshots"),
                    "action-bar.png"));
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log("[bar-preview] saved " + path);
                SessionState.SetBool(ArmedKey + ".Shot", true);
            }

            if (elapsed < ShutdownAt) return;

            var bar = game.Bar;
            Debug.Log($"[bar-preview] buttons={bar.Buttons.Count} hint=\"{bar.Hint.text}\""
                      + $" captions=[{string.Join("; ", System.Linq.Enumerable.Select(bar.Buttons, b => b.Caption.text))}]");
            SessionState.SetBool(ArmedKey, false);
            EditorApplication.update -= Tick;

            if (!bar.IsOpen || bar.Buttons.Count == 0)
            {
                Debug.LogError("[bar-preview] the action bar did not appear.");
                Quit(3);
                return;
            }

            Quit(0);
        }

        /// <summary>Every kind of decision at once, so the worst case layout is on the picture.</summary>
        private static ActionPrompt SamplePrompt()
        {
            var discard = new Tile(new TileType(TileSuit.Characters, 5), 900);
            Tile More(int rank, int id) => new(new TileType(TileSuit.Characters, rank), id);

            return ActionPrompt.FromLegalActions(new[]
            {
                new LegalAction(ActionKind.Pass, 0, null, Array.Empty<Tile>(), "Pass"),
                new LegalAction(ActionKind.Chow, 0, discard, new[] { More(3, 901), More(4, 902) }, "Chow 5c with 3c, 4c"),
                new LegalAction(ActionKind.Pung, 0, discard, new[] { More(5, 903), More(5, 904) }, "Pung of 5c"),
                new LegalAction(ActionKind.OpenKong, 0, discard, new[] { More(5, 905), More(5, 906), More(5, 907) }, "Open kong of 5c"),
                new LegalAction(ActionKind.Win, 0, discard, Array.Empty<Tile>(), "Claim Hu on 5c for 24 points")
            });
        }

        private static void Quit(int exitCode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(exitCode);
        }
    }
}
