using System.Collections.Generic;

namespace Mahjong.Rules
{
    public sealed record Fan(string Name, int Points, string Explanation);
    public sealed record ScoreResult(bool IsWinningHand, int TotalPoints, IReadOnlyList<Fan> Fans, HandAnalysis Analysis, string? Error = null);

    public static class Scoring
    {
        // Initial scoring slice. The full 81-fan WMCC table is added as isolated evaluators.
        // A win requires at least 8 points under rule 3.4.22.
        public static ScoreResult Evaluate(PlayerHand hand, ClaimSource source, Tile? winningTile = null)
        {
            var analysis = HandSolver.Analyse(hand, winningTile);
            if (!analysis.IsComplete) return new(false, 0, new List<Fan>(), analysis, "Hand is not complete.");
            var fans = new List<Fan>();
            if (source == ClaimSource.SelfDraw) fans.Add(new("Self-draw", 1, "Win from the wall."));
            if (analysis.Pattern == WinningPattern.SevenPairs) fans.Add(new("Seven pairs", 24, "Seven distinct pairs."));
            if (analysis.Pattern == WinningPattern.ThirteenOrphans) fans.Add(new("Thirteen orphans", 88, "All terminals and honors, with one duplicate."));
            var total = 0;
            foreach (var fan in fans) total += fan.Points;
            return new(total >= 8, total, fans, analysis, total >= 8 ? null : "A winning shape must score at least 8 points.");
        }
    }
}
