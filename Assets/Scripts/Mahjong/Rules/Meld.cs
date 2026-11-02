using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    public enum MeldKind { Chow, Pung, OpenKong, AddedKong, ClosedKong }
    public enum ClaimSource { SelfDraw, Discard, FlowerReplacement, KongReplacement }

    /// <summary>An exposed or declared combination.</summary>
    public sealed class Meld
    {
        public Meld(MeldKind kind, IReadOnlyList<Tile> tiles, int? claimedFromPlayer = null, int? addedToMeldIndex = null)
        {
            Kind = kind;
            Tiles = tiles;
            ClaimedFromPlayer = claimedFromPlayer;
            AddedToMeldIndex = addedToMeldIndex;
        }

        public MeldKind Kind { get; }
        public IReadOnlyList<Tile> Tiles { get; }
        public int? ClaimedFromPlayer { get; }
        /// <summary>For an added kong: index of the pung it was upgraded from.</summary>
        public int? AddedToMeldIndex { get; }

        public TileType BaseType => Tiles[0].Type;
        public bool IsKong => Kind is MeldKind.OpenKong or MeldKind.AddedKong or MeldKind.ClosedKong;
        public bool IsOpen => Kind is MeldKind.Chow or MeldKind.Pung or MeldKind.OpenKong or MeldKind.AddedKong;
        public bool IsConcealed => Kind == MeldKind.ClosedKong;
        public int TileCount => IsKong ? 4 : 3;
        public IReadOnlyList<TileType> Types => Tiles.Select(t => t.Type).ToList();
    }
}
