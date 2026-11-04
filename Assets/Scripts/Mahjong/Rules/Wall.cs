using System;
using System.Collections.Generic;

namespace Mahjong.Rules
{
    /// <summary>
    /// The Great Wall: 144 tiles standing as four walls of 18 two-tile stacks (3.5.7.4), dealt as
    /// one closed ring. Live tiles are drawn clockwise from the break point, flowers and kong
    /// replacements from the far end, as in rules 3.4.20 and 3.6.8. The wall is exhausted when the
    /// two ends meet.
    ///
    /// The classic rules of <c>rule-2.pdf</c> leave a dead end: the hand stops while fourteen tiles are
    /// still standing, and the two tiles lying on top of them are the free tiles that pay for flowers
    /// and kongs. Both numbers come from the profile (<see cref="RuleVariant.DeadWallTiles"/> and
    /// <see cref="RuleVariant.FreeTiles"/>); with zero and zero — the sporting profile — nothing changes
    /// and the wall is played to its last tile.
    ///
    /// Tiles are kept in ring order — <c>tiles[0]</c> stands at ring position 0 — so the table can be
    /// laid out from the wall itself: a tile keeps its stack and level until somebody takes it, and
    /// whatever nobody took is still standing when the hand ends.
    /// </summary>
    public sealed class Wall
    {
        private readonly List<Tile> tiles;
        private readonly Dictionary<int, int> ringOfTile = new();
        private int liveIndex;
        private int tailIndex;
        private int remaining;

        private Wall(List<Tile> tiles, int deadWallTiles, int freeTiles)
        {
            this.tiles = tiles;
            for (var ring = 0; ring < tiles.Count; ring++) ringOfTile[tiles[ring].InstanceId] = ring;
            liveIndex = 0;
            tailIndex = tiles.Count - 1;
            remaining = tiles.Count;
            DeadWallTiles = deadWallTiles;
            FreeTiles = freeTiles;
        }

        public int TotalTiles => tiles.Count;
        public int Remaining => remaining;

        /// <summary>Tiles at the far end that are never drawn: fourteen in the classic rules, none in MCR.</summary>
        public int DeadWallTiles { get; }

        /// <summary>Tiles lying on the wall as free tiles: they pay for flowers and kongs before the wall does.</summary>
        public int FreeTiles { get; }

        /// <summary>Tiles still standing at the far end of the wall for any reason: the dead end plus the free pair.</summary>
        public int ReservedTiles => DeadWallTiles + FreeTiles;

        /// <summary>How many more tiles may still be taken from the live wall.</summary>
        public int DrawableTiles => remaining > ReservedTiles ? remaining - ReservedTiles : 0;

        /// <summary>
        /// True when no live tile may be drawn any more: the wall is empty in MCR, and down to its dead
        /// end in classic. Rule 3.6.9 hangs off this — after it only Hu may be declared.
        /// </summary>
        public bool IsExhausted => remaining <= ReservedTiles;

        /// <summary>True while a flower or a kong can still be paid for with a replacement tile.</summary>
        public bool CanReplace => remaining > DeadWallTiles;

        /// <summary>
        /// True when the tile behind the last draw was the last one that could be taken live, so the
        /// hand is played out (fan 44, and the classic doubling for the last tile).
        /// </summary>
        public bool IsLastLiveTile => remaining == ReservedTiles + 1;

        /// <summary>Ring position where the live wall starts; the table draws the break marker there.</summary>
        public int BreakIndex { get; private set; }

        /// <summary>Ring position of the next live tile to be taken.</summary>
        public int LiveIndex => liveIndex;

        /// <summary>Ring position of the next replacement tile; replacements walk back from the break.</summary>
        public int TailIndex => tailIndex;

        /// <summary>Every tile in ring order, standing as it was built. The table lays out the wall from it.</summary>
        public IReadOnlyList<Tile> TilesInRingOrder => tiles;

        /// <summary>
        /// The free tiles as they lie on the wall right now: the two tiles at the far end of the part
        /// nobody has taken. When both are spent, the next two at that end take their place, which is
        /// exactly what the classic rules describe. Empty for a profile without free tiles.
        /// </summary>
        public IReadOnlyList<(int Ring, Tile Tile)> FreeTilePair()
        {
            var pair = new List<(int, Tile)>();
            if (FreeTiles == 0) return pair;
            for (var step = 0; step < FreeTiles; step++)
            {
                var ring = Mod(tailIndex - step, tiles.Count);
                if (remaining <= DeadWallTiles + step) break;
                pair.Add((ring, tiles[ring]));
            }
            return pair;
        }

        public static Wall CreateShuffled(Random random, int deadWallTiles = 0, int freeTiles = 0)
        {
            var tiles = new List<Tile>(WallGeometry.TileCount);
            var id = 0;
            foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboo })
                for (var rank = 1; rank <= 9; rank++)
                    for (var copy = 0; copy < 4; copy++)
                        tiles.Add(new Tile(new TileType(suit, rank), id++));
            for (var rank = 0; rank < 4; rank++)
                for (var copy = 0; copy < 4; copy++)
                    tiles.Add(new Tile(new TileType(TileSuit.Wind, rank), id++));
            for (var rank = 0; rank < 3; rank++)
                for (var copy = 0; copy < 4; copy++)
                    tiles.Add(new Tile(new TileType(TileSuit.Dragon, rank), id++));
            for (var rank = 0; rank < 8; rank++)
                tiles.Add(new Tile(new TileType(TileSuit.Flower, rank), id++));

            for (var i = tiles.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
            }

            return new Wall(tiles, deadWallTiles, freeTiles);
        }

        /// <summary>
        /// Rule 3.5.7.5: <paramref name="stacks"/> counted from the right end of the second thrower's
        /// wall; the live wall starts with the tile after that stack.
        /// </summary>
        public void Break(int thrower, int stacks)
        {
            BreakIndex = WallGeometry.BreakIndex(thrower, stacks);
            liveIndex = BreakIndex;
            tailIndex = Prev(BreakIndex);
            remaining = tiles.Count;
        }
        /// <summary>Ring position of a tile; the table needs it to place the tile and to take it back out.</summary>
        public int? RingOf(Tile tile) =>
            ringOfTile.TryGetValue(tile.InstanceId, out var ring) ? ring : null;

        /// <summary>Where a tile stands in the wall; the table places it by this slot.</summary>
        public WallSlot? SlotOf(Tile tile) =>
            ringOfTile.TryGetValue(tile.InstanceId, out var ring) ? WallGeometry.SlotAt(ring) : null;

        /// <summary>True while the tile is still standing in the wall, as opposed to lying in a hand.</summary>
        public bool IsStanding(Tile tile) => ringOfTile.TryGetValue(tile.InstanceId, out var ring) && IsInside(ring);

        public Tile DrawLive()
        {
            if (IsExhausted) throw new InvalidOperationException("The wall is exhausted.");
            var tile = tiles[liveIndex];
            liveIndex = Next(liveIndex);
            remaining--;
            return tile;
        }

        public Tile DrawReplacement()
        {
            if (!CanReplace) throw new InvalidOperationException("The wall is down to its dead end.");
            var tile = tiles[tailIndex];
            tailIndex = Prev(tailIndex);
            remaining--;
            return tile;
        }

        /// <summary>Whether a ring position is still inside the part of the wall nobody has taken.</summary>
        private bool IsInside(int ring)
        {
            // The standing part runs from liveIndex forward to tailIndex, wrapping at the ring seam.
            var fromLive = (ring - liveIndex + tiles.Count) % tiles.Count;
            var standing = (tailIndex - liveIndex + tiles.Count) % tiles.Count + 1;
            return fromLive < standing;
        }

        private int Next(int index) => (index + 1) % tiles.Count;

        private int Prev(int index) => (index - 1 + tiles.Count) % tiles.Count;

        private static int Mod(int value, int m)
        {
            var result = value % m;
            return result < 0 ? result + m : result;
        }
    }
}
