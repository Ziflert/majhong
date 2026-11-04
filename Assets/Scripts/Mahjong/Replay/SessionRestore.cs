using System;
using System.Linq;
using Mahjong.Net;
using Mahjong.Rules;

namespace Mahjong.Replay
{
    /// <summary>
    /// Turns a save file back into a running session: the same hands replayed from their seeds, the
    /// same scores recomputed from the replayed results, and the session clock where it stood.
    ///
    /// Scores are never read from the file — they are what the replayed hands pay, so a save cannot
    /// claim a score its own moves do not produce. The hand that was in progress is left in progress,
    /// which is what makes "continue" a continuation and not a restart.
    /// </summary>
    public static class SessionRestore
    {
        /// <summary>What the restore managed to bring back; the caller prints it, the tests check it.</summary>
        public sealed record Outcome(int HandsCompleted, int MovesReplayed, bool HandInProgress, bool TimeUp)
        {
            public override string ToString() =>
                $"{HandsCompleted} hand(s), {MovesReplayed} move(s), "
                + (HandInProgress ? "a hand in progress" : "between hands")
                + (TimeUp ? ", the session clock is spent" : string.Empty);
        }

        /// <summary>
        /// Rebuilds the session the log describes. A hand whose deal does not match the session
        /// (wrong dealer or round wind) or a move the engine would not have accepted stops the
        /// restore: a save that partly restores would score a game nobody played.
        /// </summary>
        /// <param name="fallbackTimeLimit">
        /// The limit to use when the save predates the session line; null takes the default of rule 3.5.6.
        /// </param>
        public static GameSession Restore(SessionLog log, out Outcome outcome, TimeSpan? fallbackTimeLimit = null)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (log.IsEmpty) throw new ArgumentException("a session needs at least one dealt hand", nameof(log));

            var info = log.Info
                       ?? new SessionLog.SessionInfo(TimeSpan.Zero, fallbackTimeLimit ?? GameSession.DefaultTimeLimit);
            var session = new GameSession(info.TimeLimit, log.Variant);
            var moves = 0;
            var inProgress = false;

            for (var index = 0; index < log.Hands.Count; index++)
            {
                var hand = log.Hands[index];
                var number = index + 1;

                if (hand.Dealer != session.Dealer || hand.RoundWind != session.RoundWind)
                    throw new InvalidOperationException(
                        $"hand {number} was dealt to seat {hand.Dealer} in the {hand.RoundWind} round, "
                        + $"but the session deals it to seat {session.Dealer} in the {session.RoundWind} round");

                session.StartNextHand(hand.Seed);
                moves += ReplayInto(session.State!, hand, number);

                if (session.State!.Phase != GamePhase.HandFinished)
                {
                    // The log stops mid-hand: this is the hand the player was in the middle of.
                    inProgress = true;
                    if (index != log.Hands.Count - 1)
                        throw new InvalidOperationException(
                            $"hand {number} does not end, but the save holds {log.Hands.Count - index - 1} more hand(s) after it");
                    break;
                }

                session.CompleteHand(TimeSpan.Zero);
            }

            // The clock is added last so the restored session lands exactly on the saved time.
            session.AdvanceClock(info.Elapsed);
            if (session.IsTimeUp)
            {
                session.AbandonRunningHand(TimeSpan.Zero);
                inProgress = false;
            }

            outcome = new Outcome(session.HandsPlayed, moves, inProgress, session.EndedByTimeLimit);
            return session;
        }

        /// <summary>Applies the logged moves to the freshly dealt hand; returns how many were played.</summary>
        private static int ReplayInto(GameState state, SessionLog.HandRecord hand, int number)
        {
            var played = 0;
            foreach (var (seat, wire) in hand.Moves)
            {
                if (!ActionCodec.TryDecode(wire, out var intent, out var error))
                    throw new InvalidOperationException($"hand {number} move {played + 1}: {error}");

                var action = ActionCodec.Resolve(state.GetLegalActions(seat), intent);
                if (action == null)
                    throw new InvalidOperationException(
                        $"hand {number} move {played + 1} ({wire}) is not offered to seat {seat}");

                state.Apply(action);
                played++;
            }

            return played;
        }

        /// <summary>The parts of a save a resumed game shows its player: hand number, points, seat.</summary>
        public static string DescribeTable(GameSession session, int humanSeat) => string.Join("  ",
            session.Scores.Select((score, player) => $"P{player + 1}={score}"))
        + $"  ·  {session.HandsPlayed + 1}/{GameSession.TotalHands}  ·  "
        + $"{session.Elapsed.TotalMinutes:0}/{session.TimeLimit.TotalMinutes:0} min";
    }
}
