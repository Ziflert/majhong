using System.Collections;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// У-7 inside the running prototype: a finished hand no longer flips to the next deal after three
    /// seconds of HUD text. The table stops on a screen with the settled hand, a "next hand" button
    /// and a waiting time that can be set in the menu.
    ///
    /// The hand is played by running the human windows out and letting the bots move at a scaled up
    /// time, so nothing here waits for a real minute of table time.
    /// </summary>
    public sealed class ResultScreenTests
    {
        private static MahjongGame Game()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongResultTestHost").AddComponent<MahjongGame>();
            // The welcome screen holds the table at launch; a test of the result screen starts its own game.
            game.SkipWelcome();
            return game;
        }

        /// <summary>
        /// The whole play-mode suite shares one table, so a test leaves it as it found it: no result
        /// screen holding the deal, the opening of the next hand played out, the default waiting time.
        /// Without this the tests that follow wait for a human turn that never comes.
        /// </summary>
        [UnityTearDown]
        public IEnumerator LeaveALiveTable()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null || game.Settings == null) yield break;

            if (game.IsResultOpen && game.Result.CanContinue) game.NextHand();
            game.Settings.Result = ResultAdvanceSettings.Default;

            var previousScale = Time.timeScale;
            Time.timeScale = 100f;
            var guard = 0;
            while (guard++ < 5000 && (game.Opening.IsRunning || game.Phase == GamePhase.HandFinished))
                yield return null;
            Time.timeScale = previousScale;
            yield return null;
        }

        /// <summary>Plays the running hand to its end: bots at speed, human windows run out by the test.</summary>
        private static IEnumerator PlayToHandEnd(MahjongGame game, int frames = 20000)
        {
            var guard = 0;
            while (guard++ < frames && game.Phase != GamePhase.HandFinished)
            {
                if (game.HandClock.IsRunning) game.AdvanceClock(60);
                yield return null;
            }

            Assert.AreEqual(GamePhase.HandFinished, game.Phase,
                "The hand never ended, so its result screen could not be checked:\n" + game.Status.text);
        }

        [UnityTest]
        public IEnumerator FinishedHandStopsOnTheResultScreen()
        {
            var game = Game();
            yield return null;
            yield return null;

            // The screen is held by hand here: the test wants to read it before it goes away.
            game.Settings.Result = ResultAdvance.Manual;
            Time.timeScale = 100f;
            yield return PlayToHandEnd(game);

            var guard = 0;
            while (guard++ < 30 && !game.IsResultOpen) yield return null;
            Assert.IsTrue(game.IsResultOpen, "The finished hand did not open the result screen.");

            Assert.IsTrue(game.ResultPanel.gameObject.activeInHierarchy, "The result panel object is still disabled.");
            Assert.IsFalse(game.Status.gameObject.activeInHierarchy,
                "The HUD is still drawn on top of the result screen.");
            Assert.IsNotEmpty(game.ResultPanel.Title.text, "The result screen has no title.");
            Assert.GreaterOrEqual(game.ResultPanel.LineCount, 2,
                "The result screen does not show what the hand was worth.");
            Assert.IsTrue(game.ResultPanel.LineText(0).Length > 0, "The first line of the result is empty.");
            Assert.IsTrue(game.ResultPanel.ButtonShown, "There is no next-hand button on the result screen.");

            // Every line sits below the one above it and above the button: the column of text is what
            // a player reads, and a line hidden behind the button is a line he never sees.
            for (var line = 1; line < game.ResultPanel.LineCount; line++)
                Assert.Less(game.ResultPanel.LineHeight(line), game.ResultPanel.LineHeight(line - 1),
                    $"Line {line} of the result is not below line {line - 1}.");
            Assert.Greater(game.ResultPanel.LineHeight(game.ResultPanel.LineCount - 1), game.ResultPanel.ButtonTop,
                "The last line of the result is hidden behind the button.");

            // The panel fits the frame, the way the menu and the rulebook do.
            var camera = Camera.main;
            Assert.IsNotNull(camera, "The prototype has no camera to measure the result screen against.");
            foreach (var corner in new[]
                     {
                         new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                         new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                     })
            {
                var point = camera!.WorldToViewportPoint(game.ResultPanel.Backdrop.transform.TransformPoint(corner));
                Assert.Greater(point.z, 0f, "The result screen is behind the camera.");
                Assert.IsTrue(point.x is >= 0f and <= 1f && point.y is >= 0f and <= 1f,
                    $"The result screen hangs off the screen at {point.x:0.00} x {point.y:0.00}.");
            }

            // Button-only means button-only: frames pass, the hand stays on the screen.
            Assert.IsFalse(game.Result.CountsDown, "The button-only setting still counts down.");
            var handsBefore = game.HandsPlayed;
            for (var frame = 0; frame < 30; frame++) yield return null;
            Assert.IsTrue(game.IsResultOpen, "The result screen closed itself without a button.");
            Assert.AreEqual(handsBefore, game.HandsPlayed, "A hand was scored behind the result screen.");

            // The button under the numbers answers a ray, and it deals the next hand.
            var buttonPoint = game.ResultPanel.ButtonCaption.transform.position;
            var ray = new Ray(camera.transform.position, (buttonPoint - camera.transform.position).normalized);
            Assert.IsTrue(game.ResultPanel.TryPick(ray), "A ray through the next-hand button misses it.");
            game.NextHand();
            yield return null;

            Assert.IsFalse(game.IsResultOpen, "The next hand did not close the result screen.");
            Assert.AreNotEqual(GamePhase.HandFinished, game.Phase, "The next hand was not dealt.");
            Assert.AreEqual(handsBefore, game.HandsPlayed, "Dealing the next hand scores a finished one.");
            Assert.IsTrue(game.Status.gameObject.activeInHierarchy,
                "The HUD did not come back after the result screen.");
        }

        [UnityTest]
        public IEnumerator TheResultScreenCountsDownAndDealsOnItsOwn()
        {
            var game = Game();
            yield return null;
            yield return null;

            game.Settings.Result = ResultAdvance.Three;
            Time.timeScale = 100f;
            yield return PlayToHandEnd(game);

            // Play the wait itself at table speed: the point of this test is that it lasts.
            Time.timeScale = 1f;
            var guard = 0;
            while (guard++ < 60 && !game.IsResultOpen) yield return null;

            Assert.IsTrue(game.IsResultOpen, "The finished hand did not open the result screen.");
            Assert.IsTrue(game.Result.CountsDown, "Three seconds did not start a countdown.");
            Assert.LessOrEqual(game.Result.SecondsLeft, 3f,
                "The countdown does not start at the setting it was opened with.");
            Assert.Greater(game.Result.SecondsLeft, 0f, "The countdown was already over when it opened.");

            var handsBefore = game.HandsPlayed;
            var startedAt = Time.time;
            var closed = false;
            guard = 0;

            // The wait is measured in table time, not in frames: a batch-mode run renders thousands of
            // frames a second, so counting frames is counting nothing. Ten seconds of table time with
            // a wide frame guard is enough for a three-second wait on any machine.
            while (guard++ < 200000 && !closed && Time.time - startedAt < 10f)
            {
                closed = !game.IsResultOpen;
                yield return null;
            }

            Debug.Log($"[result test] the result screen {(closed ? "closed" : "stayed")} after"
                      + $" {Time.time - startedAt:0.00} s ({guard} frames), {game.Result.SecondsLeft:0.00} s left,"
                      + $" phase {game.Phase}");
            Assert.IsTrue(closed, "The countdown never ran out.");
            Assert.GreaterOrEqual(Time.time - startedAt, 2.5f, "The next hand was dealt before the wait was over.");
            Assert.AreNotEqual(GamePhase.HandFinished, game.Phase, "The countdown did not deal the next hand.");
            Assert.AreEqual(handsBefore, game.HandsPlayed, "Dealing the next hand scores a finished one.");
        }

        [UnityTest]
        public IEnumerator MenuRowSetsHowLongTheResultScreenWaits()
        {
            var game = Game();
            yield return null;
            yield return null;

            var before = game.Settings.Result;
            game.OpenMenu();
            yield return null;

            game.MenuMove(-game.Menu.Index);
            game.MenuMove(3);
            Assert.AreEqual(MenuItem.Result, game.Menu.Selected, "The fourth row is not the result screen.");
            StringAssert.Contains(Loc.Get("menu.result"), game.MenuPanel.RowText(3),
                "The row does not name the result screen: " + game.MenuPanel.RowText(3));

            while (game.MenuAdjust(1)) { }
            Assert.AreEqual(ResultAdvance.Manual, game.Settings.Result,
                "The row cannot reach the button-only setting.");
            Assert.AreNotEqual(before, game.Settings.Result, "The row did not change the setting at all.");

            game.Settings.Result = before;
            game.CloseMenu();
            yield return null;
        }
    }
}
