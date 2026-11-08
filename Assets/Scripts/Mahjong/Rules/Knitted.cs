using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>
    /// Knitted sequences: 1-4-7, 2-5-8 and 3-6-9, one per suit.
    /// Used by Knitted Straight, Greater and Lesser Honors and Knitted Tiles.
    /// </summary>
    public static class Knitted
    {
        /// <summary>Knitted group of a numbered rank: 0 for 3-6-9, 1 for 1-4-7, 2 for 2-5-8.</summary>
        public static int GroupOf(int rank) => (rank - 1) % 3;

        /// <summary>True when every suit uses one knitted group only, and the groups differ.</summary>
        public static bool IsKnittedSet(IEnumerable<TileType> suitedTiles)
        {
            var perSuit = new Dictionary<TileSuit, int>();
            foreach (var tile in suitedTiles)
            {
                if (!tile.IsNumbered) return false;
                var group = GroupOf(tile.Rank);
                if (perSuit.TryGetValue(tile.Suit, out var existing))
                {
                    if (existing != group) return false;
                }
                else
                {
                    perSuit[tile.Suit] = group;
                }
            }
            return perSuit.Values.Distinct().Count() == perSuit.Count;
        }

        /// <summary>True for exactly the nine tiles 147 / 258 / 369 in three different suits.</summary>
        public static bool IsCompleteKnittedStraight(IReadOnlyCollection<TileType> suitedTiles)
        {
            if (suitedTiles.Count != 9) return false;
            if (!IsKnittedSet(suitedTiles)) return false;
            return suitedTiles.GroupBy(t => t.Suit).All(g => g.Count() == 3);
        }

        /// <summary>
        /// True when the 14 tiles can be read as a knitted straight plus one ordinary set and a pair.
        /// </summary>
        public static bool ContainsKnittedStraight(IEnumerable<TileType> tiles)
        {
            var list = tiles.ToList();
            if (list.Count != 14) return false;

            // Cheap pre-checks before the permutation search: the knitted part needs three numbered
            // tiles in every suit and the remaining set plus pair need at most five honors.
            var suitCounts = new Dictionary<TileSuit, int>();
            var honorCount = 0;
            foreach (var tile in list)
            {
                if (tile.IsNumbered) suitCounts[tile.Suit] = suitCounts.GetValueOrDefault(tile.Suit) + 1;
                else if (tile.IsHonor) honorCount++;
                else return false;
            }
            if (suitCounts.Count != 3 || suitCounts.Values.Any(count => count < 3)) return false;
            if (honorCount > 5) return false;

            var counts = new Dictionary<TileType, int>();
            foreach (var tile in list) counts[tile] = counts.GetValueOrDefault(tile) + 1;

            foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboo })
            foreach (var other in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboo })
            foreach (var third in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboo })
            {
                if (suit == other || other == third || suit == third) continue;
                var required = RequiredTiles(suit, other, third);
                if (required.Any(t => !counts.ContainsKey(t))) continue;

                foreach (var tile in required) counts[tile]--;
                var leftover = counts.Where(c => c.Value > 0).ToList();
                var ok = leftover.Sum(c => c.Value) == 5 && FormsSetAndPair(leftover);
                foreach (var tile in required) counts[tile]++;
                if (ok) return true;
            }

            return false;
        }

        private static List<TileType> RequiredTiles(TileSuit first, TileSuit second, TileSuit third)
        {
            var required = new List<TileType>();
            required.AddRange(new[] { 1, 4, 7 }.Select(rank => new TileType(first, rank)));
            required.AddRange(new[] { 2, 5, 8 }.Select(rank => new TileType(second, rank)));
            required.AddRange(new[] { 3, 6, 9 }.Select(rank => new TileType(third, rank)));
            return required;
        }

        private static bool FormsSetAndPair(List<KeyValuePair<TileType, int>> counts)
        {
            foreach (var pair in counts.Where(c => c.Value >= 2).Select(c => c.Key).ToList())
            {
                var rest = counts.Select(c => (c.Key, Count: c.Key == pair ? c.Value - 2 : c.Value)).ToList();
                if (rest.All(c => c.Count == 0)) continue;

                if (rest.Any(c => c.Count == 3 && rest.Sum(x => x.Count) == 3)) return true;

                foreach (var start in rest.Where(c => c.Count > 0 && c.Key.IsNumbered && c.Key.Rank <= 7))
                {
                    var second = new TileType(start.Key.Suit, start.Key.Rank + 1);
                    var third = new TileType(start.Key.Suit, start.Key.Rank + 2);
                    var hasSecond = rest.Any(c => c.Key == second && c.Count > 0);
                    var hasThird = rest.Any(c => c.Key == third && c.Count > 0);
                    if (hasSecond && hasThird) return true;
                }
            }
            return false;
        }

        /// <summary>A hand of fourteen distinct tiles: honors plus one knitted pattern.</summary>
        public static bool IsKnittedTiles(IReadOnlyDictionary<TileType, int> counts, out int honorCount)
        {
            honorCount = 0;
            if (counts.Values.Sum() != 14) return false;
            if (counts.Values.Any(value => value != 1)) return false;

            var suited = counts.Keys.Where(t => t.IsNumbered).ToList();
            var honors = counts.Keys.Where(t => t.IsHonor).ToList();
            if (counts.Keys.Any(t => t.IsFlower)) return false;
            if (suited.Count + honors.Count != 14) return false;
            if (!IsKnittedSet(suited)) return false;

            honorCount = honors.Count;
            return honorCount >= 5;
        }
    }
}
