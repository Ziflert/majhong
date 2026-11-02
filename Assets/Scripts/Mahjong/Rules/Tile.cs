using System;
using System.Collections.Generic;

namespace Mahjong.Rules
{
    public enum TileSuit { Characters, Dots, Bamboo, Wind, Dragon, Flower }
    public enum Wind { East = 0, South = 1, West = 2, North = 3 }
    public enum Dragon { Red = 0, Green = 1, White = 2 }
    public enum FlowerKind { Spring = 0, Summer = 1, Autumn = 2, Winter = 3, Plum = 4, Orchid = 5, Bamboo = 6, Chrysanthemum = 7 }

    /// <summary>A tile face. Unity-independent, value type with structural equality.</summary>
    public readonly struct TileType : IEquatable<TileType>
    {
        public TileType(TileSuit suit, int rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public TileSuit Suit { get; }
        public int Rank { get; }

        public bool IsNumbered => Suit is TileSuit.Characters or TileSuit.Dots or TileSuit.Bamboo;
        public bool IsWind => Suit == TileSuit.Wind;
        public bool IsDragon => Suit == TileSuit.Dragon;
        public bool IsHonor => IsWind || IsDragon;
        public bool IsFlower => Suit == TileSuit.Flower;
        public bool IsTerminal => IsNumbered && (Rank == 1 || Rank == 9);
        public bool IsTerminalOrHonor => IsTerminal || IsHonor;
        public bool IsSimple => IsNumbered && Rank >= 2 && Rank <= 8;

        public Wind AsWind => (Wind)Rank;
        public Dragon AsDragon => (Dragon)Rank;

        /// <summary>Tiles made only of 2, 3, 4, 6, 8 bamboo and the green dragon.</summary>
        public bool IsGreen => (Suit == TileSuit.Bamboo && (Rank == 2 || Rank == 3 || Rank == 4 || Rank == 6 || Rank == 8))
                               || (Suit == TileSuit.Dragon && Rank == (int)Dragon.Green);

        /// <summary>Tiles whose face looks the same when rotated by 180 degrees.</summary>
        public bool IsReversible => Suit switch
        {
            TileSuit.Dots => Rank is 1 or 2 or 3 or 4 or 5 or 8 or 9,
            TileSuit.Bamboo => Rank is 2 or 4 or 5 or 6 or 8 or 9,
            TileSuit.Dragon => Rank == (int)Dragon.White,
            _ => false
        };

        public static TileType WindTile(Wind wind) => new(TileSuit.Wind, (int)wind);
        public static TileType DragonTile(Dragon dragon) => new(TileSuit.Dragon, (int)dragon);

        public bool Equals(TileType other) => Suit == other.Suit && Rank == other.Rank;
        public override bool Equals(object? obj) => obj is TileType other && Equals(other);
        public override int GetHashCode() => ((int)Suit * 397) ^ Rank;
        public static bool operator ==(TileType left, TileType right) => left.Equals(right);
        public static bool operator !=(TileType left, TileType right) => !left.Equals(right);

        public override string ToString() => Suit switch
        {
            TileSuit.Characters => $"{Rank}c",
            TileSuit.Dots => $"{Rank}d",
            TileSuit.Bamboo => $"{Rank}b",
            TileSuit.Wind => ((Wind)Rank).ToString(),
            TileSuit.Dragon => ((Dragon)Rank).ToString(),
            TileSuit.Flower => ((FlowerKind)Rank).ToString(),
            _ => "?"
        };
    }

    /// <summary>A physical tile: a face plus a unique identifier inside one hand.</summary>
    public readonly struct Tile : IEquatable<Tile>
    {
        public Tile(TileType type, int instanceId)
        {
            Type = type;
            InstanceId = instanceId;
        }

        public TileType Type { get; }
        public int InstanceId { get; }
        public bool IsFlower => Type.Suit == TileSuit.Flower;

        public bool Equals(Tile other) => InstanceId == other.InstanceId && Type.Equals(other.Type);
        public override bool Equals(object? obj) => obj is Tile other && Equals(other);
        public override int GetHashCode() => (Type.GetHashCode() * 397) ^ InstanceId;
        public static bool operator ==(Tile left, Tile right) => left.Equals(right);
        public static bool operator !=(Tile left, Tile right) => !left.Equals(right);

        public override string ToString() => $"{Type}#{InstanceId}";
    }

    public static class TileTypes
    {
        /// <summary>All 34 tile faces that can be part of a hand.</summary>
        public static IReadOnlyList<TileType> All { get; } = BuildAll();

        private static List<TileType> BuildAll()
        {
            var all = new List<TileType>(34);
            foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboo })
                for (var rank = 1; rank <= 9; rank++)
                    all.Add(new TileType(suit, rank));
            for (var rank = 0; rank < 4; rank++) all.Add(new TileType(TileSuit.Wind, rank));
            for (var rank = 0; rank < 3; rank++) all.Add(new TileType(TileSuit.Dragon, rank));
            return all;
        }
    }
}
