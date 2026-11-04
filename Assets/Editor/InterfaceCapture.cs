using System;
using System.IO;
using System.Linq;
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
        private const string StepsKey = "Mahjong.InterfaceCapture.Steps";
        private const string SettledKey = "Mahjong.InterfaceCapture.Settled";
        private const string StepKey = "Mahjong.InterfaceCapture.Step";
        private const string StepStartKey = "Mahjong.InterfaceCapture.StepStartedAt";
        private const string PhaseKey = "Mahjong.InterfaceCapture.Phase";

        /// <summary>
        /// One shot per step: which screen to open and where the camera goes. The camera is set
        /// explicitly so the shot shows the table the way the check expects to see it, not the way
        /// the game happens to be framed.
        /// </summary>
        private static readonly (string Name, string Open, Vector3 Eye, Vector3 Look, double Settle)[] Steps =
        {
            // У-2: the screen a player meets on launch, before any hand is dealt.
            ("welcome", "welcome", new Vector3(0f, 2.70f, -2.60f), new Vector3(0f, 0f, 0.18f), 1.2),
            // The opening runs at the table's own pace, so this shot is taken while it is running:
            // the dice in the air, the wall still whole, the break not made yet (3.5.7.5).
            ("opening-dice", "opening", new Vector3(0f, 2.20f, -1.90f), new Vector3(0f, 0f, 0.10f), 1.1),
            ("table-overview", "none", new Vector3(0f, 2.70f, -2.60f), new Vector3(0f, 0f, 0.18f), SettleSeconds),
            ("rules-panel", "rules", new Vector3(0f, 1.15f, -1.45f), new Vector3(0f, 0f, 0.08f), SettleSeconds),
            ("free-play", "free", new Vector3(0f, 2.70f, -2.60f), new Vector3(0f, 0f, 0.18f), SettleSeconds),
            // No player sits at a capture run, so this step runs the human windows out and the shot
            // shows the table mid-hand; the review after it then has real moves to step through.
            ("table-midgame", "burn", new Vector3(0f, 2.70f, -2.60f), new Vector3(0f, 0f, 0.18f), SettleSeconds),
            // У-3: the hand is played on until melds are lying open, so the meld row and the discard
            // blocks can be read on the frame instead of being argued about from the code.
            ("melds", "melds", new Vector3(0f, 2.70f, -2.60f), new Vector3(0f, 0f, 0.18f), SettleSeconds),
            ("menu-panel", "menu", new Vector3(0f, 1.15f, -1.45f), new Vector3(0f, 0f, 0.08f), SettleSeconds),
            // У-7: the hand is played to its end by the bots and the shot is taken on the result screen.
            // The screen is held there by MAHJONG_RESULT_DELAY=0, set by CaptureAndExit below.
            ("result-screen", "result", new Vector3(0f, 1.15f, -1.45f), new Vector3(0f, 0f, 0.08f), SettleSeconds),
            ("replay-review", "replay", new Vector3(0f, 2.70f, -2.60f), new Vector3(0f, 0f, 0.18f), SettleSeconds)
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

            // A finished hand opens the result screen and deals again after five seconds; the shot of
            // that screen needs it to stay, so the wait is set to "button only" unless the caller says
            // otherwise. The same variable a player would use (docs/unity-layer.md).
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MAHJONG_RESULT_DELAY")))
                Environment.SetEnvironmentVariable("MAHJONG_RESULT_DELAY", "0");

            // The run photographs the screens of a live table, so the game deals straight away; the
            // welcome screen has its own step, which opens it by hand (У-2).
            Environment.SetEnvironmentVariable("MAHJONG_TITLE", "0");

            // A single screen can be re-shot without replaying a whole hand: MAHJONG_CAPTURE_STEPS
            // holds the names of the steps to run, in the order they are listed in Steps.
            var only = Environment.GetEnvironmentVariable("MAHJONG_CAPTURE_STEPS");
            var chosen = string.IsNullOrWhiteSpace(only)
                ? Steps.Select(step => step.Name).ToArray()
                : Steps.Where(step => only.Split(',')
                        .Any(name => string.Equals(name.Trim(), step.Name, StringComparison.OrdinalIgnoreCase)))
                    .Select(step => step.Name).ToArray();
            if (chosen.Length == 0)
            {
                Debug.LogError($"[interface capture] no step matches '{only}'.");
                EditorApplication.Exit(2);
                return;
            }

            SessionState.SetString(DirectoryKey, directory);
            SessionState.SetString(StepsKey, string.Join(",", chosen));
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

        /// <summary>The steps this run shoots, in the order of <see cref="Steps"/>.</summary>
        private static (string Name, string Open, Vector3 Eye, Vector3 Look, double Settle)[] Run()
        {
            var chosen = SessionState.GetString(StepsKey, string.Empty);
            if (string.IsNullOrEmpty(chosen)) return Steps;
            var names = chosen.Split(',');
            return Steps.Where(step => names.Contains(step.Name)).ToArray();
        }

        private static void Tick()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<Unity.MahjongGame>();
            if (game == null) return;

            var steps = Run();
            var step = SessionState.GetInt(StepKey, 0);
            if (step >= steps.Length)
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
                Open(game, steps[step]);
                SessionState.SetFloat(StepStartKey, now);
                SessionState.SetInt(PhaseKey, 0);
                return;
            }

            var waited = now - startedAt;
            if (phase == 0)
            {
                // The result screen only exists once a hand is over, and a capture run has no player:
                // the human windows are run out until the bots finish the hand and the panel is up.
                if (steps[step].Open == "result" && !game.IsResultOpen)
                {
                    if (game.HandClock.IsRunning) game.AdvanceClock(60);
                    return;
                }

                // Two open melds make the meld row worth photographing; the human windows are run
                // out the way the table does it when a turn times out. The tiles fly to their places,
                // so the settle is restarted once the melds are there and the frame is not taken
                // in the middle of the flight.
                if (steps[step].Open == "melds" && game.OpenMeldCount < 2)
                {
                    if (game.HandClock.IsRunning) game.AdvanceClock(60);
                    return;
                }

                if (steps[step].Open == "melds" && !SessionState.GetBool(SettledKey, false))
                {
                    SessionState.SetBool(SettledKey, true);
                    SessionState.SetFloat(StepStartKey, now);
                    return;
                }

                if (waited < steps[step].Settle)
                {
                    // A capture run has no player: on the preparation step the human windows are run
                    // out, which is exactly what the hand clock does when a turn times out.
                    if (steps[step].Open == "burn" && game.HandClock.IsRunning) game.AdvanceClock(60);
                    return;
                }
                Capture(game, steps[step]);
                SessionState.SetInt(PhaseKey, 1);
                SessionState.SetFloat(StepStartKey, now);
                return;
            }

            if (waited < SaveSeconds) return;
            Close(game, steps[step]);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetInt(StepKey, step + 1);
            SessionState.SetFloat(StepStartKey, 0f);
            SessionState.SetBool(SettledKey, false);
        }

        private static void Open(Unity.MahjongGame game, (string Name, string Open, Vector3 Eye, Vector3 Look, double Settle) step)
        {
            var (_, open, eye, look, _) = step;
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
                case "replay":
                    // The review opens the newest save, or the game in progress when nothing is saved.
                    game.OpenReplay();
                    game.ReplayToEnd();
                    break;
                case "menu":
                    game.OpenMenu();
                    break;
                case "welcome":
                    game.OpenTitle();
                    break;
                case "burn":
                case "melds":
                case "result":
                    break;
            }
        }

        private static void Capture(Unity.MahjongGame game, (string Name, string Open, Vector3 Eye, Vector3 Look, double Settle) step)
        {
            var (name, open, _, _, _) = step;
            var path = Path.GetFullPath(Path.Combine(SessionState.GetString(DirectoryKey, "Screenshots"),
                name + ".png"));
            ScreenCapture.CaptureScreenshot(path);
            var detail = open switch
            {
                "welcome" => $"welcome screen, {game.Title.RowCount} rows, on '{game.Title.RowAt(game.Title.Index)}',"
                             + $" panel {(game.TitlePanel.IsOpen ? "open" : "closed")}",
                "rules" => $"rulebook page {game.Rules.Page + 1}/{game.Rules.PageCount}: {game.Rules.CurrentTitle}",
                "free" => $"free play {game.FreePlay.TileViewCount} tiles, seat {game.FreePlay.ActiveSeat}",
                "replay" => $"review {game.Replay.PositionText}, last {game.Replay.LastMoveText}",
                "menu" => $"menu {game.MenuPanel.RowCount} rows, open {(game.MenuPanel.IsOpen ? "yes" : "no")},"
                          + $" panel {(game.MenuPanel.gameObject.activeInHierarchy ? "active" : "hidden")},"
                          + $" on {game.Menu.RowAt(game.Menu.Index)}",
                "opening" => $"opening {game.Opening.Step}, dice {game.Dice.ShownFirst}+{game.Dice.ShownSecond},"
                             + $" {game.TableTileCount} tiled views, wall {game.WallRemaining} left",
                "burn" => $"{game.MoveLog.MoveCount} moves played, phase {game.Phase}",
                "melds" => $"{game.OpenMeldCount} melds open ({game.HumanMeldCount} of the human),"
                           + $" {game.DiscardTileCount} discards, phase {game.Phase}",
                "result" => $"result screen '{game.ResultPanel.Title.text}',"
                            + $" {game.ResultPanel.LineCount} lines, {game.Result.Countdown}",
                _ => $"hand tiles {game.TableTileCount}"
            };
            Debug.Log($"[interface capture] {name}: {path} | {detail}");
        }

        private static void Close(Unity.MahjongGame game, (string Name, string Open, Vector3 Eye, Vector3 Look, double Settle) step)
        {
            switch (step.Open)
            {
                case "rules":
                    game.Rules.Close();
                    break;
                case "free":
                    game.CloseFreePlay();
                    break;
                case "replay":
                    game.CloseReplay();
                    break;
                case "menu":
                    game.CloseMenu();
                    break;
                case "welcome":
                    // The next step wants a live table: the screen hands over the way a pick does.
                    game.SkipWelcome();
                    break;
                case "result":
                    // The screen is held by MAHJONG_RESULT_DELAY=0; the next step wants a live table.
                    game.NextHand();
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
