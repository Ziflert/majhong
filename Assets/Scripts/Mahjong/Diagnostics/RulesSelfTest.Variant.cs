using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Localization;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the rules profile (<see cref="RuleVariant"/>): the two presets and what each switch
    /// changes, the classic scoring table with its doublings and the house limit, the wall that stops
    /// with fourteen tiles standing, the classic settlement and a whole classic session.
    ///
    /// The numbers come from the worked examples of <c>rule-2.pdf</c> (docs/rules-classic.md):
    /// example 2 — 123 and 456 Characters, 123 Bamboo, 333 Dots and a pair: 22 open, 24 concealed;
    /// example 3 — bamboo chows, a pung of Red Dragons and a pair: 2 + 4 + 20 = 26, doubled for the
    /// dragons and for the pure suit, 104; example 4 — 111 and 666 Dots and two Dot chows:
    /// 4 + 2 + 20 = 26, doubled thrice for the pure suit, 208.
    ///
    /// Wired into <see cref="RulesSelfTest.RunAll"/>, so the profile is checked headless and inside
    /// Unity, and the table on screen cannot claim a rule the engine does not play.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void VariantGroups(SelfTestReport report)
        {
            Run(report, "variant: the two presets and their switches", () => VariantPresets(report));
            Run(report, "variant: the threshold decides what may win", () => VariantThreshold(report));
            Run(report, "variant: classic scoring counts sets, doublings and the limit", () => ClassicScoringTable(report));
            Run(report, "variant: the classic wall ends with fourteen tiles and a free pair", () => ClassicWallEnd(report));
            Run(report, "variant: the classic settlement pays the full value and settles the losers",
                () => ClassicSettlementNumbers(report));
            Run(report, "variant: a classic four-set hand is offered as Hu", () => ClassicHuWithOpenAndClosedSets(report));
            Run(report, "variant: a classic session runs sixteen hands", () => ClassicSession(report));
        }

        // ------------------------------------------------------------ helpers

        /// <summary>
        /// A hand of the classic shape: concealed tiles, plus one pung per meld spec, all declared
        /// openly. Four sets and a pair is the only shape the classic rules know, so the samples are
        /// written as such.
        /// </summary>
        private static PlayerHand ClassicHand(string concealed, params string[] pungs)
        {
            var hand = Hand(concealed);
            foreach (var pung in pungs) hand.AddMeld(new Meld(MeldKind.Pung, Tiles(pung)));
            return hand;
        }

        private static int nextFlowerId = 40000;

        private static void AddFlowers(PlayerHand hand, params FlowerKind[] kinds)
        {
            foreach (var kind in kinds)
                hand.AddFlower(new Tile(new TileType(TileSuit.Flower, (int)kind), nextFlowerId++));
        }

        private static ScoreResult Classic(PlayerHand hand, TileType? winningTile, ClaimSource source,
            Wind seat = Wind.East, RuleVariant? variant = null) =>
            (variant ?? RuleVariant.Classic144).Score(new ScoringContext
            {
                Hand = hand,
                WinningTile = winningTile,
                Source = source,
                RoundWind = Wind.East,
                SeatWind = seat
            });

        private static void ClassicHuWithOpenAndClosedSets(SelfTestReport report)
        {
            // Two open chows, one open pung, one concealed pung and a pair: the exact hand
            // that must produce a Hu button on the classic table (the self-drawn tile is 7s).
            var hand = Hand("7b 7b 7b 2d 2d");
            hand.AddMeld(new Meld(MeldKind.Chow, Tiles("1c 2c 3c"), 1));
            hand.AddMeld(new Meld(MeldKind.Chow, Tiles("4c 5c 6c"), 2));
            hand.AddMeld(new Meld(MeldKind.Pung, Tiles("9d 9d 9d"), 3));

            var result = RuleVariant.Classic144.Score(new ScoringContext
            {
                Hand = hand,
                WinningTile = ParseTile("7b"),
                WinningTileInHand = true,
                Source = ClaimSource.SelfDraw,
                SeatWind = Wind.East,
                RoundWind = Wind.East
            });
            report.Check(result.IsValidWin, "two open chows + open pung + concealed pung + pair must be a classic Hu: " + result.Error);
        }

        private static int Doublings(ScoreResult result, string id) =>
            result.Classic?.Doublings.Count(doubling => doubling.Id == id) ?? 0;

        private static int Part(ScoreResult result, string id) =>
            result.Classic?.Parts.Where(part => part.Id == id).Sum(part => part.Points) ?? 0;

        private static int Points(IReadOnlyList<Payment> payments, int from, int to) =>
            payments.Where(payment => payment.FromPlayer == from && payment.ToPlayer == to)
                .Sum(payment => payment.Points);

        private static int[] Balance(IReadOnlyList<Payment> payments)
        {
            var balance = new int[4];
            foreach (var payment in payments)
            {
                balance[payment.FromPlayer] -= payment.Points;
                balance[payment.ToPlayer] += payment.Points;
            }
            return balance;
        }

        // ------------------------------------------------------------- groups

        private static void VariantPresets(SelfTestReport report)
        {
            var sports = RuleVariant.Sports;
            report.Equal("sports", sports.Id, "the sporting preset keeps its id, which saves record");
            report.Equal(ScoringMode.Fans, sports.Mode, "the sporting table is priced in fans");
            report.Equal(Scoring.MinimumPoints, sports.MinimumPoints, "the sporting minimum is the eight points of 3.4.22");
            report.Check(sports.HasMinimumPoints, "the sporting table needs a minimum to win");
            report.Equal(0, sports.DeadWallTiles, "MCR plays the wall to its last tile");
            report.Equal(0, sports.FreeTiles, "MCR has no free tiles");
            report.Equal(0, sports.HouseLimit, "MCR knows no house limit");
            report.Check(!sports.HasFreeTiles, "the sporting wall keeps nothing back");
            report.Check(!sports.DealerKeepsSeatOnWin, "MCR passes the dice on after every hand (3.4.8)");

            var classic = RuleVariant.Classic144;
            report.Equal("classic144", classic.Id, "the classic preset keeps its id, which saves record");
            report.Equal(ScoringMode.ClassicPoints, classic.Mode, "the classic table is priced in points and doublings");
            report.Equal(0, classic.MinimumPoints, "any Mah-Jong wins in the classic rules");
            report.Check(!classic.HasMinimumPoints, "the classic table has no minimum");
            report.Equal(14, classic.DeadWallTiles, "the classic hand stops with fourteen tiles standing");
            report.Equal(2, classic.FreeTiles, "two free tiles lie at the end of the classic wall");
            report.Equal(2000, classic.HouseLimit, "the house is full at two thousand, the example of the rulebook");
            report.Check(classic.HasFreeTiles, "the classic wall keeps a dead end");
            report.Check(classic.DealerKeepsSeatOnWin, "a winning dealer keeps the seat in the classic rules");

            var classic136 = RuleVariant.Classic136;
            report.Equal(136, WallGeometry.TileCountOf(classic136.WallStacks), "classic 136 has 136 wall positions");
            report.Check(!classic136.IncludeFlowers, "classic 136 excludes bonus tiles");
            var compact = new GameState(classic136);
            compact.StartHand(seed: 11);
            report.Equal(136, compact.Wall.TotalTiles, "classic 136 builds the compact wall");
            report.Equal(17, compact.Wall.StacksPerSide, "classic 136 builds seventeen stacks per side");
            report.Check(compact.Wall.TilesInRingOrder.All(tile => !tile.IsFlower), "classic 136 has no flowers");

            report.Equal(3, RuleVariant.Presets.Count, "the table offers three presets");
            report.Equal(3, RuleVariant.Presets.Select(preset => preset.Id).Distinct().Count(), "no preset is listed twice");
            report.Equal(sports, RuleVariant.Default, "a table that asks for no profile plays the sporting rules");
            report.Check(sports.NameRu != sports.NameEn, "the sporting preset is named in both languages");
            report.Check(classic.NameRu != classic.NameEn, "the classic preset is named in both languages");
            report.Check(sports.NameRu != classic.NameRu, "the presets cannot be confused on screen");

            // Ids as the environment, a save or a tool may spell them.
            foreach (var id in new[] { "sports", "SPORTS", "sport", "mcr", "MCR", "wmo", "WMCC", "sporting", " sports " })
                report.Equal(sports, RuleVariant.Parse(id), $"\"{id}\" names the sporting rules");
            foreach (var id in new[] { "classic144", "Classic-144", "classic_144", "classic", "classicpoints", "home" })
                report.Equal(classic, RuleVariant.Parse(id), $"\"{id}\" names the classic rules");
            report.Equal(classic136, RuleVariant.Parse("classic136"), "classic136 names the compact rules");

            foreach (var nonsense in new[] { "мусор", "sporting-plus" })
                report.Equal<RuleVariant?>(null, RuleVariant.TryParse(nonsense),
                    $"\"{nonsense}\" names no profile this build plays");
            report.Equal<RuleVariant?>(null, RuleVariant.TryParse(""), "an empty profile name is no profile");
            report.Equal<RuleVariant?>(null, RuleVariant.TryParse(null), "a missing profile name is no profile");
            report.Equal(sports, RuleVariant.Parse("мусор"), "a broken profile name falls back to the sporting rules");

            // The table settings hold the profile next to the language and the bots, and read it from
            // the environment so a capture run can ask for classic rules without touching the scene.
            report.Equal(sports, new GameSettings().Variant, "the game starts on the sporting rules");
            var previous = Environment.GetEnvironmentVariable("MAHJONG_VARIANT");
            try
            {
                Environment.SetEnvironmentVariable("MAHJONG_VARIANT", "classic144");
                var fromEnvironment = GameSettings.FromEnvironment();
                report.Equal(classic, fromEnvironment.Variant, "MAHJONG_VARIANT=classic144 plays the classic rules");
                Environment.SetEnvironmentVariable("MAHJONG_VARIANT", "MCR");
                report.Equal(sports, GameSettings.FromEnvironment().Variant, "MAHJONG_VARIANT=MCR stays sporting");
                Environment.SetEnvironmentVariable("MAHJONG_VARIANT", "какой-то текст");
                report.Equal(sports, GameSettings.FromEnvironment().Variant, "a broken profile keeps the sporting rules");
                Environment.SetEnvironmentVariable("MAHJONG_VARIANT", null);
                report.Equal(sports, GameSettings.FromEnvironment().Variant, "no profile variable means the sporting rules");
            }
            finally
            {
                Environment.SetEnvironmentVariable("MAHJONG_VARIANT", previous);
            }
        }

        private static void VariantThreshold(SelfTestReport report)
        {
            // Four fans of fully concealed and a pair of East: a win in the classic rules, nothing in MCR.
            var low = Hand("1c 2c 3c 4d 5d 6d 2b 3b 4b 6b 7b 8b E E");
            var context = new ScoringContext { Hand = low, Source = ClaimSource.SelfDraw };

            var sporting = RuleVariant.Sports.Score(context);
            report.Check(!sporting.IsValidWin, "four points may not win a sporting hand");
            report.Equal(4, sporting.HandPoints, "the hand is worth four fans before the minimum");
            report.Check(sporting.Error != null && sporting.Error.Contains("8"),
                $"the refusal must name the minimum: {sporting.Error}");

            var classic = RuleVariant.Classic144.Score(context);
            report.Check(classic.IsValidWin, $"the same hand wins under the classic rules: {classic.Error}");
            report.Equal(26, classic.Classic!.BasePoints, "pair of the double wind 4 + Mah-Jong 20 + two for the wall");
            report.Equal(4, Part(classic, "pair-winds"), "a pair of East in an East round scores both rows");
            report.Equal(20, Part(classic, "mahjong"), "every Mah-Jong is twenty points");
            report.Equal(2, Part(classic, "from-the-wall"), "a tile drawn from the wall scores two");

            // The profile writes its minimum into the context, so scoring the same context again with
            // the sporting rules must still refuse it: one table's rules cannot leak into another's.
            var again = RuleVariant.Sports.Score(context);
            report.Check(!again.IsValidWin, "the sporting minimum is restored after a classic score");

            // A hand worth winning in both: it is the price that differs, not the fact of the win.
            var rich = Hand("1c 1c 1c 3c 3c 3c 5c 5c 5c 7c 7c 7c 9c 9c");
            var richSporting = Score(rich, null, ClaimSource.SelfDraw);
            var richClassic = Classic(rich, null, ClaimSource.SelfDraw);
            report.Check(richSporting.IsValidWin, $"the flush must win sportingly: {richSporting.Error}");
            report.Check(richSporting.HandPoints >= Scoring.MinimumPoints, "and it must reach the minimum");
            report.Check(richClassic.IsValidWin, $"the flush must win classically: {richClassic.Error}");
            report.Equal(3, Doublings(richClassic, "pure-suit-thrice"),
                "a pure suit without honours doubles three times");
            report.Check(richClassic.Classic!.Value > richSporting.HandPoints,
                $"the two tables price the same hand differently ({richClassic.Classic.Value} against {richSporting.HandPoints})");

            // The classic rules know one shape only: seven pairs and the other special patterns of MCR
            // are not wins there (docs/rules-classic.md, decision 6).
            var pairs = Hand("1c 1c 2c 2c 3d 3d 4d 4d 5b 5b 6b 6b 1b 1b");
            report.Check(Score(pairs, null, ClaimSource.SelfDraw).IsValidWin, "seven pairs win in MCR");
            var pairsClassic = Classic(pairs, null, ClaimSource.SelfDraw);
            report.Check(!pairsClassic.IsValidWin, "seven pairs are not a classic Mah-Jong");
            report.Check(pairsClassic.Error != null && pairsClassic.Error.Contains("four sets and a pair"),
                $"the refusal must name the shape: {pairsClassic.Error}");
        }

        private static void ClassicScoringTable(SelfTestReport report)
        {
            // The table of section V, set by set.
            report.Equal(0, ClassicScoring.SetPoints(Group(GroupKind.Chow, "2d", concealed: true)), "a chow is worth nothing");
            report.Equal(2, ClassicScoring.SetPoints(Group(GroupKind.Pung, "2d", concealed: false)), "an open simple pung is two");
            report.Equal(4, ClassicScoring.SetPoints(Group(GroupKind.Pung, "2d", concealed: true)), "a concealed simple pung is four");
            report.Equal(4, ClassicScoring.SetPoints(Group(GroupKind.Pung, "1d", concealed: false)), "an open terminal pung is four");
            report.Equal(8, ClassicScoring.SetPoints(Group(GroupKind.Pung, "1d", concealed: true)), "a concealed terminal pung is eight");
            report.Equal(4, ClassicScoring.SetPoints(Group(GroupKind.Pung, "Rd", concealed: false)), "a pung of dragons is four");
            report.Equal(8, ClassicScoring.SetPoints(Group(GroupKind.Pung, "Rd", concealed: true)), "a concealed pung of dragons is eight");
            report.Equal(8, ClassicScoring.SetPoints(Group(GroupKind.Pung, "2d", concealed: false, kong: true)), "a kong is worth four pungs");
            report.Equal(16, ClassicScoring.SetPoints(Group(GroupKind.Pung, "2d", concealed: true, kong: true)), "a concealed kong is worth eight pungs");
            report.Equal(32, ClassicScoring.SetPoints(Group(GroupKind.Pung, "E", concealed: true, kong: true)), "a concealed kong of winds is thirty-two");

            report.Equal(0, ClassicScoring.PairPoints(null, Wind.East, Wind.East), "no pair, no points");
            report.Equal(0, ClassicScoring.PairPoints(ParseTile("5d"), Wind.East, Wind.East), "a pair of simples is worth nothing");
            report.Equal(2, ClassicScoring.PairPoints(ParseTile("Rd"), Wind.East, Wind.East), "a pair of dragons is two");
            report.Equal(2, ClassicScoring.PairPoints(ParseTile("E"), Wind.East, Wind.South), "the seat wind scores two");
            report.Equal(4, ClassicScoring.PairPoints(ParseTile("E"), Wind.East, Wind.East),
                "a pair that is both the seat and the round wind scores both rows");

            report.Equal((FlowerKind.Spring, FlowerKind.Plum), ClassicScoring.OwnBonuses(Wind.East), "East owns spring and plum");
            report.Equal((FlowerKind.Summer, FlowerKind.Orchid), ClassicScoring.OwnBonuses(Wind.South), "South owns summer and orchid");
            report.Equal((FlowerKind.Autumn, FlowerKind.Chrysanthemum), ClassicScoring.OwnBonuses(Wind.West), "West owns autumn and chrysanthemum");
            report.Equal((FlowerKind.Winter, FlowerKind.Bamboo), ClassicScoring.OwnBonuses(Wind.North), "North owns winter and bamboo");

            // Example 4 of the rulebook: 111 and 666 Dots and two Dot chows, won on 9 Dots from a discard.
            // 4 + 2 + 20 = 26, doubled thrice for the pure suit: 208.
            var example4 = ClassicHand("2d 3d 4d 7d 8d 5d 5d", "1d 1d 1d", "6d 6d 6d");
            var four = Classic(example4, ParseTile("9d"), ClaimSource.Discard);
            report.Check(four.IsValidWin, $"example 4 must win: {four.Error}");
            report.Equal(4, Part(four, "pung-terminals"), "an open pung of terminals is four");
            report.Equal(2, Part(four, "pung-simples"), "an open pung of simples is two");
            report.Equal(20, Part(four, "mahjong"), "Mah-Jong is twenty");
            report.Equal(26, four.Classic!.BasePoints, "the sum of example 4 of the rulebook");
            report.Equal(3, four.Classic.DoublingCount, "a pure suit without honours doubles three times");
            report.Equal(208, four.Classic.Value, "26 doubled thrice is 208, exactly as the rulebook works it out");
            report.Check(!four.Classic.AtLimit, "208 is under the house limit");

            var limited = RuleVariant.Classic144 with { HouseLimit = 100 };
            var capped = Classic(example4, ParseTile("9d"), ClaimSource.Discard, Wind.East, limited);
            report.Equal(26, capped.Classic!.BasePoints, "the limit caps the value, not the sum of the sets");
            report.Equal(100, capped.Classic.Value, "a combination over the limit is paid at the limit");
            report.Check(capped.Classic.AtLimit, "the result says it was capped");

            // Example 2: 123 and 456 Characters, 123 Bamboo, 333 Dots and a pair of 9 Bamboo.
            // 2 + 20 = 22 open, 4 + 20 = 24 concealed.
            var open = Classic(ClassicHand("1c 2c 3c 4c 5c 6c 9b 9b 4b 5b", "3d 3d 3d"), ParseTile("6b"), ClaimSource.Discard);
            report.Check(open.IsValidWin, $"example 2 must win: {open.Error}");
            report.Equal(22, open.Classic!.Value, "the open example of the rulebook is worth 22");
            report.Equal(0, open.Classic.DoublingCount, "mixed suits earn no doubling");

            var closed = Classic(ClassicHand("1c 2c 3c 4c 5c 6c 9b 9b 4b 5b 3d 3d 3d"), ParseTile("6b"), ClaimSource.Discard);
            report.Equal(24, closed.Classic!.Value, "the same hand with a concealed pung is worth 24");

            // Example 3: bamboo chows, a pung of Red Dragons and a pair: 2 + 4 + 20 = 26,
            // doubled for the dragons and for the pure suit: 104.
            var example3 = Classic(ClassicHand("1b 2b 3b 4b 5b 6b 7b 8b 9b 5b", "Rd Rd Rd"), ParseTile("5b"), ClaimSource.Discard);
            report.Check(example3.IsValidWin, $"example 3 must win: {example3.Error}");
            report.Equal(2, Part(example3, "single-wait"), "the pair wait scores two");
            report.Equal(26, example3.Classic!.BasePoints, "the sum of example 3 of the rulebook");
            report.Equal(1, Doublings(example3, "dragons"), "a pung of dragons doubles");
            report.Equal(1, Doublings(example3, "pure-suit"), "one suit plus honours doubles once");
            report.Equal(104, example3.Classic.Value, "26 doubled twice is 104, as the rulebook works it out");

            // Flowers belong to every seat, not only to the winner: four points each, the own one
            // doubles the sum, a full bouquet doubles it twice more.
            var flowers = ClassicHand("1c 2c 3c 4d 5d 6d 7b 8b 9b 1c 2c 3c 5d");
            AddFlowers(flowers, FlowerKind.Spring, FlowerKind.Autumn);
            var twoFlowers = ClassicScoring.ValueOf(flowers, Wind.East, Wind.East);
            report.Equal(8, twoFlowers.BasePoints, "two flowers are eight points");
            report.Equal(1, twoFlowers.Doublings.Count, "only the own flower doubles here");
            report.Equal(16, twoFlowers.Value, "eight doubled by the own spring is sixteen");

            AddFlowers(flowers, FlowerKind.Summer, FlowerKind.Winter);
            var bouquet = ClassicScoring.ValueOf(flowers, Wind.East, Wind.East);
            report.Equal(16, bouquet.BasePoints, "four flowers are sixteen points");
            report.Equal(3, bouquet.Doublings.Count, "the own flower and the full bouquet, twice");
            report.Equal(128, bouquet.Value, "sixteen doubled three times is 128");

            // A seat that did not win still settles by its whole combination, declared melds included.
            var kong = new PlayerHand();
            kong.AddMeld(new Meld(MeldKind.OpenKong, Tiles("5d 5d 5d 5d")));
            foreach (var tile in Tiles("1c 2c 3c 4d 6d 7d 1b 2b 3b 9b")) kong.AddConcealed(tile);
            var kongValue = ClassicScoring.ValueOf(kong, Wind.South, Wind.East);
            report.Equal(8, kongValue.BasePoints, "an open kong of simples is eight points for a loser too");
            report.Check(kongValue.Parts.Any(part => part.Id == "kong-simples"), "and it is listed in the breakdown");
        }

        private static ClassicGroup Group(GroupKind kind, string baseTile, bool concealed, bool kong = false) =>
            new() { Kind = kind, Base = ParseTile(baseTile), IsConcealed = concealed, IsKong = kong };

        private static void ClassicWallEnd(SelfTestReport report)
        {
            // The wall itself: 144 tiles, a dead end of fourteen and two free tiles on top of it.
            var wall = Wall.CreateShuffled(new Random(11), 14, 2);
            report.Equal(144, wall.TotalTiles, "the classic wall is the whole set");
            report.Equal(16, wall.ReservedTiles, "fourteen standing plus the free pair");
            report.Equal(128, wall.DrawableTiles, "128 live tiles once the dead end and the free pair are set aside");
            wall.Break(1, 6);

            for (var draw = 1; draw <= 127; draw++) wall.DrawLive();
            report.Equal(17, wall.Remaining, "one live tile is left after 127 draws");
            report.Check(wall.IsLastLiveTile, "the next draw is the last one the wall may give");
            report.Check(!wall.IsExhausted, "the live wall is not finished yet");
            report.Equal(1, wall.DrawableTiles, "exactly one live tile is still drawable");

            wall.DrawLive();
            report.Equal(16, wall.Remaining, "the classic hand stops with sixteen tiles standing");
            report.Check(wall.IsExhausted, "no more live tiles may be drawn");
            report.Equal(0, wall.DrawableTiles, "nothing is drawable any more");
            report.Check(wall.CanReplace, "a flower or a kong may still be paid from the dead end");
            report.Equal(2, wall.FreeTilePair().Count, "the two free tiles are lying on the wall");
            CheckThrows(report, () => wall.DrawLive(), "a live draw past the dead end must fail");

            wall.DrawReplacement();
            report.Equal(1, wall.FreeTilePair().Count, "one free tile is spent");
            wall.DrawReplacement();
            report.Equal(14, wall.Remaining, "the dead end is all that is left");
            report.Equal(0, wall.FreeTilePair().Count, "both free tiles are spent");
            report.Check(!wall.CanReplace, "nothing may be replaced any more");
            CheckThrows(report, () => wall.DrawReplacement(), "a replacement past the dead end must fail");

            var sporting = Wall.CreateShuffled(new Random(11));
            sporting.Break(1, 6);
            var liveDraws = 0;
            while (!sporting.IsExhausted)
            {
                sporting.DrawLive();
                liveDraws++;
            }
            report.Equal(144, liveDraws, "MCR plays the wall to its last tile");
            report.Equal(0, sporting.Remaining, "the sporting wall ends empty");
            report.Check(!sporting.CanReplace, "the sporting wall pays no replacements at its end");
            report.Equal(0, sporting.FreeTilePair().Count, "the sporting wall has no free tiles");

            // Real hands: the classic wall stops where the rules say, and the last live tile is known.
            var lastTileMarks = 0;
            var draws = 0;
            var atSixteen = 0;
            for (var seed = 0; seed < 24; seed++)
            {
                var state = new GameState(RuleVariant.Classic144);
                state.StartHand(seed: seed, dealer: seed % 4);
                PlayClassicHand(report, state, new Random(seed * 31 + 5), ref lastTileMarks);

                var inPlay = state.Players.Sum(player => player.Concealed.Count + player.Flowers.Count
                                                         + player.Melds.Sum(meld => meld.Tiles.Count))
                            + state.Discards.Count;
                report.Equal(144, inPlay + state.Wall.Remaining, $"seed {seed}: tiles in play plus the wall");

                if (!state.EndedInDraw) continue;
                draws++;
                report.Equal(0, state.Wall.DrawableTiles, $"seed {seed}: no live tile is left in a drawn hand");
                var standing = state.Wall.Remaining;
                report.Check(standing >= state.Wall.DeadWallTiles && standing <= state.Wall.ReservedTiles,
                    $"seed {seed}: a drawn hand leaves fourteen to sixteen tiles standing, got {standing}");
                report.Equal(standing - state.Wall.DeadWallTiles, state.Wall.FreeTilePair().Count,
                    $"seed {seed}: the free tiles are counted on top of the dead end");
                if (standing == 16) atSixteen++;
            }

            report.Check(draws > 12, $"random play should mostly end in a drawn hand, got {draws} of 24");
            report.Check(atSixteen >= draws - 6,
                $"most drawn hands should stop at sixteen tiles, {atSixteen} of {draws} did");
            report.Check(lastTileMarks > 0, "the last live tile of the wall must be marked at least once");
        }

        /// <summary>
        /// Plays a hand with random legal actions, noting how often the draw was the last live tile and
        /// checking that once the wall is down to its dead end only Hu may be declared.
        /// </summary>
        private static void PlayClassicHand(SelfTestReport report, GameState state, Random random, ref int lastTileMarks)
        {
            var steps = 0;
            while (state.Phase != GamePhase.HandFinished && steps++ < 4000)
            {
                if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                {
                    for (var player = 0; player < 4; player++)
                    {
                        var options = state.GetLegalActions(player);
                        if (options.Count == 0) continue;
                        if (state.Wall.IsExhausted)
                            report.Check(options.All(option => option.Kind is ActionKind.Win or ActionKind.Pass),
                                "after the last live tile only Hu may be declared");
                        state.Apply(Choose(options, random, state.Players[player]));
                        if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;
                    }
                    continue;
                }

                var current = state.GetLegalActions(state.CurrentPlayer);
                if (current.Count == 0) break;
                state.Apply(Choose(current, random, state.Players[state.CurrentPlayer]));
                if (state.LastDrawWasLastWallTile) lastTileMarks++;
            }
        }

        private static void CheckThrows(SelfTestReport report, Action action, string message)
        {
            var threw = false;
            try { action(); }
            catch (InvalidOperationException) { threw = true; }
            report.Check(threw, message);
        }

        private static void ClassicSettlementNumbers(SelfTestReport report)
        {
            // Section IV: every loser pays the winner the full value, then the losers settle by the
            // difference of their own combinations, and every transfer East takes part in is doubled.
            var winners = new[] { 26, 10, 4, 2 };

            // East (seat 0) wins: all three losers pay double, and nothing between them involves East.
            var eastWins = Settlements.ForClassic(winners, 0, 0);
            report.Equal(156, eastWins.WinnerGain, "a winning East takes twice his value from each of the three");
            report.Equal(52, Points(eastWins.Payments, 1, 0), "the first loser pays the doubled value");
            report.Equal(52, Points(eastWins.Payments, 2, 0), "the second loser pays the doubled value");
            report.Equal(52, Points(eastWins.Payments, 3, 0), "the third loser pays the doubled value");
            report.Equal(6, Points(eastWins.Payments, 2, 1), "the cheaper loser pays the difference to the dearer one");
            report.Equal(8, Points(eastWins.Payments, 3, 1), "and so does the cheapest");
            report.Equal(2, Points(eastWins.Payments, 3, 2), "the two cheapest settle between themselves");
            report.Equal(0, Points(eastWins.Payments, 0, 1), "the winner neither pays nor receives among the losers");
            report.Equal(0, Balance(eastWins.Payments).Sum(), "the settlement is zero-sum");

            // East loses: he pays double to the winner and doubles both sides of his own transfers.
            var eastLoses = Settlements.ForClassic(winners, 1, 0);
            report.Equal(40, eastLoses.WinnerGain, "the winner takes the full value of his combination from each loser");
            report.Equal(20, Points(eastLoses.Payments, 0, 1), "a losing East pays the winner double");
            report.Equal(10, Points(eastLoses.Payments, 2, 1), "the other losers pay the value itself");
            report.Equal(10, Points(eastLoses.Payments, 3, 1), "every loser pays the same value");
            report.Equal(44, Points(eastLoses.Payments, 2, 0), "a transfer with East in it is doubled");
            report.Equal(48, Points(eastLoses.Payments, 3, 0), "East receives his difference doubled too");
            report.Equal(2, Points(eastLoses.Payments, 3, 2), "a transfer between two other seats is not doubled");
            report.Equal(0, Balance(eastLoses.Payments).Sum(), "the settlement is zero-sum");

            // The result screen of a classic hand is not a fan list but the sum, the doublings, the
            // four combinations and the transfers; it is the one piece of the classic table that only
            // lives in the interface, so its text is checked here.
            var scored = Classic(ClassicHand("2d 3d 4d 7d 8d 5d 5d", "1d 1d 1d", "6d 6d 6d"),
                ParseTile("9d"), ClaimSource.Discard);
            var paid = Settlements.ForClassic(new[] { 208, 26, 4, 0 }, 0, 0);
            var win = new WinResult(0, ClaimSource.Discard, scored);
            var previousLanguage = Loc.Current;
            try
            {
                Loc.Current = Language.Ru;
                var russian = Text.ClassicSettlementSummary(win, paid);
                Loc.Current = Language.En;
                var english = Text.ClassicSettlementSummary(win, paid);
                report.Check(russian != english, "the result screen is written in both languages");
                foreach (var summary in new[] { russian, english })
                {
                    report.Check(summary.Contains("208"), $"the summary shows the value after the doublings: {summary}");
                    report.Check(summary.Contains("P1=208"), $"the summary shows what every seat is worth: {summary}");
                    report.Check(summary.Contains("416"), $"the summary shows the transfers, doubled for East: {summary}");
                }
            }
            finally
            {
                Loc.Current = previousLanguage;
            }

            var record = eastLoses.Classic!;
            report.Equal(4, record.Values.Count, "every seat has a value, winner or not");
            report.Equal(26, record.Values[0], "the values are kept as they were counted");
            report.Equal(0, record.EastSeat, "the settlement remembers which seat was East");
            var capped = Settlements.ForClassic(winners, 1, 0, limit: 100, selfDrawn: true);
            report.Equal(100, capped.Classic!.Limit, "the settlement records the house limit it was paid under");
            report.Check(capped.SelfDrawn, "the settlement remembers a self-drawn win");

            // The dispatcher picks the arithmetic of the profile: one table cannot pay the other's hand.
            var state = PlayBotHandToWin(RuleVariant.Classic144);
            report.Check(state != null, "a bot hand must win for the settled hand to be checked");
            if (state == null) return;

            var classic = Settlements.ForState(state, RuleVariant.Classic144);
            report.Check(classic.Classic != null, "a classic hand is settled by the classic arithmetic");
            report.Equal(state.Result!.Score.Classic!.Value, classic.Classic!.Values[state.Result.PlayerIndex],
                "the winner's value is the value of his combination");
            report.Equal(state.Dealer, classic.Classic.EastSeat, "the dealer is East and pays and receives double");
            report.Check(classic.WinnerGain > 0, "a winner is paid something");
            report.Equal(0, Balance(classic.Payments).Sum(), "the classic settlement of a real hand is zero-sum");
            report.Check(Settlements.ForState(state, RuleVariant.Sports).Classic == null,
                "the same hand under the sporting profile is paid by the MCR arithmetic");
        }

        /// <summary>A hand between bots played until somebody wins; null when every seed only drew.</summary>
        private static GameState? PlayBotHandToWin(RuleVariant variant)
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var state = new GameState(variant);
                state.StartHand(seed: seed, dealer: seed % 4);
                var bots = new IMahjongBot[]
                {
                    new HeuristicBot(seed * 10 + 1), new HeuristicBot(seed * 10 + 2),
                    new HeuristicBot(seed * 10 + 3), new HeuristicBot(seed * 10 + 4)
                };
                BotTable.PlayHand(state, bots, new Random(seed));
                if (state.Result != null) return state;
            }
            return null;
        }

        private static void ClassicSession(SelfTestReport report)
        {
            var dealerWins = 0;
            var wrongSeats = 0;
            var wins = 0;

            for (var seed = 0; seed < 2; seed++)
            {
                var session = new GameSession(null, RuleVariant.Classic144);
                report.Equal(RuleVariant.Classic144, session.Variant, "the session plays the profile it was given");
                var random = new Random(seed * 100 + 3);
                var bots = new IMahjongBot[]
                {
                    new HeuristicBot(seed * 10 + 1), new HeuristicBot(seed * 10 + 2),
                    new HeuristicBot(seed * 10 + 3), new HeuristicBot(seed * 10 + 4)
                };

                for (var hand = 0; hand < GameSession.TotalHands; hand++)
                {
                    var dealer = session.Dealer;
                    report.Equal((Wind)(hand / 4), session.RoundWind, $"seed {seed}: round wind of hand {hand}");
                    session.StartNextHand(random.Next());
                    report.Equal(RuleVariant.Classic144, session.State!.Variant, "every hand is dealt by the profile");
                    BotTable.UpdateMatchContext(session, bots);
                    BotTable.PlayHand(session.State!, bots, random);

                    var winner = session.State!.Result?.PlayerIndex ?? -1;
                    session.CompleteHand();

                    if (winner >= 0) wins++;
                    if (winner == dealer)
                    {
                        dealerWins++;
                        if (session.Dealer != dealer) wrongSeats++;
                    }
                    else if (session.Dealer != (dealer + 1) % 4)
                    {
                        wrongSeats++;
                    }
                }

                report.Check(session.IsFinished, "sixteen hands finish a classic session");
                report.Equal(GameSession.TotalHands, session.HandsPlayed, "the session plays sixteen hands");
                report.Equal(0, session.Scores.Sum(), "classic payments are zero-sum");
                report.Equal(GameSession.TotalHands, session.History.Count, "every hand is settled once");
                report.Check(session.History.Where(settled => settled.WinnerPlayer >= 0)
                        .All(settled => settled.Classic != null),
                    "every won hand is settled by the classic arithmetic");
                report.Check(session.History.Any(settled => settled.WinnerPlayer >= 0 && settled.WinnerGain > 0),
                    "a classic session actually pays winners");
                report.Check(session.History.All(settled => Balance(settled.Payments).Sum() == 0),
                    "every classic settlement moves points from one seat to another and loses none");
                report.Check(session.History.Sum(settled => settled.WinnerGain) > 0, "the session pays the winners");
                var tablePoints = session.TablePoints();
                report.Check(Math.Abs(tablePoints.Sum() - 7) < 1e-9,
                    $"classic table points must total 7, got {tablePoints.Sum()}");
            }

            report.Check(wins > 0, "the bots win classic hands");
            report.Check(dealerWins > 0, $"a winning dealer must occur, got {dealerWins} in two sessions");
            report.Equal(0, wrongSeats, "the dealer keeps his seat only when he wins, and passes it otherwise");
        }
    }
}
