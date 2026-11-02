using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    public enum GroupKind { Chow, Pung }
    public enum WinningPattern { Standard, SevenPairs, ThirteenOrphans, SevenShiftedPairs, KnittedTiles, KnittedStraight }
    public enum WaitType { None, Edge, Closed, Single }

    public sealed record HandGroup(GroupKind Kind, IReadOnlyList<TileType> Tiles)
    {
        public TileType Base => Tiles[0];
        public bool Contains(TileType type) => Tiles.Contains(type);
    }

    public sealed record HandDecomposition(IReadOnlyList<HandGroup> Groups, TileType Pair);

    public sealed record HandAnalysis(
        bool IsComplete,
        WinningPattern? Pattern,
        IReadOnlyList<HandDecomposition> Decompositions,
        IReadOnlyList<TileType> WaitTiles,
        WaitType Wait,
        string? Error = null)
    {
        public static HandAnalysis Fail(string error) =>
            new(false, null, Array.Empty<HandDecomposition>(), Array.Empty<TileType>(), WaitType.None, error);

        public static HandAnalysis Ok(WinningPattern pattern, IReadOnlyList<HandDecomposition> decompositions,
            IReadOnlyList<TileType> waitTiles, WaitType wait) =>
            new(true, pattern, decompositions, waitTiles, wait);
    }

    /// <summary>
    /// Turns concealed tiles plus declared melds into win/no-win plus every legal decomposition.
    /// Independent of Unity and of scoring.
    /// </summary>
    public static class HandSolver
    {
        private static readonly HashSet<TileType> Orphans = BuildOrphans();

        /// <summary>Checks a complete hand. Pass <paramref name="winningTile"/> to also describe the wait.</summary>
        public static HandAnalysis Analyse(PlayerHand hand, TileType? winningTile = null)
        {
            var concealed = hand.Concealed.Select(t => t.Type).ToList();
            if (winningTile.HasValue) concealed.Add(winningTile.Value);

            var expected = 14 - hand.Melds.Count * 3;
            if (concealed.Count != expected)
                return HandAnalysis.Fail($"Expected {expected} concealed tiles, got {concealed.Count}.");

            var counts = CountOf(concealed);
            var waitTiles = winningTile.HasValue ? ComputeWaits(concealed, hand.Melds.Count, winningTile.Value) : new List<TileType>();
            var wait = winningTile.HasValue && waitTiles.Count == 1
                ? DetermineWait(concealed, hand.Melds.Count, winningTile.Value)
                : WaitType.None;

            var decompositions = new List<HandDecomposition>();
            EnumerateDecompositions(counts, 4 - hand.Melds.Count, new List<HandGroup>(), decompositions);

            var special = DetectSpecialPattern(hand, counts, concealed, decompositions.Count);
            if (!special.HasValue && decompositions.Count == 0)
                return HandAnalysis.Fail("The tiles do not form four groups and a pair.");

            return HandAnalysis.Ok(special ?? WinningPattern.Standard, decompositions, waitTiles, wait);
        }

        /// <summary>All faces that turn the waiting hand into a complete one.</summary>
        public static IReadOnlyList<TileType> WaitingTiles(PlayerHand hand)
        {
            var concealed = hand.Concealed.Select(t => t.Type).ToList();
            if (concealed.Count != 13 - hand.Melds.Count * 3) return Array.Empty<TileType>();

            var result = new List<TileType>();
            foreach (var candidate in TileTypes.All)
            {
                var counts = CountOf(concealed);
                counts[candidate] = counts.GetValueOrDefault(candidate) + 1;
                if (Completes(counts, 4 - hand.Melds.Count)) result.Add(candidate);
            }
            return result;
        }

        public static bool CanComplete(PlayerHand hand, TileType tile)
        {
            var concealed = hand.Concealed.Select(t => t.Type).ToList();
            if (concealed.Count != 13 - hand.Melds.Count * 3) return false;
            var counts = CountOf(concealed);
            counts[tile] = counts.GetValueOrDefault(tile) + 1;
            if (Completes(counts, 4 - hand.Melds.Count)) return true;

            var complete = concealed.Append(tile).ToList();
            if (DetectSpecialPattern(hand, counts, complete, 0).HasValue) return true;
            return hand.Melds.All(m => !m.IsKong) && Knitted.ContainsKnittedStraight(Flatten(hand, complete));
        }

        private static bool Completes(Dictionary<TileType, int> counts, int groupsNeeded)
        {
            if (groupsNeeded < 0) return false;
            if (counts.Values.Sum() != groupsNeeded * 3 + 2) return false;
            foreach (var pair in counts.Where(p => p.Value >= 2).Select(p => p.Key).ToList())
            {
                var rest = new Dictionary<TileType, int>(counts);
                rest[pair] -= 2;
                if (TryGroups(rest, groupsNeeded, new List<HandGroup>())) return true;
            }
            return false;
        }

        private static void EnumerateDecompositions(Dictionary<TileType, int> counts, int groupsNeeded,
            List<HandGroup> acc, List<HandDecomposition> output)
        {
            if (groupsNeeded < 0) return;
            if (counts.Values.Sum() != groupsNeeded * 3 + 2) return;

            foreach (var pair in counts.Where(p => p.Value >= 2).Select(p => p.Key).ToList())
            {
                var rest = new Dictionary<TileType, int>(counts);
                rest[pair] -= 2;
                var groups = new List<HandGroup>();
                if (TryGroups(rest, groupsNeeded, groups)) output.Add(new HandDecomposition(groups, pair));
            }
        }

        private static bool TryGroups(Dictionary<TileType, int> counts, int groupsNeeded, List<HandGroup> groups)
        {
            if (groupsNeeded == 0) return counts.Values.All(v => v == 0);
            var first = FirstRemaining(counts);
            if (!first.HasValue) return false;

            var type = first.Value;
            if (counts[type] >= 3)
            {
                Change(counts, type, -3);
                groups.Add(new HandGroup(GroupKind.Pung, new[] { type, type, type }));
                if (TryGroups(counts, groupsNeeded - 1, groups)) return true;
                groups.RemoveAt(groups.Count - 1);
                Change(counts, type, 3);
            }

            if (type.IsNumbered && type.Rank <= 7)
            {
                var second = new TileType(type.Suit, type.Rank + 1);
                var third = new TileType(type.Suit, type.Rank + 2);
                if (counts.GetValueOrDefault(second) > 0 && counts.GetValueOrDefault(third) > 0)
                {
                    Change(counts, type, -1);
                    Change(counts, second, -1);
                    Change(counts, third, -1);
                    groups.Add(new HandGroup(GroupKind.Chow, new[] { type, second, third }));
                    if (TryGroups(counts, groupsNeeded - 1, groups)) return true;
                    groups.RemoveAt(groups.Count - 1);
                    Change(counts, type, 1);
                    Change(counts, second, 1);
                    Change(counts, third, 1);
                }
            }

            return false;
        }

        private static TileType? FirstRemaining(Dictionary<TileType, int> counts)
        {
            foreach (var entry in counts)
                if (entry.Value > 0) return entry.Key;
            return null;
        }

        /// <summary>
        /// Special hands that do not follow the four groups and a pair shape. They are reported as the
        /// primary pattern, but ordinary decompositions are still returned so scoring can pick the
        /// highest-paying reading (for example Pure Terminal Chows instead of Seven Pairs).
        /// </summary>
        private static WinningPattern? DetectSpecialPattern(PlayerHand hand, Dictionary<TileType, int> counts,
            IReadOnlyList<TileType> concealed, int decompositionCount)
        {
            if (hand.Melds.Count == 0)
            {
                if (IsThirteenOrphans(counts)) return WinningPattern.ThirteenOrphans;
                if (Knitted.IsKnittedTiles(counts, out _)) return WinningPattern.KnittedTiles;
                if (IsSevenShiftedPairs(counts)) return WinningPattern.SevenShiftedPairs;
                if (IsSevenPairs(counts)) return WinningPattern.SevenPairs;
            }

            if (decompositionCount == 0 && hand.Melds.All(m => !m.IsKong)
                && Knitted.ContainsKnittedStraight(Flatten(hand, concealed)))
                return WinningPattern.KnittedStraight;

            return null;
        }

        private static List<TileType> Flatten(PlayerHand hand, IEnumerable<TileType> concealed)
        {
            var all = concealed.ToList();
            foreach (var meld in hand.Melds) all.AddRange(meld.Types);
            return all;
        }

        private static List<TileType> ComputeWaits(List<TileType> completeTiles, int meldCount, TileType winningTile)
        {
            var waiting = new List<TileType>(completeTiles);
            var removed = waiting.Remove(winningTile);
            if (!removed) return new List<TileType>();

            var melds = meldCount;
            var result = new List<TileType>();
            foreach (var candidate in TileTypes.All)
            {
                var counts = CountOf(waiting);
                counts[candidate] = counts.GetValueOrDefault(candidate) + 1;
                if (Completes(counts, 4 - melds)
                    || (melds == 0 && (IsThirteenOrphans(counts) || IsSevenPairs(counts) || IsSevenShiftedPairs(counts)
                                      || Knitted.IsKnittedTiles(counts, out _)))
                    || (melds == 0 && Knitted.ContainsKnittedStraight(waiting.Append(candidate).ToList())))
                    result.Add(candidate);
            }
            return result;
        }

        private static WaitType DetermineWait(List<TileType> completeTiles, int meldCount, TileType winningTile)
        {
            var decompositions = new List<HandDecomposition>();
            EnumerateDecompositions(CountOf(completeTiles), 4 - meldCount, new List<HandGroup>(), decompositions);

            foreach (var decomposition in decompositions)
            {
                if (decomposition.Pair == winningTile) return WaitType.Single;
                if (decomposition.Groups.Any(g => g.Kind == GroupKind.Pung && g.Base == winningTile)) return WaitType.Single;
            }
            foreach (var decomposition in decompositions)
                foreach (var group in decomposition.Groups.Where(g => g.Kind == GroupKind.Chow && g.Contains(winningTile)))
                {
                    var index = group.Tiles.ToList().IndexOf(winningTile);
                    if (index == 1) return WaitType.Closed;
                    if (group.Base.Rank == 1 && winningTile.Rank == 3) return WaitType.Edge;
                    if (group.Base.Rank == 7 && winningTile.Rank == 7) return WaitType.Edge;
                }
            return WaitType.None;
        }

        private static Dictionary<TileType, int> CountOf(IEnumerable<TileType> tiles)
        {
            var counts = new Dictionary<TileType, int>();
            foreach (var tile in tiles) counts[tile] = counts.GetValueOrDefault(tile) + 1;
            return counts;
        }

        private static void Change(Dictionary<TileType, int> counts, TileType type, int delta) =>
            counts[type] = counts.GetValueOrDefault(type) + delta;

        private static bool IsSevenPairs(Dictionary<TileType, int> counts)
        {
            // MCR allows four identical tiles to count as two pairs.
            if (counts.Values.Sum() != 14) return false;
            if (counts.Values.Any(value => value % 2 != 0)) return false;
            return counts.Values.Sum(value => value / 2) == 7;
        }

        private static bool IsSevenShiftedPairs(Dictionary<TileType, int> counts)
        {
            if (!IsSevenPairs(counts)) return false;
            var types = counts.Keys.OrderBy(t => t.Rank).ToList();
            if (types.Any(t => !t.IsNumbered)) return false;
            if (types.Select(t => t.Suit).Distinct().Count() != 1) return false;
            for (var i = 1; i < types.Count; i++)
                if (types[i].Rank != types[i - 1].Rank + 1) return false;
            return true;
        }

        private static bool IsThirteenOrphans(Dictionary<TileType, int> counts)
        {
            if (counts.Values.Sum() != 14) return false;
            if (counts.Keys.Any(t => !Orphans.Contains(t))) return false;
            if (!Orphans.All(o => counts.ContainsKey(o))) return false;
            return counts.Values.Count(v => v == 2) == 1 && counts.Values.All(v => v == 1 || v == 2);
        }

        private static HashSet<TileType> BuildOrphans()
        {
            var set = new HashSet<TileType>();
            foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboo })
            {
                set.Add(new TileType(suit, 1));
                set.Add(new TileType(suit, 9));
            }
            for (var rank = 0; rank < 4; rank++) set.Add(new TileType(TileSuit.Wind, rank));
            for (var rank = 0; rank < 3; rank++) set.Add(new TileType(TileSuit.Dragon, rank));
            return set;
        }
    }
}
