using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>What the UI may show about the potential of the player's sporting hand.</summary>
    public enum HandPotentialMode
    {
        Off,
        BestAchievable,
        Forecast
    }

    public static class HandPotentialModeSettings
    {
        public static HandPotentialMode? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return text.Trim().ToLowerInvariant() switch
            {
                "off" or "0" => HandPotentialMode.Off,
                "max" or "best" or "bestachievable" => HandPotentialMode.BestAchievable,
                "forecast" or "expected" => HandPotentialMode.Forecast,
                _ => null
            };
        }

        public static string Id(HandPotentialMode mode) => mode switch
        {
            HandPotentialMode.BestAchievable => "max",
            HandPotentialMode.Forecast => "forecast",
            _ => "off"
        };
    }

    /// <summary>One discard and the waits it opens, with the real scorer behind every winning face.</summary>
    public sealed record PotentialLine(
        TileType? Discard,
        IReadOnlyList<TileType> Waits,
        int LiveCopies,
        int MaximumPoints,
        double ExpectedPoints,
        IReadOnlyList<TileType> WinningFaces)
    {
        public bool IsReady => WinningFaces.Count > 0;
        public double Probability => LiveCopies == 0 ? 0d : (double)LiveCopies / 4d;
    }

    /// <summary>
    /// Conservative, deterministic hand potential. It analyses the current waits, or each legal
    /// one-discard continuation when the player has fourteen concealed tiles. It never searches hidden
    /// opponent hands and never invents a score: every candidate is evaluated by the selected scorer.
    ///
    /// MaximumPoints is an achievable upper bound for the analysed continuation. ExpectedPoints is a
    /// simple live-copy weighted forecast, not a probability of winning: it deliberately does not
    /// pretend to model future draws, opponents, or strategy. A later predictor can replace this
    /// adapter without changing the HUD contract.
    /// </summary>
    public static class HandPotential
    {
        public static PotentialLine Evaluate(GameState state, int player, IReadOnlyCollection<TileType>? visible = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if ((uint)player >= (uint)state.Players.Length) throw new ArgumentOutOfRangeException(nameof(player));
            var hand = state.Players[player];
            var seen = visible ?? VisibleCopies(state, hand);
            var candidates = new List<(TileType? Discard, PlayerHand Hand)>();

            if (hand.Concealed.Count == 14)
            {
                foreach (var tile in hand.Concealed.GroupBy(t => t.Type).Select(g => g.First()))
                {
                    var next = Copy(hand, tile.Type);
                    candidates.Add((tile.Type, next));
                }
            }
            else candidates.Add((null, Copy(hand)));

            PotentialLine? best = null;
            foreach (var candidate in candidates)
            {
                var line = EvaluateLine(state, candidate.Discard, candidate.Hand, player, seen);
                if (best == null || Compare(line, best) > 0) best = line;
            }
            return best ?? new PotentialLine(null, Array.Empty<TileType>(), 0, 0, 0,
                Array.Empty<TileType>());
        }

        private static PotentialLine EvaluateLine(GameState state, TileType? discard, PlayerHand hand,
            int player, IReadOnlyCollection<TileType> visible)
        {
            var waits = HandSolver.WaitingTiles(hand);
            var winning = new List<TileType>();
            var weighted = 0d;
            var live = 0;
            var maximum = 0;
            foreach (var wait in waits)
            {
                var result = state.Variant.Score(new ScoringContext
                {
                    Hand = hand,
                    WinningTile = wait,
                    Source = ClaimSource.Discard,
                    SeatWind = state.SeatWindOf(player),
                    RoundWind = state.RoundWind
                });
                if (!result.IsValidWin) continue;
                winning.Add(wait);
                var copies = Math.Max(0, 4 - Count(visible, wait));
                live += copies;
                maximum = Math.Max(maximum, result.TotalPoints);
                weighted += copies * result.TotalPoints;
            }
            return new PotentialLine(discard, waits, live, maximum,
                live == 0 ? 0d : weighted / live, winning);
        }

        private static int Compare(PotentialLine left, PotentialLine right)
        {
            var score = left.MaximumPoints.CompareTo(right.MaximumPoints);
            if (score != 0) return score;
            score = left.LiveCopies.CompareTo(right.LiveCopies);
            if (score != 0) return score;
            return left.ExpectedPoints.CompareTo(right.ExpectedPoints);
        }

        private static PlayerHand Copy(PlayerHand source, TileType? remove = null)
        {
            var copy = new PlayerHand();
            var removed = false;
            foreach (var tile in source.Concealed)
            {
                if (!removed && remove.HasValue && tile.Type.Equals(remove.Value))
                {
                    removed = true;
                    continue;
                }
                copy.AddConcealed(new Tile(tile.Type, tile.InstanceId));
            }
            foreach (var meld in source.Melds) copy.AddMeld(meld);
            foreach (var flower in source.Flowers) copy.AddFlower(new Tile(flower.Type, flower.InstanceId));
            return copy;
        }

        private static IReadOnlyCollection<TileType> VisibleCopies(GameState state, PlayerHand ownHand)
        {
            var result = new List<TileType>();
            result.AddRange(ownHand.Concealed.Select(tile => tile.Type));
            result.AddRange(ownHand.Flowers.Select(tile => tile.Type));
            foreach (var discard in state.Discards) result.Add(discard.Tile.Type);
            foreach (var hand in state.Players)
                foreach (var meld in hand.Melds)
                    result.AddRange(meld.Tiles.Select(tile => tile.Type));
            return result;
        }

        private static int Count(IReadOnlyCollection<TileType> types, TileType target) =>
            types.Count(type => type.Equals(target));
    }
}
