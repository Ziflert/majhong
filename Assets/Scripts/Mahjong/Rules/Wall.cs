using System;
using System.Collections.Generic;

namespace Mahjong.Rules
{
    /// <summary>
    /// The Great Wall. Live tiles are drawn from the break point forward (clockwise),
    /// flower and kong replacements from the far end, as in rules 3.4.20 and 3.6.8.
    /// The wall is exhausted when the two ends meet.
    /// </summary>
    public sealed class Wall
    {
        private readonly List<Tile> tiles;
        private int liveCursor;
        private int tailCursor;

        private Wall(List<Tile> tiles)
        {
            this.tiles = tiles;
            liveCursor = 0;
            tailCursor = tiles.Count - 1;
        }

        public int TotalTiles => tiles.Count;
        public int Remaining => Math.Max(0, tailCursor - liveCursor + 1);
        public bool IsExhausted => Remaining == 0;
        public bool IsLastLiveTile => Remaining == 1;
        public int BreakOffset { get; private set; }

        public static Wall CreateShuffled(Random random)
        {
            var tiles = new List<Tile>(144);
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

            return new Wall(tiles);
        }

        /// <summary>Break the wall <paramref name="stacks"/> stacks from the right end of the thrower's wall.</summary>
        public void Break(int stacks)
        {
            var offset = Mod(stacks, tiles.Count);
            BreakOffset = offset;
            liveCursor = offset;
            tailCursor = offset - 1 + tiles.Count;
        }

        public Tile DrawLive()
        {
            if (IsExhausted) throw new InvalidOperationException("The wall is exhausted.");
            return At(liveCursor++);
        }

        public Tile DrawReplacement()
        {
            if (IsExhausted) throw new InvalidOperationException("The wall is exhausted.");
            return At(tailCursor--);
        }

        private Tile At(int absolute) => tiles[Mod(absolute, tiles.Count)];

        private static int Mod(int value, int m)
        {
            var result = value % m;
            return result < 0 ? result + m : result;
        }
    }
}
