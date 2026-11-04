using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mahjong.EditorTools
{
    /// <summary>
    /// Screenshots of the screens that are not the running hand: the whole table from above, the
    /// rulebook and the free-play table. The prototype capture (<see cref="PrototypeCapture"/>) shows
    /// how a live hand reads; this one shows the layout, so a table that lost its far row or a panel
    /// that hangs off the frame is visible without opening the editor by hand.
    ///
    /// Unity.exe -projectPath &lt;project&gt; -executeMethod Mahjong.EditorTools.InterfaceCapture.CaptureAndExit
    /// Screenshots go to MAHJONG_SHOT_DIR (default <c>Screenshots</c>).
    /// Exit codes: 0 — all shots saved, 3 — the table did not come up.
    /// </summary>
    public static class InterfaceCapture
    {
        private const string ArmedKey = "Mahjong.InterfaceCapture.Armed";
        private const string DirectoryKey = "Mahjong.InterfaceCapture.Directory";
        private const string StepKey = "Mahjong.InterfaceCapture.Step";
        private const string StepStartKey = "Mahjong.InterfaceCapture.StepStartedAt";
        private const string PhaseKey = "Mahjong.InterfaceCapture.Phase";

        /// <summary>
        /// One shot per step: which screen to open and where the camera goes. The camera is set
        /// explicitly so the shot shows the table the way the check expects to see it, not the way
        /// the game happens to be framed.
        /// </summary>
        private static readonly (string Name, string Open, Vector3 Eye, Vector3 Look)[] Steps =
        {
            ("table-overview", "none", new Vector3(0f, 2.15f, -1.85f), new Vector3(0f, 0f, 0.08f)),
            ("rules-panel", "rules", new Vector3(0f, 1.15f, -1.45f), new Vector3(0f, 0f, 0.08f)),
            ("free-play", "free", new Vector3(0f, 2.15f, -1.85f), new Vector3(0f, 0f, 0.08f))
        };

        /// <summary>Seconds a step is given before its shot: the deal and the panels need a frame or two.</summary>
        private const double SettleSeconds = 2.0;

        /// <summary>
        /// ScreenCapture writes the file at the end of the frame it was asked in, so the screen has
        /// to stay as it is for at least one more frame; closing the panel in the same tick would
        /// photograph an empty table.
        /// </summary>
        private const double SaveSeconds = 0.4;

        public static void CaptureAndExit()
        {
            var directory = Environment.GetEnvironmentVariable("MAHJONG_SHOT_DIR") ?? "Screenshots";
            Directory.CreateDirectory(directory);

            SessionState.SetString(DirectoryKey, directory);
            SessionState.SetInt(StepKey, 0);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetFloat(StepStartKey, 0f);
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
            var game = UnityEngine.Object.FindAnyObjectByType<Unity.MahjongGame>();
            if (game == null) return;

            var step = SessionState.GetInt(StepKey, 0);
            if (step >= Steps.Length)
            {
                Finish(game);
                return;
            }

            // A step runs in three beats: set the stage, wait for the scene to settle, take the shot,
            // wait for the file to be written, then put the screen back the way it was.
            var now = (float)EditorApplication.timeSinceStartup;
            var startedAt = SessionState.GetFloat(StepStartKey, 0f);
            var phase = SessionState.GetInt(PhaseKey, 0);

            if (startedAt <= 0f)
            {
                Open(game, step);
                SessionState.SetFloat(StepStartKey, now);
                SessionState.SetInt(PhaseKey, 0);
                return;
            }

            var waited = now - startedAt;
            if (phase == 0)
            {
                if (waited < SettleSeconds) return;
                Capture(game, step);
                SessionState.SetInt(PhaseKey, 1);
                SessionState.SetFloat(StepStartKey, now);
                return;
            }

            if (waited < SaveSeconds) return;
            Close(game, step);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetInt(StepKey, step + 1);
            SessionState.SetFloat(StepStartKey, 0f);
        }

        private static void Open(Unity.MahjongGame game, int step)
        {
            var (_, open, eye, look) = Steps[step];
            var camera = Camera.main!;
            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(look - eye, Vector3.up);

            switch (open)
            {
                case "rules":
                    game.Rules.SetOpen(true);
                    break;
                case "free":
                    game.OpenFreePlay();
                    break;
            }
        }

        private static void Capture(Unity.MahjongGame game, int step)
        {
            var (name, open, _, _) = Steps[step];
            var path = Path.GetFullPath(Path.Combine(SessionState.GetString(DirectoryKey, "Screenshots"),
                name + ".png"));
            ScreenCapture.CaptureScreenshot(path);
            var detail = open switch
            {
                "rules" => $"rulebook page {game.Rules.Page + 1}/{game.Rules.PageCount}: {game.Rules.CurrentTitle}",
                "free" => $"free play {game.FreePlay.TileViewCount} tiles, seat {game.FreePlay.ActiveSeat}",
                _ => $"hand tiles {game.TableTileCount}"
            };
            Debug.Log($"[interface capture] {name}: {path} | {detail}");
        }

        private static void Close(Unity.MahjongGame game, int step)
        {
            switch (Steps[step].Open)
            {
                case "rules":
                    game.Rules.Close();
                    break;
                case "free":
                    game.CloseFreePlay();
                    break;
            }
        }

        /// <summary>Stops play and exits, so the run is usable from a script.</summary>
        private static void Finish(Unity.MahjongGame game)
        {
            SessionState.SetBool(ArmedKey, false);
            EditorApplication.update -= Tick;

            if (game.Status == null || string.IsNullOrEmpty(game.Status.text))
            {
                Debug.LogError("[interface capture] the table is empty.");
                Quit(3);
                return;
            }

            Debug.Log("[interface capture] done");
            Quit(0);
        }

        private static void Quit(int exitCode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(exitCode);
        }
    }
}
