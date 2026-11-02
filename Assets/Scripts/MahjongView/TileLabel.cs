using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>Compact text and colour for a tile face. No art assets required.</summary>
    public static class TileLabel
    {
        public static string Short(TileType type)
        {
            return type.Suit switch
            {
                TileSuit.Characters => type.Rank + "c",
                TileSuit.Dots => type.Rank + "d",
                TileSuit.Bamboo => type.Rank + "b",
                TileSuit.Wind => ((Wind)type.Rank) switch
                {
                    Wind.East => "E",
                    Wind.South => "S",
                    Wind.West => "W",
                    _ => "N"
                },
                TileSuit.Dragon => ((Dragon)type.Rank) switch
                {
                    Dragon.Red => "Rd",
                    Dragon.Green => "Gd",
                    _ => "Wd"
                },
                _ => "Fl"
            };
        }

        public static Color Face(TileType type)
        {
            return type.Suit switch
            {
                TileSuit.Characters => new Color(0.95f, 0.93f, 0.85f),
                TileSuit.Dots => new Color(0.93f, 0.96f, 0.98f),
                TileSuit.Bamboo => new Color(0.90f, 0.97f, 0.90f),
                TileSuit.Wind => new Color(0.97f, 0.95f, 0.99f),
                TileSuit.Dragon => new Color(0.99f, 0.94f, 0.92f),
                _ => new Color(0.98f, 0.90f, 0.96f)
            };
        }

        public static Color Text(TileType type)
        {
            if (type.IsWind) return new Color(0.15f, 0.2f, 0.5f);
            if (type.Suit == TileSuit.Dragon && type.Rank == (int)Dragon.Green) return new Color(0.1f, 0.45f, 0.15f);
            if (type.Suit == TileSuit.Dragon && type.Rank == (int)Dragon.Red) return new Color(0.6f, 0.1f, 0.1f);
            return new Color(0.12f, 0.12f, 0.12f);
        }
    }
}
