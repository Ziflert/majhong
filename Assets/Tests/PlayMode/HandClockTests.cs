using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// Checks the hand clock of rules 3.5.7.6, 3.6.4 and 3.6.7 inside the running prototype: the
    /// human turn opens a countdown in the HUD, and when the window runs out the table plays the
    /// forced move instead of waiting forever. The test advances the clock itself, so nothing here
    /// sleeps for ten seconds.
    /// </summary>
    public sealed class HandClockTests
    {
        private static MahjongGame Game()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongClockHost").AddComponent<MahjongGame>();
            // The welcome screen holds the table at launch; a test of the clock starts its own game.
            game.SkipWelcome();
            return game;
        }

        [UnityTest]
        public IEnumerator TimedOutTurnDiscardsForTheHuman()
        {
            var game = Game();
            yield return null;
            yield return null;

            // Wait for the human decision of the hand; the bots act on their own timer.
            var frames = 0;
            while (frames++ < 600 && !game.HandClock.IsRunning) yield return null;

            Assert.IsTrue(game.HandClock.IsRunning, "The human turn does not start the hand clock:\n" + game.Status.text);
            var window = game.HandClock.Window;
            Assert.IsTrue(window is ClockWindow.Arrange or ClockWindow.Discard,
                $"A human turn must be on the discard clock, not on {window}.");
            StringAssert.Contains(Text.ClockLabel(window), game.Status.text,
                "The HUD does not show the countdown of the open window:\n" + game.Status.text);

            var discardsBefore = game.DiscardTileCount;
            game.AdvanceClock(60);

            Assert.AreEqual(discardsBefore + 1, game.DiscardTileCount,
                "A turn that ran out of time must put a tile on the table for the human.");
            Assert.IsFalse(game.HandClock.IsExpired, "The clock must open a new window after the forced discard.");

            // The forced discard opens a claim window, so the human is no longer the player to act.
            yield return null;
            Debug.Log($"[clock test] forced discard: discards {discardsBefore} -> {game.DiscardTileCount},"
                      + $" phase={game.Phase}, seat={game.CurrentSeat}, window={game.HandClock.Window}");
            Assert.IsFalse(game.IsYourTurn,
                "The timed-out turn did not end the human turn:\n" + game.Status.text);
        }

        [UnityTest]
        public IEnumerator AWindowWithoutALimitWaitsForTheHuman()
        {
            var game = Game();
            yield return null;
            yield return null;

            // У-1: the player chose a table without time limits, so a quiet window must not play for him.
            // The windows open after the opening of the hand, which the test lets run in scaled time.
            var previousScale = Time.timeScale;
            Time.timeScale = 30f;
            var previousPace = game.Settings.ClockPace;
            var discardsBefore = 0;
            var window = ClockWindow.Idle;
            try
            {
                game.Settings.ClockPace = ClockPace.Unlimited;

                var frames = 0;
                while (frames++ < 5000 && (!game.Opening.IsDone || !game.HandClock.IsRunning)) yield return null;

                Assert.IsTrue(game.HandClock.IsRunning,
                    "The human turn does not open a window:\n" + game.Status.text);
                Assert.IsFalse(game.HandClock.IsLimited, "A table without limits still counts the window down.");
                StringAssert.Contains(Loc.Format("hud.secondsNone", Text.ClockLabel(game.HandClock.Window)),
                    game.Status.text, "The HUD does not show the open window without a countdown:\n" + game.Status.text);
                StringAssert.Contains(Loc.Get("hud.notByRules"), game.Status.text,
                    "The HUD does not say that the windows are a house rule:\n" + game.Status.text);

                discardsBefore = game.DiscardTileCount;
                window = game.HandClock.Window;
                game.AdvanceClock(600);
                yield return null;

                Assert.AreEqual(window, game.HandClock.Window, "An hour of waiting closed a window that has no limit.");
                Assert.AreEqual(discardsBefore, game.DiscardTileCount,
                    "The table discarded for the human on a table without time limits.");
            }
            finally
            {
                // Leave the suite a table that plays by the rules again, whatever happened above.
                game.Settings.ClockPace = previousPace == ClockPace.Unlimited ? ClockPace.Rules : previousPace;
                Time.timeScale = previousScale;
            }

            game.StartNewGame(11);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClaimWindowGivesThreeSecondsAndThenPasses()
        {
            var game = Game();
            var previousScale = Time.timeScale;
            // Batch mode runs over a thousand frames a second, so the frame delta alone would take
            // minutes of wall time to reach a claim window. The scale speeds the bots up and the test
            // plays the human's turns by running their windows out, which is exactly what the clock does.
            Time.timeScale = 100f;
            yield return null;

            // The human claim windows open on their own, so the test plays until the next one.
            var poll = new List<string>();
            var frames = 0;
            while (frames++ < 8000 && game.HandClock.Window != ClockWindow.Claim)
            {
                if (game.HandClock.Window is ClockWindow.Discard or ClockWindow.Arrange)
                    game.AdvanceClock(30);
                if (frames % 500 == 0)
                    poll.Add($"{frames}:{game.Phase}/seat{game.CurrentSeat}/w{game.HandClock.Window}");
                yield return null;
            }

            // One more frame: the bar is rebuilt from the prompt a frame after the window opens.
            yield return null;

            var observed = game.HandClock.Window == ClockWindow.Claim;
            var text = game.Status.text;
            var secondsLeft = game.HandClock.SecondsLeft;
            var promptOpen = game.Prompt.IsOpen;
            var promptCount = game.Prompt.Count;
            Time.timeScale = previousScale;

            var trace = string.Join(" ", poll);
            Assert.IsTrue(observed, "No human claim window appeared within twenty-five seconds of simulated play.\n" + trace);
            StringAssert.Contains(Text.ClockLabel(ClockWindow.Claim), text,
                "The HUD does not show the three-second claim window:\n" + text);
            Assert.LessOrEqual(secondsLeft, 3, "A claim window may not last longer than three seconds.");
            Assert.IsTrue(promptOpen, "The claim window is on the clock but the action bar is closed.");

            game.AdvanceClock(5);
            yield return null;
            Debug.Log($"[clock test] claim window ran out: prompt was {promptCount} options, window={game.HandClock.Window},"
                      + $" prompt now {game.Prompt}");

            Assert.AreNotEqual(ClockWindow.Claim, game.HandClock.Window,
                "Silence in a claim window must close it instead of letting it run.");
            Assert.IsFalse(game.Prompt.IsOpen, "The action bar must close when the claim window runs out.");
        }
    }
}
