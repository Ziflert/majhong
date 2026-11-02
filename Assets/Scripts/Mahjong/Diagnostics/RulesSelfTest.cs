using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Controls;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>Result of a headless rules self-test run.</summary>
    public sealed class SelfTestReport
    {
        public List<string> Failures { get; } = new();
        public List<string> Passed { get; } = new();
        public int Checks { get; private set; }
        public bool PassedAll => Failures.Count == 0;

        internal void Check(bool condition, string message)
        {
            Checks++;
            if (!condition) throw new SelfTestFailure(message);
        }

        internal void Equal<T>(T expected, T actual, string message)
        {
            Checks++;
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new SelfTestFailure($"{message}: expected {expected}, got {actual}");
        }
    }

    public sealed class SelfTestFailure : Exception
    {
        public SelfTestFailure(string message) : base(message) { }
    }

    /// <summary>
    /// Verification of the rules core that runs both inside Unity (NUnit wrapper)
    /// and headless (Tools/RulesCheck). No UnityEngine dependency.
    /// </summary>
    public static class RulesSelfTest
    {
        public static SelfTestReport RunAll()
        {
            var report = new SelfTestReport();

            Run(report, "wall: 144 tiles and a break point", () => WallBasics(report));
            Run(report, "deal: tile counts and wall left", () => DealBasics(report));
            Run(report, "hand: standard decomposition", () => StandardHand(report));
            Run(report, "hand: seven pairs", () => SevenPairsHand(report));
            Run(report, "hand: thirteen orphans", () => ThirteenOrphansHand(report));
            Run(report, "hand: waits", () => WaitDetection(report));
            Run(report, "score: seven pairs", () => ScoreSevenPairs(report));
            Run(report, "score: thirteen orphans", () => ScoreThirteenOrphans(report));
            Run(report, "score: big four winds", () => ScoreBigFourWinds(report));
            Run(report, "score: little three dragons", () => ScoreLittleThreeDragons(report));
            Run(report, "score: nine gates", () => ScoreNineGates(report));
            Run(report, "score: four kongs", () => ScoreFourKongs(report));
            Run(report, "score: full flush four concealed pungs", () => ScoreFullFlushPungs(report));
            Run(report, "score: chicken hand", () => ScoreChickenHand(report));
            Run(report, "score: minimum eight points", () => ScoreRejectsLowHand(report));
            Run(report, "score: flowers are paid but do not help the minimum", () => ScoreFlowers(report));
            Run(report, "score: pure terminal chows is worth 64", () => ScorePureTerminalChows(report));
            Run(report, "hand: seven pairs may use four of a kind as two pairs", () => SevenPairsWithQuad(report));
            Run(report, "score: knitted tiles (lesser and greater)", () => ScoreKnittedTiles(report));
            Run(report, "score: knitted straight hand", () => ScoreKnittedStraight(report));
            Run(report, "settlement: self-draw and discard payments", () => SettlementPayments(report));
            Run(report, "session: sixteen hands, dealer rotation and table points", () => SessionFlow(report));
            Run(report, "bots: heuristic play finishes sessions and wins hands", () => BotSession(report));
            Run(report, "controls: the action bar offers every human decision", () => ActionBarCoverage(report));
            Run(report, "claims: priority of Hu, kong, pung, chow and seat order", () => ClaimPriorityRules(report));
            Run(report, "state: every mechanic is reachable in random play", () => RandomPlayInvariants(report));

            return report;
        }

        private static void Run(SelfTestReport report, string name, Action test)
        {
            try
            {
                test();
                report.Passed.Add(name);
            }
            catch (SelfTestFailure failure)
            {
                report.Failures.Add($"{name}: {failure.Message}");
            }
            catch (Exception exception)
            {
                report.Failures.Add($"{name}: unexpected {exception.GetType().Name}: {exception.Message}");
            }
        }

        // ------------------------------------------------------------- helpers

        private static PlayerHand Hand(string spec)
        {
            var hand = new PlayerHand();
            var id = 0;
            foreach (var token in spec.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                hand.AddConcealed(new Tile(ParseTile(token), id++));
            return hand;
        }

        private static TileType ParseTile(string token)
        {
            if (token.Length == 1)
            {
                return token[0] switch
                {
                    'E' => new TileType(TileSuit.Wind, (int)Wind.East),
                    'S' => new TileType(TileSuit.Wind, (int)Wind.South),
                    'W' => new TileType(TileSuit.Wind, (int)Wind.West),
                    'N' => new TileType(TileSuit.Wind, (int)Wind.North),
                    _ => throw new SelfTestFailure($"Bad tile token: {token}")
                };
            }

            if (char.IsDigit(token[0]))
            {
                var rank = token[0] - '0';
                var suit = token[1] switch
                {
                    'c' => TileSuit.Characters,
                    'd' => TileSuit.Dots,
                    'b' => TileSuit.Bamboo,
                    _ => throw new SelfTestFailure($"Bad suit in {token}")
                };
                return new TileType(suit, rank);
            }

            return token switch
            {
                "Rd" => new TileType(TileSuit.Dragon, (int)Dragon.Red),
                "Gd" => new TileType(TileSuit.Dragon, (int)Dragon.Green),
                "Wd" => new TileType(TileSuit.Dragon, (int)Dragon.White),
                _ => throw new SelfTestFailure($"Bad tile token: {token}")
            };
        }

        private static List<Tile> Tiles(string spec)
        {
            var id = 1000;
            var list = new List<Tile>();
            foreach (var token in spec.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                list.Add(new Tile(ParseTile(token), id++));
            return list;
        }

        private static ScoreResult Score(PlayerHand hand, TileType? winningTile, ClaimSource source,
            Wind seat = Wind.East, bool lastTileOfWall = false, bool lastDiscard = false, bool robbedKong = false)
        {
            return Scoring.Evaluate(new ScoringContext
            {
                Hand = hand,
                WinningTile = winningTile,
                Source = source,
                RoundWind = Wind.East,
                SeatWind = seat,
                LastTileOfWall = lastTileOfWall,
                LastDiscard = lastDiscard,
                RobbedKong = robbedKong
            });
        }

        private static bool Has(ScoreResult result, Fan fan) => result.Fans.Any(f => f.Fan.Id == fan.Id);
        private static string Ids(ScoreResult result) => string.Join(", ", result.Fans.Select(f => f.Fan.Id));

        // ---------------------------------------------------------------- tests

        private static void WallBasics(SelfTestReport report)
        {
            var wall = Wall.CreateShuffled(new Random(7));
            report.Equal(144, wall.TotalTiles, "total tiles");
            report.Equal(144, wall.Remaining, "remaining before the deal");
            wall.Break(17);
            report.Equal(144, wall.Remaining, "remaining after the break");

            var drawn = new HashSet<int>();
            for (var i = 0; i < 16; i++) drawn.Add(wall.DrawLive().InstanceId);
            for (var i = 0; i < 16; i++) drawn.Add(wall.DrawReplacement().InstanceId);
            report.Equal(32, drawn.Count, "live and replacement draws must not overlap");
            report.Equal(112, wall.Remaining, "remaining after 32 draws");
        }

        private static void DealBasics(SelfTestReport report)
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var state = new GameState();
                state.StartHand(seed: seed, dealer: seed % 4);

                report.Equal(14, state.Players[state.Dealer].HandSize, "dealer hand size");
                for (var player = 0; player < 4; player++)
                    if (player != state.Dealer)
                        report.Equal(13, state.Players[player].HandSize, $"player {player} hand size");

                report.Check(state.Discards.Count == 0, "no discards after the deal");
                report.Equal(GamePhase.AwaitingDiscard, state.Phase, "phase after the deal");
                AssertConservation(report, state, $"seed {seed}");
            }
        }

        private static void StandardHand(SelfTestReport report)
        {
            var hand = Hand("1c 2c 3c 4d 5d 6d 7b 7b 7b 8c 8c 8c E E");
            var analysis = HandSolver.Analyse(hand);
            report.Check(analysis.IsComplete, $"expected a complete hand: {analysis.Error}");
            report.Equal(WinningPattern.Standard, analysis.Pattern, "pattern");
            report.Check(analysis.Decompositions.Count > 0, "at least one decomposition");
        }

        private static void SevenPairsHand(SelfTestReport report)
        {
            var hand = Hand("1c 1c 2c 2c 3d 3d 4d 4d 5b 5b 6b 6b 1b 1b");
            var analysis = HandSolver.Analyse(hand);
            report.Check(analysis.IsComplete, $"expected seven pairs: {analysis.Error}");
            report.Equal(WinningPattern.SevenPairs, analysis.Pattern, "pattern");
        }

        private static void ThirteenOrphansHand(SelfTestReport report)
        {
            var hand = Hand("1c 9c 1d 9d 1b 9b E S W N Rd Gd Wd 1c");
            var analysis = HandSolver.Analyse(hand);
            report.Check(analysis.IsComplete, $"expected thirteen orphans: {analysis.Error}");
            report.Equal(WinningPattern.ThirteenOrphans, analysis.Pattern, "pattern");
        }

        private static void WaitDetection(SelfTestReport report)
        {
            var hand = Hand("1c 2c 3c 4d 5d 6d 7b 7b 7b 8c 8c 8c E");
            var waits = HandSolver.WaitingTiles(hand);
            report.Check(waits.Count == 1 && waits[0] == new TileType(TileSuit.Wind, (int)Wind.East),
                $"expected a single East wait, got [{string.Join(",", waits)}]");

            var winningTile = new TileType(TileSuit.Wind, (int)Wind.East);
            var analysis = HandSolver.Analyse(hand, winningTile);
            report.Check(analysis.IsComplete, "waiting hand plus winning tile must be complete");
            report.Equal(WaitType.Single, analysis.Wait, "wait type");
        }

        private static void ScoreSevenPairs(SelfTestReport report)
        {
            var hand = Hand("1c 1c 2c 2c 3d 3d 4d 4d 5b 5b 6b 6b 1b 1b");
            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Check(result.IsValidWin, $"seven pairs must win: {result.Error}");
            report.Check(Has(result, Fans.SevenPairs), "seven pairs fan");
            report.Equal(28, result.HandPoints, "24 seven pairs + 4 fully concealed, self-draw does not stack");
        }

        private static void ScoreThirteenOrphans(SelfTestReport report)
        {
            var hand = Hand("1c 9c 1d 9d 1b 9b E S W N Rd Gd Wd 1c");
            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Check(result.IsValidWin, $"thirteen orphans must win: {result.Error}");
            report.Check(Has(result, Fans.ThirteenOrphans), "thirteen orphans fan");
            report.Equal(92, result.HandPoints, "88 + 4 fully concealed, self-draw does not stack");
        }

        private static void ScoreBigFourWinds(SelfTestReport report)
        {
            var hand = Hand("E E E S S S W W W N N N 1c 1c");
            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Check(result.IsValidWin, $"big four winds must win: {result.Error}");
            report.Check(Has(result, Fans.BigFourWinds), $"expected big four winds, got [{Ids(result)}]");
            report.Check(!Has(result, Fans.AllPungs), "big four winds replaces all pungs");
        }

        private static void ScoreLittleThreeDragons(SelfTestReport report)
        {
            var hand = Hand("Rd Rd Rd Gd Gd Gd Wd Wd 1c 2c 3c 4d 5d 6d");
            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Check(result.IsValidWin, $"little three dragons must win: {result.Error}");
            report.Check(Has(result, Fans.LittleThreeDragons), $"expected little three dragons, got [{Ids(result)}]");
        }

        private static void ScoreNineGates(SelfTestReport report)
        {
            var hand = Hand("1c 1c 1c 2c 3c 4c 5c 5c 6c 7c 8c 9c 9c 9c");
            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Check(result.IsValidWin, $"nine gates must win: {result.Error}");
            report.Check(Has(result, Fans.NineGates), $"expected nine gates, got [{Ids(result)}]");
        }

        private static void ScoreFourKongs(SelfTestReport report)
        {
            var hand = Hand("2c 2c");
            hand.AddMeld(new Meld(MeldKind.ClosedKong, Tiles("1c 1c 1c 1c")));
            hand.AddMeld(new Meld(MeldKind.ClosedKong, Tiles("4d 4d 4d 4d")));
            hand.AddMeld(new Meld(MeldKind.ClosedKong, Tiles("6b 6b 6b 6b")));
            hand.AddMeld(new Meld(MeldKind.ClosedKong, Tiles("8d 8d 8d 8d")));
            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Check(result.IsValidWin, $"four kongs must win: {result.Error}");
            report.Check(Has(result, Fans.FourKongs), $"expected four kongs, got [{Ids(result)}]");
        }

        private static void ScoreFullFlushPungs(SelfTestReport report)
        {
            var hand = Hand("1c 1c 1c 3c 3c 3c 5c 5c 5c 7c 7c 7c 9c 9c");
            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Check(result.IsValidWin, $"full flush must win: {result.Error}");
            report.Check(Has(result, Fans.FullFlush), "full flush fan");
            report.Check(Has(result, Fans.FourConcealedPungs), "four concealed pungs fan");
            report.Check(!Has(result, Fans.AllPungs), "four concealed pungs replaces all pungs");
        }

        private static void ScoreChickenHand(SelfTestReport report)
        {
            var hand = Hand("2b 3b 4b 6b 7b 8b E E");
            hand.AddMeld(new Meld(MeldKind.Chow, Tiles("1c 2c 3c"), 2));
            hand.AddMeld(new Meld(MeldKind.Pung, Tiles("5d 5d 5d"), 1));
            var result = Score(hand, null, ClaimSource.Discard);
            report.Check(Has(result, Fans.ChickenHand), $"expected chicken hand, got [{Ids(result)}]");
        }

        private static void ScoreRejectsLowHand(SelfTestReport report)
        {
            var hand = Hand("1c 2c 3c 4d 5d 6d 2b 3b 4b 6b 7b 8b E E");
            var result = Score(hand, null, ClaimSource.Discard);
            report.Check(!result.IsValidWin || result.HandPoints >= Scoring.MinimumPoints,
                "a winning shape must still reach the eight-point minimum");
        }

        private static void ScoreFlowers(SelfTestReport report)
        {
            var hand = Hand("1c 1c 1c 3c 3c 3c 5c 5c 5c 7c 7c 7c 9c 9c");
            hand.AddFlower(new Tile(new TileType(TileSuit.Flower, 0), 900));
            hand.AddFlower(new Tile(new TileType(TileSuit.Flower, 1), 901));
            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Equal(result.HandPoints + 2, result.TotalPoints, "every flower adds one point");
        }

        private static void ScorePureTerminalChows(SelfTestReport report)
        {
            var hand = Hand("1c 2c 3c 1c 2c 3c 7c 8c 9c 7c 8c 9c 5c 5c");
            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Check(result.IsValidWin, $"pure terminal chows must win: {result.Error}");
            var fan = result.Fans.FirstOrDefault(f => f.Fan.Id == Fans.FourTerminalChows.Id);
            report.Check(fan != null, $"expected pure terminal chows, got [{Ids(result)}]");
            report.Equal(64, fan!.Fan.Points, "the rulebook value of Pure Terminal Chows");
        }

        private static void SevenPairsWithQuad(SelfTestReport report)
        {
            var hand = Hand("1c 1c 1c 1c 2c 2c 3d 3d 4d 4d 5b 5b 6b 6b");
            var analysis = HandSolver.Analyse(hand);
            report.Check(analysis.IsComplete, $"four of a kind counts as two pairs: {analysis.Error}");
            report.Equal(WinningPattern.SevenPairs, analysis.Pattern, "pattern");
        }

        private static void ScoreKnittedTiles(SelfTestReport report)
        {
            // Nine knitted suit tiles plus five single honors.
            var lesser = Hand("1c 4c 7c 2d 5d 8d 3b 6b 9b E S W N Rd");
            var lesserAnalysis = HandSolver.Analyse(lesser);
            report.Check(lesserAnalysis.IsComplete, $"lesser knitted must win: {lesserAnalysis.Error}");
            report.Equal(WinningPattern.KnittedTiles, lesserAnalysis.Pattern, "lesser knitted pattern");

            var lesserScore = Score(lesser, null, ClaimSource.Discard);
            report.Check(Has(lesserScore, Fans.LesserHonorsKnitted), $"expected lesser honors knitted, got [{Ids(lesserScore)}]");
            report.Check(Has(lesserScore, Fans.KnittedStraight), "complete knitted straight stacks with lesser honors");

            // Seven honors plus seven knitted suit tiles.
            var greater = Hand("1c 4c 7c 2d 5d 8d 3b E S W N Rd Gd Wd");
            var greaterAnalysis = HandSolver.Analyse(greater);
            report.Equal(WinningPattern.KnittedTiles, greaterAnalysis.Pattern, "greater knitted pattern");
            var greaterScore = Score(greater, null, ClaimSource.Discard);
            report.Check(Has(greaterScore, Fans.GreaterHonorsKnitted), $"expected greater honors knitted, got [{Ids(greaterScore)}]");
            report.Check(!Has(greaterScore, Fans.LesserHonorsKnitted), "greater replaces lesser");
        }

        private static void ScoreKnittedStraight(SelfTestReport report)
        {
            // Nine knitted tiles plus a pung and a pair: not four sets and a pair.
            var hand = Hand("1c 4c 7c 2d 5d 8d 3b 6b 9b 5d 5d 5d E E");
            var analysis = HandSolver.Analyse(hand);
            report.Check(analysis.IsComplete, $"knitted straight must win: {analysis.Error}");
            report.Equal(WinningPattern.KnittedStraight, analysis.Pattern, "knitted straight pattern");

            var result = Score(hand, null, ClaimSource.SelfDraw);
            report.Check(result.IsValidWin, $"knitted straight must reach the minimum: {result.Error}");
            report.Check(Has(result, Fans.KnittedStraight), $"expected knitted straight, got [{Ids(result)}]");
        }

        private static void SettlementPayments(SelfTestReport report)
        {
            var discardWin = SettlementFromHand("1c 1c 1c 3c 3c 3c 5c 5c 5c 7c 7c 7c 9c 9c", ClaimSource.Discard, 2);
            report.Check(discardWin != null, "the sample hand must be a win");
            report.Equal(3, discardWin!.Payments.Count, "three opponents pay");
            report.Equal(discardWin.HandPoints + Settlements.ExtraPoints * 3, discardWin.TotalPaid,
                "discarder pays hand plus eight, the other two pay eight each");
            report.Equal(discardWin.HandPoints + Settlements.ExtraPoints,
                discardWin.Payments.Single(p => p.FromPlayer == 2).Points, "the discarder pays the hand value");

            var selfDrawn = SettlementFromHand("1c 1c 1c 3c 3c 3c 5c 5c 5c 7c 7c 7c 9c 9c", ClaimSource.SelfDraw, null);
            report.Check(selfDrawn != null, "the sample hand must be a win");
            report.Equal((selfDrawn!.HandPoints + Settlements.ExtraPoints) * 3, selfDrawn.TotalPaid, "every opponent pays in full");
        }

        private static Settlement? SettlementFromHand(string spec, ClaimSource source, int? discarder)
        {
            var hand = Hand(spec);
            var result = Score(hand, null, source);
            if (!result.IsValidWin) return null;
            return Settlements.ForWin(new WinResult(0, source, result, discarder));
        }

        private static void SessionFlow(SelfTestReport report)
        {
            var session = new GameSession();
            for (var hand = 0; hand < GameSession.TotalHands; hand++)
            {
                report.Check(!session.IsFinished, "the session must not end early");
                report.Equal((Wind)(hand / 4), session.RoundWind, $"round wind of hand {hand}");
                report.Equal(hand % 4, session.Dealer, $"dealer of hand {hand}");

                session.StartNextHand(seed: 1000 + hand);
                PlayHandToEnd(session.State!, new Random(hand * 31 + 5));
                session.CompleteHand();
            }

            report.Check(session.IsFinished, "sixteen hands finish the session");
            report.Equal(0, session.Scores.Sum(), "payments are zero-sum");
            var tablePoints = session.TablePoints();
            report.Check(Math.Abs(tablePoints.Sum() - 7) < 1e-9, $"table points must total 7, got {tablePoints.Sum()}");
        }

        private static void PlayHandToEnd(GameState state, Random random)
        {
            var steps = 0;
            while (state.Phase != GamePhase.HandFinished && steps++ < 3000)
            {
                if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                {
                    for (var player = 0; player < 4; player++)
                    {
                        var options = state.GetLegalActions(player);
                        if (options.Count == 0) continue;
                        state.Apply(Choose(options, random, state.Players[player]));
                        if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;
                    }
                    continue;
                }

                var current = state.GetLegalActions(state.CurrentPlayer);
                if (current.Count == 0) break;
                state.Apply(Choose(current, random, state.Players[state.CurrentPlayer]));
            }
        }

        private static void BotSession(SelfTestReport report)
        {
            var wins = 0;
            var draws = 0;
            var sessions = 0;

            for (var seed = 0; seed < 3; seed++)
            {
                var bots = new IMahjongBot[]
                {
                    new HeuristicBot(seed * 10 + 1),
                    new HeuristicBot(seed * 10 + 2),
                    new HeuristicBot(seed * 10 + 3),
                    new HeuristicBot(seed * 10 + 4)
                };
                var session = BotTable.PlaySession(seed, bots);
                sessions++;
                report.Equal(16, session.HandsPlayed, "a bot session plays sixteen hands");
                report.Equal(0, session.Scores.Sum(), "bot payments are zero-sum");
                wins += session.History.Count(s => s.WinnerPlayer >= 0);
                draws += session.History.Count(s => s.WinnerPlayer < 0);
            }

            report.Check(sessions == 3, "three sessions were played");
            report.Check(wins > draws, $"heuristic bots should win more often than they draw (wins {wins}, draws {draws})");
            report.Check(wins > 10, $"bots should complete hands regularly, got {wins} wins");
        }

        private static void AssertConservation(SelfTestReport report, GameState state, string label)
        {
            var ids = new List<int>();
            for (var player = 0; player < 4; player++)
            {
                var hand = state.Players[player];
                ids.AddRange(hand.Concealed.Select(t => t.InstanceId));
                ids.AddRange(hand.Flowers.Select(t => t.InstanceId));
                foreach (var meld in hand.Melds) ids.AddRange(meld.Tiles.Select(t => t.InstanceId));
            }
            ids.AddRange(state.Discards.Select(d => d.Tile.InstanceId));

            report.Equal(144, ids.Count + state.Wall.Remaining, $"{label}: tiles in play plus the wall");
            report.Equal(ids.Count, ids.Distinct().Count(), $"{label}: duplicate tile instances");

            for (var player = 0; player < 4; player++)
                report.Check(state.Players[player].HandSize is 13 or 14,
                    $"{label}: player {player} holds {state.Players[player].HandSize} tiles");
        }

        /// <summary>
        /// Plays hundreds of hands with random legal actions and checks that the whole
        /// state machine stays consistent and that the rare branches really happen.
        /// </summary>
        private static void RandomPlayInvariants(SelfTestReport report)
        {
            var winsFromWall = 0;
            var winsFromDiscard = 0;
            var draws = 0;
            var chows = 0;
            var pungs = 0;
            var kongs = 0;
            var robberies = 0;
            var lastTileWins = 0;

            for (var seed = 0; seed < 300; seed++)
            {
                var state = new GameState();
                var random = new Random(seed * 977 + 13);
                state.StartHand(seed: seed, dealer: seed % 4);
                AssertConservation(report, state, $"seed {seed} after deal");

                var steps = 0;
                while (state.Phase != GamePhase.HandFinished && steps++ < 3000)
                {
                    if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                    {
                        for (var player = 0; player < 4; player++)
                        {
                            var options = state.GetLegalActions(player);
                            if (state.Phase == GamePhase.AwaitingClaims && state.Wall.IsExhausted)
                                report.Check(options.All(o => o.Kind is ActionKind.Win or ActionKind.Pass),
                                    $"seed {seed}: only Hu may be declared after the last wall tile");
                            if (options.Count == 0) continue;
                            state.Apply(Choose(options, random, state.Players[player]));
                            if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;
                        }
                        continue;
                    }

                    var current = state.GetLegalActions(state.CurrentPlayer);
                    report.Check(current.Count > 0, $"seed {seed}: no legal action for the current player in {state.Phase}");
                    state.Apply(Choose(current, random, state.Players[state.CurrentPlayer]));
                }

                report.Check(state.Phase == GamePhase.HandFinished, $"seed {seed}: the hand did not finish");
                AssertConservation(report, state, $"seed {seed} after the hand");

                if (state.Result != null)
                {
                    if (state.Result.Source == ClaimSource.Discard) winsFromDiscard++;
                    else winsFromWall++;
                    if (state.Result.Score.Fans.Any(f => f.Fan.Id == "last-tile-draw" || f.Fan.Id == "last-tile-claim")) lastTileWins++;
                    if (state.Result.Score.Fans.Any(f => f.Fan.Id == "robbing-kong")) robberies++;
                }
                else
                {
                    report.Check(state.EndedInDraw, $"seed {seed}: finished without a result or a draw");
                    draws++;
                }

                foreach (var meld in state.Players.SelectMany(p => p.Melds))
                {
                    switch (meld.Kind)
                    {
                        case MeldKind.Chow: chows++; break;
                        case MeldKind.Pung: pungs++; break;
                        default: kongs++; break;
                    }
                }
            }

            report.Check(winsFromWall > 0, "random play must produce wins from the wall");
            report.Check(draws > 0, "random play must produce drawn hands");
            report.Check(chows > 0, "random play must produce chow claims");
            report.Check(pungs > 0, "random play must produce pung claims");
            report.Check(kongs > 0, "random play must produce kongs");
            report.Check(winsFromDiscard > 0 || lastTileWins > 0, "random play must produce discard wins or last-tile wins");
            _ = robberies;
        }
        /// <summary>
        /// Plays hundreds of hands and checks that the action bar never hides a legal decision:
        /// every choice the engine offers ends up as a numbered button.
        /// </summary>
        private static void ActionBarCoverage(SelfTestReport report)
        {
            var kinds = new Dictionary<ActionKind, int>();
            var prompts = 0;

            for (var seed = 0; seed < 60; seed++)
            {
                var state = new GameState();
                state.StartHand(seed: seed, dealer: seed % 4);

                var steps = 0;
                while (state.Phase != GamePhase.HandFinished && steps++ < 3000)
                {
                    if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                    {
                        for (var player = 0; player < 4; player++)
                        {
                            var options = state.GetLegalActions(player);
                            if (options.Count == 0) continue;

                            Count(Prompt(report, options, ref prompts));
                            state.Apply(BotLikeChoice(options));
                            if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;
                        }
                        continue;
                    }

                    var current = state.GetLegalActions(state.CurrentPlayer);
                    if (current.Count == 0) break;

                    Count(Prompt(report, current, ref prompts));
                    state.Apply(BotLikeChoice(current));
                }
            }

            report.Check(prompts > 100, $"random play should offer many decisions, got {prompts}");
            report.Check(kinds.GetValueOrDefault(ActionKind.Win) > 0, "the bar never offered Hu");
            report.Check(kinds.GetValueOrDefault(ActionKind.Pung) > 0, "the bar never offered a pung");
            report.Check(kinds.GetValueOrDefault(ActionKind.Chow) > 0, "the bar never offered a chow");
            report.Check(kinds.GetValueOrDefault(ActionKind.OpenKong) + kinds.GetValueOrDefault(ActionKind.ClosedKong)
                         + kinds.GetValueOrDefault(ActionKind.AddedKong) > 0, "the bar never offered a kong");
            return;

            void Count(ActionPrompt prompt)
            {
                foreach (var option in prompt.Options)
                    kinds[option.Action.Kind] = kinds.GetValueOrDefault(option.Action.Kind) + 1;
            }
        }

        /// <summary>Checks one bar rebuild against the actions the engine offered.</summary>
        private static ActionPrompt Prompt(SelfTestReport report, IReadOnlyList<LegalAction> options, ref int prompts)
        {
            var offered = options.Where(action => action.Kind is not (ActionKind.Discard or ActionKind.Draw)).ToList();
            var prompt = ActionPrompt.FromLegalActions(options);

            if (offered.All(action => action.Kind == ActionKind.Pass))
            {
                report.Check(!prompt.IsOpen, "a window that only offers a pass must not open the action bar");
                return prompt;
            }

            prompts++;
            report.Equal(offered.Count, prompt.Count, "the action bar offers every legal decision");
            report.Check(prompt.Options.All(option => !string.IsNullOrWhiteSpace(option.Label)),
                "every button has a caption");
            report.Check(prompt.Options.All(option => !string.IsNullOrWhiteSpace(option.Hint)),
                "every button has a hint from LegalAction.Description");

            for (var i = 0; i < prompt.Count; i++)
            {
                var option = prompt.Options[i];
                report.Equal(i + 1, option.Number, "hotkeys are numbered from one");
                report.Check(prompt.TrySelect(option.Number, out var selected), $"hotkey {option.Number} selects an action");
                report.Check(offered.Any(action => action.Kind == selected.Kind && action.Tile?.InstanceId == selected.Tile?.InstanceId),
                    $"the bar invented an action: {selected}");
                if (i == 0) continue;
                report.Check(ActionPrompt.Priority(prompt.Options[i - 1].Action.Kind) <= ActionPrompt.Priority(option.Action.Kind),
                    "buttons follow the claim priority: Hu, kong, pung, chow, pass");
            }

            return prompt;
        }

        /// <summary>A decision for the coverage run: prefer a claim, otherwise discard the last tile.</summary>
        private static LegalAction BotLikeChoice(IReadOnlyList<LegalAction> options)
        {
            return options.FirstOrDefault(action => action.Kind is ActionKind.Win
                       or ActionKind.OpenKong or ActionKind.Pung or ActionKind.Chow
                       or ActionKind.ClosedKong or ActionKind.AddedKong)
                   ?? options.FirstOrDefault(action => action.Kind == ActionKind.Pass)
                   ?? options.FirstOrDefault(action => action.Kind == ActionKind.Draw)
                   ?? options[options.Count - 1];
        }

        /// <summary>
        /// Synthetic claim windows: the winner cannot depend on who answered first, which is what the
        /// engine used to do when two players both claim the same discard.
        /// </summary>
        private static void ClaimPriorityRules(SelfTestReport report)
        {
            var discard = new Tile(new TileType(TileSuit.Characters, 5), 500);
            LegalAction Claim(ActionKind kind, int player) =>
                new(kind, player, discard, Array.Empty<Tile>(), $"{kind} by P{player}");

            report.Equal(null, ClaimPriority.Winner(new[] { Claim(ActionKind.Pass, 1), Claim(ActionKind.Pass, 2) }, 0),
                "all passes leave the window without a claim");

            report.Equal(3, ClaimPriority.Winner(new[] { Claim(ActionKind.Chow, 2), Claim(ActionKind.Pung, 3) }, 0)!.PlayerIndex,
                "a pung beats a chow even from a further seat");

            report.Equal(1, ClaimPriority.Winner(new[] { Claim(ActionKind.Pung, 3), Claim(ActionKind.OpenKong, 1) }, 0)!.PlayerIndex,
                "a kong beats a pung");

            report.Equal(3, ClaimPriority.Winner(new[] { Claim(ActionKind.Win, 3), Claim(ActionKind.OpenKong, 1) }, 0)!.PlayerIndex,
                "Hu beats every meld");

            report.Equal(1, ClaimPriority.Winner(new[] { Claim(ActionKind.Pung, 3), Claim(ActionKind.Pung, 1) }, 0)!.PlayerIndex,
                "equal claims go to the nearest seat after the discarder");

            report.Equal(2, ClaimPriority.Winner(new[] { Claim(ActionKind.Chow, 3), Claim(ActionKind.Chow, 2) }, 1)!.PlayerIndex,
                "seat order is counted from the discarder, not from seat zero");

            report.Equal(1, ClaimPriority.Winner(new[] { Claim(ActionKind.Win, 3), Claim(ActionKind.Win, 1) }, 0)!.PlayerIndex,
                "double Hu goes to the nearest seat (rule 3.6.7.2)");

            report.Equal(1, ClaimPriority.Distance(1, 0), "the next seat is one step away");
            report.Equal(1, ClaimPriority.Distance(0, 3), "the round wraps from North to East");
        }

        private static LegalAction Choose(IReadOnlyList<LegalAction> options, Random random, PlayerHand hand)
        {
            var win = options.FirstOrDefault(o => o.Kind == ActionKind.Win);
            if (win != null && random.Next(2) == 0) return win;

            var kong = options.FirstOrDefault(o => o.Kind is ActionKind.AddedKong or ActionKind.ClosedKong);
            if (kong != null && random.Next(4) == 0) return kong;

            var claim = options.Where(o => o.Kind is ActionKind.Chow or ActionKind.Pung or ActionKind.OpenKong).ToList();
            if (claim.Count > 0 && random.Next(3) == 0) return claim[random.Next(claim.Count)];

            var discards = options.Where(o => o.Kind == ActionKind.Discard).ToList();
            if (discards.Count > 0) return LeastUseful(discards, hand, random);

            if (options.Any(o => o.Kind == ActionKind.Pass) && random.Next(2) == 0)
                return options.First(o => o.Kind == ActionKind.Pass);

            return options[random.Next(options.Count)];
        }

        /// <summary>A crude but win-producing policy: throw away the least connected tile.</summary>
        private static LegalAction LeastUseful(List<LegalAction> discards, PlayerHand hand, Random random)
        {
            var types = hand.Concealed.Select(t => t.Type).ToList();
            var best = discards[0];
            var bestScore = int.MaxValue;
            foreach (var option in discards)
            {
                var type = option.Tile!.Value.Type;
                var score = Usefulness(type, types) * 10 + random.Next(10);
                if (score < bestScore) { bestScore = score; best = option; }
            }
            return best;
        }

        private static int Usefulness(TileType type, List<TileType> hand)
        {
            var score = hand.Count(t => t == type) * 3;
            if (!type.IsNumbered) score += hand.Count(t => t.IsHonor) > 2 ? 1 : 0;
            if (!type.IsNumbered) return score;
            for (var delta = -2; delta <= 2; delta++)
            {
                if (delta == 0) continue;
                var rank = type.Rank + delta;
                if (rank < 1 || rank > 9) continue;
                score += hand.Count(t => t.Suit == type.Suit && t.Rank == rank) * (Math.Abs(delta) == 1 ? 2 : 1);
            }
            return score;
        }
    }
}
