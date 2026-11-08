using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mahjong.EditorTools
{
    /// <summary>
    /// Headful smoke run of the 3D prototype: enters Play mode, lets the table live for a few
    /// seconds, saves Game view screenshots and exits. Logs an error code when the scene does not
    /// come up, so it works as a check and not only as a screenshot tool.
    ///
    /// Unity.exe -projectPath &lt;project&gt; -executeMethod Mahjong.EditorTools.PrototypeCapture.CaptureAndExit
    /// Screenshot directory comes from the MAHJONG_SHOT_DIR environment variable.
    ///
    /// Note: entering Play mode reloads the app domain and wipes static state and
    /// EditorApplication.update subscriptions, so the run is re-armed from InitializeOnLoadMethod
    /// and all progress is kept in SessionState.
    /// </summary>
    public static class PrototypeCapture
    {
        private const string ArmedKey = "Mahjong.Capture.Armed";
        private const string DirectoryKey = "Mahjong.Capture.Directory";
        private const string StartKey = "Mahjong.Capture.StartedAt";
        private const string ShotKey = "Mahjong.Capture.NextShot";

        private static readonly string[] Shots = { "play-3s", "play-12s" };
        private static readonly double[] ShotAt = { 3.0, 12.0 };
        private const double ShutdownAt = 15.0;

        public static void CaptureAndExit()
        {
            var directory = Environment.GetEnvironmentVariable("MAHJONG_SHOT_DIR")
                ?? Path.Combine("Screenshots", "current");
            Directory.CreateDirectory(directory);

            // The dice and the break are captured by InterfaceCapture, which owns the opening shot;
            // this run wants the assembled hand, so the show is skipped unless the caller says otherwise.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MAHJONG_OPENING_SPEED")))
                Environment.SetEnvironmentVariable("MAHJONG_OPENING_SPEED", "0");

            // This run photographs a live hand: the welcome screen (У-2) is skipped, the way a player
            // skips it by picking "new game".
            Environment.SetEnvironmentVariable("MAHJONG_TITLE", "0");

            SessionState.SetString(DirectoryKey, directory);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetInt(ShotKey, 0);
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

            var shot = SessionState.GetInt(ShotKey, 0);
            if (shot < Shots.Length)
            {
                if (elapsed < ShotAt[shot]) return;

                var path = Path.GetFullPath(Path.Combine(SessionState.GetString(DirectoryKey,
                    Path.Combine("Screenshots", "current")), Shots[shot] + ".png"));
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log("[capture] saved " + path);
                SessionState.SetInt(ShotKey, shot + 1);
                return;
            }

            if (elapsed < ShutdownAt) return;

            var status = game.Status;
            var tiles = UnityEngine.Object.FindObjectsByType<Unity.TileObject>(FindObjectsSortMode.None);
            Debug.Log($"[capture] tiles={tiles.Length} faceUp={tiles.Count(tile => tile.IsFaceUp)}"
                      + $" status={(status == null ? "<none>" : status.text.Replace("\n", " | "))}");

            SessionState.SetBool(ArmedKey, false);
            EditorApplication.update -= Tick;

            if (tiles.Length == 0 || status == null || string.IsNullOrEmpty(status.text))
            {
                Debug.LogError("[capture] table is empty: no tiles or no status text.");
                Quit(3);
                return;
            }

            Quit(0);
        }

        private static void Quit(int exitCode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(exitCode);
        }
    }
}
