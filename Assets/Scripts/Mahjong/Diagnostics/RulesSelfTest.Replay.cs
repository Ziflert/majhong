using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Net;
using Mahjong.Replay;
using Mahjong.Rules;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the save format and the replay: a played session writes a text log, that log reads
    /// back unchanged and rebuilds the same tables from the seeds alone, a broken save names the line
    /// that broke it, and the review cursor steps forward and back to the same states.
    ///
    /// Kept in its own file and wired into <see cref="RulesSelfTest.RunAll"/>, so the same checks run
    /// headless and inside Unity.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void ReplayGroups(SelfTestReport report)
        {
            Run(report, "replay: a saved session round trips through text", () => SaveRoundTrip(report));
            Run(report, "replay: the log rebuilds every hand to the same state", () => LogRebuildsHands(report));
            Run(report, "replay: a corrupt save names the line it died on", () => CorruptSaveRejected(report));
            Run(report, "replay: a forged move in the log refuses to replay", () => ForgedMoveRejected(report));
            Run(report, "replay: the cursor steps forward and back to the same states", () => CursorStepping(report));
            Run(report, "replay: a saved session resumes with the same scores and clock", () => SessionResumes(report));
            Run(report, "replay: a hand cut mid-play resumes where it stopped", () => SessionResumesMidHand(report));
            Run(report, "replay: a spent session clock comes back spent", () => SpentClockStopsTheSession(report));
            Run(report, "replay: a save dealt out of order refuses to resume", () => WrongDealRefused(report));
        }

        // -------------------------------------------------------------- saving

        private static void SaveRoundTrip(SelfTestReport report)
        {
            var log = new SessionLog();
            for (var hand = 0; hand < 3; hand++)
            {
                var table = new InProcessTable(hand % 4, 70000 + hand * 13, Wind.East,
                    BotLevel.Strategic, 0, 1, 2, 3);
                table.PlayOut();
                log.BeginHand(table.Server.Dealer, table.Server.Seed, table.Server.RoundWind);
                foreach (var (seat, wire) in table.Server.Log)
                    log.RecordWire(seat, wire);
            }

            report.Check(log.MoveCount > 30, $"three hands played {log.MoveCount} moves");
            var text = log.SaveText();
            report.Check(text.StartsWith(SessionLog.Header, StringComparison.Ordinal),
                "a save starts with its format header");

            var parsed = SessionLog.Parse(text);
            report.Equal(text, parsed.SaveText(), "a save parses back into the same text");
            report.Equal(3, parsed.Hands.Count, "three hands in the save");
            report.Equal(log.MoveCount, parsed.MoveCount, "no move is lost in the round trip");
            report.Equal(1, parsed.Hands[1].Dealer, "the dealer of each hand is kept");
            report.Equal<int?>(70013, parsed.Hands[1].Seed, "the seed of each hand is kept");
            report.Equal(Wind.East, parsed.Hands[1].RoundWind, "the round wind of each hand is kept");

            // The file round trip is the one the game actually uses.
            var path = Path.Combine(Path.GetTempPath(), "mahjong-sessionlog-selftest.mahjong");
            try
            {
                log.Save(path);
                report.Equal(text, SessionLog.Load(path).SaveText(), "the file round trip loses nothing");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static void LogRebuildsHands(SelfTestReport report)
        {
            var log = new SessionLog();
            var originals = new List<InProcessTable>();
            for (var hand = 0; hand < 3; hand++)
            {
                var table = new InProcessTable((hand + 1) % 4, 81000 + hand * 7, Wind.South,
                    BotLevel.Strategic, 0, 1, 2, 3);
                table.PlayOut();
                log.BeginHand(table.Server.Dealer, table.Server.Seed, table.Server.RoundWind);
                foreach (var (seat, wire) in table.Server.Log)
                    log.RecordWire(seat, wire);
                originals.Add(table);
            }

            var replayed = log.Replay();
            report.Equal(3, replayed.Count, "one rebuilt table per hand");
            for (var hand = 0; hand < originals.Count; hand++)
            {
                for (var seat = 0; seat < 4; seat++)
                    report.Equal(originals[hand].View(seat).Describe(), replayed[hand].ViewFor(seat).Describe(),
                        $"hand {hand + 1}: seat {seat} ends up in the same state after a replay");
                report.Equal(
                    originals[hand].State.Result?.Score.TotalPoints ?? 0,
                    replayed[hand].State.Result?.Score.TotalPoints ?? 0,
                    $"hand {hand + 1}: the replay scores the same");
            }
        }

        // ------------------------------------------------------------- refusal

        private static void CorruptSaveRejected(SelfTestReport report)
        {
            void Broken(string text, string why)
            {
                try
                {
                    SessionLog.Parse(text);
                    report.Check(false, why + " must be refused");
                }
                catch (FormatException failure)
                {
                    report.Check(failure.Message.Contains("line") || failure.Message.Contains("header"),
                        why + " names the place it died on: " + failure.Message);
                }
            }

            Broken("this is not a save\n", "a file without the format header");
            Broken(SessionLog.Header + "\nhand|x|y|z\n", "a hand header with a dealer that is not a seat");
            Broken(SessionLog.Header + "\nhand|0|abc|East\n", "a hand header with a broken seed");
            Broken(SessionLog.Header + "\nhand|0|5|East\n5|discard|17|-\n", "a move from seat 5");
            Broken(SessionLog.Header + "\nhand|0|5|East\n0|ход в никуда|-|-\n", "a move that is not a move");
            Broken(SessionLog.Header + "\n0|discard|17|-\n", "a move before any deal");

            // Comments survive the round trip instead of breaking the parse.
            var noted = SessionLog.Parse(SessionLog.Header + "\n# saved by the self-test\nhand|0|5|East\n");
            report.Equal("saved by the self-test", noted.Notes.FirstOrDefault(), "a file comment is read");
            report.Equal(SessionLog.Header + "\n# saved by the self-test\nhand|0|5|East\n",
                noted.SaveText(), "a file comment survives the round trip");
        }

        private static void ForgedMoveRejected(SelfTestReport report)
        {
            var log = PlayOneHand(1, 90210);

            // The line parses as a move — it is the engine that must refuse to replay it.
            var hand = log.Hands[0];
            hand.Moves[1] = (hand.Moves[1].Seat, "discard|999999|-");
            report.Check(SessionLog.Parse(log.SaveText()).Hands[0].Moves.Count == hand.Moves.Count,
                "the forged save still parses as text");

            try
            {
                log.ReplayHand(0);
                report.Check(false, "a forged move must stop the replay");
            }
            catch (InvalidOperationException failure)
            {
                report.Check(failure.Message.Contains("hand 1"), "the refusal names the hand: " + failure.Message);
            }
        }

        // --------------------------------------------------------------- cursor

        private static void CursorStepping(SelfTestReport report)
        {
            var played = PlayOneHand(3, 51500);

            // The walk is over a partial hand: a cursor only ever steps accepted moves, and a shorter
            // hand keeps the forward/backward comparison readable in a failure.
            var log = new SessionLog();
            log.BeginHand(played.Hands[0].Dealer, played.Hands[0].Seed, played.Hands[0].RoundWind);
            foreach (var move in played.Hands[0].Moves.Take(40))
                log.RecordWire(move.Seat, move.Wire);

            var cursor = new ReplayCursor(log);
            report.Equal(0, cursor.MoveIndex, "a replay opens at the deal");
            report.Equal(0, cursor.HandIndex, "a replay opens at the first hand");
            report.Check(cursor.LastMoveText.Length == 0, "the deal has no last move");
            report.Equal($"1/1 · 0/{cursor.MoveCount}", cursor.PositionText, "the position text counts moves");

            var forward = new List<string> { Fingerprint(cursor.Table) };
            while (cursor.Next())
            {
                forward.Add(Fingerprint(cursor.Table));
                report.Check(cursor.LastMoveText.StartsWith("P", StringComparison.Ordinal),
                    "every step names the move it stepped over: " + cursor.LastMoveText);
            }

            report.Equal(log.Hands[0].Moves.Count, cursor.MoveIndex, "the cursor walked every move");
            report.Check(!cursor.Next(), "the end of the hand is the end of the walk");

            var backward = new List<string> { Fingerprint(cursor.Table) };
            while (cursor.Back())
                backward.Insert(0, Fingerprint(cursor.Table));

            report.Check(!cursor.Back(), "the deal is the start of the walk");
            report.Equal(forward.Count, backward.Count, "forward and back visit the same number of states");
            for (var step = 0; step < Math.Min(forward.Count, backward.Count); step++)
                report.Equal(forward[step], backward[step], $"state {step} is the same walked either way");

            // Jumps land where they say: to the ends and to the next deal.
            cursor.ToEnd();
            report.Check(cursor.AtEnd, "the end jump reaches the end of the hand");

            // At the end of the hand every tile of every move is on the table, so a review can name
            // them all by face: a name like "#17" would mean the log and the table drifted apart.
            for (var move = 0; move < log.Hands[0].Moves.Count; move++)
            {
                var described = cursor.MoveAt(move);
                report.Equal(log.Hands[0].Moves[move].Seat, described.Seat, $"move {move + 1} names its seat");
                report.Check(described.Tiles.All(name => name.Length > 0 && !name.StartsWith("#", StringComparison.Ordinal)),
                    $"move {move + 1} names real faces: {described}");
            }

            cursor.ToStart();
            report.Check(cursor.AtStart, "the start jump returns to the deal");

            var two = new SessionLog();
            two.BeginHand(0, 11, Wind.East);
            two.BeginHand(1, 22, Wind.East);
            var crossed = new ReplayCursor(two);
            report.Check(crossed.NextHand(), "a second hand is reachable");
            report.Equal(1, crossed.HandIndex, "the cursor is on the second hand");
            report.Equal<int?>(22, crossed.Log.Hands[crossed.HandIndex].Seed, "the second hand replays from its own seed");
            report.Check(crossed.PreviousHand(), "the first hand comes back");
            report.Equal(0, crossed.HandIndex, "the cursor is on the first hand again");
            report.Check(!crossed.PreviousHand(), "the first hand has no hand before it");
        }

        // ------------------------------------------------------------- restore

        private static void SessionResumes(SelfTestReport report)
        {
            var elapsed = TimeSpan.FromMinutes(37.5);
            var limit = TimeSpan.FromMinutes(90);
            var log = new SessionLog { Info = new SessionLog.SessionInfo(elapsed, limit) };
            var expected = new int[4];

            for (var hand = 0; hand < 4; hand++)
            {
                var table = new InProcessTable(hand, 61000 + hand * 101, Wind.East,
                    BotLevel.Strategic, 0, 1, 2, 3);
                table.PlayOut();
                log.BeginHand(table.Server.Dealer, table.Server.Seed, table.Server.RoundWind);
                foreach (var (seat, wire) in table.Server.Log)
                    log.RecordWire(seat, wire);

                // What those hands pay, worked out apart from the session: this is the score the
                // resumed session has to arrive at by replaying, not by trusting the file.
                var settlement = table.State.Result != null
                    ? Settlements.ForWin(table.State.Result)
                    : Settlements.ForDraw();
                foreach (var payment in settlement.Payments)
                {
                    expected[payment.FromPlayer] -= payment.Points;
                    expected[payment.ToPlayer] += payment.Points;
                }
            }

            var saved = SessionLog.Parse(log.SaveText());
            report.Equal(elapsed, saved.Info?.Elapsed, "the session clock survives the file");
            report.Equal(limit, saved.Info?.TimeLimit, "the session limit survives the file");

            var resumed = SessionRestore.Restore(saved, out var outcome);
            for (var seat = 0; seat < 4; seat++)
                report.Equal(expected[seat], resumed.Scores[seat], $"seat {seat} resumes with the points of its hands");
            report.Equal(4, resumed.HandsPlayed, "four hands are counted as played");
            report.Equal(elapsed, resumed.Elapsed, "the clock comes back where it stopped");
            report.Equal(limit, resumed.TimeLimit, "the session keeps its own limit");
            report.Equal(4, resumed.History.Count, "four settled hands in the history");
            report.Check(!outcome.TimeUp, "the clock is not spent: " + outcome);
            report.Check(!outcome.HandInProgress, "the restore stops between hands: " + outcome);
            report.Check(!resumed.IsFinished, "four of sixteen hands leave the session running");

            // A table without a clock keeps that too (8.7): no seconds is a length, not a missing line.
            var clocklessLog = new SessionLog
            {
                Info = new SessionLog.SessionInfo(TimeSpan.FromMinutes(5), TimeSpan.Zero)
            };
            clocklessLog.BeginHand(0, 777, Wind.East);
            var clockless = SessionLog.Parse(clocklessLog.SaveText());
            var restoredClockless = SessionRestore.Restore(clockless, out _);
            report.Check(!restoredClockless.HasTimeLimit, "a table without a clock comes back without one");
            report.Check(!restoredClockless.IsTimeUp, "and five minutes are not a spent clock");

            // The session goes on: the fifth hand is dealt to the next dealer in the next round.
            resumed.StartNextHand(4242);
            report.Equal(0, resumed.State!.Dealer, "the fifth hand goes to the fourth seat of the round");
            report.Equal(Wind.South, resumed.State!.RoundWind, "the fifth hand opens the next round");
        }

        private static void SessionResumesMidHand(SelfTestReport report)
        {
            var table = new InProcessTable(0, 4242, Wind.East, BotLevel.Strategic, 0, 1, 2, 3);
            table.PlayOut();
            var all = table.Server.Log.ToList();
            var cut = Math.Min(24, all.Count - 1);

            var log = new SessionLog();
            log.BeginHand(table.Server.Dealer, table.Server.Seed, table.Server.RoundWind);
            foreach (var (seat, wire) in all.Take(cut))
                log.RecordWire(seat, wire);

            var resumed = SessionRestore.Restore(log, out var outcome);
            report.Check(outcome.HandInProgress, "a cut log resumes a hand in progress: " + outcome);
            report.Equal(0, resumed.HandsPlayed, "a hand in progress is not a hand played yet");
            report.Check(resumed.State!.Phase != GamePhase.HandFinished, "the resumed hand is still running");

            // The hand resumes exactly where the log stopped: the same four projections, not just
            // "some state with thirteen tiles".
            var expected = AuthoritativeTable.Replay(table.Server.Dealer, table.Server.Seed,
                table.Server.RoundWind, log.Hands[0].LogLines());
            for (var seat = 0; seat < 4; seat++)
                report.Equal(SeatView.For(expected.State, seat).Describe(),
                    SeatView.For(resumed.State!, seat).Describe(),
                    $"seat {seat} resumes where the hand stopped");

            // And it can be played on to the end, which is what "continue" has to mean.
            foreach (var (seat, wire) in all.Skip(cut))
            {
                report.Check(ActionCodec.TryDecode(wire, out var intent, out var error), $"{wire} decodes: {error}");
                var action = ActionCodec.Resolve(resumed.State!.GetLegalActions(seat), intent);
                report.Check(action != null, $"{wire} is still offered to seat {seat} after the resume");
                resumed.State!.Apply(action!);
            }

            report.Equal(GamePhase.HandFinished, resumed.State!.Phase, "the rest of the hand plays out");
            var settlement = resumed.CompleteHand();
            report.Equal(1, resumed.HandsPlayed, "the continued hand is counted as played");

            var original = table.State.Result != null
                ? Settlements.ForWin(table.State.Result)
                : Settlements.ForDraw();
            report.Equal(original.WinnerPlayer, settlement.WinnerPlayer,
                "the continued hand has the winner the hand had");
            report.Equal(original.TotalPaid, settlement.TotalPaid,
                "the continued hand pays what it paid when it was first played");
        }

        private static void SpentClockStopsTheSession(SelfTestReport report)
        {
            var table = new InProcessTable(0, 31337, Wind.East, BotLevel.Strategic, 0, 1, 2, 3);
            table.PlayOut();
            var log = new SessionLog { Info = new SessionLog.SessionInfo(TimeSpan.FromMinutes(31), TimeSpan.FromMinutes(30)) };
            log.BeginHand(table.Server.Dealer, table.Server.Seed, table.Server.RoundWind);
            foreach (var (seat, wire) in table.Server.Log)
                log.RecordWire(seat, wire);

            var resumed = SessionRestore.Restore(log, out var outcome);
            report.Check(outcome.TimeUp, "a spent clock comes back spent: " + outcome);
            report.Check(resumed.EndedByTimeLimit, "the session ended when its clock ran out");
            report.Check(resumed.IsTimeUp, "the restored clock is past its limit");
            report.Check(!outcome.HandInProgress, "nothing is in progress once play has stopped");

            // Rule 3.5.6: a stopped session deals no further hand.
            try
            {
                resumed.StartNextHand(1);
                report.Check(false, "a session stopped by the clock must not deal another hand");
            }
            catch (InvalidOperationException)
            {
                report.Check(true, "a session stopped by the clock refuses to deal another hand");
            }
        }

        private static void WrongDealRefused(SelfTestReport report)
        {
            var first = PlayOneHand(0, 70707);
            var second = PlayOneHand(0, 80808);   // the session deals the second hand to seat 1

            var log = new SessionLog();
            foreach (var hand in first.Hands)
            {
                log.BeginHand(hand.Dealer, hand.Seed, hand.RoundWind);
                foreach (var move in hand.Moves) log.RecordWire(move.Seat, move.Wire);
            }

            foreach (var hand in second.Hands)
            {
                log.BeginHand(hand.Dealer, hand.Seed, hand.RoundWind);
                foreach (var move in hand.Moves) log.RecordWire(move.Seat, move.Wire);
            }

            try
            {
                SessionRestore.Restore(log, out _);
                report.Check(false, "a save whose hand is dealt to the wrong seat must not resume");
            }
            catch (InvalidOperationException failure)
            {
                report.Check(failure.Message.Contains("hand 2"),
                    "the refusal names the hand that does not fit: " + failure.Message);
            }
        }

        // -------------------------------------------------------------- helpers

        private static SessionLog PlayOneHand(int dealer, int seed)
        {
            var table = new InProcessTable(dealer, seed, Wind.East, BotLevel.Strategic, 0, 1, 2, 3);
            table.PlayOut();
            var log = new SessionLog();
            log.BeginHand(table.Server.Dealer, table.Server.Seed, table.Server.RoundWind);
            foreach (var (seat, wire) in table.Server.Log)
                log.RecordWire(seat, wire);
            return log;
        }

        /// <summary>Everything a replayed table says, all four projections; a state has no other face.</summary>
        private static string Fingerprint(AuthoritativeTable table) =>
            string.Join("\n", Enumerable.Range(0, 4).Select(seat => table.ViewFor(seat).Describe()));
    }
}
