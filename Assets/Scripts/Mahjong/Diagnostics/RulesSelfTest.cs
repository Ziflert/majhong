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
    public static partial class RulesSelfTest
    {
        public static SelfTestReport RunAll()
        {
            var report = new SelfTestReport();

            Run(report, "wall: 144 tiles and a break point", () => WallBasics(report));
            Run(report, "wall: four walls of eighteen stacks, dealt as one ring", () => WallShape(report));
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
            Run(report, "score: golden interpretation boundaries", () => ScoreGoldenInterpretationBoundaries(report));
            Run(report, "settlement: self-draw and discard payments", () => SettlementPayments(report));
            Run(report, "session: sixteen hands, dealer rotation and table points", () => SessionFlow(report));
            Run(report, "penalties: catalogue, escalation and doubling", () => PenaltyEscalation(report));
            Run(report, "penalties: a forbidden player cannot declare Hu", () => ForbiddenWin(report));
            Run(report, "penalties: points are charged when the hand ends", () => PenaltySettlement(report));
            Run(report, "session: the 150-minute clock stops an unfinished hand", () => SessionClock(report));
            Run(report, "session: standings rank by game points and split ties", () => SessionStandings(report));
            Run(report, "bots: heuristic play finishes sessions and wins hands", () => BotSession(report));
            Run(report, "controls: the action bar offers every human decision", () => ActionBarCoverage(report));
            Run(report, "clock: three, ten and twenty-second windows", () => HandClockRules(report));
            Run(report, "clock: silence becomes a pass or a discard, never a Hu", () => ClockExpiryRules(report));
            Run(report, "claims: priority of Hu, kong, pung, chow and seat order", () => ClaimPriorityRules(report));
            Run(report, "claims: every offered chow can be played as offered", () => ChowIdentity(report));
            Run(report, "bots: the danger read follows the tiles on the table", () => DangerReads(report));
            Run(report, "bots: the cautious level throws safer tiles without wrecking shape", () => BotDefense(report));
            Run(report, "bots: the strategic level values the hand and the match", () => BotStrategy(report));
            Run(report, "state: every mechanic is reachable in random play", () => RandomPlayInvariants(report));
            Run(report, "state: a kong on the last wall tile keeps the turn alive", () => KongAtWallEnd(report));
            Run(report, "score: a self-draw keeps the wait fans", () => SelfDrawWaits(report));
            Run(report, "score: last tile fans need the real last tile", () => LastTileBoundaries(report));
            Run(report, "hand: waiting tiles cover the special patterns", () => SpecialPatternWaits(report));
            Run(report, "session: a forfeited player takes no table points", () => ForfeitedRanking(report));
            NetAndUiGroups(report);
            ReplayGroups(report);
            MenuGroups(report);
            OpeningGroups(report);

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

        /// <summary>
        /// Scores a hand whose winning tile is already one of its tiles, the way a self-drawn win reaches
        /// the scorer. Kept here so the tests do not have to remember the flag themselves.
        /// </summary>
        private static ScoreResult ScoreSelfDrawn(PlayerHand hand, TileType winningTile, bool lastTileOfWall = false) =>
            Scoring.Evaluate(new ScoringContext
            {
                Hand = hand,
                WinningTile = winningTile,
                WinningTileInHand = true,
                Source = ClaimSource.SelfDraw,
                RoundWind = Wind.East,
                SeatWind = Wind.East,
                LastTileOfWall = lastTileOfWall
            });

        // ---------------------------------------------------------------- tests

        private static void WallBasics(SelfTestReport report)
        {
            var wall = Wall.CreateShuffled(new Random(7));
            report.Equal(144, wall.TotalTiles, "total tiles");
            report.Equal(144, wall.Remaining, "remaining before the deal");
            wall.Break(0, 17);
            report.Equal(144, wall.Remaining, "remaining after the break");

            var drawn = new HashSet<int>();
            for (var i = 0; i < 16; i++) drawn.Add(wall.DrawLive().InstanceId);
            for (var i = 0; i < 16; i++) drawn.Add(wall.DrawReplacement().InstanceId);
            report.Equal(32, drawn.Count, "live and replacement draws must not overlap");
            report.Equal(112, wall.Remaining, "remaining after 32 draws");
        }

        /// <summary>
        /// The wall as the rules build it: four walls of eighteen two-tile stacks in a square, dealt
        /// as one closed ring that is walked clockwise, so a player's own wall is taken from his right
        /// end to his left end. The break is counted in stacks from the second thrower's right end.
        /// </summary>
        private static void WallShape(SelfTestReport report)
        {
            report.Equal(144, WallGeometry.TileCount, "the wall holds the whole set");
            report.Equal(4, WallGeometry.Sides, "four walls");
            report.Equal(18, WallGeometry.StacksPerSide, "eighteen stacks in a wall");
            report.Equal(36, WallGeometry.TilesPerSide, "thirty-six tiles per player (3.5.7.4)");

            // Every ring position names one tile and every tile names its ring position back.
            var seen = new HashSet<WallSlot>();
            for (var ring = 0; ring < WallGeometry.TileCount; ring++)
            {
                var slot = WallGeometry.SlotAt(ring);
                report.Check(slot.Side is >= 0 and < 4, $"ring {ring}: side inside the square");
                report.Check(slot.Stack is >= 0 and < 18, $"ring {ring}: stack inside the wall");
                report.Check(slot.Level is 0 or 1, $"ring {ring}: the stack is two tiles high");
                report.Check(seen.Add(slot), $"ring {ring}: {slot} was handed out twice");
                report.Equal(ring, WallGeometry.RingIndex(slot.Side, slot.Stack, slot.Level),
                    $"ring {ring}: {slot} leads back to the same place");
            }

            // Each side owns 36 consecutive positions and the stack is taken top first.
            for (var side = 0; side < 4; side++)
            {
                var start = WallGeometry.RingStart(side);
                report.Equal(side, WallGeometry.SlotAt(start).Side, $"side {side}: its wall starts on the right end");
                report.Equal(side, WallGeometry.SlotAt(start + 35).Side, $"side {side}: its wall ends 36 tiles later");
                report.Equal((side + 3) % 4, WallGeometry.SlotAt(start + 36).Side,
                    $"side {side}: the deal walks to the wall on the player's left");
                report.Check(WallGeometry.SlotAt(start).IsTop, $"side {side}: the top tile of a stack comes first");
                report.Equal(0, WallGeometry.SlotAt(start + 1).Stack, $"side {side}: second tile is the same stack");
                report.Equal(1, WallGeometry.SlotAt(start + 2).Stack, $"side {side}: the next stack follows");
            }

            // Ring order walks the square clockwise: near wall right to left, then the left wall.
            report.Equal(new WallSlot(0, 0, 1), WallGeometry.SlotAt(0), "the ring starts at the near wall's right top");
            report.Equal(new WallSlot(0, 17, 0), WallGeometry.SlotAt(35), "the near wall ends on its left bottom");
            report.Equal(new WallSlot(3, 0, 1), WallGeometry.SlotAt(36), "then the left player's wall begins");
            report.Equal(new WallSlot(1, 17, 0), WallGeometry.SlotAt(143), "and the right player's wall closes the ring");

            // The break is counted in stacks from the thrower's right end and lands in his own wall
            // for every sum a pair of dice can give, carrying into the next wall only past its end.
            for (var thrower = 0; thrower < 4; thrower++)
            {
                var start = WallGeometry.RingStart(thrower);
                for (var stacks = 4; stacks <= 17; stacks++)
                {
                    var index = WallGeometry.BreakIndex(thrower, stacks);
                    report.Equal(start + stacks * 2, index, $"thrower {thrower}, {stacks} stacks: break after that stack");
                    report.Equal(thrower, WallGeometry.SlotAt(index).Side, $"thrower {thrower}, {stacks}: break inside his wall");
                }

                // A count that reaches or passes the end of his own wall carries on into the next one.
                foreach (var stacks in new[] { 18, 19, 20, 21, 22, 23, 24 })
                    report.Equal((thrower + 3) % 4, WallGeometry.SlotAt(WallGeometry.BreakIndex(thrower, stacks)).Side,
                        $"thrower {thrower}, {stacks} stacks: a long count runs into the next wall");
            }

            // A real wall: the dealt tiles leave in ring order from the break and nobody takes the
            // rest, so the leftovers are still standing when the deal is done.
            var wall = Wall.CreateShuffled(new Random(31));
            wall.Break(2, 11);
            var breakSide = WallGeometry.SlotAt(wall.BreakIndex).Side;
            report.Equal(2, breakSide, "the break stands in the second thrower's wall");
            report.Equal(144, wall.TilesInRingOrder.Count, "every tile of the set has a place in the wall");
            report.Equal(144, wall.Remaining, "a broken wall is not a smaller wall");

            var takenSlots = new List<WallSlot>();
            for (var i = 0; i < 53; i++)
            {
                var tile = wall.DrawLive();
                report.Check(wall.SlotOf(tile).HasValue, $"deal {i}: the drawn tile knows its place in the wall");
                report.Check(!wall.IsStanding(tile), $"deal {i}: a drawn tile has left the wall");
                takenSlots.Add(wall.SlotOf(tile)!.Value);
            }

            report.Equal(91, wall.Remaining, "the deal leaves ninety-one tiles standing");
            for (var i = 1; i < takenSlots.Count; i++)
            {
                var sameStack = takenSlots[i].Side == takenSlots[i - 1].Side
                                && takenSlots[i].Stack == takenSlots[i - 1].Stack;
                report.Check(!sameStack || takenSlots[i - 1].IsTop,
                    $"deal {i}: the bottom of a stack is not taken before its top");
            }

            var standing = wall.TilesInRingOrder.Count(tile => wall.IsStanding(tile));
            report.Equal(91, standing, "the standing tiles are exactly what nobody took");
            report.Equal(53, wall.TilesInRingOrder.Count(tile => wall.SlotOf(tile).HasValue) - standing,
                "the tiles that left the wall are the dealt ones");

            var replacement = wall.DrawReplacement();
            var replacementSlot = wall.SlotOf(replacement)!.Value;
            var liveSlot = WallGeometry.SlotAt(wall.LiveIndex);
            report.Check(replacementSlot.Side != liveSlot.Side || replacementSlot.Stack > liveSlot.Stack,
                "a replacement comes from the other end of the wall, not from the live wall");
            var takenReplacement = WallGeometry.SlotAt(wall.TailIndex + 1);
            report.Equal(0, takenReplacement.Level,
                "the replacement end starts with the bottom of the last stack before the break");
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
            report.Equal(29, result.HandPoints, "24 seven pairs + 4 fully concealed + 1 no honors (rules 19, 76)");
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

            // Rule 3.8.1 for both knitted hands: "may be combined with Fully Concealed on a self-draw".
            var lesserSelfDraw = Score(lesser, null, ClaimSource.SelfDraw);
            report.Check(Has(lesserSelfDraw, Fans.FullyConcealed),
                $"a self-drawn knitted hand also earns fully concealed, got [{Ids(lesserSelfDraw)}]");
            var greaterSelfDraw = Score(greater, null, ClaimSource.SelfDraw);
            report.Check(Has(greaterSelfDraw, Fans.FullyConcealed),
                $"the same for the greater hand, got [{Ids(greaterSelfDraw)}]");
            report.Check(!Has(greaterScore, Fans.FullyConcealed), "a claimed knitted hand is not fully concealed");
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

        private static void ScoreGoldenInterpretationBoundaries(SelfTestReport report)
        {
            // Rule 3.8.1 explicitly excludes Concealed Hand and All Pungs, but does not
            // exclude Single Wait. Keep Single Wait when the winning decomposition has it.
            var fourPungs = Score(Hand("1c 1c 1c 3c 3c 3c 5d 5d 5d 7b 7b 7b 9d"), ParseTile("9d"), ClaimSource.Discard);
            report.Check(Has(fourPungs, Fans.FourConcealedPungs), "golden: four concealed pungs");
            report.Check(Has(fourPungs, Fans.SingleWait), "golden: four concealed pungs may combine with single wait");

            // The fourth visible copy includes the winning discard, which earns Last Tile.
            var last = Hand("1c 2c 3c 4c 5c 6c 7c 8c 9c 1d 2d 5d 5d");
            var winning = ParseTile("3d");
            var discardPile = new[] { ParseTile("3d"), ParseTile("3d"), ParseTile("3d") };
            var lastResult = Scoring.Evaluate(new ScoringContext
            {
                Hand = last,
                WinningTile = winning,
                Source = ClaimSource.Discard,
                DiscardPile = discardPile
            });
            report.Check(Has(lastResult, Fans.LastTile), "golden: winning tile completes the fourth visible copy");
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

        private static void PenaltyEscalation(SelfTestReport report)
        {
            var expected = new[] { 0, 5, 10, 20, 40, 80, 80 };
            for (var offence = 1; offence <= expected.Length; offence++)
                report.Equal(expected[offence - 1], Penalties.EscalatedPoints(offence),
                    $"penalty for offence {offence} (warning, five, ten, twenty, then doubling)");

            report.Equal(12, Penalties.Catalogue.Count, "the catalogue covers every violation of 3.11.6 and the lateness fine");
            report.Equal(Penalties.Catalogue.Count,
                Penalties.Catalogue.Select(rule => rule.Kind).Distinct().Count(), "no violation is listed twice");
            report.Check(Penalties.Catalogue.All(rule => !string.IsNullOrWhiteSpace(rule.Name)),
                "every penalty has a name");
            report.Check(Penalties.Catalogue.All(rule => !string.IsNullOrWhiteSpace(rule.Reference)),
                "every penalty points at a rule number");

            var ledger = new PenaltyLedger();
            var first = ledger.Register(1, ViolationKind.EmptyClaim);
            report.Check(first.WarningOnly, "the first empty requirement is only a warning");
            report.Equal(0, first.TotalLoss, "a warning costs nothing");
            report.Equal(5, ledger.Register(1, ViolationKind.EmptyClaim).TotalLoss, "the second empty requirement costs five points");
            report.Equal(10, ledger.Register(1, ViolationKind.EmptyClaim).TotalLoss, "the third costs ten points");
            var fourth = ledger.Register(1, ViolationKind.EmptyClaim);
            report.Equal(20, fourth.TotalLoss, "the fourth costs twenty points");
            report.Equal(20, fourth.PointsLostFromTable, "nobody receives the fine (3.11.2)");
            report.Check(ledger.Register(2, ViolationKind.EmptyClaim).WarningOnly, "counters are per player");
            report.Equal(4, ledger.Occurrences(1, ViolationKind.EmptyClaim), "four offences are counted for P1");

            ledger.StartRound(0);
            ledger.Register(0, ViolationKind.LatePungClaim);
            report.Equal(5, ledger.Register(0, ViolationKind.LatePungClaim).TotalLoss,
                "the second late pung of the round costs five points");
            ledger.StartRound(1);
            report.Check(ledger.Register(0, ViolationKind.LatePungClaim).WarningOnly,
                "the late pung counter restarts in every round (3.11.6.5)");

            var falseHu = ledger.Register(3, ViolationKind.FalseHuClaim);
            report.Equal(20, falseHu.PointsPerRecipient, "a wrong Hu claim costs twenty points to every opponent");
            report.Equal(3, falseHu.Recipients.Count, "all three opponents are paid");
            report.Equal(60, falseHu.TotalLoss, "the wrong Hu claim costs sixty points in total");
            report.Check(falseHu.LosesRightToWin, "a false Hu takes the right to win the current hand");

            var touched = ledger.Register(2, ViolationKind.TouchedWallTile);
            report.Check(touched.WarningOnly && !touched.LosesRightToWin, "an unseen wall tile only earns a warning");
            var seen = ledger.Register(2, ViolationKind.TouchedWallTile, tileSeen: true);
            report.Equal(5, seen.TotalLoss, "a second offence costs five points");
            report.Check(seen.LosesRightToWin, "a seen wall tile also costs the right to declare Hu");

            var mistaken = ledger.Register(0, ViolationKind.MistakenMeld);
            report.Equal(0, mistaken.TotalLoss, "a mistaken meld costs no points");
            report.Check(mistaken.LosesRightToWin, "a mistaken meld costs the right to win the hand");

            var knocked = ledger.Register(1, ViolationKind.KnockedOverTile, judgePoints: 25, victim: 2);
            report.Equal(25, knocked.TotalLoss, "the referee sets the fine for knocking a tile over");
            report.Equal(2, knocked.Recipients.Single(), "only the player whose tile was knocked over is paid");
            report.Check(ledger.Register(0, ViolationKind.Deception).WarningOnly,
                "a referee may answer cheating with a warning alone");

            var rejected = false;
            try { ledger.Register(0, ViolationKind.Deception, judgePoints: 61); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            report.Check(rejected, "a referee fine above sixty points is rejected");

            report.Equal(0, Penalties.LateArrivalPoints(TimeSpan.Zero), "arriving on time is free");
            report.Equal(10, Penalties.LateArrivalPoints(TimeSpan.FromMinutes(7)), "up to ten minutes late costs ten points");
            report.Equal(20, Penalties.LateArrivalPoints(TimeSpan.FromMinutes(13)), "up to fifteen minutes late costs twenty points");
            report.Check(!Penalties.MissesSession(TimeSpan.FromMinutes(15)), "fifteen minutes late still plays the session");
            report.Check(Penalties.MissesSession(TimeSpan.FromMinutes(16)), "past fifteen minutes the session is missed");
        }

        /// <summary>
        /// Rule 3.11.6: a player who lost the right to win keeps drawing and discarding,
        /// but the engine never offers Hu again in that hand.
        /// </summary>
        private static void ForbiddenWin(SelfTestReport report)
        {
            for (var seed = 0; seed < 3; seed++)
            {
                var state = new GameState();
                var random = new Random(seed * 17 + 3);
                state.StartHand(seed % 4, 7000 + seed);
                for (var player = 0; player < 4; player++) state.ForbidWin(player, "self-test");

                var offeredWin = false;
                var steps = 0;
                while (state.Phase != GamePhase.HandFinished && steps++ < 3000)
                {
                    if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                    {
                        for (var player = 0; player < 4; player++)
                        {
                            var options = state.GetLegalActions(player);
                            if (options.Count == 0) continue;
                            offeredWin |= options.Any(option => option.Kind == ActionKind.Win);
                            state.Apply(Choose(options, random, state.Players[player]));
                            if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;
                        }
                        continue;
                    }

                    var current = state.GetLegalActions(state.CurrentPlayer);
                    if (current.Count == 0) break;
                    offeredWin |= current.Any(action => action.Kind == ActionKind.Win);
                    state.Apply(Choose(current, random, state.Players[state.CurrentPlayer]));
                }

                report.Check(!offeredWin, $"seed {seed}: a forbidden player is never offered Hu");
                report.Check(state.Phase == GamePhase.HandFinished, $"seed {seed}: the hand still finishes");
                report.Check(state.EndedInDraw, $"seed {seed}: with nobody allowed to win the hand is a draw");
                report.Check(state.IsWinForbidden(0) && state.WinForbiddenReason(0) != null,
                    $"seed {seed}: the ban and its reason are kept");

                state.StartHand(0, 7000 + seed);
                report.Check(!state.IsWinForbidden(0), "a new hand clears the ban");
            }
        }

        private static void PenaltySettlement(SelfTestReport report)
        {
            var session = new GameSession();
            session.StartNextHand(seed: 99);
            session.ReportViolation(2, ViolationKind.EmptyClaim);
            session.ReportViolation(2, ViolationKind.EmptyClaim);
            var falseHu = session.ReportViolation(3, ViolationKind.FalseHuClaim);

            report.Check(falseHu.LosesRightToWin, "the false Hu claim takes the right to win");
            report.Check(session.State!.IsWinForbidden(3), "the engine forbids Hu for that player");
            report.Equal(3, session.PendingPenalties.Count, "penalties wait for the end of the hand (3.9.1.2)");
            report.Equal(0, session.Scores.Sum(), "nothing is charged while the hand runs");

            PlayHandToEnd(session.State!, new Random(3));
            var before = session.Scores.Sum();
            session.CompleteHand();

            report.Equal(0, session.PendingPenalties.Count, "every penalty is paid once");
            report.Equal(60, session.AppliedPenalties.Where(penalty => penalty.Kind == ViolationKind.FalseHuClaim)
                .Sum(penalty => penalty.TotalLoss), "the wrong Hu claim costs sixty points");
            report.Equal(before - 5, session.Scores.Sum(),
                "a fine leaves the table while a transfer stays inside it (3.11.2)");
        }

        private static void SessionClock(SelfTestReport report)
        {
            var session = new GameSession();
            report.Equal(TimeSpan.FromMinutes(150), session.TimeLimit, "a session lasts 150 minutes (3.5.6)");
            report.Equal(TimeSpan.FromMinutes(15), GameSession.TimeWarning, "the referee warns a quarter of an hour ahead");

            session.StartNextHand(seed: 1);
            PlayHandToEnd(session.State!, new Random(7));
            session.CompleteHand(TimeSpan.FromMinutes(100));
            report.Equal(100.0, session.Elapsed.TotalMinutes, "the hand time goes into the clock");
            report.Check(!session.IsFinalQuarterHour && !session.IsTimeUp, "a hundred minutes leave time to play");

            session.StartNextHand(seed: 2);
            PlayHandToEnd(session.State!, new Random(8));
            session.CompleteHand(TimeSpan.FromMinutes(40));
            report.Equal(140.0, session.Elapsed.TotalMinutes, "the clock adds up");
            report.Check(session.IsFinalQuarterHour, "the last quarter of an hour is announced");
            report.Check(!session.IsTimeUp, "the clock has not run out yet");

            var handsBefore = session.HandsPlayed;
            var pointsBefore = session.Scores.Sum();
            session.StartNextHand(seed: 3);
            var running = session.State!;
            session.AbandonRunningHand(TimeSpan.FromMinutes(10));

            report.Check(running.Phase != GamePhase.HandFinished, "the hand really was still running");
            report.Check(session.IsTimeUp, "ten more minutes use up the session");
            report.Equal(handsBefore, session.HandsPlayed, "an unfinished hand is not counted as played");
            report.Equal(pointsBefore, session.Scores.Sum(), "an unfinished hand scores nothing (3.5.6)");
            report.Equal(1, session.AbandonedHands.Count, "the abandoned hand is recorded");
            report.Check(session.IsFinished && session.EndedByTimeLimit, "the session ends on the clock");

            var started = false;
            try { session.StartNextHand(seed: 4); }
            catch (InvalidOperationException) { started = true; }
            report.Check(started, "no hand may start after the clock ran out");
        }

        private static void SessionStandings(SelfTestReport report)
        {
            var session = new GameSession();
            session.Apply(new Settlement(0, 0, false,
                new[] { new Payment(1, 0, 30), new Payment(2, 3, 40), new Payment(3, 2, 30) }, 0, "self-test"));

            report.Equal(30, session.Scores[0], "P0 leads");
            report.Equal(-30, session.Scores[1], "P1 is last");
            report.Equal(-10, session.Scores[2], "P2 holds minus ten");
            report.Equal(10, session.Scores[3], "P3 holds ten");

            var standings = session.Standings();
            report.Equal(4, standings.Count, "every player gets a row");
            report.Equal(1, standings[0].Place, "first place");
            report.Equal(0, standings[0].Player, "the leader is P0 (3.9.2)");
            report.Equal(4.0, standings[0].TablePoints, "first place is worth four table points");
            report.Equal(2, standings[1].Place, "second place");
            report.Equal(3, standings[1].Player, "P3 takes second place");
            report.Equal(2.0, standings[1].TablePoints, "second place is worth two table points");
            report.Equal(3, standings[2].Place, "third place");
            report.Equal(2, standings[2].Player, "P2 takes third place");
            report.Equal(1.0, standings[2].TablePoints, "third place is worth one table point");
            report.Equal(4, standings[3].Place, "last place");
            report.Equal(1, standings[3].Player, "P1 takes the last place");
            report.Equal(0.0, standings[3].TablePoints, "last place is worth nothing");
            report.Check(session.PlaceOf(0) == 1 && session.PlaceOf(1) == 4, "PlaceOf answers for both ends of the table");
            report.Check(Math.Abs(session.TablePoints().Sum() - 7) < 1e-9, "table points total seven");

            var tied = new GameSession();
            tied.Apply(new Settlement(0, 0, false,
                new[] { new Payment(1, 0, 30), new Payment(2, 0, 30), new Payment(3, 0, 30) }, 0, "self-test"));
            var tiedStandings = tied.Standings();
            report.Equal(1, tiedStandings[0].Place, "the leader keeps first place alone");
            report.Check(tiedStandings.Skip(1).All(row => row.Place == 2 && row.Tied),
                "three tied players share second place");
            report.Check(tiedStandings.Skip(1).All(row => Math.Abs(row.TablePoints - 1.0) < 1e-9),
                "places two to four are split three ways");
            report.Check(Math.Abs(tied.TablePoints().Sum() - 7) < 1e-9, "a split still totals seven table points");

            var late = new GameSession();
            report.Equal(0, late.ReportLateArrival(2, TimeSpan.FromMinutes(18)),
                "a player eighteen minutes late scores nothing (3.11.2)");
            report.Check(late.IsForfeited(2), "that player misses the session");
            report.Check(late.Standings().Single(row => row.Player == 2).Forfeited, "the table marks the forfeit");
            report.Equal(10, late.ReportLateArrival(1, TimeSpan.FromMinutes(8)), "eight minutes late costs ten points");
            report.Equal(-10, late.Scores[1], "the lateness fine leaves the table");
            report.Check(late.Scores.Sum() == -10, "no opponent receives a lateness fine");
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
                    CheckTurnInvariants(report, state, seed);
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
        /// Rules 3.5.7.6, 3.6.4 and 3.6.7: twenty seconds to arrange the tiles, ten to discard and three
        /// to claim. Checked on the clock itself, with no waiting: the view feeds it frame deltas.
        /// </summary>
        private static void HandClockRules(SelfTestReport report)
        {
            report.Equal(TimeSpan.FromSeconds(3), PlayClock.ClaimLimit, "a claim has three seconds (3.6.7)");
            report.Equal(TimeSpan.FromSeconds(10), PlayClock.DiscardLimit, "a discard has ten seconds (3.6.4)");
            report.Equal(TimeSpan.FromSeconds(20), PlayClock.ArrangeLimit, "arranging the tiles has twenty seconds (3.5.7.6)");
            report.Equal(PlayClock.ClaimLimit, PlayClock.LimitOf(ClockWindow.Claim), "a claim window carries its own limit");
            report.Equal(PlayClock.DiscardLimit, PlayClock.LimitOf(ClockWindow.Discard), "a discard window carries its own limit");
            report.Equal(PlayClock.ArrangeLimit, PlayClock.LimitOf(ClockWindow.Arrange), "an arrange window carries its own limit");
            report.Equal(TimeSpan.Zero, PlayClock.LimitOf(ClockWindow.Idle), "an idle window has no limit");

            var clock = new PlayClock();
            report.Check(!clock.IsRunning && !clock.IsExpired, "a fresh clock is idle and not expired");
            report.Equal(0, clock.SecondsLeft, "an idle clock shows no seconds");
            report.Equal(0d, clock.Fraction, "an idle clock has no progress to show");

            clock.Restart(ClockWindow.Claim);
            report.Equal(ClockWindow.Claim, clock.Window, "the claim window is open");
            report.Equal(TimeSpan.FromSeconds(3), clock.Limit, "the claim window takes the limit of 3.6.7");
            report.Equal(3, clock.SecondsLeft, "three seconds are left at the start");

            report.Check(!clock.Tick(TimeSpan.FromMilliseconds(2500)), "two and a half seconds do not expire a claim");
            report.Equal(1, clock.SecondsLeft, "half a second left is shown as one second");
            report.Check(Math.Abs(clock.Fraction - 2.5 / 3.0) < 1e-9, "the used fraction of the window is two thirds");
            report.Check(clock.Tick(TimeSpan.FromMilliseconds(500)), "the claim expires exactly at three seconds");
            report.Check(clock.IsExpired, "an expired window stays expired");
            report.Equal(TimeSpan.Zero, clock.Remaining, "nothing is left of an expired window");
            report.Equal(0, clock.SecondsLeft, "an expired window shows no seconds");
            report.Equal(1d, clock.Fraction, "an expired window is fully used");
            report.Check(!clock.Tick(TimeSpan.FromSeconds(5)), "expiry is reported once, not on every following tick");

            clock.Restart(ClockWindow.Discard);
            report.Check(!clock.IsExpired, "restarting clears the expiry");
            report.Equal(10, clock.SecondsLeft, "a discard has ten seconds");
            report.Check(!clock.Tick(TimeSpan.FromSeconds(9.5)), "nine and a half seconds do not expire a discard");
            report.Check(clock.Tick(TimeSpan.FromSeconds(0.5)), "ten seconds end the discard window");

            clock.Restart(ClockWindow.Discard, TimeSpan.FromSeconds(1));
            report.Equal(1, clock.SecondsLeft, "a window can be shortened for demos and tests");
            report.Check(!clock.Tick(TimeSpan.Zero), "a zero tick does nothing");
            report.Check(!clock.Tick(TimeSpan.FromSeconds(-4)), "a negative tick is ignored");
            report.Equal(1, clock.SecondsLeft, "the clock never runs backwards");

            clock.Restart(ClockWindow.Arrange);
            report.Equal(20, clock.SecondsLeft, "arranging the hand gets twenty seconds");
            clock.Tick(TimeSpan.FromSeconds(5));
            clock.Restart(ClockWindow.Claim);
            report.Equal(3, clock.SecondsLeft, "a new window does not inherit the time of the old one");

            clock.Stop();
            report.Check(!clock.IsRunning && !clock.IsExpired, "a stopped clock is neither running nor expired");
            report.Equal(TimeSpan.Zero, clock.Limit, "a stopped clock carries no limit");
            report.Equal(0, clock.SecondsLeft, "a stopped clock shows no seconds");
            report.Check(!clock.Tick(TimeSpan.FromSeconds(1)), "ticking a stopped clock does nothing");

            var idleThrew = false;
            try { clock.Restart(ClockWindow.Idle); }
            catch (ArgumentOutOfRangeException) { idleThrew = true; }
            report.Check(idleThrew, "an idle window cannot be restarted with a limit");
        }

        /// <summary>
        /// What an expired window does to the table: a claim that nobody spoke is a pass, a turn that
        /// ran out is played for the player, and the clock never claims Hu for a silent player.
        /// </summary>
        private static void ClockExpiryRules(SelfTestReport report)
        {
            var bot = new HeuristicBot(17);
            var random = new Random(4242);
            var claimWindows = 0;
            var forcedDiscards = 0;
            var passOnlyWindows = 0;

            for (var handIndex = 0; handIndex < 30; handIndex++)
            {
                var state = new GameState();
                state.StartHand(dealer: handIndex % 4, seed: random.Next());

                var guard = 0;
                while (state.Phase != GamePhase.HandFinished && guard++ < 3000)
                {
                    if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                    {
                        claimWindows++;
                        var answered = false;
                        for (var player = 0; player < 4; player++)
                        {
                            if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;

                            var options = state.GetLegalActions(player);
                            if (options.Count == 0) continue;

                            var forced = ClockExpiry.Choose(state, player, options, bot);
                            report.Equal(ActionKind.Pass, forced?.Kind ?? ActionKind.Draw,
                                "silence in a claim window is a pass, whatever the player could have claimed");
                            passOnlyWindows += options.Count == 1 ? 1 : 0;

                            if (!answered && random.Next(4) == 0) { state.Apply(forced!); answered = true; }
                        }

                        // everybody stays silent: the window has to close by itself
                        for (var player = 0; player < 4; player++)
                        {
                            if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;
                            var pass = state.GetLegalActions(player).FirstOrDefault(option => option.Kind == ActionKind.Pass);
                            if (pass == null) continue;
                            state.Apply(pass);
                        }
                        continue;
                    }

                    var current = state.CurrentPlayer;
                    var legal = state.GetLegalActions(current);
                    if (legal.Count == 0) break;

                    var draw = legal.FirstOrDefault(option => option.Kind == ActionKind.Draw);
                    if (draw != null) { state.Apply(draw); continue; }

                    var turn = ClockExpiry.Choose(state, current, legal, bot);
                    report.Check(turn is { Kind: ActionKind.Discard },
                        "a turn that has finished drawing always has a discard the clock may play");
                    report.Check(turn!.Kind != ActionKind.Win, "the clock never declares Hu for a silent player");
                    forcedDiscards++;

                    state.Apply(bot.Choose(state, current, legal));
                }
            }

            report.Check(claimWindows > 0, "random play reaches claim windows");
            report.Check(forcedDiscards > 0, "the clock plays the discard of a silent turn");
            report.Check(passOnlyWindows > 0, "some claim windows offer nothing but a pass");

            // A window with nothing to play: the clock returns null instead of inventing an action.
            var idleState = new GameState();
            idleState.StartHand(dealer: 0, seed: 7);
            report.Equal(null, ClockExpiry.Choose(idleState, 0, Array.Empty<LegalAction>()),
                "an empty action list gives the clock nothing to play");
            idleState.Apply(idleState.GetLegalActions(0).First(option => option.Kind == ActionKind.Discard));
            report.Equal(null, ClockExpiry.Choose(idleState, 0, idleState.GetLegalActions(0)),
                "a turn that already discarded has nothing left for the clock");
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

        private static void DangerReads(SelfTestReport report)
        {
            // A table built by hand: seat 1 threw 5c and holds an open pung of 3b,
            // seat 2 threw 1c. Nothing else is public.
            var state = new GameState();
            state.Players[1].AddDiscard(new Tile(ParseTile("5c"), 1));
            state.Players[1].AddMeld(new Meld(MeldKind.Pung, Tiles("3b 3b 3b"), 0));
            state.Players[2].AddDiscard(new Tile(ParseTile("1c"), 2));

            var model = DangerModel.Read(state, 0);

            report.Equal(0.0, model.Pressure(0), "a player is not a threat to itself");
            report.Check(model.Turn == 2, $"two discards should be on the table, got {model.Turn}");
            report.Check(model.VisibleCopies(ParseTile("3b")) == 3, "a meld makes its three tiles visible");

            var ownDiscard = model.FaceDanger(ParseTile("5c"), 1);
            var freshFace = model.FaceDanger(ParseTile("5d"), 1);
            report.Check(ownDiscard < freshFace,
                $"a face the opponent already threw must read safer than a fresh one ({ownDiscard:0.00} vs {freshFace:0.00})");
            report.Check(ownDiscard > 0.0,
                "MCR has no furiten, so an opponent's own discard is unlikely but never impossible");

            var melded = model.Danger(ParseTile("3b"));
            report.Check(melded < model.Danger(ParseTile("4b")),
                $"a face locked in an opponent's meld is safer than a fresh neighbour ({melded:0.00} vs {model.Danger(ParseTile("4b")):0.00})");

            report.Check(model.Danger(ParseTile("1d")) < model.Danger(ParseTile("5d")),
                "terminals wait on fewer shapes than middle tiles");
            // Three copies of an honour are on the table: no pung and no pair wait is possible.
            var honourState = new GameState();
            honourState.Players[1].AddDiscard(new Tile(ParseTile("Rd"), 10));
            honourState.Players[2].AddDiscard(new Tile(ParseTile("Rd"), 11));
            honourState.Players[3].AddMeld(new Meld(MeldKind.Pung, Tiles("Rd Rd Rd"), 1));
            var honourModel = DangerModel.Read(honourState, 0);
            report.Check(honourModel.Danger(ParseTile("Rd")) < 0.1,
                $"three visible copies of an honour leave nothing to wait on, got {honourModel.Danger(ParseTile("Rd")):0.00}");
            report.Check(honourModel.Danger(ParseTile("Gd")) > honourModel.Danger(ParseTile("Rd")),
                "a dragon nobody has shown is more dangerous than one that is exhausted");

            // The same face gets worse as the table warms up: more melds, more discards.
            var warmState = new GameState();
            for (var index = 0; index < 12; index++)
                warmState.Players[index % 4].AddDiscard(new Tile(new TileType(TileSuit.Dots, index % 9 + 1), 100 + index));
            warmState.Players[1].AddMeld(new Meld(MeldKind.Chow, Tiles("1d 2d 3d"), 0));
            warmState.Players[1].AddMeld(new Meld(MeldKind.Pung, Tiles("7d 7d 7d"), 0));
            var warm = DangerModel.Read(warmState, 0);
            report.Check(warm.Pressure(1) > model.Pressure(1),
                $"melds and discards must raise the pressure (warm {warm.Pressure(1):0.00}, cold {model.Pressure(1):0.00})");
            report.Check(warm.Pressure(1) <= 1.0, "pressure stays inside zero to one");

            // Copies in the own hand cut the danger: fewer copies are left for a wait.
            var ownState = new GameState();
            ownState.Players[0].AddConcealed(new Tile(ParseTile("6d"), 20));
            ownState.Players[0].AddConcealed(new Tile(ParseTile("6d"), 21));
            var holding = DangerModel.Read(ownState, 0).Danger(ParseTile("6d"));
            var notHolding = DangerModel.Read(ownState, 1).Danger(ParseTile("6d"));
            report.Check(holding < notHolding,
                $"two copies in the own hand make a face safer ({holding:0.00} vs {notHolding:0.00})");
        }

        private static void BotDefense(SelfTestReport report)
        {
            var dangerFast = 0.0;
            var dangerCautious = 0.0;
            var shantenFast = 0.0;
            var shantenCautious = 0.0;
            var decisions = 0;

            for (var handIndex = 0; handIndex < 6; handIndex++)
            {
                var state = new GameState();
                state.StartHand(handIndex % 4, 5000 + handIndex);
                var bots = Enumerable.Range(0, 4)
                    .Select(seat => (Cautious: new HeuristicBot(handIndex * 7 + seat, BotLevel.Cautious),
                        Fast: new HeuristicBot(handIndex * 14 + seat * 2, BotLevel.Fast)))
                    .ToArray();
                var random = new Random(handIndex);
                var steps = 0;

                while (state.Phase != GamePhase.HandFinished && steps++ < 2000)
                {
                    if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                    {
                        for (var player = 0; player < 4; player++)
                        {
                            var options = state.GetLegalActions(player);
                            if (options.Count == 0) continue;
                            state.Apply(bots[player].Fast.Choose(state, player, options));
                            if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;
                        }
                        continue;
                    }

                    var actor = state.CurrentPlayer;
                    var current = state.GetLegalActions(actor);
                    if (current.Count == 0) break;

                    if (current.Any(option => option.Kind == ActionKind.Discard))
                    {
                        var model = DangerModel.Read(state, actor);
                        var tiles = state.Players[actor].Concealed.Select(tile => tile.Type).ToList();

                        var cautious = bots[actor].Cautious.Choose(state, actor, current);
                        var fast = bots[actor].Fast.Choose(state, actor, current);
                        if (cautious.Kind == ActionKind.Discard && fast.Kind == ActionKind.Discard)
                        {
                            decisions++;
                            dangerCautious += model.Danger(cautious.Tile!.Value.Type);
                            dangerFast += model.Danger(fast.Tile!.Value.Type);
                            shantenCautious += ShantenAfter(tiles, cautious.Tile!.Value.Type, state.Players[actor].Melds.Count);
                            shantenFast += ShantenAfter(tiles, fast.Tile!.Value.Type, state.Players[actor].Melds.Count);
                        }
                    }

                    state.Apply(bots[state.CurrentPlayer].Fast.Choose(state, state.CurrentPlayer, current));
                }

                report.Check(true, "a hand with the cautious bot reaches the end");
            }

            report.Check(decisions > 200, $"the comparison should see many discards, got {decisions}");
            var avgCautious = dangerCautious / Math.Max(1, decisions);
            var avgFast = dangerFast / Math.Max(1, decisions);
            var shantenCost = (shantenCautious - shantenFast) / Math.Max(1, decisions);

            report.Check(avgCautious < avgFast * 0.9,
                $"the cautious level must throw safer tiles (cautious {avgCautious:0.000}, fast {avgFast:0.000})");
            report.Check(shantenCost < 0.1,
                $"safety must not cost much shape, average shanten cost was {shantenCost:0.000}");
        }

        private static int ShantenAfter(List<TileType> tiles, TileType discarded, int meldCount)
        {
            var rest = new List<TileType>(tiles);
            rest.Remove(discarded);
            return HandShape.Shanten(rest, meldCount);
        }

        private static void BotStrategy(SelfTestReport report)
        {
            // The waiting hand is read with the real scorer, not with a hand-made table:
            // a full flush pays, a mixed chicken hand does not even reach the eight-point minimum.
            var nineGates = Tiles("1d 1d 1d 2d 3d 4d 5d 6d 7d 8d 9d 9d 9d").Select(tile => tile.Type).ToList();
            var flushWait = WaitQuality.Of(nineGates, Array.Empty<Meld>());
            report.Check(flushWait.Waits == 9, $"nine gates wait on every dot, got {flushWait.Waits}");
            report.Check(flushWait.AveragePoints >= 24,
                $"a full flush must be valued as a full flush, got {flushWait.AveragePoints:0.0}");
            report.Check(flushWait.LiveCopies > 0, "the wait must count the copies that are still unseen");

            // A wait nobody can serve is worth less than a live one.
            var blocked = WaitQuality.Of(nineGates, Array.Empty<Meld>(), Wind.East, Wind.East,
                face => face == ParseTile("5d") ? 4 : 0);
            report.Check(blocked.LiveCopies < flushWait.LiveCopies,
                $"a wait on a face the table has shown four times must lose value ({blocked.LiveCopies} vs {flushWait.LiveCopies})");

            var cheapWait = WaitQuality.Of(Tiles("1c 2c 3c 4d 5d 6d 7b 8b 9b 5c 6c 5c 5c").Select(tile => tile.Type).ToList(),
                Array.Empty<Meld>());
            report.Check(cheapWait.Value < flushWait.Value,
                $"a mixed hand must read cheaper than a flush ({cheapWait.Value:0.0} vs {flushWait.Value:0.0})");

            // A hand with no shape at all: no set, no pair, no knitted pattern. (The knitted hand
            // 1c 4c 7c 2d 5d 8d 3b 6b 9b E S W N does wait — on the three dragons — so it is the
            // wrong example here and is checked in SpecialPatternWaits.)
            var notWaiting = WaitQuality.Of(Tiles("1c 4c 7c 2d 5d 8d 3b 6b 9b E E S W").Select(tile => tile.Type).ToList(),
                Array.Empty<Meld>());
            report.Equal(0, notWaiting.Waits, "a scattered hand has no waits");
            report.Equal(0.0, notWaiting.AveragePoints, "a hand without waits is worth nothing");

            // Match position: behind means push more, ahead means fold earlier.
            var even = HeuristicBot.PushWeight(20, 2);
            report.Check(HeuristicBot.PushWeight(20, 2, 80) > even, "a seat far behind must take more risk");
            report.Check(HeuristicBot.PushWeight(20, 2, -80) < even, "a seat in front must take less risk");
            report.Check(HeuristicBot.PushWeight(20, 0, 80) < 0.3,
                "a tenpai hand pushes on its own, match position only trims the risk");

            // On a tenpai hand the strategic bot keeps the wait that pays: same shape, better value.
            var state = new GameState();
            var hand = Tiles("1d 1d 1d 2d 3d 4d 5d 6d 7d 8d 9d 9d 9d 1c");
            foreach (var tile in hand) state.Players[0].AddConcealed(tile);

            var options = state.Players[0].Concealed
                .Select(tile => new LegalAction(ActionKind.Discard, 0, tile, Array.Empty<Tile>(), $"Discard {tile.Type}"))
                .ToList();

            var strategic = new HeuristicBot(3, BotLevel.Strategic) { PointsBehind = 0 };
            var discard = strategic.Choose(state, 0, options);
            report.Check(discard.Kind == ActionKind.Discard, "a strategic bot still discards");
            report.Equal(ParseTile("1c"), discard.Tile!.Value.Type,
                "the odd tile of another suit goes before a full flush is broken");

            // A session of strategic bots: same guarantees as the other levels.
            var bots = new IMahjongBot[]
            {
                new HeuristicBot(41, BotLevel.Strategic),
                new HeuristicBot(42, BotLevel.Strategic),
                new HeuristicBot(43, BotLevel.Strategic),
                new HeuristicBot(44, BotLevel.Strategic)
            };
            var session = BotTable.PlaySession(7, bots);
            report.Equal(GameSession.TotalHands, session.HandsPlayed, "a strategic session plays sixteen hands");
            report.Equal(0, session.Scores.Sum(), "strategic bot payments are zero-sum");
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

        /// <summary>
        /// Rule 3.6.6: a discard can allow two different chows. The action carries the tiles it wants,
        /// and the engine has to consume exactly those. Matching only on the discarded face used to take
        /// the first chow offered and quietly replace the player's choice.
        /// </summary>
        private static void ChowIdentity(SelfTestReport report)
        {
            var checkedWindows = 0;
            for (var seed = 0; seed < 200 && checkedWindows < 25; seed++)
            {
                var state = new GameState();
                var random = new Random(seed * 31 + 7);
                state.StartHand(seed % 4, 5000 + seed);
                var steps = 0;
                while (state.Phase != GamePhase.HandFinished && steps++ < 600)
                {
                    if (state.Phase == GamePhase.AwaitingClaims)
                    {
                        var claimant = -1;
                        LegalAction? wanted = null;
                        for (var player = 0; player < 4; player++)
                        {
                            var chows = state.GetLegalActions(player)
                                .Where(action => action.Kind == ActionKind.Chow).ToList();
                            if (chows.Count < 2) continue;
                            claimant = player;
                            wanted = chows[chows.Count - 1];
                            break;
                        }

                        if (claimant >= 0)
                        {
                            checkedWindows++;
                            // Everyone else passes first, so the chow is the only live claim in the window.
                            for (var player = 0; player < 4; player++)
                            {
                                if (player == claimant) continue;
                                foreach (var pass in state.GetLegalActions(player)
                                             .Where(action => action.Kind == ActionKind.Pass).ToList())
                                    state.Apply(pass);
                            }

                            state.Apply(wanted!);
                            var meld = state.Players[claimant].Melds.Last();
                            var expected = wanted!.RequiredTiles.Select(tile => tile.InstanceId).OrderBy(id => id).ToList();
                            var taken = meld.Tiles
                                .Where(tile => tile.InstanceId != wanted.Tile!.Value.InstanceId)
                                .Select(tile => tile.InstanceId).OrderBy(id => id).ToList();
                            report.Equal(MeldKind.Chow, meld.Kind, $"seed {seed}: the claim became a chow");
                            report.Check(expected.SequenceEqual(taken),
                                $"seed {seed}: the chow that was played must be the chow that was offered");
                            break;
                        }
                    }

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

            report.Check(checkedWindows > 0, "random play must offer two chows on one discard");
        }

        /// <summary>
        /// Two invariants of a turn that the engine has to keep on every state it offers:
        /// Hu only while the player holds a freshly taken tile (a claim cannot be "upgraded" to Hu
        /// afterwards), and the last-wall-tile flag of the draw it describes.
        /// </summary>
        private static void CheckTurnInvariants(SelfTestReport report, GameState state, int seed)
        {
            if (state.Phase != GamePhase.AwaitingDiscard) return;

            report.Equal(state.Wall.IsExhausted, state.LastDrawWasLastWallTile,
                $"seed {seed}: the last wall tile flag describes the draw that was just made");

            if (state.LastDrawSource.HasValue) return;
            var current = state.GetLegalActions(state.CurrentPlayer);
            report.Check(current.All(action => action.Kind != ActionKind.Win),
                $"seed {seed}: Hu is not offered after a chow or a pung claim");
        }

        /// <summary>
        /// Rule 3.8.1 (the example of fan 56): a self-drawn win combines with the wait fans. The engine
        /// used to score a self-draw without a winning tile, so the wait could never be described.
        /// </summary>
        private static void SelfDrawWaits(SelfTestReport report)
        {
            var hand = Hand("1c 1c 1c 3c 3c 3c 5d 5d 5d 7b 7b 7b 9d 9d");
            var claimed = Score(Hand("1c 1c 1c 3c 3c 3c 5d 5d 5d 7b 7b 7b 9d"), ParseTile("9d"), ClaimSource.Discard);
            var selfDrawn = ScoreSelfDrawn(hand, ParseTile("9d"));

            report.Check(Has(claimed, Fans.SingleWait), "the claimed win of this hand waits on the pair alone");
            report.Check(Has(selfDrawn, Fans.SingleWait), "a self-drawn win keeps the single wait fan");
            report.Check(Has(selfDrawn, Fans.FourConcealedPungs), "a self-drawn win still reads four concealed pungs");
            report.Check(Has(selfDrawn, Fans.FullyConcealed), "a closed self-drawn hand is fully concealed");
            report.Check(!Has(selfDrawn, Fans.SelfDrawn), "fully concealed replaces self-drawn, as before");
            report.Check(selfDrawn.Analysis.Wait == WaitType.Single, "the self-drawn wait is described");
            report.Check(selfDrawn.Analysis.WaitTiles.Count == 1, "the self-drawn hand has exactly one wait");

            var openHand = new PlayerHand();
            openHand.AddMeld(new Meld(MeldKind.Pung, Tiles("7b 7b 7b"), 1));
            foreach (var tile in Tiles("1c 1c 1c 3c 3c 3c 5d 5d 5d 9d 9d")) openHand.AddConcealed(tile);
            var openSelfDraw = ScoreSelfDrawn(openHand, ParseTile("9d"));
            report.Check(!Has(openSelfDraw, Fans.ConcealedHand), "an open hand is not concealed");
            report.Check(Has(openSelfDraw, Fans.SelfDrawn), "an open self-drawn hand still earns self-drawn");
        }

        /// <summary>
        /// Fans 44, 45 and 58: the wall-based fans need the real last tile, and "last tile" counts how
        /// many copies of the winning face are visible — never the winner's own melds twice.
        /// </summary>
        private static void LastTileBoundaries(SelfTestReport report)
        {
            var hand = Hand("1c 1c 1c 3c 3c 3c 5d 5d 5d 7b 7b 7b 9d 9d");
            var lastDraw = ScoreSelfDrawn(hand, ParseTile("9d"), lastTileOfWall: true);
            report.Check(Has(lastDraw, Fans.LastTileDraw), "fan 44 is awarded when the wall gave its last tile");
            report.Check(!Has(lastDraw, Fans.SelfDrawn), "fan 44 replaces self-drawn (3.8.1)");

            var ordinaryDraw = ScoreSelfDrawn(hand, ParseTile("9d"));
            report.Check(!Has(ordinaryDraw, Fans.LastTileDraw), "no fan 44 while the wall still has tiles");

            var lastClaim = Score(Hand("1c 1c 1c 3c 3c 3c 5d 5d 5d 7b 7b 7b 9d"), ParseTile("9d"),
                ClaimSource.Discard, lastDiscard: true);
            report.Check(Has(lastClaim, Fans.LastTileClaim), "fan 45 is awarded on the last discard of the hand");

            // One copy of 5d sits in the winner's own chow (4d 5d 6d), one in the concealed hand and the
            // win itself is the third: the fourth copy still exists, so there is no Last Tile. Counting
            // the winner's own melds a second time used to award it anyway.
            var ownCopy = new PlayerHand();
            ownCopy.AddMeld(new Meld(MeldKind.Chow, Tiles("4d 5d 6d"), 1));
            foreach (var tile in Tiles("1c 1c 1c 7b 7b 7b 9c 9c 9c 5d")) ownCopy.AddConcealed(tile);
            var counted = Scoring.Evaluate(new ScoringContext
            {
                Hand = ownCopy,
                WinningTile = ParseTile("5d"),
                Source = ClaimSource.Discard,
                DiscardPile = Array.Empty<TileType>(),
                OtherMelds = Array.Empty<Meld>()
            });
            report.Check(!Has(counted, Fans.LastTile),
                "the winner's own meld must not be counted twice for the last tile");

            var fourth = Scoring.Evaluate(new ScoringContext
            {
                Hand = ownCopy,
                WinningTile = ParseTile("5d"),
                Source = ClaimSource.Discard,
                DiscardPile = new[] { ParseTile("5d") },
                OtherMelds = Array.Empty<Meld>()
            });
            report.Check(Has(fourth, Fans.LastTile), "the fourth visible copy of the winning face earns fan 58");
        }

        /// <summary>
        /// The waits of a hand must be read with the same test that decides whether a face completes it:
        /// a knitted or thirteen-orphans hand has waits even though it never splits into four sets.
        /// </summary>
        private static void SpecialPatternWaits(SelfTestReport report)
        {
            var knitted = WaitQuality.Of(Tiles("1c 4c 7c 2d 5d 8d 3b 6b 9b E S W N").Select(tile => tile.Type).ToList(),
                Array.Empty<Meld>());
            report.Equal(3, knitted.Waits, "a knitted hand waits on the three dragons");
            report.Check(knitted.AveragePoints >= 24, $"and it is worth a knitted hand, got {knitted.AveragePoints:0.0}");

            var orphans = Tiles("1c 9c 1d 9d 1b 9b E S W N Rd Gd Wd").Select(tile => tile.Type).ToList();
            var orphanWaits = HandSolver.WaitingTiles(WaitingHand(orphans));
            report.Equal(13, orphanWaits.Count, "thirteen orphans waits on all thirteen orphans");

            var pairs = Tiles("1c 1c 3c 3c 5d 5d 7b 7b 9b 9b E E Wd").Select(tile => tile.Type).ToList();
            var pairWaits = HandSolver.WaitingTiles(WaitingHand(pairs));
            report.Equal(1, pairWaits.Count, "seven pairs with one single waits on its own face");
            report.Check(pairWaits[0] == ParseTile("Wd"), "and the wait is the unpaired tile");

            // The same check has to run through the scorer, which is what the bots use.
            var ordinary = WaitQuality.Of(Tiles("1d 1d 1d 2d 3d 4d 5d 6d 7d 8d 9d 9d 9d").Select(t => t.Type).ToList(),
                Array.Empty<Meld>());
            report.Equal(9, ordinary.Waits, "a nine gates hand waits on every dot");
        }

        private static PlayerHand WaitingHand(IEnumerable<TileType> types)
        {
            var hand = new PlayerHand();
            var id = 0;
            foreach (var type in types) hand.AddConcealed(new Tile(type, id++));
            return hand;
        }

        /// <summary>
        /// Rule 3.11.2: a player more than fifteen minutes late has missed the session. He must not
        /// receive table points, and the seats that did play share the places among themselves.
        /// </summary>
        private static void ForfeitedRanking(SelfTestReport report)
        {
            var session = new GameSession();
            session.Apply(new Settlement(0, 100, true,
                new List<Payment> { new(1, 0, 108), new(2, 0, 108), new(3, 0, 108) }, 324, "self-test"));
            session.ReportLateArrival(2, TimeSpan.FromMinutes(20));

            var standings = session.Standings();
            var missing = standings.Single(row => row.Player == 2);
            report.Check(missing.Forfeited, "the table marks the missed session");
            report.Equal(4, missing.Place, "a player who did not come is ranked last");
            report.Equal(0d, missing.TablePoints, "and receives no table points");
            report.Equal(1, standings.Single(row => row.Player == 0).Place, "the leading seat keeps place one");
            report.Equal(1.5d, standings.Single(row => row.Player == 1).TablePoints, "the two tied seats share 2 and 1");
            report.Equal(1.5d, standings.Single(row => row.Player == 3).TablePoints, "both tied seats get the same share");
            report.Equal(7d, standings.Sum(row => row.TablePoints), "4 / 2 / 1 stay on the table");

            // Nobody may win table points by not showing up, even when every playing seat is in the red.
            var red = new GameSession();
            red.Apply(new Settlement(0, 10, false,
                new List<Payment> { new(0, 1, 18), new(0, 2, 8), new(0, 3, 8) }, 34, "self-test"));
            red.ReportLateArrival(1, TimeSpan.FromMinutes(30));
            report.Equal(0d, red.Standings().Single(row => row.Player == 1).TablePoints,
                "a forfeited seat cannot take table points out of a losing table");
        }

        /// <summary>
        /// Fan 46 and rule 3.6.8: when the kong replacement is the last tile of the wall, the hand
        /// must not stop as a draw — the player may win on that very tile or discard it, and only
        /// that discard can end the hand. The line below (never claim, always discard) burns the
        /// wall; the first hand that reaches a kong with exactly one tile left is checked, and the
        /// seed window is wide enough to survive a change in how the wall is dealt (the ring deal
        /// moved the first such hand from 1308 to 1975).
        /// </summary>
        private static void KongAtWallEnd(SelfTestReport report)
        {
            for (var seed = 1900; seed < 2150; seed++)
            {
                var state = new GameState();
                state.StartHand(seed % 4, seed);
                var steps = 0;
                while (state.Phase != GamePhase.HandFinished && steps++ < 1000)
                {
                    if (state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                    {
                        for (var player = 0; player < 4; player++)
                        {
                            var options = state.GetLegalActions(player);
                            if (options.Count == 0) continue;
                            state.Apply(options.First(action => action.Kind == ActionKind.Pass));
                            if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;
                        }
                        continue;
                    }

                    var current = state.GetLegalActions(state.CurrentPlayer);
                    if (current.Count == 0) break;
                    var kong = current.FirstOrDefault(action =>
                        action.Kind is ActionKind.ClosedKong or ActionKind.AddedKong);
                    if (kong != null && state.Wall.Remaining == 1)
                    {
                        var player = state.CurrentPlayer;
                        state.Apply(kong);
                        report.Equal(GamePhase.AwaitingDiscard, state.Phase,
                            $"seed {seed}: the last kong replacement keeps the turn alive");
                        report.Check(state.Wall.IsExhausted, $"seed {seed}: and it did take the last tile of the wall");
                        report.Check(state.LastDrawWasLastWallTile,
                            $"seed {seed}: the replacement is the last wall tile");
                        report.Equal(14, state.Players[player].HandSize,
                            $"seed {seed}: the player holds a full hand and has to discard");
                        return;
                    }

                    var discard = kong ?? current.FirstOrDefault(action => action.Kind == ActionKind.Discard);
                    state.Apply(discard ?? current[0]);
                }
            }

            report.Check(false, "no hand reached a kong on the last wall tile");
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
