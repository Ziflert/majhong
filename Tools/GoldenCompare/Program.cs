using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mahjong.Rules;

namespace Mahjong.GoldenCompare
{
    /// <summary>
    /// Scores the golden hands in hands.json with our engine and prints one JSON line per hand in
    /// the same shape as Tools/mcr-ref/score-reference.mjs, so the two runs can be diffed.
    ///
    ///   dotnet run --project Tools/GoldenCompare -- Tools/GoldenCompare/hands.json
    ///
    /// The notation is the reference one: suits m/p/s, honors E S W N and C F P (red, green, white
    /// dragons). The winning tile is separate from the 13 concealed tiles.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var path = args.Length > 0 ? args[0] : "Tools/GoldenCompare/hands.json";
            var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var hand in doc.RootElement.GetProperty("hands").EnumerateArray())
            {
                var id = hand.GetProperty("id").GetString()!;
                try
                {
                    Console.WriteLine(ScoreOne(hand, id));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["id"] = id, ["failed"] = "invalid-input", ["message"] = ex.Message,
                    }));
                }
            }

            return 0;
        }

        private static string ScoreOne(JsonElement hand, string id)
        {
            var playerHand = new PlayerHand();
            var ids = 0;
            foreach (var type in ParseTiles(hand.GetProperty("tiles").GetString()!))
                playerHand.AddConcealed(new Tile(type, ids++));

            foreach (var meld in hand.TryGetProperty("melds", out var melds) ? melds.EnumerateArray() : default)
            {
                var kind = meld.GetProperty("kind").GetString() switch
                {
                    "chow" => MeldKind.Chow,
                    "pung" => MeldKind.Pung,
                    "melded-kong" => MeldKind.OpenKong,
                    "concealed-kong" => MeldKind.ClosedKong,
                    var other => throw new ArgumentException($"Unknown meld kind {other}."),
                };
                var tiles = ParseTiles(meld.GetProperty("tiles").GetString()!)
                    .Select(t => new Tile(t, ids++)).ToList();
                playerHand.AddMeld(new Meld(kind, tiles));
            }

            var win = ParseTiles(hand.GetProperty("win").GetString()!)[0];
            var source = hand.GetProperty("source").GetString() == "wall" ? ClaimSource.SelfDraw : ClaimSource.Discard;
            var discardPile = GetFlag(hand, "lastCopy")
                ? Enumerable.Repeat(win, 3).ToArray()
                : Array.Empty<TileType>();

            var context = new ScoringContext
            {
                Hand = playerHand,
                WinningTile = win,
                Source = source,
                RoundWind = ParseWind(hand, "roundWind", Wind.East),
                SeatWind = ParseWind(hand, "seatWind", Wind.East),
                LastTileOfWall = GetFlag(hand, "lastWallEvent") && source == ClaimSource.SelfDraw,
                LastDiscard = GetFlag(hand, "lastWallEvent") && source == ClaimSource.Discard,
                RobbedKong = false,
                DiscardPile = discardPile,
            };

            if (hand.TryGetProperty("flowerCount", out var flowerCount))
            {
                for (var i = 0; i < flowerCount.GetInt32(); i++)
                    playerHand.AddFlower(new Tile(new TileType(TileSuit.Flower, i), ids++));
            }

            var result = Scoring.Evaluate(context);
            var fans = result.Fans
                .Select(f => new Dictionary<string, object?>
                {
                    ["code"] = f.Fan.Id, ["name"] = f.Fan.NameEn, ["points"] = f.Fan.Points,
                })
                .ToList();

            var flowers = playerHand.Flowers.Count;
            var flowerPoints = flowers * Fans.Flower.Points;
            return JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["id"] = id,
                ["tiles"] = hand.GetProperty("tiles").GetString()!,
                ["win"] = hand.GetProperty("win").GetString()!,
                ["total"] = result.TotalPoints,
                ["nonFlowerTotal"] = result.HandPoints,
                ["flowerPoints"] = flowerPoints,
                ["validWin"] = result.IsValidWin,
                ["error"] = result.Error,
                ["fans"] = fans,
            });
        }

        private static bool GetFlag(JsonElement hand, string name) =>
            hand.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

        private static Wind ParseWind(JsonElement hand, string name, Wind fallback)
        {
            if (!hand.TryGetProperty(name, out var value)) return fallback;
            return value.GetString() switch
            {
                "E" => Wind.East, "S" => Wind.South, "W" => Wind.West, "N" => Wind.North,
                _ => fallback,
            };
        }

        /// <summary>Reads the reference notation: "123m 456p ESWNCFP".</summary>
        /// <summary>
        /// A token is a suited run only when it is digits followed by one suit letter ("123m");
        /// otherwise it is a run of honor letters ("ESWNCFP"). Deciding on the last character alone
        /// would misread the honor letters S and P as the bamboo and dot suits.
        /// </summary>
        private static List<TileType> ParseTiles(string text)
        {
            var result = new List<TileType>();
            foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var suited = token.Length > 1 && token[^1] is 'm' or 'p' or 's'
                             && token[..^1].All(char.IsDigit);
                if (suited)
                {
                    var suit = token[^1] switch
                    {
                        'm' => TileSuit.Characters, 'p' => TileSuit.Dots, _ => TileSuit.Bamboo,
                    };
                    foreach (var c in token[..^1])
                        result.Add(new TileType(suit, c - '0'));
                    continue;
                }

                foreach (var c in token)
                {
                    result.Add(char.ToUpperInvariant(c) switch
                    {
                        'E' => new TileType(TileSuit.Wind, (int)Wind.East),
                        'S' => new TileType(TileSuit.Wind, (int)Wind.South),
                        'W' => new TileType(TileSuit.Wind, (int)Wind.West),
                        'N' => new TileType(TileSuit.Wind, (int)Wind.North),
                        'C' => new TileType(TileSuit.Dragon, (int)Dragon.Red),
                        'F' => new TileType(TileSuit.Dragon, (int)Dragon.Green),
                        'P' => new TileType(TileSuit.Dragon, (int)Dragon.White),
                        var other => throw new ArgumentException($"Unknown tile {other} in {token}."),
                    });
                }
            }

            return result;
        }
    }
}
