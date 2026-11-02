using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    public enum WinningPattern { Standard, SevenPairs, ThirteenOrphans }

    public sealed record HandAnalysis(bool IsComplete, WinningPattern? Pattern, IReadOnlyList<IReadOnlyList<TileType>> Groups, string? Error = null);

    public static class HandSolver
    {
        private static readonly HashSet<TileType> Orphans = BuildOrphans();

        public static HandAnalysis Analyse(PlayerHand hand, Tile? winningTile = null)
        {
            var tiles = hand.Concealed.Select(t => t.Type).ToList();
            if (winningTile.HasValue) tiles.Add(winningTile.Value.Type);
            var expected = 14 - hand.Melds.Sum(m => m.TileCount);
            if (tiles.Count != expected) return new(false, null, Array.Empty<IReadOnlyList<TileType>>(), $"Expected {expected} concealed tiles, got {tiles.Count}.");

            if (IsThirteenOrphans(tiles)) return new(true, WinningPattern.ThirteenOrphans, Array.Empty<IReadOnlyList<TileType>>());
            if (IsSevenPairs(tiles)) return new(true, WinningPattern.SevenPairs, Array.Empty<IReadOnlyList<TileType>>());

            var counts = tiles.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
            foreach (var pair in counts.Where(p => p.Value >= 2).Select(p => p.Key))
            {
                var rest = new Dictionary<TileType, int>(counts);
                rest[pair] -= 2;
                if (TryGroups(rest, new List<IReadOnlyList<TileType>>(), out var groups))
                    return new(true, WinningPattern.Standard, groups.Prepend(new[] { pair, pair }).ToList());
            }
            return new(false, null, Array.Empty<IReadOnlyList<TileType>>());
        }

        private static bool TryGroups(Dictionary<TileType, int> counts, List<IReadOnlyList<TileType>> groups, out List<IReadOnlyList<TileType>> result)
        {
            var first = counts.FirstOrDefault(p => p.Value > 0).Key;
            if (first == default) { result = groups; return true; }
            if (counts[first] >= 3)
            {
                Change(counts, first, -3); groups.Add(new[] { first, first, first });
                if (TryGroups(counts, groups, out result)) return true;
                groups.RemoveAt(groups.Count - 1); Change(counts, first, 3);
            }
            if (first.IsNumbered && first.Rank <= 7)
            {
                var second = new TileType(first.Suit, first.Rank + 1);
                var third = new TileType(first.Suit, first.Rank + 2);
                if (counts.GetValueOrDefault(second) > 0 && counts.GetValueOrDefault(third) > 0)
                {
                    Change(counts, first, -1); Change(counts, second, -1); Change(counts, third, -1);
                    groups.Add(new[] { first, second, third });
                    if (TryGroups(counts, groups, out result)) return true;
                    groups.RemoveAt(groups.Count - 1); Change(counts, first, 1); Change(counts, second, 1); Change(counts, third, 1);
                }
            }
            result = groups; return false;
        }

        private static void Change(Dictionary<TileType, int> counts, TileType type, int delta) => counts[type] = counts.GetValueOrDefault(type) + delta;
        private static bool IsSevenPairs(List<TileType> tiles) => tiles.Count == 14 && tiles.GroupBy(t => t).All(g => g.Count() == 2) && tiles.GroupBy(t => t).Count() == 7;
        private static bool IsThirteenOrphans(List<TileType> tiles) => tiles.Count == 14 && Orphans.All(o => tiles.Contains(o)) && tiles.GroupBy(t => t).Count(g => g.Count() == 2) == 1 && tiles.All(t => Orphans.Contains(t));
        private static HashSet<TileType> BuildOrphans() => new(Enumerable.Range(0, 3).SelectMany(s => new[] { 1, 9 }.Select(r => new TileType((TileSuit)s, r))).Concat(Enumerable.Range(0, 4).Select(r => new TileType(TileSuit.Wind, r))).Concat(Enumerable.Range(0, 3).Select(r => new TileType(TileSuit.Dragon, r))));
    }
}
