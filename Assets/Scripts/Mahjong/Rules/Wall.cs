using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    public sealed class Wall
    {
        private readonly List<Tile> live = new();
        private readonly List<Tile> replacement = new();

        public int RemainingLive => live.Count;
        public int RemainingReplacement => replacement.Count;
        public bool IsExhausted => live.Count == 0;

        public static Wall CreateShuffled(Random random)
        {
            var tiles = new List<Tile>(144);
            var id = 0;
            foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboo })
                for (var rank = 1; rank <= 9; rank++)
                    for (var copy = 0; copy < 4; copy++) tiles.Add(new Tile(new TileType(suit, rank), id++));
            for (var rank = 0; rank < 4; rank++)
                for (var copy = 0; copy < 4; copy++) tiles.Add(new Tile(new TileType(TileSuit.Wind, rank), id++));
            for (var rank = 0; rank < 3; rank++)
                for (var copy = 0; copy < 4; copy++) tiles.Add(new Tile(new TileType(TileSuit.Dragon, rank), id++));
            for (var rank = 0; rank < 8; rank++) tiles.Add(new Tile(new TileType(TileSuit.Flower, rank), id++));

            for (var i = tiles.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
            }

            // The final 16 tiles are replacement tiles for flowers and kongs.
            var wall = new Wall();
            wall.replacement.AddRange(tiles.TakeLast(16));
            wall.live.AddRange(tiles.Take(128));
            return wall;
        }

        public Tile DrawLive()
        {
            if (live.Count == 0) throw new InvalidOperationException("The live wall is exhausted.");
            var tile = live[0];
            live.RemoveAt(0);
            return tile;
        }

        public Tile DrawReplacement()
        {
            if (replacement.Count == 0) throw new InvalidOperationException("No replacement tiles remain.");
            var index = replacement.Count - 1;
            var tile = replacement[index];
            replacement.RemoveAt(index);
            return tile;
        }
    }
}
