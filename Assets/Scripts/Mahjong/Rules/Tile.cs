namespace Mahjong.Rules
{
    public enum TileSuit { Characters, Dots, Bamboo, Wind, Dragon, Flower }
    public enum Wind { East, South, West, North }
    public enum Dragon { Red, Green, White }
    public enum Flower { Spring, Summer, Autumn, Winter, Plum, Orchid, Bamboo, Chrysanthemum }

    public readonly record struct TileType(TileSuit Suit, int Rank)
    {
        public bool IsNumbered => Suit is TileSuit.Characters or TileSuit.Dots or TileSuit.Bamboo;
        public bool IsHonor => Suit is TileSuit.Wind or TileSuit.Dragon;
        public bool IsTerminal => IsNumbered && (Rank == 1 || Rank == 9);
        public bool IsTerminalOrHonor => IsTerminal || IsHonor;
        public override string ToString() => Suit switch
        {
            TileSuit.Characters => $"Characters {Rank}",
            TileSuit.Dots => $"Dots {Rank}",
            TileSuit.Bamboo => $"Bamboo {Rank}",
            TileSuit.Wind => $"Wind {(Wind)Rank}",
            TileSuit.Dragon => $"Dragon {(Dragon)Rank}",
            TileSuit.Flower => $"Flower {(Flower)Rank}",
            _ => "Unknown"
        };
    }

    public readonly record struct Tile(TileType Type, int InstanceId)
    {
        public bool IsFlower => Type.Suit == TileSuit.Flower;
        public override string ToString() => $"{Type}#{InstanceId}";
    }
}
