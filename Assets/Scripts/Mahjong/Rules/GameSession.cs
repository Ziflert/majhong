using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>A hand that was stopped by the clock and therefore scores nothing (rule 3.5.6).</summary>
    public sealed record AbandonedHand(int HandsPlayed, GamePhase Phase, int CurrentPlayer);

    /// <summary>One row of the final table (rules 3.9.2 and 3.10.1).</summary>
    public sealed record Standing(int Place, int Player, int GamePoints, double TablePoints, bool Tied, bool Forfeited);

    /// <summary>
    /// A full game session: four rounds of four hands, dealer rotating counter-clockwise
    /// after every hand, game points accumulated and converted into table points (3.9.2, 3.10).
    /// Also keeps the 150-minute session clock (3.5.6) and the penalty ledger of rule 3.11.
    /// </summary>
    public sealed class GameSession
    {
        public const int RoundsPerGame = 4;
        public const int HandsPerRound = 4;
        public const int TotalHands = RoundsPerGame * HandsPerRound;

        /// <summary>Rule 3.5.6: a session usually lasts 150 minutes.</summary>
        public static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromMinutes(150);

        /// <summary>Rule 3.5.6: the referee warns the table when a quarter of an hour is left.</summary>
        public static readonly TimeSpan TimeWarning = TimeSpan.FromMinutes(15);

        private readonly List<Settlement> history = new();
        private readonly int[] scores = new int[4];
        private readonly List<PenaltyResult> pendingPenalties = new();
        private readonly List<PenaltyResult> penaltyHistory = new();
        private readonly List<AbandonedHand> abandonedHands = new();
        private readonly HashSet<int> forfeited = new();

        public GameSession(TimeSpan? timeLimit = null)
        {
            TimeLimit = timeLimit ?? DefaultTimeLimit;
        }

        public IReadOnlyList<int> Scores => scores;
        public IReadOnlyList<Settlement> History => history;

        /// <summary>Penalties registered for the running hand: they are paid once the hand ends (3.9.1.2).</summary>
        public IReadOnlyList<PenaltyResult> PendingPenalties => pendingPenalties;

        /// <summary>Penalties already deducted from the score sheet.</summary>
        public IReadOnlyList<PenaltyResult> AppliedPenalties => penaltyHistory;

        public IReadOnlyList<AbandonedHand> AbandonedHands => abandonedHands;

        /// <summary>Per-player counters behind warnings and the doubling of repeated violations.</summary>
        public PenaltyLedger Violations { get; } = new();

        /// <summary>Number of hands already completed.</summary>
        public int HandsPlayed { get; private set; }
        public int RoundIndex => Math.Min(HandsPlayed / HandsPerRound, RoundsPerGame - 1);
        public bool IsFinished => EndedByTimeLimit || HandsPlayed >= TotalHands;

        /// <summary>Rule 3.4.8: the dealer passes the dice on after every hand, win or draw.</summary>
        public int Dealer => HandsPlayed % 4;
        public Wind RoundWind => (Wind)Math.Min(HandsPlayed / HandsPerRound, RoundsPerGame - 1);
        public GameState? State { get; private set; }
        public Settlement? LastSettlement { get; private set; }

        /// <summary>Rule 3.5.6: session clock.</summary>
        public TimeSpan TimeLimit { get; }
        public TimeSpan Elapsed { get; private set; }
        public TimeSpan TimeLeft => Elapsed >= TimeLimit ? TimeSpan.Zero : TimeLimit - Elapsed;
        public bool IsTimeUp => Elapsed >= TimeLimit;

        /// <summary>Rule 3.5.6: the last quarter of an hour of the session.</summary>
        public bool IsFinalQuarterHour => !IsTimeUp && TimeLeft <= TimeWarning;

        /// <summary>True when the session stopped on the clock instead of after sixteen hands.</summary>
        public bool EndedByTimeLimit { get; private set; }

        public void StartNextHand(int? seed = null)
        {
            if (IsFinished) throw new InvalidOperationException(EndedByTimeLimit
                ? "The session time is up."
                : "All sixteen hands have been played.");
            Violations.StartRound(RoundIndex);
            State = new GameState();
            State.StartHand(Dealer, seed, RoundWind);
            pendingPenalties.Clear();
        }

        /// <summary>Applies the finished hand to the score sheet and moves to the next hand.</summary>
        public Settlement CompleteHand(TimeSpan? handDuration = null)
        {
            if (State == null) throw new InvalidOperationException("No hand is in progress.");
            if (State.Phase != GamePhase.HandFinished) throw new InvalidOperationException("The hand is still running.");

            var settlement = State.Result != null ? Settlements.ForWin(State.Result) : Settlements.ForDraw();
            Apply(settlement);
            ApplyPendingPenalties();
            HandsPlayed++;
            AdvanceClock(handDuration ?? TimeSpan.Zero);
            return settlement;
        }

        /// <summary>
        /// Rule 3.5.6: when the time is up, play stops at once and the hand in progress scores nothing.
        /// </summary>
        public void AbandonRunningHand(TimeSpan? handDuration = null)
        {
            if (State != null && State.Phase != GamePhase.HandFinished)
                abandonedHands.Add(new AbandonedHand(HandsPlayed, State.Phase, State.CurrentPlayer));

            pendingPenalties.Clear();
            AdvanceClock(handDuration ?? TimeSpan.Zero);
            if (IsTimeUp) EndedByTimeLimit = true;
        }

        /// <summary>Ends the session before its time, e.g. by a referee's decision.</summary>
        public void StopEarly()
        {
            EndedByTimeLimit = true;
            if (State != null && State.Phase != GamePhase.HandFinished)
                abandonedHands.Add(new AbandonedHand(HandsPlayed, State.Phase, State.CurrentPlayer));
            pendingPenalties.Clear();
        }

        public void AdvanceClock(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            Elapsed += duration;
        }

        public void Apply(Settlement settlement)
        {
            foreach (var payment in settlement.Payments)
            {
                scores[payment.FromPlayer] -= payment.Points;
                scores[payment.ToPlayer] += payment.Points;
            }
            LastSettlement = settlement;
            history.Add(settlement);
        }

        // ------------------------------------------------------------ penalties

        /// <summary>
        /// Reports a violation of rule 3.11. Points are charged when the hand ends (3.9.1.2),
        /// the loss of the right to win takes effect immediately for the running hand.
        /// </summary>
        public PenaltyResult ReportViolation(int player, ViolationKind kind, int? judgePoints = null,
            int? victim = null, bool tileSeen = false)
        {
            var result = Violations.Register(player, kind, judgePoints, victim, tileSeen);
            if (result.LosesRightToWin) State?.ForbidWin(player, DescribeForbidden(result));
            pendingPenalties.Add(result);
            return result;
        }

        /// <summary>Rule 3.11.2: lateness costs points and, past fifteen minutes, the whole session.</summary>
        public int ReportLateArrival(int player, TimeSpan delay)
        {
            if (Penalties.MissesSession(delay))
            {
                forfeited.Add(player);
                scores[player] = 0;
                return 0;
            }

            var points = Penalties.LateArrivalPoints(delay);
            if (points <= 0) return 0;
            var result = Violations.Register(player, ViolationKind.LateArrival, points);
            scores[player] -= points;
            penaltyHistory.Add(result);
            return points;
        }

        public bool IsForfeited(int player) => forfeited.Contains(player);

        private void ApplyPendingPenalties()
        {
            foreach (var penalty in pendingPenalties)
            {
                if (penalty.TotalLoss > 0)
                {
                    scores[penalty.Player] -= penalty.TotalLoss;
                    foreach (var recipient in penalty.Recipients)
                        scores[recipient] += penalty.PointsPerRecipient;
                }
                penaltyHistory.Add(penalty);
            }
            pendingPenalties.Clear();
        }

        private static string DescribeForbidden(PenaltyResult penalty) =>
            penalty.Kind switch
            {
                ViolationKind.MistakenMeld => "a mistaken meld was declared",
                ViolationKind.FalseHuBelowMinimum => "a false Hu was declared with fewer than eight points",
                ViolationKind.FalseHuClaim => "a false Hu was claimed",
                ViolationKind.ExposedTilesAfterFalseHu => "tiles were exposed after a false Hu",
                ViolationKind.WrongTileCount => "the hand held the wrong number of tiles",
                ViolationKind.InformationLeak => "information was disclosed",
                ViolationKind.TouchedWallTile => "a wall tile was touched and seen before the discard",
                _ => penalty.Kind.ToString()
            };

        // ------------------------------------------------------------ standings

        /// <summary>Rule 3.9.2: 4 / 2 / 1 / 0 table points; ties share the points of their places.</summary>
        public double[] TablePoints()
        {
            var points = new double[4];
            foreach (var standing in Standings()) points[standing.Player] = standing.TablePoints;
            return points;
        }

        /// <summary>
        /// Rule 3.9.2 plus 3.10.1: players are ranked by game points, ties share the places
        /// and the table points of those places are split between them.
        /// </summary>
        public IReadOnlyList<Standing> Standings()
        {
            var order = Enumerable.Range(0, 4)
                .OrderByDescending(player => EffectiveScore(player))
                .ThenBy(player => player)
                .ToList();

            var result = new List<Standing>(4);
            var index = 0;
            while (index < order.Count)
            {
                var player = order[index];
                var score = EffectiveScore(player);
                var tied = new List<int> { player };
                var next = index + 1;
                while (next < order.Count && EffectiveScore(order[next]) == score)
                {
                    tied.Add(order[next]);
                    next++;
                }

                var place = index + 1;
                var share = 0.0;
                for (var slot = index; slot < next; slot++) share += PlacePoints(slot);
                foreach (var tiedPlayer in tied)
                    result.Add(new Standing(place, tiedPlayer, score, share / tied.Count, tied.Count > 1,
                        forfeited.Contains(tiedPlayer)));

                index = next;
            }
            return result;
        }

        public int? PlaceOf(int player) => Standings().FirstOrDefault(s => s.Player == player)?.Place;

        private int EffectiveScore(int player) => forfeited.Contains(player) ? 0 : scores[player];

        private static int PlacePoints(int place) => place switch
        {
            0 => 4,
            1 => 2,
            2 => 1,
            _ => 0
        };

        public string Describe()
        {
            var lines = Standings().Select(standing =>
                $"P{standing.Player}: {standing.GamePoints} game points, {standing.TablePoints:0.##} table points, "
                + $"place {standing.Place}{(standing.Tied ? " (tied)" : string.Empty)}{(standing.Forfeited ? " (forfeited)" : string.Empty)}");
            var clock = $"Clock: {Elapsed.TotalMinutes:0} of {TimeLimit.TotalMinutes:0} minutes"
                        + (IsTimeUp ? " - time is up" : IsFinalQuarterHour ? " - final quarter of an hour" : string.Empty);
            return string.Join(Environment.NewLine, lines.Append(clock));
        }
    }
}
