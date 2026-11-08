using System;
using System.Collections.Generic;
using System.Globalization;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>
    /// Text form of a tile, the same one the project documentation and the logs use: <c>5c</c>,
    /// <c>1d</c>, <c>9b</c>, <c>E</c>, <c>S</c>, <c>W</c>, <c>N</c>, <c>Rd</c>, <c>Gd</c>, <c>Wd</c>,
    /// flowers <c>Fl1</c>..<c>Fl8</c>.
    ///
    /// Lives in the network layer because a move on the wire names tiles, and a replay log has to stay
    /// readable. Parsing also accepts the long spellings (<c>East</c>, <c>Red</c>, <c>Spring</c>), so a
    /// hand written in a test or in a bug report can be loaded without translating it first.
    /// </summary>
    public static class TileNotation
    {
        /// <summary>Short code of a face: the wire form and the log form.</summary>
        public static string Format(TileType type) => type.Suit switch
        {
            TileSuit.Characters => $"{type.Rank}c",
            TileSuit.Dots => $"{type.Rank}d",
            TileSuit.Bamboo => $"{type.Rank}b",
            TileSuit.Wind => type.Rank switch
            {
                (int)Wind.East => "E",
                (int)Wind.South => "S",
                (int)Wind.West => "W",
                _ => "N"
            },
            TileSuit.Dragon => type.Rank switch
            {
                (int)Dragon.Red => "Rd",
                (int)Dragon.Green => "Gd",
                _ => "Wd"
            },
            TileSuit.Flower => $"Fl{type.Rank + 1}",
            _ => "?"
        };

        /// <summary>A physical tile with its identity: <c>5c#17</c>.</summary>
        public static string Format(Tile tile) => $"{Format(tile.Type)}#{tile.InstanceId}";

        /// <summary>Reads a physical tile: <c>5c#17</c>. A face without an identity is not a tile.</summary>
        public static bool TryParseTile(string? text, out Tile tile)
        {
            tile = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var raw = text.Trim();
            var hash = raw.IndexOf('#');
            if (hash <= 0 || hash == raw.Length - 1) return false;
            if (!TryParse(raw.Substring(0, hash), out var type)) return false;
            if (!int.TryParse(raw.Substring(hash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                return false;

            tile = new Tile(type, id);
            return true;
        }

        public static Tile ParseTile(string text) =>
            TryParseTile(text, out var tile) ? tile : throw new FormatException($"Not a tile: {text}");

        public static string Format(IEnumerable<Tile> tiles) => string.Join(",", FormatEach(tiles));

        private static IEnumerable<string> FormatEach(IEnumerable<Tile> tiles)
        {
            foreach (var tile in tiles) yield return Format(tile);
        }

        public static bool TryParse(string? text, out TileType type)
        {
            type = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var raw = text.Trim();
            var lower = raw.ToLowerInvariant();

            // Flowers: Fl1..Fl8, F1..F8, or by name.
            if (lower.StartsWith("fl") || lower.StartsWith('f'))
            {
                var digits = lower.StartsWith("fl") ? lower.Substring(2) : lower.Substring(1);
                if (int.TryParse(digits, out var flower) && flower is >= 1 and <= 8)
                {
                    type = new TileType(TileSuit.Flower, flower - 1);
                    return true;
                }
            }

            if (TryParseWord(lower, out type)) return true;

            // Numbered tiles: a rank 1..9 followed by a suit letter (c/m, d/p, b/s of other notations).
            if (raw.Length >= 2 && char.IsDigit(raw[0]))
            {
                var rank = raw[0] - '0';
                if (raw.Length > 2 && char.IsDigit(raw[1])) return false;
                if (rank is < 1 or > 9) return false;

                switch (char.ToLowerInvariant(raw[^1]))
                {
                    case 'c':
                    case 'm':
                        type = new TileType(TileSuit.Characters, rank);
                        return true;
                    case 'd':
                    case 'p':
                        type = new TileType(TileSuit.Dots, rank);
                        return true;
                    case 'b':
                    case 's':
                        type = new TileType(TileSuit.Bamboo, rank);
                        return true;
                    default:
                        return false;
                }
            }

            return false;
        }

        public static TileType Parse(string text) =>
            TryParse(text, out var type) ? type : throw new FormatException($"Not a tile: {text}");

        /// <summary>Parses a comma separated list: <c>3c,4c</c>.</summary>
        public static IReadOnlyList<TileType> ParseList(string? text)
        {
            var result = new List<TileType>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (var part in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryParse(part, out var type)) result.Add(type);
                else throw new FormatException($"Not a tile: {part}");
            }

            return result;
        }

        /// <summary>Every face of the set in a stable order; used by tests and by the free-play reset.</summary>
        public static IReadOnlyList<TileType> AllFaces { get; } = BuildFaces();

        private static List<TileType> BuildFaces()
        {
            var faces = new List<TileType>(TileTypes.All);
            for (var flower = 0; flower < 8; flower++) faces.Add(new TileType(TileSuit.Flower, flower));
            return faces;
        }

        private static bool TryParseWord(string lower, out TileType type)
        {
            switch (lower)
            {
                case "e" or "east" or "восток" or "в":
                    type = TileType.WindTile(Wind.East);
                    return true;
                case "s" or "south" or "юг" or "ю":
                    type = TileType.WindTile(Wind.South);
                    return true;
                case "w" or "west" or "запад" or "з":
                    type = TileType.WindTile(Wind.West);
                    return true;
                case "n" or "north" or "север" or "с":
                    type = TileType.WindTile(Wind.North);
                    return true;
                case "rd" or "red" or "reddragon" or "dragonred" or "красный":
                    type = TileType.DragonTile(Dragon.Red);
                    return true;
                case "gd" or "green" or "greendragon" or "dragongreen" or "зелёный" or "зеленый":
                    type = TileType.DragonTile(Dragon.Green);
                    return true;
                case "wd" or "white" or "whitedragon" or "dragonwhite" or "белый":
                    type = TileType.DragonTile(Dragon.White);
                    return true;
                case "spring" or "весна":
                    type = new TileType(TileSuit.Flower, (int)FlowerKind.Spring);
                    return true;
                case "summer" or "лето":
                    type = new TileType(TileSuit.Flower, (int)FlowerKind.Summer);
                    return true;
                case "autumn" or "осень":
                    type = new TileType(TileSuit.Flower, (int)FlowerKind.Autumn);
                    return true;
                case "winter" or "зима":
                    type = new TileType(TileSuit.Flower, (int)FlowerKind.Winter);
                    return true;
                case "plum" or "слива":
                    type = new TileType(TileSuit.Flower, (int)FlowerKind.Plum);
                    return true;
                case "orchid" or "орхидея":
                    type = new TileType(TileSuit.Flower, (int)FlowerKind.Orchid);
                    return true;
                case "chrysanthemum" or "хризантема":
                    type = new TileType(TileSuit.Flower, (int)FlowerKind.Chrysanthemum);
                    return true;
                default:
                    type = default;
                    return false;
            }
        }
    }
}
