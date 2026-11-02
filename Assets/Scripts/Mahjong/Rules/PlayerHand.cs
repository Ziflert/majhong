using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>Everything one player owns during a hand.</summary>
    public sealed class PlayerHand
    {
        private readonly List<Tile> concealed = new();
        private readonly List<Meld> melds = new();
        private readonly List<Tile> flowers = new();
        private readonly List<Tile> discards = new();

        public IReadOnlyList<Tile> Concealed => concealed;
        public IReadOnlyList<Meld> Melds => melds;
        public IReadOnlyList<Tile> Flowers => flowers;
        public IReadOnlyList<Tile> Discards => discards;

        public bool HasOpenMelds => melds.Any(m => m.IsOpen);
        public bool HasKong => melds.Any(m => m.IsKong);

        /// <summary>Hand size counted the official way: a kong counts as three tiles.</summary>
        public int HandSize => concealed.Count + melds.Count * 3;

        public void AddConcealed(Tile tile) => concealed.Add(tile);
        public bool RemoveConcealed(Tile tile) => concealed.Remove(tile);

        public bool RemoveConcealed(IEnumerable<Tile> tiles)
        {
            var selected = tiles.ToList();
            if (selected.Any(tile => !concealed.Contains(tile))) return false;
            foreach (var tile in selected) concealed.Remove(tile);
            return true;
        }

        public void AddMeld(Meld meld) => melds.Add(meld);
        public void ReplaceMeld(int index, Meld meld) => melds[index] = meld;
        public void AddFlower(Tile tile) => flowers.Add(tile);
        public void AddDiscard(Tile tile) => discards.Add(tile);
        public bool RemoveLastDiscard(Tile tile)
        {
            if (discards.Count == 0 || discards[discards.Count - 1] != tile) return false;
            discards.RemoveAt(discards.Count - 1);
            return true;
        }

        public List<Tile> TakeConcealedFlowers()
        {
            var found = concealed.Where(t => t.IsFlower).ToList();
            foreach (var flower in found) concealed.Remove(flower);
            return found;
        }

        public IEnumerable<TileType> AllTypes()
        {
            foreach (var tile in concealed) yield return tile.Type;
            foreach (var meld in melds)
                foreach (var tile in meld.Tiles) yield return tile.Type;
        }

        public IEnumerable<Tile> AllTiles()
        {
            foreach (var tile in concealed) yield return tile;
            foreach (var meld in melds)
                foreach (var tile in meld.Tiles) yield return tile;
        }

        public void SortConcealed() => concealed.Sort((a, b) => Compare(a.Type, b.Type));

        private static int Compare(TileType a, TileType b)
        {
            if (a.Suit != b.Suit) return a.Suit.CompareTo(b.Suit);
            return a.Rank.CompareTo(b.Rank);
        }
    }
}
