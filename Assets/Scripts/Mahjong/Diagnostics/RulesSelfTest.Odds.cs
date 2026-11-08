using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Rules;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the win forecast (<see cref="WinOdds"/>): the distance of a hand, the four shares of a
    /// deal, the two doors the rules profile comes in by (minimum points and the classic dead wall) and
    /// the promise that the interface version never reads a hand it is not allowed to see.
    ///
    /// The numbers are properties, not golden values: a share is a probability, so the test asks for
    /// the things a probability must do — stay inside 0…1, add up to one with the draw, grow when the
    /// hand is closer, and stay put when only hidden tiles changed.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void OddsGroups(SelfTestReport report)
        {
            Run(report, "odds: the four shares and the draw add up to one", () => OddsDistribution(report));
            Run(report, "odds: distance to a win is measured, not guessed", () => OddsShanten(report));
            Run(report, "odds: a ready hand is worth more than a distant one", () => OddsRace(report));
            Run(report, "odds: the visible version never reads a hidden hand", () => OddsHiddenHands(report));
            Run(report, "odds: the profile sets the price, the wall and the minimum", () => OddsProfiles(report));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// A deal of four hands the test owns: the wall, the winds and the dice are real, and the tiles
        /// of each seat are exactly the ones written down, so a forecast can be compared with a shape.
        /// </summary>
        private static GameState OddsDeal(RuleVariant variant, string dealer, string south, string west,
            string north, int seed = 424242)
        {
            var state = new GameState(variant);
            state.StartHand(dealer: 0, seed: seed);
            var specs = new[] { dealer, south, west, north };
            for (var seat = 0; seat < 4; seat++)
            {
                if (specs[seat].Length == 0) continue;
                state.Players[seat] = Hand(specs[seat]);
            }
            return state;
        }

        private static readonly string[] RandomHands =
        {
            "1c 2c 3c 4d 5d 6d 7b 8b 9b 1d 2d 3d E",
            "1c 2c 3c 4d 5d 6d 7b 8b 9b 1d 2d 4d E",
            "1c 2c 3c 4d 5d 6d 7b 8b 9b E S W N",
            "1c 1c 2c 2c 3d 3d 4d 4d 5b 5b 6b 6b 1b",
            "1c 9c 1d 9d 1b 9b E S W N Rd Gd Wd 1c"
        };

        // ------------------------------------------------------------------ groups

        /// <summary>
        /// Every seat of a real deal gets a share, no share leaves 0…1, and the four of them plus the
        /// chance of a draw are exactly the whole hand — the property that makes the four numbers one
        /// distribution instead of four unrelated guesses.
        /// </summary>
        private static void OddsDistribution(SelfTestReport report)
        {
            foreach (var variant in RuleVariant.Presets)
            {
                for (var seed = 1; seed <= 12; seed++)
                {
                    var state = new GameState(variant);
                    state.StartHand(dealer: seed % 4, seed: seed * 7919);

                    foreach (var table in new[] { WinOdds.Oracle(state), WinOdds.FromSeat(state, state.Dealer) })
                    {
                        var total = table.DrawChance;
                        foreach (var seat in table.Seats)
                        {
                            report.Check(seat.WinChance >= 0d && seat.WinChance <= 1d,
                                $"share of P{seat.Seat} stays a probability ({seat.WinChance})");
                            report.Check(seat.ReadyChance >= 0d && seat.ReadyChance <= 1d,
                                $"readiness of P{seat.Seat} stays a probability ({seat.ReadyChance})");
                            report.Check(seat.Turns >= 0, "a seat never has negative draws left");
                            total += seat.WinChance;
                        }
                        report.Check(Math.Abs(total - 1d) < 1e-4,
                            $"the hand is a distribution: {total:F6} for {variant.Id}, seed {seed}");
                        report.Check(table.DrawChance >= 0d && table.DrawChance <= 1d,
                            "the draw keeps a probability of its own");
                        report.Check(table.WallDraws >= 0, "draws left are never negative");
                    }
                }
            }

            var sports = new GameState(RuleVariant.Sports);
            sports.StartHand(dealer: 1, seed: 99);
            var first = WinOdds.Oracle(sports).Describe();
            var again = WinOdds.Oracle(sports).Describe();
            report.Equal(first, again, "the forecast is deterministic for the same table");
        }

        /// <summary>
        /// The distance of a hand is the backbone of the whole estimate, so it is checked on shapes whose
        /// answer is known by hand: a finished hand, a ready one, six pairs, twelve orphans and a hand
        /// three combinations away.
        /// </summary>
        private static void OddsShanten(SelfTestReport report)
        {
            int Distance(string spec) => WinOdds.ShantenOf(
                Hand(spec).Concealed.Select(tile => tile.Type).ToList(), 0);

            report.Equal(-1, Distance("1c 2c 3c 4d 5d 6d 7b 8b 9b 1d 2d 3d E E"),
                "a complete hand is a won hand, not a distant one");
            report.Equal(0, Distance("1c 2c 3c 4d 5d 6d 7b 8b 9b 1d 2d 3d E"),
                "four sets and a single is tenpai");
            report.Equal(0, Distance("1c 1c 2c 2c 3d 3d 4d 4d 5b 5b 6b 6b 1b"),
                "six pairs are tenpai on the seventh");
            report.Equal(0, Distance("1c 9c 1d 9d 1b 9b E S W N Rd Gd Wd 1c"),
                "twelve orphans and a pair are tenpai");
            report.Equal(2, Distance("1c 2c 3c 4d 5d 6d 7b 8b 9b E S W N"),
                "three sets and four lone honours are two steps away");
            report.Check(Distance("1c 4c 7c 1d 4d 7d 1b 4b 7b E E S S")
                         > Distance("1c 2c 3c 1d 4d 7d 1b 4b 7b E E S S"),
                "a hand with a run in it is closer than the same hand without one");
            report.Equal(0, WinOdds.ShantenOf(Array.Empty<TileType>(), 4),
                "four declared sets and a lone pair is tenpai");
        }

        /// <summary>
        /// The race itself: with everything else equal, the seat that is one tile away must be given a
        /// better chance than the seat that is three tiles away, and the ready hand must be the
        /// favourite of the table.
        /// </summary>
        private static void OddsRace(SelfTestReport report)
        {
            var state = OddsDeal(RuleVariant.Sports,
                dealer: "1c 2c 3c 4d 5d 6d 7b 8b 9b 1d 2d 3d E",
                south: "1c 4c 7c 1d 4d 7d 1b 4b 7b E S W N",
                west: "2c 2c 2c 5d 5d 5d 8b 8b 8b 3c 3c 3c S",
                north: "1d 2d 3d 4b 5b 6b Rd Rd Rd 9c 9c 9c N");

            var oracle = WinOdds.Oracle(state);
            report.Check(oracle.Of(0).WinChance > oracle.Of(1).WinChance,
                "the ready hand wins more often than a hand three steps away");
            report.Equal(0, oracle.Of(0).Shanten, "the ready seat is measured as ready");
            report.Check(oracle.Of(1).Shanten >= 2, "the distant seat is measured as distant");
            report.Check(oracle.Favourite.Seat == 0, "the favourite of the table is the ready hand");
            report.Check(oracle.Of(0).ReadyChance >= oracle.Of(1).ReadyChance,
                "readiness follows the same race");

            // Same hands, fewer draws: a seat that has less wall left must never be given more hope.
            var early = WinOdds.Oracle(state).Of(0).WinChance;
            for (var draw = 0; draw < 40; draw++) state.Wall.DrawLive();
            var late = WinOdds.Oracle(state).Of(0).WinChance;
            report.Check(late <= early, $"a shorter wall is not better news ({late:F4} ≤ {early:F4})");
        }

        /// <summary>
        /// The promise of the doc comment: the estimate an interface may show cannot change when the
        /// hidden tiles of another seat change, while the oracle is free to notice the difference.
        /// </summary>
        private static void OddsHiddenHands(SelfTestReport report)
        {
            var one = OddsDeal(RuleVariant.Sports,
                dealer: "1c 2c 3c 4d 5d 6d 7b 8b 9b 1d 2d 3d E",
                south: "1c 4c 7c 1d 4d 7d 1b 4b 7b E S W N",
                west: "2c 2c 2c 5d 5d 5d 8b 8b 8b 3c 3c 3c S",
                north: "1d 2d 3d 4b 5b 6b Rd Rd Rd 9c 9c 9c N");
            var two = OddsDeal(RuleVariant.Sports,
                dealer: "1c 2c 3c 4d 5d 6d 7b 8b 9b 1d 2d 3d E",
                south: "2c 3c 4c 5d 6d 7d 8b 9b 1b E E E S",
                west: "2c 2c 2c 5d 5d 5d 8b 8b 8b 3c 3c 3c S",
                north: "1d 2d 3d 4b 5b 6b Rd Rd Rd 9c 9c 9c N");

            var visibleOne = WinOdds.FromSeat(one, 0);
            var visibleTwo = WinOdds.FromSeat(two, 0);
            report.Equal(visibleOne.Describe(), visibleTwo.Describe(),
                "the visible forecast ignores the tiles of the other seats");
            report.Check(!visibleOne.Of(1).Known, "a stranger's hand is reported as unknown");
            report.Check(visibleOne.Of(0).Known, "the observer's own hand is read exactly");
            report.Check(WinOdds.Oracle(one).Of(1).WinChance != WinOdds.Oracle(two).Of(1).WinChance,
                "the oracle does see the difference, which is why the interface may not use it");
        }

        /// <summary>
        /// The two profiles are not the same race. MCR asks eight points before a hand may win at all, so
        /// a ready chicken hand has no winning face there and has one in classic; the classic wall also
        /// stops with fourteen tiles standing and two free tiles, which is fewer draws for everybody.
        /// </summary>
        private static void OddsProfiles(SelfTestReport report)
        {
            // Three chows, a pair and a single: 123 Characters, 456 Dots, 789 Bamboo, 123 Dots, EE.
            // It is complete, and in the sporting profile it is worth nothing at all.
            const string chicken = "1c 2c 3c 4d 5d 6d 7b 8b 9b 1d 2d 3d E";
            var sports = OddsDeal(RuleVariant.Sports, chicken, "", "", "");
            var classic = OddsDeal(RuleVariant.Classic144, chicken, "", "", "");

            var sportsSeat = WinOdds.FromSeat(sports, 0).Of(0);
            var classicSeat = WinOdds.FromSeat(classic, 0).Of(0);
            report.Equal(0, sportsSeat.Shanten, "the hand is ready in both profiles");
            report.Equal(0, classicSeat.Shanten, "the hand is ready in both profiles");
            report.Equal(0, sportsSeat.LiveTiles, "MCR gives the chicken hand no winning face");
            report.Check(classicSeat.LiveTiles > 0, "classic lets the same hand win");
            report.Check(classicSeat.WinChance > sportsSeat.WinChance,
                "the hand that may win is worth more than the hand that may not");

            report.Check(classic.Wall.DrawableTiles < sports.Wall.DrawableTiles,
                "the classic wall keeps its dead end and its free tiles");
            report.Check(WinOdds.FromSeat(classic, 0).Of(0).Turns <= WinOdds.FromSeat(sports, 0).Of(0).Turns,
                "fewer live tiles in the wall means fewer draws");

            // A hand that is worth something in both profiles: the price comes from the profile's scorer.
            var valuable = "1c 1c 1c 2c 3c 4c 5c 6c 7c 8c 9c 9c 9c E";
            var sportsValue = WinOdds.FromSeat(OddsDeal(RuleVariant.Sports, valuable, "", "", ""), 0).Of(0);
            var classicValue = WinOdds.FromSeat(OddsDeal(RuleVariant.Classic144, valuable, "", "", ""), 0).Of(0);
            report.Check(sportsValue.BestPoints >= RuleVariant.Sports.MinimumPoints,
                $"a nine gates is worth the eight points of MCR ({sportsValue.BestPoints})");
            report.Check(classicValue.BestPoints > 0,
                $"the classic profile prices the same hand in points ({classicValue.BestPoints})");
            report.Check(classicValue.BestPoints != sportsValue.BestPoints,
                "the two profiles do not report the same number for the same hand");

            report.Equal(RuleVariant.Sports, RuleVariant.Parse("sports"),
                "the forecast of a table without a profile is the sporting one");
        }
    }
}
