using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    public enum MeldKind { Chow, Pung, OpenKong, ClosedKong }
    public enum ClaimSource { SelfDraw, Discard, FlowerReplacement, KongReplacement }

    public sealed record Meld(MeldKind Kind, IReadOnlyList<Tile> Tiles, int? ClaimedFromPlayer = null)
    {
        public TileType BaseType => Tiles[0].Type;
        public bool IsOpen => Kind is MeldKind.Chow or MeldKind.Pung or MeldKind.OpenKong;
        public int TileCount => Kind is MeldKind.OpenKong or MeldKind.ClosedKong ? 4 : 3;
    }

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
        public bool IsClosed => melds.All(m => !m.IsOpen);

        public void AddConcealed(Tile tile) => concealed.Add(tile);
        public bool RemoveConcealed(Tile tile) => concealed.Remove(tile);
        public void AddMeld(Meld meld) => melds.Add(meld);
        public void AddFlower(Tile tile) => flowers.Add(tile);
        public void AddDiscard(Tile tile) => discards.Add(tile);
        public void SortConcealed() => concealed.Sort((a, b) =>
            string.Compare(a.Type.ToString(), b.Type.ToString(), StringComparison.Ordinal));
    }
}
