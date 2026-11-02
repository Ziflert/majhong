using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>Violations listed in rule 3.11.6 plus the lateness fine of 3.11.2.</summary>
    public enum ViolationKind
    {
        Deception,
        MistakenMeld,
        EmptyClaim,
        TouchedWallTile,
        LatePungClaim,
        FalseHuBelowMinimum,
        FalseHuClaim,
        ExposedTilesAfterFalseHu,
        WrongTileCount,
        InformationLeak,
        KnockedOverTile,
        LateArrival
    }

    /// <summary>How the size of a penalty is determined.</summary>
    public enum PenaltyScale
    {
        /// <summary>No points: the offence only costs the right to win the current hand.</summary>
        NoPoints,

        /// <summary>First offence is a warning, then 5, 10, 20, 40, 80 ... points (3.11.6.3-3.11.6.5).</summary>
        WarningThenDoubling,

        /// <summary>Fixed amount paid to every opponent (3.11.6.6-3.11.6.7).</summary>
        FixedToOpponents,

        /// <summary>Amount chosen by the referee inside an allowed range (3.11.6.1, 3.11.6.7).</summary>
        JudgeDiscretion,

        /// <summary>Fixed fine that disappears from the table instead of being paid to someone (3.11.2).</summary>
        FixedFine
    }

    /// <summary>Which stretch of play the warning/doubling counter belongs to.</summary>
    public enum PenaltyScope
    {
        /// <summary>Counter lives for the whole session (four rounds).</summary>
        Session,

        /// <summary>Counter starts again in every round (explicit for the late pung, 3.11.6.5).</summary>
        Round,

        /// <summary>Not escalating: every offence is treated the same way.</summary>
        None
    }

    /// <summary>Description of one entry of the penalty catalogue.</summary>
    public sealed record PenaltyRule(
        ViolationKind Kind,
        string Name,
        string Reference,
        PenaltyScale Scale,
        int Points,
        int MinimumPoints,
        int MaximumPoints,
        bool LosesRightToWin,
        PenaltyScope Scope,
        bool NeedsSeenTile)
    {
        public bool IsEscalating => Scale == PenaltyScale.WarningThenDoubling;
    }

    /// <summary>The outcome of one reported violation: what it costs and what it forbids.</summary>
    public sealed record PenaltyResult(
        ViolationKind Kind,
        int Player,
        int OffenceNumber,
        bool WarningOnly,
        int PointsPerRecipient,
        IReadOnlyList<int> Recipients,
        int TotalLoss,
        int TotalPaid,
        bool LosesRightToWin,
        string Description)
    {
        /// <summary>Points that leave the offender but do not reach anybody (rule 3.11.2).</summary>
        public int PointsLostFromTable => TotalLoss - TotalPaid;
    }

    /// <summary>
    /// Catalogue of penalties from rule 3.11: what each violation costs, whether it takes the
    /// right to win the current hand, and where the warning/doubling counter resets.
    /// Pure data plus bookkeeping, no Unity and no referee.
    /// </summary>
    public static class Penalties
    {
        private static readonly PenaltyRule[] catalogue =
        {
            new(ViolationKind.Deception, "Cheating", "3.11.6.1", PenaltyScale.JudgeDiscretion, 0, 5, 60,
                false, PenaltyScope.None, false),
            new(ViolationKind.MistakenMeld, "Mistaken chow, pung, kong or flower replacement", "3.11.6.2",
                PenaltyScale.NoPoints, 0, 0, 0, true, PenaltyScope.None, false),
            new(ViolationKind.EmptyClaim, "Empty requirement (change of mind)", "3.11.6.3",
                PenaltyScale.WarningThenDoubling, 5, 0, 0, false, PenaltyScope.Session, false),
            new(ViolationKind.TouchedWallTile, "Touching a wall tile too early", "3.11.6.4",
                PenaltyScale.WarningThenDoubling, 5, 0, 0, false, PenaltyScope.Session, true),
            new(ViolationKind.LatePungClaim, "Late pung claim", "3.11.6.5",
                PenaltyScale.WarningThenDoubling, 5, 0, 0, false, PenaltyScope.Round, false),
            new(ViolationKind.FalseHuBelowMinimum, "False Hu: hand below eight points", "3.11.6.6",
                PenaltyScale.FixedToOpponents, 10, 0, 0, true, PenaltyScope.None, false),
            new(ViolationKind.FalseHuClaim, "False Hu: wrong claim", "3.11.6.6",
                PenaltyScale.FixedToOpponents, 20, 0, 0, true, PenaltyScope.None, false),
            new(ViolationKind.ExposedTilesAfterFalseHu, "Exposed tiles after a false Hu", "3.11.6.7",
                PenaltyScale.FixedToOpponents, 30, 0, 0, true, PenaltyScope.None, false),
            new(ViolationKind.WrongTileCount, "Wrong number of tiles", "3.11.6.8",
                PenaltyScale.NoPoints, 0, 0, 0, true, PenaltyScope.None, false),
            new(ViolationKind.InformationLeak, "Disclosure of information", "3.11.6.9",
                PenaltyScale.NoPoints, 0, 0, 0, true, PenaltyScope.None, false),
            new(ViolationKind.KnockedOverTile, "Knocked over another player's tile", "3.11.6.7",
                PenaltyScale.JudgeDiscretion, 0, 5, 60, false, PenaltyScope.None, false),
            new(ViolationKind.LateArrival, "Late arrival", "3.11.2",
                PenaltyScale.FixedFine, 0, 10, 20, false, PenaltyScope.None, false)
        };

        public static IReadOnlyList<PenaltyRule> Catalogue => catalogue;

        public static PenaltyRule Rule(ViolationKind kind) =>
            catalogue.First(rule => rule.Kind == kind);

        /// <summary>
        /// Size of an escalated penalty: the first offence is only a warning, then 5, 10, 20, 40, 80 ...
        /// (3.11.6.3-3.11.6.5: "в первый раз ... предупреждение, во второй раз 5, в третий 10, в четвертый 20, и так далее").
        /// </summary>
        public static int EscalatedPoints(int offenceNumber)
        {
            if (offenceNumber <= 1) return 0;
            if (offenceNumber >= 6) return 80;
            return 5 << (offenceNumber - 2);
        }

        /// <summary>Rule 3.11.2: 10 points up to ten minutes late, 20 up to fifteen, later the session is missed.</summary>
        public static int LateArrivalPoints(TimeSpan delay) =>
            delay <= TimeSpan.Zero ? 0 : delay <= TimeSpan.FromMinutes(10) ? 10 : delay <= TimeSpan.FromMinutes(15) ? 20 : 0;

        /// <summary>Rule 3.11.2: more than fifteen minutes late means the whole session is forfeited.</summary>
        public static bool MissesSession(TimeSpan delay) => delay > TimeSpan.FromMinutes(15);

        /// <summary>Everybody except the offender, in seat order.</summary>
        public static IReadOnlyList<int> OpponentsOf(int player) =>
            Enumerable.Range(0, 4).Where(other => other != player).ToList();
    }

    /// <summary>
    /// Keeps the running count of offences per player, so that repeated violations grow
    /// from a warning to 5, 10, 20 ... points. Counters are either per session or per round.
    /// </summary>
    public sealed class PenaltyLedger
    {
        private readonly Dictionary<(int Player, ViolationKind Kind, int ScopeKey), int> offences = new();
        private readonly List<PenaltyResult> entries = new();

        /// <summary>Round index used by counters that reset every round (3.11.6.5).</summary>
        public int RoundIndex { get; set; }

        public IReadOnlyList<PenaltyResult> Entries => entries;

        public int Occurrences(int player, ViolationKind kind, int? scopeKey = null)
        {
            var rule = Penalties.Rule(kind);
            var key = rule.Scope == PenaltyScope.Round ? scopeKey ?? RoundIndex : 0;
            return offences.GetValueOrDefault((player, kind, key));
        }

        /// <summary>
        /// Registers one violation and returns what it costs.
        /// <paramref name="judgePoints"/> is used by referee-discretion penalties,
        /// <paramref name="victim"/> by penalties paid to a single player.
        /// </summary>
        public PenaltyResult Register(int player, ViolationKind kind, int? judgePoints = null, int? victim = null,
            bool tileSeen = false)
        {
            if (player is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(player));
            var rule = Penalties.Rule(kind);
            var scopeKey = rule.Scope == PenaltyScope.Round ? RoundIndex : 0;
            var key = (player, kind, scopeKey);
            var number = offences.GetValueOrDefault(key) + 1;
            offences[key] = number;

            var points = rule.Scale switch
            {
                PenaltyScale.WarningThenDoubling => Penalties.EscalatedPoints(number),
                PenaltyScale.FixedToOpponents or PenaltyScale.FixedFine => rule.Points,
                PenaltyScale.JudgeDiscretion => judgePoints ?? 0,
                _ => 0
            };

            if (rule.Scale == PenaltyScale.JudgeDiscretion && points != 0 &&
                (points < rule.MinimumPoints || points > rule.MaximumPoints))
                throw new ArgumentOutOfRangeException(nameof(judgePoints),
                    $"{kind} must be between {rule.MinimumPoints} and {rule.MaximumPoints} points, got {points}.");

            var recipients = new List<int>();
            if (points > 0 && victim.HasValue)
            {
                if (victim.Value == player) throw new ArgumentException("A player cannot pay a penalty to themselves.");
                recipients.Add(victim.Value);
            }
            else if (points > 0 && rule.Scale == PenaltyScale.FixedToOpponents)
            {
                recipients.AddRange(Penalties.OpponentsOf(player));
            }

            var totalPaid = points * recipients.Count;
            var totalLoss = points > 0 && recipients.Count == 0 ? points : totalPaid;

            var losesRightToWin = rule.LosesRightToWin || (rule.NeedsSeenTile && tileSeen);
            var result = new PenaltyResult(kind, player, number, points == 0, points, recipients, totalLoss,
                totalPaid, losesRightToWin, Describe(player, rule, number, points, recipients, losesRightToWin, tileSeen));
            entries.Add(result);
            return result;
        }

        /// <summary>Clears the per-round counters and moves to the next round.</summary>
        public void StartRound(int roundIndex) => RoundIndex = roundIndex;

        private static string Describe(int player, PenaltyRule rule, int number, int points,
            IReadOnlyList<int> recipients, bool losesRightToWin, bool tileSeen)
        {
            var head = points == 0
                ? $"P{player} {rule.Name}: warning only (offence {number})"
                : $"P{player} {rule.Name}: {points} points";
            var who = recipients.Count switch
            {
                0 => points == 0 ? string.Empty : " taken out of play",
                1 => $" to P{recipients[0]}",
                _ => $" to each of {string.Join(", ", recipients.Select(p => "P" + p))}"
            };
            var win = losesRightToWin ? "; may not declare Hu in this hand" : string.Empty;
            var seen = rule.NeedsSeenTile ? tileSeen ? "; the tile was seen" : "; the tile was not seen" : string.Empty;
            return head + who + seen + win + $" ({rule.Reference})";
        }
    }
}
