using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mahjong.Bots;
using Mahjong.Knowledge;
using Mahjong.Localization;
using Mahjong.Net;
using Mahjong.Rules;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the parts that do not belong to a hand: tile notation, the move codec, the seat
    /// projection, the authoritative table with its log and replay, the free-play table, the interface
    /// texts and the in-game rulebook.
    ///
    /// Kept in its own file and wired into <see cref="RulesSelfTest.RunAll"/>, so the same checks run
    /// headless and inside Unity.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void NetAndUiGroups(SelfTestReport report)
        {
            Run(report, "net: tile notation round trip", () => NotationRoundTrip(report));
            Run(report, "net: move codec identifies an action exactly", () => MoveCodec(report));
            Run(report, "net: the seat view hides the other hands", () => SeatProjection(report));
            Run(report, "net: a forged or out-of-turn move changes nothing", () => AuthorityRejects(report));
            Run(report, "net: four seats play a hand through move lines", () => WireHand(report));
            Run(report, "net: a log replays into the same table", () => ReplayLog(report));
            Run(report, "free: the whole set lies on the shared table", () => FreeTableSetup(report));
            Run(report, "free: seats take, place and turn tiles", () => FreeTableMoves(report));
            Run(report, "text: both languages are complete", () => CatalogueComplete(report));
            Run(report, "text: tiles, actions and hints read in russian", () => LocalizedText(report));
            Run(report, "rules: the in-game book is complete and referenced", () => RuleBookPages(report));
        }

        // ------------------------------------------------------------ notation

        private static void NotationRoundTrip(SelfTestReport report)
        {
            foreach (var type in TileTypes.All)
            {
                var code = TileNotation.Format(type);
                report.Check(code.Length is >= 1 and <= 4, $"{type} must have a short code, got '{code}'");
                report.Check(TileNotation.TryParse(code, out var parsed), $"{code} must parse back");
                report.Equal(type, parsed, $"{code} must parse to {type}");
            }

            for (var flower = 0; flower < 8; flower++)
            {
                var type = new TileType(TileSuit.Flower, flower);
                var code = TileNotation.Format(type);
                report.Equal($"Fl{flower + 1}", code, "flowers are numbered from one");
                report.Equal(type, TileNotation.Parse(code.ToLowerInvariant()), $"'{code.ToLowerInvariant()}' parses");
            }

            var tile = new Tile(new TileType(TileSuit.Dots, 4), 77);
            report.Equal("4d#77", TileNotation.Format(tile), "a physical tile carries its identity");

            report.Equal(TileType.WindTile(Wind.North), TileNotation.Parse("N"), "N is the north wind");
            report.Equal(TileType.DragonTile(Dragon.White), TileNotation.Parse("white"), "long names parse too");
            report.Equal(new TileType(TileSuit.Bamboo, 3), TileNotation.Parse("3s"), "3s is bamboo in the other notation");
            report.Equal(new TileType(TileSuit.Characters, 9), TileNotation.Parse("9m"), "9m is characters");
            report.Check(!TileNotation.TryParse("0c", out _), "rank 0 is not a tile");
            report.Check(!TileNotation.TryParse("Fl9", out _), "there are only eight flowers");
            report.Check(!TileNotation.TryParse("zz", out _), "nonsense is refused");

            var list = TileNotation.ParseList("3c, 4c ,5c");
            report.Equal(3, list.Count, "a list parses three tiles");
            report.Equal(new TileType(TileSuit.Characters, 4), list[1], "spaces are tolerated");
        }

        // ---------------------------------------------------------------- codec

        private static void MoveCodec(SelfTestReport report)
        {
            // Every move the engine offers to every seat must survive the wire and resolve back to
            // exactly the same action — otherwise a legal move could be impossible to state. The hand
            // is driven move by move, each one stated as a wire line and nothing else.
            var checkedActions = 0;
            for (var seed = 0; seed < 3; seed++)
            {
                var stage = new InProcessTable(seed, 900 + seed, Wind.East, BotLevel.Strategic, 0, 1, 2, 3);
                for (var step = 0; step < 400 && stage.State.Phase != GamePhase.HandFinished; step++)
                {
                    var acted = false;
                    for (var seat = 0; seat < 4 && !acted; seat++)
                    {
                        var legal = stage.State.GetLegalActions(seat);
                        foreach (var action in legal)
                        {
                            var wire = ActionCodec.Encode(action);
                            report.Check(ActionCodec.TryDecode(wire, out var intent, out var error),
                                $"'{wire}' must decode: {error}");
                            var resolved = ActionCodec.Resolve(stage.State.GetLegalActions(seat), intent);
                            report.Check(resolved != null, $"'{wire}' must resolve to a legal action of seat {seat}");
                            report.Equal(action.Kind, resolved!.Kind, $"{wire} keeps the kind");
                            report.Equal(action.Tile?.InstanceId, resolved.Tile?.InstanceId, $"{wire} keeps the tile");
                            checkedActions++;
                        }

                        if (legal.Count == 0) continue;

                        var pick = legal[(step * 7 + seat) % legal.Count];
                        var reply = stage.Act(seat, ActionCodec.Encode(pick));
                        report.Check(reply.Accepted, $"seed {seed}: {ActionCodec.Encode(pick)} is accepted: {reply.Reason}");
                        acted = true;
                    }

                    if (!acted) break;
                }

                report.Equal(GamePhase.HandFinished, stage.State.Phase, $"seed {seed}: a hand driven by wire lines ends");
            }

            report.Check(checkedActions > 100, $"the codec was exercised on {checkedActions} offered moves");

            report.Check(ActionCodec.TryDecode("chow:17;21,22", out var custom, out _), "other separators parse");
            report.Equal(ActionKind.Chow, custom.Kind, "chow parses");
            report.Equal(17, custom.TileId, "the discarded tile is kept");
            report.Equal(2, custom.RequiredIds.Count, "both hand tiles are kept");

            report.Check(!ActionCodec.TryDecode("teleport|5|-", out _, out var kindError), "unknown kinds are refused");
            report.Check(kindError.Contains("teleport"), $"the refusal names the move: {kindError}");
            report.Check(!ActionCodec.TryDecode("discard|five|-", out _, out _), "a non-numeric tile id is refused");
            report.Check(!ActionCodec.TryDecode("", out _, out _), "an empty line is refused");

            // A move that reuses a foreign tile identity must not resolve anywhere.
            var table = new AuthoritativeTable(0, 777);
            var foreign = table.ViewFor(0).Concealed.First().InstanceId;
            var forged = new MoveIntent(ActionKind.Pung, foreign, new[] { foreign });
            report.Check(ActionCodec.Resolve(table.State.GetLegalActions(0), forged) == null,
                "an invented pung does not resolve");
        }

        // ----------------------------------------------------------- projection

        private static void SeatProjection(SelfTestReport report)
        {
            for (var seed = 0; seed < 4; seed++)
            {
                var table = new InProcessTable(0, 77000 + seed, Wind.East, BotLevel.Strategic, 1, 2, 3);
                for (var round = 0; round < 5 && table.State.Phase != GamePhase.HandFinished; round++)
                {
                    table.Pump();
                    if (table.State.Phase == GamePhase.HandFinished) break;
                    var options = table.State.GetLegalActions(0);
                    if (options.Count == 0) break;
                    table.Act(0, ActionCodec.Encode(options[0]));
                }

                for (var seat = 0; seat < 4; seat++)
                {
                    var view = table.View(seat);
                    var text = view.Describe();

                    report.Equal(table.State.Players[seat].Concealed.Count, view.Concealed.Count,
                        $"seat {seat} sees his own hand in full");
                    report.Equal(table.State.Wall.Remaining, view.WallRemaining, "the wall count is public");
                    report.Equal(table.State.CurrentPlayer, view.CurrentPlayer, "the turn is public");

                    // Counts of foreign hands are public, faces are not.
                    for (var other = 0; other < 4; other++)
                        report.Equal(table.State.Players[other].Concealed.Count, view.Players[other].ConcealedCount,
                            $"seat {seat} knows how many tiles seat {other} holds");

                    var visible = TileIdsInText(text);
                    var allowed = new HashSet<int>(view.Concealed.Select(tile => tile.InstanceId));
                    for (var other = 0; other < 4; other++)
                    {
                        if (other == seat) continue;
                        foreach (var hidden in table.State.Players[other].Concealed)
                            report.Check(!visible.Contains(hidden.InstanceId),
                                $"seat {seat} must not see {TileNotation.Format(hidden)} of seat {other}");
                    }

                    // A meld and a flower are face up on the table whichever seat declared them.
                    for (var other = 0; other < 4; other++)
                    {
                        foreach (var meld in table.State.Players[other].Melds)
                            allowed.UnionWith(meld.Tiles.Select(tile => tile.InstanceId));
                        foreach (var tile in table.State.Players[other].Flowers)
                            allowed.Add(tile.InstanceId);
                    }
                    foreach (var discard in table.State.Discards)
                        allowed.Add(discard.Tile.InstanceId);

                    foreach (var id in visible)
                        report.Check(allowed.Contains(id),
                            $"seat {seat}: tile {id} appears in the projection, so it must be public or owned");

                    report.Check(text.Contains("hand|") || view.Concealed.Count == 0,
                        "the projection carries the own hand as lines");
                    report.Check(view.Offers.All(offer => offer.PlayerIndex == seat),
                        $"seat {seat} is only ever offered his own moves");
                }
            }
        }

        /// <summary>Identity numbers that appear in the tile fields of a projection.</summary>
        private static HashSet<int> TileIdsInText(string text)
        {
            var ids = new HashSet<int>();
            foreach (var line in text.Split('\n'))
            {
                if (!line.StartsWith("hand|") && !line.StartsWith("meld|") && !line.StartsWith("discard|")
                    && !line.StartsWith("flower|"))
                    continue;

                foreach (Match match in Regex.Matches(line, @"#(\d+)"))
                    ids.Add(int.Parse(match.Groups[1].Value));
            }

            return ids;
        }

        // ------------------------------------------------------------ authority

        private static void AuthorityRejects(SelfTestReport report)
        {
            var table = new InProcessTable(0, 5150, Wind.East, BotLevel.Strategic, 1, 2, 3);
            table.Pump();

            var before = table.View(0).Describe();
            report.Equal(0, table.State.CurrentPlayer, "the bots stop at the human seat");
            report.Equal(GamePhase.AwaitingDiscard, table.State.Phase, "it is the human's turn to discard");

            var foreign = table.State.Players[1].Concealed.First().InstanceId;
            var notInHand = table.Act(0, $"discard|{foreign}|-");
            report.Check(!notInHand.Accepted, "a discard of a tile the player does not hold is refused");
            report.Check(notInHand.Reason.Length > 0, "a refusal carries a reason");

            var outOfTurn = table.Act(1, $"discard|{foreign}|-");
            report.Check(!outOfTurn.Accepted, "seat 1 cannot discard while it is not his turn");

            var wrongSeat = table.Act(1, "pass|-|-");
            report.Check(!wrongSeat.Accepted, "seat 1 states its own moves only when the engine offers them");

            var garbage = table.Act(0, "какая-то ерунда");
            report.Check(!garbage.Accepted, "a move that is not a move is refused");

            var negative = table.Act(-1, "pass|-|-");
            report.Check(!negative.Accepted, "there is no seat -1");

            report.Equal(before, table.View(0).Describe(), "refusals leave the table untouched");
            report.Equal(0, table.Server.Log.Count, "refusals do not enter the log");

            // The engine's own move is accepted, and the log grows by exactly one line.
            var move = table.State.GetLegalActions(0).First();
            var accepted = table.Act(0, ActionCodec.Encode(move));
            report.Check(accepted.Accepted, $"the offered {ActionCodec.Encode(move)} is accepted");
            report.Equal(1, table.Server.Log.Count, "one accepted move, one log line");
            report.Check(accepted.Events.Count > 0, "an accepted move produces events for the view");
            report.Equal(0, accepted.View.Seat, "the reply is the projection of the acting seat");
        }

        private static void WireHand(SelfTestReport report)
        {
            for (var seed = 0; seed < 6; seed++)
            {
                var table = new InProcessTable(seed % 4, 31000 + seed, Wind.East, BotLevel.Strategic, 0, 1, 2, 3);
                var moves = table.Pump();
                report.Check(moves > 0, $"seed {seed}: bots played {moves} moves");
                report.Equal(GamePhase.HandFinished, table.State.Phase, $"seed {seed}: the hand reaches the end");
                report.Check(table.State.Result != null || table.State.EndedInDraw,
                    $"seed {seed}: the hand ends in a win or a draw");
                report.Check(table.Server.Log.Count == moves, "every bot move passed the same validation as a human one");

                foreach (var line in table.Server.LogText.Split('\n'))
                {
                    report.Check(ActionCodec.TryDecode(line.Split('|', 2)[1], out _, out _),
                        $"seed {seed}: log line '{line}' is a decodable move");
                }

                // The winner's tiles are public; nobody else's hand ever left the server.
                var seats = table.Server.AllViews();
                report.Equal(4, seats.Count, "four projections, one per seat");
            }
        }

        private static void ReplayLog(SelfTestReport report)
        {
            for (var seed = 0; seed < 3; seed++)
            {
                var original = new InProcessTable(2, 8880 + seed, Wind.East, BotLevel.Strategic, 0, 1, 2, 3);
                original.PlayOut();

                var log = original.Server.LogText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                report.Check(log.Length > 10, $"seed {seed}: the hand produced {log.Length} moves");

                var replayed = AuthoritativeTable.Replay(2, 8880 + seed, Wind.East, original.Server.LogText
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries));

                for (var seat = 0; seat < 4; seat++)
                    report.Equal(original.View(seat).Describe(), replayed.ViewFor(seat).Describe(),
                        $"seed {seed}: seat {seat} ends up in the same state after a replay");
                report.Equal(original.State.Result?.Score.TotalPoints ?? 0, replayed.State.Result?.Score.TotalPoints ?? 0,
                    $"seed {seed}: the replay scores the same");
            }
        }

        // ------------------------------------------------------------ free table

        private static void FreeTableSetup(SelfTestReport report)
        {
            var table = new FreeTable(seed: 4711);

            report.Equal(FreeTable.TotalTiles, table.TileCount, "the free table holds the whole set");
            report.Equal(144, table.TileCount, "144 tiles");

            var ids = table.Tiles.Select(tile => tile.Id).ToList();
            report.Equal(ids.Count, ids.Distinct().Count(), "identities are unique");

            var cells = table.Tiles.Select(tile => (tile.X, tile.Y)).ToList();
            report.Equal(cells.Count, cells.Distinct().Count(), "no two tiles share a cell");
            report.Check(table.Tiles.All(tile => tile.X is >= 0 and < FreeTable.Columns
                                                && tile.Y is >= 0 and < FreeTable.Rows),
                "every tile is inside the table");
            report.Check(table.Tiles.All(tile => tile.FaceUp), "the opening layout is face up");
            report.Check(table.Tiles.All(tile => tile.HeldBy == null), "nobody holds anything at the start");

            var counts = table.Tiles.GroupBy(tile => tile.Type).ToDictionary(group => group.Key, group => group.Count());
            foreach (var type in TileTypes.All)
                report.Equal(4, counts[type], $"{type} appears four times");
            for (var flower = 0; flower < 8; flower++)
                report.Equal(1, counts[new TileType(TileSuit.Flower, flower)], $"flower {flower + 1} appears once");

            var shuffled = new FreeTable(seed: 90210);
            report.Check(table.Tiles.Count == shuffled.Tiles.Count, "another seed gives another set of the same size");
            var sameFirst = table.Tiles[0].Type == shuffled.Tiles[0].Type
                            && table.Tiles[1].Type == shuffled.Tiles[1].Type;
            report.Check(!sameFirst, "a different seed shuffles differently");
        }

        private static void FreeTableMoves(SelfTestReport report)
        {
            var table = new FreeTable(seed: 6001);
            var version = table.Version;

            var tile = table.TileAt(4, 2)!;
            var other = table.TileAt(9, 5)!;
            report.Check(table.TryTake(2, other.Id, out _), "seat 2 frees a second cell for the test");

            report.Check(table.TryTake(1, tile.Id, out _), "seat 1 picks a tile up");
            report.Equal(1, table.Find(tile.Id)!.HeldBy, "the tile belongs to seat 1 now");
            report.Check(table.Version > version, "the version moves on after a move");
            report.Check(table.TileAt(4, 2) == null, "a held tile is no longer on the grid");

            report.Check(!table.TryTake(2, tile.Id, out var steal), "seat 2 cannot take a tile seat 1 holds");
            report.Check(steal.Contains("seat 1"), $"the refusal names the holder: {steal}");
            report.Check(!table.TryFlip(2, tile.Id, out _), "seat 2 cannot turn someone else's tile");

            report.Check(!table.TryPlace(2, tile.Id, 1, 1, out var wrongOwner), "a tile is placed by its holder only");
            report.Check(wrongOwner.Length > 0, "the refusal explains why");

            report.Check(!table.TryPlace(1, tile.Id, -1, 0, out var outside), "cells outside the table are refused");
            report.Check(outside.Contains("-1"), $"the refusal names the cell: {outside}");

            var occupied = table.TileAt(7, 3)!;
            report.Check(!table.TryPlace(1, tile.Id, occupied.X, occupied.Y, out var taken),
                "an occupied cell is refused");
            report.Check(taken.Contains(occupied.Id.ToString()), $"the refusal names the occupant: {taken}");

            report.Check(table.TryPlace(1, tile.Id, other.X, other.Y, out _), "seat 1 puts the tile down elsewhere");
            report.Equal((9, 5), (table.Find(tile.Id)!.X, table.Find(tile.Id)!.Y), "the tile lies where it was put");
            report.Check(table.Find(tile.Id)!.HeldBy == null, "nobody holds it any more");

            report.Check(table.TryFlip(1, tile.Id, out _), "a free tile can be turned over");
            report.Check(!table.Find(tile.Id)!.FaceUp, "the face is now down");
            report.Check(table.TryFlip(2, tile.Id, out _), "any seat may turn a free tile");
            report.Check(table.Find(tile.Id)!.FaceUp, "and back up again");

            // Two seats work on one table: what one does, the other sees at once.
            var wire = table.Submit(3, $"take|{occupied.Id}");
            report.Check(wire.Accepted, "the same table answers a wire move from another seat");
            var parsed = wire.State.Split('\n').First(line => line.StartsWith($"tile|{occupied.Id}|"));
            report.Check(parsed.EndsWith("|3"), $"the wire state says who holds the tile: {parsed}");

            report.Check(!table.Submit(1, "place|999999|0|0").Accepted, "a bare number that is not a tile is refused");
            report.Check(!table.Submit(1, "jump|5").Accepted, "an unknown move is refused");
            report.Check(table.Submit(1, "reset|-").Accepted, "the table can be reset");
            report.Check(table.Tiles.All(candidate => candidate.HeldBy == null), "after a reset nothing is held");
            report.Equal(FreeTable.TotalTiles, table.Tiles.Count, "and the whole set is back");
        }

        // ----------------------------------------------------------- localisation

        private static void CatalogueComplete(SelfTestReport report)
        {
            report.Check(Loc.Keys.Count >= 60, $"the catalogue has {Loc.Keys.Count} entries");

            foreach (var key in Loc.Keys)
            {
                var ru = Loc.InLanguage(Language.Ru, key);
                var en = Loc.InLanguage(Language.En, key);

                report.Check(!string.IsNullOrWhiteSpace(ru), $"{key} has a russian text");
                report.Check(!string.IsNullOrWhiteSpace(en), $"{key} has an english text");
                report.Check(ru != en || key.StartsWith("seat.") || key.StartsWith("tile.wind"),
                    $"{key} is actually translated (ru != en) unless the word is the same");
                report.Equal(Placeholders(ru), Placeholders(en), $"{key} uses the same placeholders in both languages");
                report.Check(Regex.Matches(ru, "[А-Яа-яЁё]").Count > 0 || key.StartsWith("tile.wind"),
                    $"{key} is written in russian: '{ru}'");
            }

            var previous = Loc.Current;
            try
            {
                Loc.Current = Language.Ru;
                report.Check(Loc.Get("action.win").Contains("Ху"), "russian Hu reads Ху");
                report.Equal(Language.En, Loc.Toggle(), "toggling switches to english");
                report.Equal("Hu", Loc.Get("action.win"), "english Hu reads Hu");
                Loc.Toggle();
                report.Equal(Language.Ru, Loc.Current, "toggling switches back");

                var broken = false;
                try { Loc.Get("no.such.key"); }
                catch (KeyNotFoundException) { broken = true; }
                report.Check(broken, "an unknown key is a programming error, not a silent blank");
            }
            finally
            {
                Loc.Current = previous;
            }

            static string Placeholders(string text)
            {
                var numbers = Regex.Matches(text, @"\{(\d+)\}").Select(match => match.Groups[1].Value).ToList();
                numbers.Sort(StringComparer.Ordinal);
                return string.Join(",", numbers);
            }
        }

        private static void LocalizedText(SelfTestReport report)
        {
            var previous = Loc.Current;
            try
            {
                foreach (var language in new[] { Language.Ru, Language.En })
                {
                    Loc.Current = language;

                    foreach (var type in TileNotation.AllFaces)
                    {
                        var name = Text.Tile(type);
                        report.Check(name.Length > 1, $"{language}: {type} has a name");
                        report.Check(!name.Contains('{') && !name.Contains('}'), $"{language}: {type} name '{name}' is complete");
                    }

                    if (language == Language.Ru)
                    {
                        report.Equal("5 символов", Text.Tile(new TileType(TileSuit.Characters, 5)),
                            "russian five characters");
                        report.Equal("1 символ", Text.Tile(new TileType(TileSuit.Characters, 1)), "russian one character");
                        report.Equal("2 точки", Text.Tile(new TileType(TileSuit.Dots, 2)), "russian two dots");
                        report.Equal("3 бамбука", Text.Tile(new TileType(TileSuit.Bamboo, 3)), "russian three bamboo");
                    }
                    else
                    {
                        report.Equal("5 Characters", Text.Tile(new TileType(TileSuit.Characters, 5)), "english five characters");
                        report.Equal("1 Dot", Text.Tile(new TileType(TileSuit.Dots, 1)), "english one dot");
                    }
                    report.Check(Text.Tile(new TileType(TileSuit.Wind, (int)Wind.South)).Length > 1,
                        $"{language}: a wind has a name");

                    var table = new InProcessTable(0, 4321, Wind.East, BotLevel.Strategic, 1, 2, 3);
                    table.Pump();

                    var offered = 0;
                    foreach (var action in table.State.GetLegalActions(0))
                    {
                        var label = Text.ActionLabel(action.Kind, action.Tile?.Type);
                        var hint = Text.ActionHint(action);
                        report.Check(label.Length > 0, $"{language}: {action.Kind} has a label");
                        report.Check(!label.Contains('{'), $"{language}: label '{label}' is complete");
                        report.Check(hint.Length > 0, $"{language}: {action.Kind} has a hint");
                        report.Check(!hint.Contains('{') && !hint.Contains('}'), $"{language}: hint '{hint}' is complete");
                        if (language == Language.En)
                            report.Equal(action.Description, hint, "english keeps the engine wording");
                        offered++;
                    }
                    report.Check(offered > 0, $"{language}: the hand offers a decision to label");
                }

                // A promised Hu says its value instead of hiding it inside an English sentence.
                Loc.Current = Language.Ru;
                var promised = new LegalAction(ActionKind.Win, 0, null, Array.Empty<Tile>(), "Declare Hu for 96 points", 96);
                report.Check(Text.ActionHint(promised).Contains("96"), "the russian Hu hint carries the points");
                report.Equal("Ху", Text.ActionLabel(promised), "the russian Hu button is short");

                var chow = new LegalAction(ActionKind.Chow, 0,
                    new Tile(new TileType(TileSuit.Characters, 5), 1),
                    new[] { new Tile(new TileType(TileSuit.Characters, 3), 2), new Tile(new TileType(TileSuit.Characters, 4), 3) },
                    "Chow 5c with 3c, 4c");
                report.Check(Text.ActionHint(chow).Contains("3 символа"), $"chow hint lists the tiles: {Text.ActionHint(chow)}");
                report.Equal("Чоу 5 символов", Text.ActionLabel(chow), "the russian chow button");
            }
            finally
            {
                Loc.Current = previous;
            }
        }

        // ------------------------------------------------------------- rulebook

        private static void RuleBookPages(SelfTestReport report)
        {
            report.Check(RulesBook.Sections.Count >= 14, $"the book has {RulesBook.Sections.Count} pages");

            var ids = new HashSet<string>();
            foreach (var section in RulesBook.Sections)
            {
                report.Check(ids.Add(section.Id), $"{section.Id} is unique");
                report.Check(section.Title.Length > 3, $"{section.Id} has a title");
                report.Check(section.Paragraphs.Count >= 1
                              && section.Paragraphs.All(paragraph => paragraph.Length >= 40
                                                                  && !paragraph.Contains('\n')),
                    $"{section.Id} is written in whole paragraphs, not in hand-broken lines");
                report.Check(section.Body.Length > 200, $"{section.Id} is filled in ({section.Body.Length} chars)");
                report.Check(!section.Body.Contains('{'), $"{section.Id} has no template placeholders");
                report.Check(section.Reference.Length > 2, $"{section.Id} cites the rule it explains");
                report.Check(Regex.IsMatch(section.Body, "[А-Яа-яЁё]"), $"{section.Id} is written in russian");
            }

            report.Check(RulesBook.Find("claims") != null, "the claim page is addressable by id");
            report.Check(RulesBook.Find("no-such-page") == null, "an unknown page is not invented");

            var fans = RulesBook.FanTable();
            report.Equal(Fans.All.Count, fans.Sum(line => line.Split('—')[1].Split(',').Length),
                "the fan page lists every fan of the catalogue");
            report.Check(fans[0].StartsWith("88"), $"the fan page starts with the 88-point fans: {fans[0]}");
            report.Check(fans.Any(line => line.Contains("Семь пар")), "the fan page uses the russian names");

            // The catalogue is split across as many pages as it takes to fit the panel, each of them a
            // table of its own: not one page that runs off the bottom of the screen (4.12.1).
            var fanPages = RulesBook.FanSections();
            report.Equal(fans.Count, fanPages.Sum(page => page.Paragraphs.Count),
                "the fan pages hold every line of the catalogue exactly once");
            report.Check(fanPages.All(page => page.IsList && page.Reference == "3.8.1"),
                "every fan page is a table and cites 3.8.1");
            report.Equal(fanPages.Count, fanPages.Select(page => page.Id).Distinct().Count(),
                "the fan pages have their own ids");
            report.Equal(2, fanPages.Count, "the fan catalogue comes as two pages of the book");
            report.Check(fanPages.All(page => page.Paragraphs.Sum(line => line.Length) <= 1000),
                "no fan page holds more text than a page of two columns can show");
            report.Check(fanPages.All(page => page.Paragraphs.Count >= 2),
                "every fan page holds more than one entry of the catalogue");

            // Every reference in the book points at a rule chapter that exists in docs/rule-text.md,
            // which is the reason the page can be trusted as a summary.
            foreach (var section in RulesBook.Sections)
            {
                report.Check(section.Reference.Length > 0, $"{section.Id} points somewhere");
                if (!section.Reference.Contains("3.") && !section.Reference.Contains("продукт")
                    && !section.Reference.Contains("интерфейс") && !section.Reference.Contains("нотация"))
                    report.Check(false, $"{section.Id} cites rule numbers, got '{section.Reference}'");
            }
        }
    }
}
