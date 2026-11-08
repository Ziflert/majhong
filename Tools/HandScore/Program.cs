using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Rules;

namespace Mahjong.HandScore
{
    /// <summary>
    /// Scores one hand from a text notation, so a hand can be compared with an outside MCR calculator
    /// instead of only with the self-test.
    ///
    ///   dotnet run --project Tools/HandScore -- "123m 456m 789m 11p 55p" --win 5p --self-draw
    ///
    /// Hand notation: letters m (characters), p (dots), s (bamboo), z (honours, 1-4 winds,
    /// 5 red dragon, 6 green dragon, 7 white dragon). Each group of digits is followed by its suit
    /// letter, so "123m456m" is two chows. A meld is written in square brackets: "[123p]" is a
    /// declared chow, "[555s]" a pung, "[555s]k" an open kong, "[555s]K" a concealed kong.
    /// The hand may also be given as space-separated groups.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] is "-h" or "--help")
            {
                PrintUsage();
                return args.Length == 0 ? 1 : 0;
            }

            var hand = new PlayerHand();
            var ids = new IdSource();
            TileType? winning = null;
            var selfDraw = false;
            var lastTileOfWall = false;
            var lastDiscard = false;
            var robbedKong = false;
            var round = Wind.East;
            var seat = Wind.East;
            var flowers = 0;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--win":
                        winning = ParseTile(args[++i]);
                        continue;
                    case "--self-draw":
                        selfDraw = true;
                        continue;
                    case "--last-tile-of-wall":
                        lastTileOfWall = true;
                        continue;
                    case "--last-discard":
                        lastDiscard = true;
                        continue;
                    case "--robbed-kong":
                        robbedKong = true;
                        continue;
                    case "--round":
                        round = ParseWind(args[++i]);
                        continue;
                    case "--seat":
                        seat = ParseWind(args[++i]);
                        continue;
                    case "--flowers":
                        flowers = int.Parse(args[++i]);
                        continue;
                }

                if (arg.StartsWith("--")) throw new ArgumentException($"Unknown option {arg}.");
                AddNotation(hand, arg, ids);
            }

            for (var f = 0; f < flowers; f++)
                hand.AddFlower(new Tile(new TileType(TileSuit.Flower, f), ids.Next()));

            var context = new ScoringContext
            {
                Hand = hand,
                WinningTile = winning,
                Source = selfDraw ? ClaimSource.SelfDraw : ClaimSource.Discard,
                RoundWind = round,
                SeatWind = seat,
                LastTileOfWall = lastTileOfWall,
                LastDiscard = lastDiscard,
                RobbedKong = robbedKong,
            };

            var result = Scoring.Evaluate(context);
            Console.WriteLine($"hand      : {Describe(hand)}");
            Console.WriteLine($"winning   : {(winning.HasValue ? TileName(winning.Value) : "-")} ({(selfDraw ? "self-draw" : "discard")})");
            Console.WriteLine($"valid win : {(result.IsValidWin ? "yes" : "no")}" + (result.Error == null ? "" : $" - {result.Error}"));
            Console.WriteLine($"hand fan  : {result.HandPoints}");
            Console.WriteLine($"total     : {result.TotalPoints}");
            Console.WriteLine("fans      :");
            foreach (var fan in result.Fans.OrderByDescending(f => f.Fan.Points))
                Console.WriteLine($"  {fan.Fan.Points,3}  {fan.Fan.NameEn}  ({fan.Evidence})");

            return result.IsValidWin ? 0 : 2;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: dotnet run --project Tools/HandScore -- \"<hand>\" [options]");
            Console.WriteLine("  hand        e.g. \"123m456m789m11p55p\" or \"[123p] 55s 777s 999s 11z\"");
            Console.WriteLine("  --win T     winning tile, e.g. 5p");
            Console.WriteLine("  --self-draw won from the wall");
            Console.WriteLine("  --last-tile-of-wall, --last-discard, --robbed-kong");
            Console.WriteLine("  --round E|S|W|N, --seat E|S|W|N, --flowers N");
        }

        private static Wind ParseWind(string text) => text.ToUpperInvariant() switch
        {
            "E" or "EAST" => Wind.East,
            "S" or "SOUTH" => Wind.South,
            "W" or "WEST" => Wind.West,
            "N" or "NORTH" => Wind.North,
            _ => throw new ArgumentException($"Unknown wind {text}."),
        };

        /// <summary>Mutable counter of tile instance ids; a ref parameter cannot be used in a lambda.</summary>
        private sealed class IdSource
        {
            private int value;
            public int Next() => value++;
        }

        private static void AddNotation(PlayerHand hand, string text, IdSource ids)
        {
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '[')
                {
                    var close = text.IndexOf(']', i);
                    if (close < 0) throw new ArgumentException($"Unclosed meld in {text}.");
                    var body = text[(i + 1)..close];
                    var kind = MeldKind.Chow;
                    var after = close + 1;
                    if (after < text.Length && (text[after] == 'k' || text[after] == 'K'))
                    {
                        kind = text[after] == 'k' ? MeldKind.OpenKong : MeldKind.ClosedKong;
                        after++;
                    }
                    var types = ParseGroup(body);
                    if (kind == MeldKind.Chow)
                        kind = types.Count == 4 ? MeldKind.OpenKong : MeldKind.Pung;

                    var tiles = types.Select(t => new Tile(t, ids.Next())).ToList();
                    hand.AddMeld(new Meld(kind, tiles));
                    i = after;
                    continue;
                }

                var start = i;
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == 'm' || text[i] == 'p' || text[i] == 's' || text[i] == 'z'))
                {
                    i++;
                    if (i <= text.Length && i > start && "mpsz".IndexOf(text[i - 1]) >= 0) break;
                }
                var group = text[start..i];
                if (group.Length == 0) throw new ArgumentException($"Cannot read {text} at {start}.");
                foreach (var type in ParseGroup(group))
                    hand.AddConcealed(new Tile(type, ids.Next()));
            }
        }

        /// <summary>Reads "123m456p" or "123m" into faces; digits without a suit are honour ranks.</summary>
        private static List<TileType> ParseGroup(string text)
        {
            var result = new List<TileType>();
            var digits = new List<int>();
            foreach (var c in text)
            {
                if (char.IsDigit(c))
                {
                    digits.Add(c - '0');
                    continue;
                }

                var s = char.ToLowerInvariant(c);
                if (s is not ('m' or 'p' or 's' or 'z'))
                    throw new ArgumentException($"Unknown suit '{c}' in {text}.");

                // The suit letter closes the digits before it; honours may also be written bare as
                // "1z2z", where every digit carries its own letter.
                foreach (var digit in digits) result.Add(Face(s, digit));
                digits.Clear();
            }

            if (digits.Count > 0)
                throw new ArgumentException($"Digits without a suit in {text}.");

            return result;
        }

        private static TileType Face(char suit, int digit) => suit switch
        {
            'm' => new TileType(TileSuit.Characters, digit),
            'p' => new TileType(TileSuit.Dots, digit),
            's' => new TileType(TileSuit.Bamboo, digit),
            'z' => digit switch
            {
                >= 1 and <= 4 => new TileType(TileSuit.Wind, digit - 1),
                5 => new TileType(TileSuit.Dragon, (int)Dragon.Red),
                6 => new TileType(TileSuit.Dragon, (int)Dragon.Green),
                7 => new TileType(TileSuit.Dragon, (int)Dragon.White),
                _ => throw new ArgumentException($"Honour {digit}z does not exist."),
            },
            _ => throw new ArgumentException($"Unknown suit {suit}."),
        };

        private static TileType ParseTile(string text) => ParseGroup(text)[0];

        private static string Describe(PlayerHand hand)
        {
            var concealed = string.Join(" ", GroupConcealed(hand.Concealed.Select(t => t.Type)));
            var melds = string.Join(" ", hand.Melds.Select(m =>
                "[" + string.Join("", m.Types.Select(Short)) + "]" + (m.Kind == MeldKind.OpenKong ? "k" : m.Kind == MeldKind.ClosedKong ? "K" : "")));
            return (concealed + " " + melds).Trim();
        }

        /// <summary>Numbered suits print as one run per suit; honours print tile by tile, "1z"-style.</summary>
        private static IEnumerable<string> GroupConcealed(IEnumerable<TileType> types)
        {
            foreach (var group in types.GroupBy(t => t.Suit).OrderBy(g => g.Key))
            {
                if (group.Key is TileSuit.Wind or TileSuit.Dragon)
                {
                    foreach (var type in group.OrderBy(t => t.Rank)) yield return Short(type);
                    continue;
                }

                var ordered = group.OrderBy(t => t.Rank);
                var rank = group.Key switch
                {
                    TileSuit.Characters => 0,
                    _ => 0,
                };
                yield return string.Concat(ordered.Select(t => t.Rank.ToString())) + SuitLetter(group.Key);
            }
        }

        private static string Short(TileType type) => type.Suit switch
        {
            TileSuit.Wind => (type.Rank + 1) + "z",
            TileSuit.Dragon => (type.Rank + 5) + "z",
            _ => type.Rank + SuitLetter(type.Suit).ToString(),
        };

        private static char SuitLetter(TileSuit suit) => suit switch
        {
            TileSuit.Characters => 'm',
            TileSuit.Dots => 'p',
            TileSuit.Bamboo => 's',
            _ => 'z',
        };

        private static string TileName(TileType type) => Short(type);
    }
}
