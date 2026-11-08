using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>One transfer of game points between two players.</summary>
    public sealed record Payment(int FromPlayer, int ToPlayer, int Points);

    /// <summary>
    /// What every seat's combination was worth in a classic hand, and which seat was East. The three
    /// losers are compared by these numbers, but the settlement itself is the list of payments like
    /// in MCR — the score sheet only ever adds transfers.
    /// </summary>
    public sealed record ClassicSettlement(IReadOnlyList<int> Values, int EastSeat, int Limit);

    /// <summary>
    /// Settlement of a finished hand under rule 3.9.1.2-3.9.1.3:
    /// the winner's hand points always include flowers, and every loser also pays
    /// the fixed eight extra points.
    /// </summary>
    public sealed record Settlement(
        int WinnerPlayer,
        int HandPoints,
        bool SelfDrawn,
        IReadOnlyList<Payment> Payments,
        int WinnerGain,
        string Explanation,
        ClassicSettlement? Classic = null)
    {
        public int TotalPaid => Payments.Sum(p => p.Points);
    }

    public static class Settlements
    {
        /// <summary>Fixed points every loser pays on top of the winner's hand value.</summary>
        public const int ExtraPoints = 8;

        public static Settlement ForWin(WinResult result)
        {
            var winner = result.PlayerIndex;
            var points = result.Score.TotalPoints;
            var selfDrawn = result.Source != ClaimSource.Discard;
            var payments = new List<Payment>();

            if (selfDrawn)
            {
                for (var player = 0; player < 4; player++)
                    if (player != winner) payments.Add(new Payment(player, winner, points + ExtraPoints));

                return new Settlement(winner, points, true, payments, points * 3 + ExtraPoints * 3,
                    $"Self-draw: every opponent pays {points} + {ExtraPoints}.");
            }

            for (var player = 0; player < 4; player++)
            {
                if (player == winner) continue;
                var isDiscarder = result.DiscarderPlayer == player;
                payments.Add(new Payment(player, winner, isDiscarder ? points + ExtraPoints : ExtraPoints));
            }

            return new Settlement(winner, points, false, payments, points + ExtraPoints * 3,
                $"Discard win: the discarder pays {points} + {ExtraPoints}, the other two pay {ExtraPoints} each.");
        }

        /// <summary>No payments at all for a drawn hand.</summary>
        public static Settlement ForDraw() =>
            new(-1, 0, false, new List<Payment>(), 0, "Draw: no points change hands.");

        /// <summary>
        /// The settlement of a finished hand under whichever profile priced it. One place decides
        /// which arithmetic applies, so a session cannot pay a classic hand by MCR rules.
        /// </summary>
        public static Settlement ForState(GameState state, RuleVariant variant)
        {
            if (state.Result == null) return ForDraw();
            return variant.Mode == ScoringMode.ClassicPoints
                ? ForClassic(state, variant)
                : ForWin(state.Result);
        }

        /// <summary>
        /// Classic settlement from <c>rule-2.pdf</c> section IV: the three losers each pay the winner
        /// the full value of his combination, then settle among themselves by the difference of their
        /// own combinations. East pays and receives double. A combination above the house limit is
        /// already capped by the scorer.
        /// </summary>
        public static Settlement ForClassic(GameState state, RuleVariant variant)
        {
            var values = new int[4];
            var winner = state.Result!.PlayerIndex;
            values[winner] = state.Result.Score.Classic?.Value ?? state.Result.Score.TotalPoints;
            for (var player = 0; player < 4; player++)
            {
                if (player == winner) continue;
                values[player] = ClassicScoring
                    .ValueOf(state.Players[player], state.SeatWindOf(player), state.RoundWind, variant.HouseLimit)
                    .Value;
            }

            return ForClassic(values, winner, EastSeatOf(state), variant.HouseLimit,
                state.Result.Source != ClaimSource.Discard);
        }

        /// <summary>
        /// The payments of a classic hand from four finished values. Split out so the arithmetic of
        /// section IV can be checked on numbers alone, without building a whole hand first.
        /// </summary>
        public static Settlement ForClassic(IReadOnlyList<int> values, int winner, int eastSeat, int limit = 0,
            bool selfDrawn = false)
        {
            var payments = new List<Payment>();
            var losers = Enumerable.Range(0, 4).Where(player => player != winner).ToList();

            foreach (var loser in losers)
                payments.Add(new Payment(loser, winner, values[winner] * Factor(loser, winner, eastSeat)));

            for (var i = 0; i < losers.Count; i++)
                for (var j = i + 1; j < losers.Count; j++)
                {
                    var left = losers[i];
                    var right = losers[j];
                    var difference = values[left] - values[right];
                    if (difference == 0) continue;
                    var payer = difference < 0 ? left : right;
                    var receiver = difference < 0 ? right : left;
                    payments.Add(new Payment(payer, receiver, Math.Abs(difference) * Factor(payer, receiver, eastSeat)));
                }

            var gain = payments.Where(payment => payment.ToPlayer == winner).Sum(payment => payment.Points);
            var explanation = $"Classic: every loser pays {values[winner]}, then the losers settle by the difference "
                              + $"of their combinations ({string.Join(", ", values)})"
                              + (eastSeat >= 0 ? $"; East is seat {eastSeat + 1} and pays and receives double." : ".");

            return new Settlement(winner, values[winner], selfDrawn, payments, gain, explanation,
                new ClassicSettlement(values, eastSeat, limit));
        }

        /// <summary>Classic section IV.3: every transfer East takes part in is doubled.</summary>
        private static int Factor(int from, int to, int eastSeat) =>
            from == eastSeat || to == eastSeat ? 2 : 1;

        private static int EastSeatOf(GameState state)
        {
            for (var player = 0; player < 4; player++)
                if (state.SeatWindOf(player) == Wind.East) return player;
            return -1;
        }
    }
}
