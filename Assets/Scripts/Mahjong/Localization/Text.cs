using System.Collections.Generic;
using System.Linq;
using Mahjong.Controls;
using Mahjong.Rules;

namespace Mahjong.Localization
{
    /// <summary>
    /// Turns rules objects into interface text. The engine keeps its English
    /// <see cref="LegalAction.Description"/> — it is the log line and the reference wording — and every
    /// sentence the player reads is assembled here from the <see cref="Loc"/> catalogue.
    ///
    /// Cards, tiles and phases are translated from structured data (suit, rank, kind), never by
    /// translating an English sentence, so a new language cannot break the tile order of a sentence.
    /// </summary>
    public static class Text
    {
        // --------------------------------------------------------------- tiles

        public static string Tile(TileType type)
        {
            if (Loc.Current == Language.En) return TileEn(type);

            return type.Suit switch
            {
                TileSuit.Characters => $"{type.Rank} {Loc.Plural(type.Rank, "символ", "символа", "символов")}",
                TileSuit.Dots => $"{type.Rank} {Loc.Plural(type.Rank, "точка", "точки", "точек")}",
                TileSuit.Bamboo => $"{type.Rank} {Loc.Plural(type.Rank, "бамбук", "бамбука", "бамбуков")}",
                TileSuit.Wind => Loc.Get(WindKey((Wind)type.Rank)),
                TileSuit.Dragon => type.Rank switch
                {
                    (int)Dragon.Red => Loc.Get("tile.dragon.red"),
                    (int)Dragon.Green => Loc.Get("tile.dragon.green"),
                    _ => Loc.Get("tile.dragon.white")
                },
                TileSuit.Flower => Loc.Get(FlowerKey((FlowerKind)type.Rank)),
                _ => type.ToString()
            };
        }

        private static string TileEn(TileType type) => type.Suit switch
        {
            TileSuit.Characters => $"{type.Rank} {(type.Rank == 1 ? "Character" : "Characters")}",
            TileSuit.Dots => $"{type.Rank} {(type.Rank == 1 ? "Dot" : "Dots")}",
            TileSuit.Bamboo => $"{type.Rank} Bamboo",
            TileSuit.Wind => Loc.Get(WindKey((Wind)type.Rank)),
            TileSuit.Dragon => type.Rank switch
            {
                (int)Dragon.Red => Loc.Get("tile.dragon.red"),
                (int)Dragon.Green => Loc.Get("tile.dragon.green"),
                _ => Loc.Get("tile.dragon.white")
            },
            TileSuit.Flower => Loc.Get(FlowerKey((FlowerKind)type.Rank)),
            _ => type.ToString()
        };

        public static string WindName(Wind wind) => Loc.Get(WindKey(wind));

        private static string WindKey(Wind wind) => wind switch
        {
            Wind.East => "seat.east",
            Wind.South => "seat.south",
            Wind.West => "seat.west",
            _ => "seat.north"
        };

        private static string FlowerKey(FlowerKind kind) => kind switch
        {
            FlowerKind.Spring => "tile.flower.spring",
            FlowerKind.Summer => "tile.flower.summer",
            FlowerKind.Autumn => "tile.flower.autumn",
            FlowerKind.Winter => "tile.flower.winter",
            FlowerKind.Plum => "tile.flower.plum",
            FlowerKind.Orchid => "tile.flower.orchid",
            FlowerKind.Bamboo => "tile.flower.bamboo",
            _ => "tile.flower.chrysanthemum"
        };

        /// <summary>Tiles of a required-tile list, comma separated.</summary>
        public static string Tiles(IEnumerable<Tile> tiles) =>
            string.Join(", ", tiles.Select(tile => Tile(tile.Type)));

        // ------------------------------------------------------------- variants

        /// <summary>Name of the rules profile: "Спортивный (ВОМ)" or "Классика 144".</summary>
        public static string VariantName(RuleVariant variant) =>
            Loc.Current == Language.Ru ? variant.NameRu : variant.NameEn;

        // ------------------------------------------------------------- actions

        /// <summary>Short caption that fits on a button of the action bar.</summary>
        public static string ActionLabel(ActionKind kind, TileType? tile)
        {
            // The English captions are the engine's own wording and are pinned by the editor tests.
            if (Loc.Current == Language.En)
            {
                return kind switch
                {
                    ActionKind.Win => "Hu",
                    ActionKind.Pass => "Pass",
                    ActionKind.Chow => "Chow " + tile,
                    ActionKind.Pung => "Pung " + tile,
                    ActionKind.OpenKong or ActionKind.ClosedKong or ActionKind.AddedKong => "Kong " + tile,
                    ActionKind.Discard => "Discard " + tile,
                    ActionKind.Draw => "Draw",
                    _ => kind.ToString()
                };
            }

            return kind switch
            {
                ActionKind.Win => Loc.Get("action.win"),
                ActionKind.Pass => Loc.Get("action.pass"),
                ActionKind.Chow => Loc.Format("action.chow", Span(tile)),
                ActionKind.Pung => Loc.Format("action.pung", Span(tile)),
                ActionKind.OpenKong or ActionKind.ClosedKong or ActionKind.AddedKong =>
                    Loc.Format("action.kong", Span(tile)),
                ActionKind.Discard => Loc.Format("action.discard", Span(tile)),
                ActionKind.Draw => Loc.Get("action.draw"),
                _ => kind.ToString()
            };
        }

        public static string ActionLabel(LegalAction action) => ActionLabel(action.Kind, action.Tile?.Type);

        private static string Span(TileType? tile) => tile.HasValue ? Tile(tile.Value).ToLowerInvariant() : string.Empty;

        /// <summary>
        /// Full sentence for the hint line. English reuses the engine description, which is the
        /// canonical wording of the rules layer; Russian is assembled from the same structured data.
        /// </summary>
        public static string ActionHint(LegalAction action)
        {
            if (Loc.Current == Language.En) return action.Description;

            return action.Kind switch
            {
                ActionKind.Win => Loc.Format("hint.win", action.Points),
                ActionKind.Pass => Loc.Get("hint.pass"),
                ActionKind.Chow => Loc.Format("hint.chow", Tiles(action.RequiredTiles)),
                ActionKind.Pung => Loc.Format("hint.pung", Tiles(action.RequiredTiles)),
                ActionKind.OpenKong or ActionKind.ClosedKong or ActionKind.AddedKong =>
                    Loc.Format("hint.kong", action.RequiredTiles.Count > 0
                        ? Tiles(action.RequiredTiles)
                        : Tile(action.Tile!.Value.Type)),
                ActionKind.Discard => Loc.Format("hint.discard", Tile(action.Tile!.Value.Type)),
                ActionKind.Draw => Loc.Get("hint.draw"),
                _ => action.Description
            };
        }

        // --------------------------------------------------------------- clock

        public static string ClockLabel(ClockWindow window) => window switch
        {
            ClockWindow.Arrange => Loc.Get("clock.arrange"),
            ClockWindow.Discard => Loc.Get("clock.discard"),
            ClockWindow.Claim => Loc.Get("clock.claim"),
            _ => Loc.Get("clock.time")
        };

        // --------------------------------------------------------------- seats

        public static string Seat(int player) => Loc.Format("seat.player", player);

        public static string SeatName(Wind wind) => WindName(wind);

        // ---------------------------------------------------------------- score

        /// <summary>Fan name in the active language; the catalogue already carries both spellings.</summary>
        public static string FanName(Fan fan) => Loc.Current == Language.Ru ? fan.NameRu : fan.NameEn;

        /// <summary>
        /// The breakdown of a winning hand as one line: every fan with its value and the total.
        /// English keeps the engine sentence, Russian is assembled from the fan catalogue.
        /// </summary>
        public static string ScoreSummary(WinResult result)
        {
            if (Loc.Current == Language.En) return result.Score.Fans.Count == 0
                ? string.Empty
                : string.Join(" + ", result.Score.Fans.Select(fan => $"{fan.Fan.NameEn} {fan.Fan.Points}"));

            var parts = result.Score.Fans
                .Select(scored => $"{scored.Fan.NameRu} {scored.Fan.Points}")
                .ToList();
            var total = Loc.Format("result.total", result.Score.HandPoints);
            return parts.Count == 0 ? total : string.Join(" + ", parts) + " = " + total;
        }

        /// <summary>
        /// The result screen of a classic hand: what the winner's combination is made of, the doublings
        /// it earned, what every seat's combination was worth and who paid whom (rule-2.pdf, IV–V).
        /// </summary>
        public static string ClassicSettlementSummary(WinResult result, Settlement settlement)
        {
            var score = result.Score.Classic;
            var lines = new List<string>();

            if (score != null)
            {
                var parts = string.Join(" + ", score.Parts.Select(part =>
                    $"{(Loc.Current == Language.Ru ? part.NameRu : part.NameEn)} {part.Points}"));
                lines.Add(Loc.Format("classic.base", parts, score.BasePoints));

                if (score.Doublings.Count > 0)
                {
                    var names = string.Join(", ", score.Doublings.Select(doubling =>
                        Loc.Current == Language.Ru ? doubling.NameRu : doubling.NameEn));
                    lines.Add(Loc.Format("classic.doublings", names, score.DoublingCount, score.Value)
                              + (score.AtLimit ? "  " + Loc.Get("classic.limit") : string.Empty));
                }
            }

            if (settlement.Classic != null)
            {
                var values = string.Join("  ", settlement.Classic.Values
                    .Select((value, seat) => $"P{seat + 1}={value}"));
                lines.Add(Loc.Format("classic.values", values));
            }

            if (settlement.Payments.Count > 0)
                lines.Add(string.Join("  ", settlement.Payments
                    .Select(payment => $"P{payment.FromPlayer + 1}→P{payment.ToPlayer + 1} {payment.Points}")));

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Who pays whom in one line: "Payments: player 3 → player 2: 32  player 4 → player 2: 32".
        /// The classic table prints its own transfer list, so this is the sports wording.
        /// </summary>
        public static string PaymentSummary(Settlement settlement)
        {
            var transfers = string.Join("  ", settlement.Payments.Select(payment =>
                Loc.Format("result.payment", payment.FromPlayer + 1, payment.ToPlayer + 1, payment.Points)));
            return Loc.Format("result.payments", transfers);
        }

        /// <summary>
        /// The end-of-hand screen: who won, what the hand was worth and how it was paid, the points
        /// after the hand and — after the fourth hand of a round or the sixteenth of the session —
        /// where the players stand. Assembled here so the panel only draws what it is given.
        /// </summary>
        public static HandResultContent HandResult(GameSession session, int humanSeat)
        {
            var settlement = session.LastSettlement ?? Settlements.ForDraw();
            var result = session.State?.Result;
            var lines = new List<string>();

            var title = settlement.WinnerPlayer < 0
                ? Loc.Get("result.draw")
                : settlement.WinnerPlayer == humanSeat
                    ? Loc.Get("result.youWin")
                    : Loc.Format("result.playerWins", settlement.WinnerPlayer + 1);

            if (settlement.Classic != null && result != null)
                lines.Add(ClassicSettlementSummary(result, settlement));
            else if (result != null && result.Score.IsValidWin)
                lines.Add(ScoreSummary(result));
            else
                lines.Add(settlement.Explanation);

            // The classic summary carries the transfers of section IV itself; the sports table has to
            // list them here, because its own result line only says what the hand was worth.
            if (settlement.Payments.Count > 0 && settlement.Classic == null)
                lines.Add(PaymentSummary(settlement));

            if (settlement.Payments.Count > 0)
                lines.Add(Loc.Format("result.paid", settlement.TotalPaid));

            lines.Add(Loc.Format("result.scores", string.Join("  ",
                session.Scores.Select((score, player) => $"P{player + 1}={score}"))));

            var standings = new List<string>();
            if (session.IsFinished)
            {
                standings.AddRange(Lines(Standings(session.Standings(), humanSeat)));
            }
            else if (session.HandsPlayed > 0 && session.HandsPlayed % GameSession.HandsPerRound == 0)
            {
                var roundWind = (Wind)((session.HandsPlayed - 1) / GameSession.HandsPerRound);
                standings.Add(Loc.Format("result.roundEnd", WindName(roundWind)));
                // The header of the final table would read as the end of the session; the round line
                // above says which round it is instead.
                standings.AddRange(Lines(Standings(session.Standings(), humanSeat)).Skip(1));
            }

            return new HandResultContent(title, lines, standings, !session.IsFinished);
        }

        private static IEnumerable<string> Lines(string text) => text.Split('\n');

        /// <summary>
        /// The final table of a session: place, seat, game points and table points, with a mark for a
        /// shared place and for a player who did not turn up. English keeps the engine wording.
        /// </summary>
        public static string Standings(IReadOnlyList<Standing> standings, int humanSeat)
        {
            if (Loc.Current == Language.En)
            {
                var lines = new List<string> { Loc.Get("standings.header") };
                foreach (var standing in standings)
                {
                    var who = $"Player {standing.Player + 1}";
                    lines.Add($"{standing.Place}. {who} {standing.GamePoints} "
                              + $"({standing.TablePoints:0.##})"
                              + (standing.Tied ? " tied" : string.Empty)
                              + (standing.Forfeited ? " forfeited" : string.Empty));
                }
                return string.Join("\n", lines);
            }

            var result = new List<string> { Loc.Get("standings.header") };
            foreach (var standing in standings)
            {
                var who = standing.Player == humanSeat
                    ? Loc.Format("standings.you", standing.Player + 1)
                    : Loc.Format("seat.player", standing.Player + 1);
                var marks = (standing.Tied ? Loc.Get("standings.tie") : string.Empty)
                            + (standing.Forfeited ? Loc.Get("standings.forfeit") : string.Empty);
                result.Add(Loc.Format("standings.line", standing.Place, who, standing.GamePoints,
                    standing.TablePoints.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), marks));
            }

            return string.Join("\n", result);
        }

        // -------------------------------------------------------------- phrases

        public static string BarHint(int count) =>
            count == 1 ? string.Empty : Loc.Format("hint.bar", count);
    }
}
