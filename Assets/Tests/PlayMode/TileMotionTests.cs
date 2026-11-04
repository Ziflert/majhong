using System.Collections;
using System.Linq;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The table animates by itself: every tile keeps its view object and glides towards the place the
    /// engine gave it. That is what makes a discard travel from the hand to the pile instead of
    /// appearing there as a new cube, so the checks are about identity and arrival, not about pixels.
    /// </summary>
    public sealed class TileMotionTests
    {
        private static MahjongGame Game()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongMotionHost").AddComponent<MahjongGame>();
            // The welcome screen holds the table at launch; a test of tile motion starts its own game.
            game.SkipWelcome();
            return game;
        }

        private static TileObject[] Tiles() => Object.FindObjectsByType<TileObject>(FindObjectsSortMode.None);

        /// <summary>Runs frames until every tile has arrived; the board is animated in scaled time.</summary>
        private static IEnumerator Settle()
        {
            var previousScale = Time.timeScale;
            Time.timeScale = 30f;
            var frames = 0;
            while (frames++ < 600 && Tiles().Any(tile => !tile.IsSettled)) yield return null;
            Time.timeScale = previousScale;
        }

        [UnityTest]
        public IEnumerator DiscardTravelsFromTheHandToThePile()
        {
            var game = Game();
            yield return null;
            yield return Settle();

            // Speed the bots up: batch mode runs over a thousand frames a second, so waiting for the
            // human turn in real time would take minutes. The wait is for the human's *discard* turn,
            // not for any running clock: a claim window also opens a countdown, and passing it moves
            // no tile at all.
            // Batch mode runs tens of thousands of frames a second, so "wait" would be a guess. What
            // the wait does instead is pass every claim window for the human on his behalf (silence in
            // that window is a pass anyway, see ClockExpiry), which is what the player would do when
            // he has no claim to make.
            Time.timeScale = 30f;
            var frames = 0;
            while (frames++ < 40000 && !game.IsYourTurn)
            {
                if (game.HandClock.IsRunning) game.AdvanceClock(60);
                yield return null;
            }
            Time.timeScale = 1f;
            Assert.IsTrue(game.IsYourTurn, "The human turn never came:\n" + game.Status.text);

            yield return Settle();
            var before = Tiles();
            Assert.IsTrue(before.All(tile => tile.IsSettled), "The table did not settle before the discard.");

            game.AdvanceClock(60);

            var after = Tiles();
            Assert.AreEqual(before.Length, after.Length,
                "A discard must be the same tile that was in the hand, not a new object: the count changed.");

            var moving = after.Where(tile => !tile.IsSettled).ToList();
            Assert.IsNotEmpty(moving, "The discarded tile jumped to the pile instead of sliding there.");
            Assert.GreaterOrEqual(moving.Count, 1, "at least the discarded tile has to be on its way");

            yield return Settle();

            var stragglers = Tiles().Where(tile => !tile.IsSettled).Select(tile => tile.name).ToList();
            Assert.IsEmpty(stragglers, "Tiles never reached their places: " + string.Join(", ", stragglers));
        }
    }
}
