using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mahjong.Replay;
using Mahjong.Rules;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// Saving and reviewing a played game inside the prototype: the move log of a live hand writes a
    /// file, the file reads back into the same log and replays into a table, and the review screen
    /// steps that table forward and back. All of it is scene work, so it is checked in play mode.
    /// </summary>
    public sealed class ReplayTests
    {
        private static MahjongGame Game()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<MahjongGame>();
            return game != null ? game : new GameObject("MahjongReplayTestHost").AddComponent<MahjongGame>();
        }

        /// <summary>
        /// Plays the table until the log holds enough moves to review. The bots act on their own timer,
        /// so the scale is raised and every human window is run out by the clock instead of waiting.
        /// </summary>
        private static IEnumerator PlaySomeMoves(MahjongGame game, int moves)
        {
            var frames = 0;
            while (frames++ < 12000 && game.MoveLog.MoveCount < moves)
            {
                if (game.HandClock.IsRunning) game.AdvanceClock(60);
                yield return null;
            }

            Assert.GreaterOrEqual(game.MoveLog.MoveCount, moves,
                $"the table played {game.MoveLog.MoveCount} moves, {moves} are needed:\n{game.Status.text}");
        }

        [UnityTest]
        public IEnumerator SavedLogRoundTripsThroughAFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "mahjong-playmode-save.mahjong");
            var previousSave = Environment.GetEnvironmentVariable("MAHJONG_SAVE");
            var previousScale = Time.timeScale;
            Environment.SetEnvironmentVariable("MAHJONG_SAVE", path);
            Time.timeScale = 100f;

            var game = Game();
            yield return null;
            yield return null;

            yield return PlaySomeMoves(game, 6);

            var saved = game.SaveReplay();
            Assert.AreEqual(Path.GetFullPath(path), Path.GetFullPath(saved),
                "the save did not go to the file MAHJONG_SAVE names");
            Assert.IsTrue(File.Exists(path), "the session was not written to disk");

            var hands = game.MoveLog.Hands.Count;
            var moves = game.MoveLog.MoveCount;
            var text = game.MoveLog.SaveText();

            var loaded = SessionLog.Load(path);
            Assert.AreEqual(hands, loaded.Hands.Count, "the save does not hold every hand");
            Assert.AreEqual(moves, loaded.MoveCount, "the save does not hold every move");
            Assert.AreEqual(text, loaded.SaveText(), "the file says something else than the log");

            var replayed = new ReplayCursor(loaded);
            Assert.IsNotNull(replayed.State, "the saved game does not replay into a table");
            Assert.Greater(replayed.MoveCount, 0, "the replayed hand has no moves");

            File.Delete(path);
            Environment.SetEnvironmentVariable("MAHJONG_SAVE", previousSave);
            Time.timeScale = previousScale;
        }

        [UnityTest]
        public IEnumerator ReviewStepsThroughTheSavedGame()
        {
            var path = Path.Combine(Path.GetTempPath(), "mahjong-playmode-review.mahjong");
            var previousReplay = Environment.GetEnvironmentVariable("MAHJONG_REPLAY");
            var previousScale = Time.timeScale;
            Time.timeScale = 100f;

            var game = Game();
            yield return null;
            yield return null;

            yield return PlaySomeMoves(game, 4);

            game.MoveLog.Save(path);
            Environment.SetEnvironmentVariable("MAHJONG_REPLAY", path);

            Assert.IsTrue(game.OpenReplay(), "the review did not open the saved game");
            Assert.IsTrue(game.IsReplayOpen, "the review is not holding the table");
            Assert.AreEqual(0, game.Replay.MoveIndex, "the review must open at the deal");
            var total = game.Replay.MoveCount;
            Assert.Greater(total, 0, "the review opened a hand without moves");
            StringAssert.Contains($"0/{total}", game.Replay.PositionText,
                "the HUD does not say where the review stands");
            Assert.IsNotEmpty(game.Status.text, "the review has no HUD");

            Assert.IsTrue(game.ReplayToEnd(), "the review cannot reach the end of the hand");
            Assert.AreEqual(total, game.Replay.MoveIndex, "the end jump did not land on the last move");
            StringAssert.Contains("P", game.Replay.LastMoveText,
                "the review does not name the move it stepped over: " + game.Replay.LastMoveText);

            Assert.IsTrue(game.ReplayStep(-1), "the review cannot step back");
            Assert.AreEqual(total - 1, game.Replay.MoveIndex, "a step back did not move one move");

            Assert.IsTrue(game.ReplayToStart(), "the review cannot return to the deal");
            Assert.AreEqual(0, game.Replay.MoveIndex, "the start jump did not land on the deal");
            Assert.AreEqual(string.Empty, game.Replay.LastMoveText, "the deal has no last move");

            Assert.IsTrue(game.ReplayStep(1), "the review cannot step forward");
            Assert.AreEqual(1, game.Replay.MoveIndex, "a step forward did not move one move");
            Assert.Greater(game.TableTileCount, 0, "the review shows no tiles");

            game.CloseReplay();
            yield return null;

            Assert.IsFalse(game.IsReplayOpen, "the review did not close");
            Assert.IsNotEmpty(game.Status.text, "the table HUD did not come back after the review");

            File.Delete(path);
            Environment.SetEnvironmentVariable("MAHJONG_REPLAY", previousReplay);
            Time.timeScale = previousScale;
        }

        [UnityTest]
        public IEnumerator SavedSessionResumesWithTheSameScoreSheet()
        {
            var path = Path.Combine(Path.GetTempPath(), "mahjong-playmode-resume.mahjong");
            var previousSave = Environment.GetEnvironmentVariable("MAHJONG_SAVE");
            var previousReplay = Environment.GetEnvironmentVariable("MAHJONG_REPLAY");
            var previousScale = Time.timeScale;
            Time.timeScale = 100f;

            var game = Game();
            yield return null;
            yield return null;

            // Play until a hand is settled: an empty score sheet would prove nothing about a resume.
            var frames = 0;
            while (frames++ < 20000 && game.HandsPlayed < 1)
            {
                if (game.HandClock.IsRunning) game.AdvanceClock(60);
                yield return null;
            }

            Assert.GreaterOrEqual(game.HandsPlayed, 1, $"no hand was finished:\n{game.Status.text}");

            Environment.SetEnvironmentVariable("MAHJONG_SAVE", path);
            Environment.SetEnvironmentVariable("MAHJONG_REPLAY", path);
            Assert.IsNotEmpty(game.SaveReplay(), "the session was not saved");

            var handsPlayed = game.HandsPlayed;
            var scores = game.Scores.ToArray();
            var handsInLog = game.MoveLog.Hands.Count;
            var movesInLog = game.MoveLog.MoveCount;
            var movesInSavedHand = game.MoveLog.Hands[handsInLog - 1].Moves.Count;

            Assert.IsTrue(game.ResumeSavedSession(), "the saved session did not resume:\n" + game.Status.text);
            Assert.AreEqual(handsPlayed, game.HandsPlayed,
                "the resumed session counts another number of hands as played");
            CollectionAssert.AreEqual(scores, game.Scores.ToArray(),
                "the resumed session has a different score sheet");
            // The saved hand was already settled, so the game deals the next one: the log keeps every
            // move it was resumed from and grows by that new deal, never shrinks.
            Assert.GreaterOrEqual(game.MoveLog.Hands.Count, handsInLog,
                "the resumed session threw away the log it was resumed from");
            Assert.AreEqual(movesInSavedHand, game.MoveLog.Hands[handsInLog - 1].Moves.Count,
                "the saved hand lost moves on the way through the resume");
            Assert.GreaterOrEqual(game.MoveLog.MoveCount, movesInLog, "the resumed log lost moves");
            Assert.AreNotEqual(GamePhase.NotStarted, game.Phase, "the resumed session has no hand on the table");
            Assert.Greater(game.TableTileCount, 0, "the resumed table is empty");

            // The saved hand was already settled: the resume must not settle it a second time.
            for (var frame = 0; frame < 5; frame++) yield return null;
            Assert.AreEqual(handsPlayed, game.HandsPlayed, "the resumed session settled a hand twice");

            File.Delete(path);
            Environment.SetEnvironmentVariable("MAHJONG_SAVE", previousSave);
            Environment.SetEnvironmentVariable("MAHJONG_REPLAY", previousReplay);
            Time.timeScale = previousScale;
        }
    }
}
