using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>One transfer of game points between two players.</summary>
    public sealed record Payment(int FromPlayer, int ToPlayer, int Points);

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
        string Explanation)
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
    }
}
