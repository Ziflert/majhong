using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>Everything the scorer needs besides the hand itself.</summary>
    public sealed class ScoringContext
    {
        public PlayerHand Hand { get; set; } = null!;
        public TileType? WinningTile { get; set; }
        public ClaimSource Source { get; set; }
        public Wind RoundWind { get; set; } = Wind.East;
        public Wind SeatWind { get; set; } = Wind.East;
        public bool LastTileOfWall { get; set; }
        public bool LastDiscard { get; set; }
        public bool RobbedKong { get; set; }
        public IReadOnlyCollection<TileType> DiscardPile { get; set; } = Array.Empty<TileType>();
        public IReadOnlyCollection<Meld> OtherMelds { get; set; } = Array.Empty<Meld>();
    }

    public sealed record ScoreResult(
        bool IsValidWin,
        int TotalPoints,
        int HandPoints,
        IReadOnlyList<ScoredFan> Fans,
        HandAnalysis Analysis,
        string? Error = null)
    {
        /// <summary>A hand that may not win at all, e.g. after a violation of rule 3.11.6.</summary>
        public static ScoreResult Forbidden(string reason) =>
            new(false, 0, 0, Array.Empty<ScoredFan>(), HandAnalysis.Fail(reason), reason);
    }

    internal sealed class ShapeGroup
    {
        public GroupKind Kind { get; init; }
        public IReadOnlyList<TileType> Tiles { get; init; } = Array.Empty<TileType>();
        public bool IsConcealed { get; init; } = true;
        public bool IsKong { get; init; }
        public bool IsAddedKong { get; init; }
        public TileType Base => Tiles[0];
        public int Count => Tiles.Count;
    }

    internal sealed class Shape
    {
        public List<ShapeGroup> Groups { get; } = new();
        public TileType Pair { get; init; }
        public bool HasPair { get; init; }
        public WinningPattern Pattern { get; init; } = WinningPattern.Standard;
    }

    /// <summary>
    /// Fan evaluation. A hand must reach eight points excluding flowers (rule 3.4.22 and 3.11.6.6).
    /// </summary>
    public static class Scoring
    {
        public const int MinimumPoints = 8;

        public static ScoreResult Evaluate(ScoringContext context)
        {
            var analysis = HandSolver.Analyse(context.Hand, context.WinningTile);
            if (!analysis.IsComplete)
                return new ScoreResult(false, 0, 0, Array.Empty<ScoredFan>(), analysis, analysis.Error);

            ScoreResult? best = null;
            foreach (var shape in BuildShapes(context, analysis))
            {
                var fans = EvaluateShape(shape, context, analysis);
                var handPoints = fans.Sum(f => f.Fan.Points);
                var flowers = context.Hand.Flowers.Count * Fans.Flower.Points;
                var error = handPoints >= MinimumPoints
                    ? null
                    : $"The hand scores {handPoints} points; a win needs {MinimumPoints} excluding flowers.";
                var candidate = new ScoreResult(handPoints >= MinimumPoints, handPoints + flowers, handPoints, fans, analysis, error);
                if (best == null || candidate.TotalPoints > best.TotalPoints) best = candidate;
            }

            return best ?? new ScoreResult(false, 0, 0, Array.Empty<ScoredFan>(), analysis, "No decomposition could be scored.");
        }

        private static IEnumerable<Shape> BuildShapes(ScoringContext context, HandAnalysis analysis)
        {
            if (analysis.Pattern.HasValue && analysis.Pattern.Value != WinningPattern.Standard)
                yield return new Shape { Pattern = analysis.Pattern.Value };

            foreach (var decomposition in analysis.Decompositions)
            {
                var shape = new Shape { Pair = decomposition.Pair, HasPair = true };
                var winningAssigned = false;

                foreach (var meld in context.Hand.Melds)
                    shape.Groups.Add(new ShapeGroup
                    {
                        Kind = meld.Kind == MeldKind.Chow ? GroupKind.Chow : GroupKind.Pung,
                        Tiles = meld.Types,
                        IsConcealed = meld.IsConcealed,
                        IsKong = meld.IsKong,
                        IsAddedKong = meld.Kind == MeldKind.AddedKong
                    });

                foreach (var group in decomposition.Groups)
                {
                    var concealed = true;
                    if (!winningAssigned && context.Source == ClaimSource.Discard && context.WinningTile.HasValue
                        && group.Contains(context.WinningTile.Value))
                    {
                        concealed = false;
                        winningAssigned = true;
                    }
                    shape.Groups.Add(new ShapeGroup { Kind = group.Kind, Tiles = group.Tiles, IsConcealed = concealed });
                }

                yield return shape;
            }
        }

        private static List<ScoredFan> EvaluateShape(Shape shape, ScoringContext context, HandAnalysis analysis)
        {
            var fans = new List<ScoredFan>();
            void Add(Fan fan, string evidence) => fans.Add(new ScoredFan(fan, evidence));

            var groups = shape.Groups;
            var pungs = groups.Where(g => g.Kind == GroupKind.Pung).ToList();
            var chows = groups.Where(g => g.Kind == GroupKind.Chow).ToList();
            var tiles = groups.SelectMany(g => g.Tiles).ToList();
            if (shape.HasPair) tiles.Add(shape.Pair);

            var isSelfDraw = context.Source != ClaimSource.Discard;
            var closed = !context.Hand.HasOpenMelds;

            if (shape.Pattern != WinningPattern.Standard)
            {
                switch (shape.Pattern)
                {
                    case WinningPattern.ThirteenOrphans:
                        Add(Fans.ThirteenOrphans, "all terminals and honors, one duplicated");
                        break;
                    case WinningPattern.SevenShiftedPairs:
                        Add(Fans.SevenShiftedPairs, "seven consecutive pairs in one suit");
                        break;
                    case WinningPattern.SevenPairs:
                        Add(Fans.SevenPairs, "seven pairs");
                        break;
                    case WinningPattern.KnittedTiles:
                        EvaluateKnittedTiles(context, Add);
                        break;
                    case WinningPattern.KnittedStraight:
                        Add(Fans.KnittedStraight, "147, 258 and 369 in three suits plus one set and a pair");
                        break;
                }
                if (shape.Pattern != WinningPattern.KnittedTiles && closed && isSelfDraw)
                    Add(Fans.FullyConcealed, "closed hand won by self-draw");
                if (isSelfDraw) Add(Fans.SelfDrawn, "won from the wall");
                return ApplyExclusions(fans);
            }

            var numberedSuits = tiles.Where(t => t.IsNumbered).Select(t => t.Suit).Distinct().ToList();
            var hasHonors = tiles.Any(t => t.IsHonor);
            var kongs = groups.Where(g => g.IsKong).ToList();
            var concealedPungs = pungs.Where(p => p.IsConcealed).ToList();
            var dragonPungs = pungs.Where(p => p.Base.IsDragon).ToList();
            var windPungs = pungs.Where(p => p.Base.IsWind).ToList();

            // ---- 88 points
            if (pungs.Count == 4 && pungs.All(g => g.Base.IsWind))
                Add(Fans.BigFourWinds, "pungs of all four winds");
            if (dragonPungs.Count == 3)
                Add(Fans.BigThreeDragons, "pungs of all three dragons");
            if (tiles.All(t => t.IsGreen))
                Add(Fans.AllGreen, "only green tiles");
            if (nineGatesPredicate(context))
                Add(Fans.NineGates, "1112345678999 in one suit");
            if (kongs.Count == 4)
                Add(Fans.FourKongs, "four kongs");

            // ---- 64 points
            if (tiles.All(t => t.IsTerminal))
                Add(Fans.AllTerminals, "only 1s and 9s");
            if (windPungs.Count == 3 && shape.HasPair && shape.Pair.IsWind)
                Add(Fans.LittleFourWinds, "three wind pungs and the fourth wind as the pair");
            if (dragonPungs.Count == 2 && shape.HasPair && shape.Pair.IsDragon)
                Add(Fans.LittleThreeDragons, "two dragon pungs and the third dragon as the pair");
            if (tiles.All(t => t.IsHonor))
                Add(Fans.AllHonors, "only honor tiles");
            if (pungs.Count == 4 && concealedPungs.Count == 4)
                Add(Fans.FourConcealedPungs, "four concealed pungs");

            // ---- 48 points
            if (isFourTerminalChows(chows, shape))
                Add(Fans.FourTerminalChows, "123 and 789 twice in one suit with a pair of 5s");
            if (chows.Count == 4 && chows.Select(c => (c.Base.Suit, c.Base.Rank)).Distinct().Count() == 1)
                Add(Fans.QuadrupleChow, "four identical chows");
            if (isFourPureShiftedPungs(pungs))
                Add(Fans.FourPureShiftedPungs, "four consecutive pungs in one suit");

            // ---- 32 points
            if (isFourPureShiftedChows(chows))
                Add(Fans.FourPureShiftedChows, "four chows shifted by one or two in one suit");
            if (kongs.Count == 3)
                Add(Fans.ThreeKongs, "three kongs");
            if (tiles.All(t => t.IsTerminalOrHonor))
                Add(Fans.AllTerminalsAndHonors, "only terminals and honors");

            // ---- 24 points
            if (pungs.Count == 4 && pungs.All(g => g.Base.IsNumbered && g.Base.Rank % 2 == 0)
                && shape.HasPair && shape.Pair.IsNumbered && shape.Pair.Rank % 2 == 0)
                Add(Fans.AllEvenPungs, "pungs of 2, 4, 6 and 8");
            if (numberedSuits.Count == 1 && !hasHonors)
                Add(Fans.FullFlush, $"all tiles in {numberedSuits[0]}");
            if (chows.Count >= 3 && chows.GroupBy(c => (c.Base.Suit, c.Base.Rank)).Any(g => g.Count() == 3))
                Add(Fans.PureTripleChow, "three identical chows in one suit");
            if (isPureShiftedPungs(pungs))
                Add(Fans.PureShiftedPungs, "three consecutive pungs in one suit");
            if (hasOnlyRankRange(tiles, 7, 9)) Add(Fans.UpperTiles, "only 7, 8 and 9");
            if (hasOnlyRankRange(tiles, 4, 6)) Add(Fans.MiddleTiles, "only 4, 5 and 6");
            if (hasOnlyRankRange(tiles, 1, 3)) Add(Fans.LowerTiles, "only 1, 2 and 3");

            // ---- 16 points
            if (hasPureStraight(chows))
                Add(Fans.PureStraight, "123, 456 and 789 in one suit");
            if (isThreeSuitedTerminalChows(chows, shape))
                Add(Fans.ThreeSuitedTerminalChows, "123 and 789 in two suits, pair of 5s in the third");
            if (isPureShiftedChows(chows))
                Add(Fans.PureShiftedChows, "three shifted chows in one suit");
            if (groups.Count > 0 && groups.All(g => g.Tiles.Any(t => t.IsNumbered && t.Rank == 5))
                && shape.HasPair && shape.Pair.IsNumbered && shape.Pair.Rank == 5)
                Add(Fans.AllFives, "every set contains a 5");
            if (isTriplePung(pungs))
                Add(Fans.TriplePung, "same-rank pungs in all three suits");
            if (concealedPungs.Count == 3)
                Add(Fans.ThreeConcealedPungs, "three concealed pungs");

            // ---- 12 points
            if (hasOnlyRankRange(tiles, 6, 9)) Add(Fans.UpperFour, "only 6 to 9");
            if (hasOnlyRankRange(tiles, 1, 4)) Add(Fans.LowerFour, "only 1 to 4");
            if (windPungs.Count == 3)
                Add(Fans.BigThreeWinds, "three wind pungs");

            // ---- 8 points
            if (hasMixedStraight(chows))
                Add(Fans.MixedStraight, "123, 456 and 789 in three suits");
            if (tiles.All(t => t.IsReversible))
                Add(Fans.ReversibleTiles, "only reversible tiles");
            if (isMixedTripleChow(chows))
                Add(Fans.MixedTripleChow, "the same chow in three suits");
            if (isMixedShiftedPungs(pungs))
                Add(Fans.MixedShiftedPungs, "consecutive pungs in three suits");
            if (context.LastTileOfWall && isSelfDraw)
                Add(Fans.LastTileDraw, "won on the last tile of the wall");
            if (context.LastDiscard && !isSelfDraw)
                Add(Fans.LastTileClaim, "won on the last discard of the hand");
            if (context.Source == ClaimSource.KongReplacement)
                Add(Fans.OutWithReplacement, "won on the kong replacement tile");
            if (context.RobbedKong)
                Add(Fans.RobbingKong, "robbed the kong");
            if (kongs.Count(g => g.IsConcealed) == 2)
                Add(Fans.TwoConcealedKongs, "two concealed kongs");

            // ---- 6 points
            if (pungs.Count == 4)
                Add(Fans.AllPungs, "four pungs");
            if (hasHonors && numberedSuits.Count == 1)
                Add(Fans.HalfFlush, $"one suit plus honors ({numberedSuits[0]})");
            if (isMixedShiftedChows(chows))
                Add(Fans.MixedShiftedChows, "consecutive chows in three suits");
            if (Knitted.ContainsKnittedStraight(tiles))
                Add(Fans.KnittedStraight, "147, 258 and 369 in three suits plus one set and a pair");
            if (kongs.Count(k => k.IsConcealed) == 1 && kongs.Count(k => !k.IsConcealed) == 1)
                Add(Fans.ConcealedAndMeldedKong, "one concealed and one melded kong");
            if (isAllTypes(groups, shape))
                Add(Fans.AllTypes, "five different tile types");
            if (isMeldedHand(context))
                Add(Fans.MeldedHand, "all four sets claimed, won on a discard");
            if (dragonPungs.Count == 2)
                Add(Fans.TwoDragonPungs, "two dragon pungs");

            // ---- 4 points
            if (groups.Count == 4 && groups.All(g => g.Tiles.Any(t => t.IsTerminalOrHonor))
                && shape.HasPair && shape.Pair.IsTerminalOrHonor)
                Add(Fans.OutsideHand, "every set and the pair contain a terminal or honor");
            if (closed && isSelfDraw)
                Add(Fans.FullyConcealed, "closed hand won by self-draw");
            if (kongs.Count(g => !g.IsConcealed) == 2)
                Add(Fans.TwoMeldedKongs, "two melded kongs");
            if (context.WinningTile.HasValue && visibleCopies(context) >= 4)
                Add(Fans.LastTile, "the fourth copy of the winning tile");

            // ---- 2 points
            foreach (var pung in dragonPungs) Add(Fans.DragonPung, $"pung of {(Dragon)pung.Base.Rank}");
            foreach (var pung in windPungs.Where(p => (Wind)p.Base.Rank == context.RoundWind))
                Add(Fans.RoundWindPung, $"pung of the round wind ({context.RoundWind})");
            foreach (var pung in windPungs.Where(p => (Wind)p.Base.Rank == context.SeatWind))
                Add(Fans.SeatWindPung, $"pung of the seat wind ({context.SeatWind})");
            if (closed && !isSelfDraw)
                Add(Fans.ConcealedHand, "closed hand won on a discard");
            if (pungs.Count == 0 && !hasHonors)
                Add(Fans.AllChows, "four chows, no honors");
            if (isTileHog(context))
                Add(Fans.TileHog, "four identical tiles outside a kong");
            if (isDoublePung(pungs))
                Add(Fans.DoublePung, "two pungs of the same rank in different suits");
            if (concealedPungs.Count == 2)
                Add(Fans.TwoConcealedPungs, "two concealed pungs");
            if (kongs.Count(g => g.IsConcealed) == 1)
                Add(Fans.ConcealedKong, "one concealed kong");
            if (tiles.All(t => t.IsSimple))
                Add(Fans.AllSimples, "no terminals and no honors");

            // ---- 1 point
            foreach (var evidence in pureDoubleChows(chows)) Add(Fans.PureDoubleChow, evidence);
            foreach (var evidence in mixedDoubleChows(chows)) Add(Fans.MixedDoubleChow, evidence);
            foreach (var evidence in shortStraights(chows)) Add(Fans.ShortStraight, evidence);
            if (twoTerminalChows(chows)) Add(Fans.TwoTerminalChows, "123 and 789 in the same suit");
            foreach (var pung in pungs.Where(p => p.Base.IsTerminal || p.Base.IsWind))
                Add(Fans.PungOfTerminalsOrHonors, $"pung of {pung.Base}");
            foreach (var kong in kongs.Where(k => !k.IsConcealed)) Add(Fans.MeldedKong, $"melded kong of {kong.Base}");
            if (numberedSuits.Count == 2 && hasHonors)
                Add(Fans.OneVoidedSuit, $"only two of the three suits: {string.Join(",", numberedSuits)}");
            if (!hasHonors && numberedSuits.Count > 0)
                Add(Fans.NoHonors, "no honors");
            switch (analysis.Wait)
            {
                case WaitType.Edge: Add(Fans.EdgeWait, "edge wait"); break;
                case WaitType.Closed: Add(Fans.ClosedWait, "closed wait"); break;
                case WaitType.Single: Add(Fans.SingleWait, "single wait"); break;
            }
            if (isSelfDraw)
                Add(Fans.SelfDrawn, "won from the wall");

            var kept = ApplyExclusions(fans);
            if (kept.Count == 0) kept.Add(new ScoredFan(Fans.ChickenHand, "no other fan scored"));
            return kept;
        }

        // ------------------------------------------------------------ predicates

        private static void EvaluateKnittedTiles(ScoringContext context, System.Action<Fan, string> add)
        {
            var tiles = context.Hand.Concealed.Select(t => t.Type).ToList();
            if (context.WinningTile.HasValue) tiles.Add(context.WinningTile.Value);

            var suited = tiles.Where(t => t.IsNumbered).ToList();
            var honors = tiles.Where(t => t.IsHonor).ToList();

            if (honors.Count == 7) add(Fans.GreaterHonorsKnitted, "all seven honors plus knitted suit tiles");
            else add(Fans.LesserHonorsKnitted, $"{honors.Count} single honors plus knitted suit tiles");

            if (Knitted.IsCompleteKnittedStraight(suited))
                add(Fans.KnittedStraight, "147, 258 and 369 in three suits");
        }

        private static bool nineGatesPredicate(ScoringContext context)
        {
            if (context.Hand.Melds.Count > 0) return false;
            var tiles = context.Hand.Concealed.Select(t => t.Type).ToList();
            if (context.WinningTile.HasValue) tiles.Add(context.WinningTile.Value);
            if (tiles.Count != 14 || !tiles.All(t => t.IsNumbered)) return false;
            if (tiles.Select(t => t.Suit).Distinct().Count() != 1) return false;
            var counts = tiles.GroupBy(t => t.Rank).ToDictionary(g => g.Key, g => g.Count());
            if (counts.GetValueOrDefault(1) < 3 || counts.GetValueOrDefault(9) < 3) return false;
            for (var rank = 2; rank <= 8; rank++)
                if (counts.GetValueOrDefault(rank) < 1) return false;
            return true;
        }

        private static bool hasOnlyRankRange(IReadOnlyCollection<TileType> tiles, int low, int high) =>
            tiles.Count > 0 && tiles.All(t => t.IsNumbered && t.Rank >= low && t.Rank <= high);

        private static bool isFourTerminalChows(List<ShapeGroup> chows, Shape shape) =>
            shape.HasPair && shape.Pair.IsNumbered && shape.Pair.Rank == 5
            && chows.Count == 4
            && chows.All(c => c.Base.Suit == shape.Pair.Suit)
            && chows.Count(c => c.Base.Rank == 1) == 2
            && chows.Count(c => c.Base.Rank == 7) == 2;

        private static bool isFourPureShiftedPungs(List<ShapeGroup> pungs) => isPureShiftedPungs(pungs, 4);

        private static bool isPureShiftedPungs(List<ShapeGroup> pungs, int count = 3)
        {
            if (pungs.Count != count) return false;
            if (pungs.Select(p => p.Base.Suit).Distinct().Count() != 1) return false;
            var ranks = pungs.Select(p => p.Base.Rank).OrderBy(r => r).ToList();
            for (var i = 1; i < ranks.Count; i++)
                if (ranks[i] != ranks[i - 1] + 1) return false;
            return true;
        }

        private static bool isFourPureShiftedChows(List<ShapeGroup> chows) => isPureShiftedChows(chows, 4);

        private static bool isPureShiftedChows(List<ShapeGroup> chows, int count = 3)
        {
            if (chows.Count != count) return false;
            if (chows.Select(c => c.Base.Suit).Distinct().Count() != 1) return false;
            var starts = chows.Select(c => c.Base.Rank).OrderBy(r => r).ToList();
            var steps = new HashSet<int>();
            for (var i = 1; i < starts.Count; i++) steps.Add(starts[i] - starts[i - 1]);
            return steps.Count == 1 && (steps.First() == 1 || steps.First() == 2);
        }

        private static bool hasPureStraight(List<ShapeGroup> chows) =>
            chows.Select(c => (c.Base.Suit, c.Base.Rank)).Distinct().Count(g => g.Rank is 1 or 4 or 7) >= 3
            && new[] { 1, 4, 7 }.All(start => chows.Any(c => c.Base.Rank == start)
                && chows.Any(c => c.Base.Suit == chows.First(x => x.Base.Rank == start).Base.Suit));

        private static bool isThreeSuitedTerminalChows(List<ShapeGroup> chows, Shape shape)
        {
            if (!shape.HasPair || !shape.Pair.IsNumbered || shape.Pair.Rank != 5) return false;
            var bySuit = chows.GroupBy(c => c.Base.Suit).ToList();
            if (bySuit.Count != 2) return false;
            var withBoth = bySuit.Count(g => g.Count(c => c.Base.Rank == 1) == 1 && g.Count(c => c.Base.Rank == 7) == 1);
            return withBoth == 2 && !bySuit.Any(g => g.Key == shape.Pair.Suit);
        }

        private static bool hasMixedStraight(List<ShapeGroup> chows)
        {
            foreach (var startGroup in chows.Where(c => c.Base.Rank == 1))
            foreach (var middleGroup in chows.Where(c => c.Base.Rank == 4 && c.Base.Suit != startGroup.Base.Suit))
            foreach (var endGroup in chows.Where(c => c.Base.Rank == 7 && c.Base.Suit != startGroup.Base.Suit && c.Base.Suit != middleGroup.Base.Suit))
                return true;
            return false;
        }

        private static bool isMixedTripleChow(List<ShapeGroup> chows) =>
            chows.GroupBy(c => c.Base.Rank).Any(g => g.Select(c => c.Base.Suit).Distinct().Count() == 3);

        private static bool isTriplePung(List<ShapeGroup> pungs) =>
            pungs.GroupBy(p => p.Base.Rank).Any(g => g.Select(p => p.Base.Suit).Distinct().Count() == 3);

        private static bool isDoublePung(List<ShapeGroup> pungs) =>
            pungs.GroupBy(p => p.Base.Rank).Any(g => g.Select(p => p.Base.Suit).Distinct().Count() >= 2);

        private static bool isMixedShiftedPungs(List<ShapeGroup> pungs)
        {
            if (pungs.Count != 3) return false;
            if (pungs.Select(p => p.Base.Suit).Distinct().Count() != 3) return false;
            var ranks = pungs.Select(p => p.Base.Rank).OrderBy(r => r).ToList();
            return ranks[1] == ranks[0] + 1 && ranks[2] == ranks[1] + 1;
        }

        private static bool isMixedShiftedChows(List<ShapeGroup> chows)
        {
            if (chows.Count != 3) return false;
            if (chows.Select(c => c.Base.Suit).Distinct().Count() != 3) return false;
            var starts = chows.Select(c => c.Base.Rank).OrderBy(r => r).ToList();
            return starts[1] == starts[0] + 1 && starts[2] == starts[1] + 1;
        }

        private static bool isAllTypes(IReadOnlyList<ShapeGroup> groups, Shape shape)
        {
            var categories = new HashSet<TileSuit>();
            foreach (var group in groups) categories.Add(Category(group.Base));
            if (shape.HasPair) categories.Add(Category(shape.Pair));
            return categories.Count == 5
                   && categories.Contains(TileSuit.Characters) && categories.Contains(TileSuit.Dots)
                   && categories.Contains(TileSuit.Bamboo) && categories.Contains(TileSuit.Wind)
                   && categories.Contains(TileSuit.Dragon);
        }

        private static TileSuit Category(TileType type) => type.Suit switch
        {
            TileSuit.Wind => TileSuit.Wind,
            TileSuit.Dragon => TileSuit.Dragon,
            _ => type.Suit
        };

        private static bool isMeldedHand(ScoringContext context) =>
            context.Hand.Melds.Count == 4
            && context.Hand.Melds.All(m => m.IsOpen && m.ClaimedFromPlayer.HasValue)
            && context.Source == ClaimSource.Discard;

        private static bool isTileHog(ScoringContext context)
        {
            var counts = new Dictionary<TileType, int>();
            foreach (var type in context.Hand.AllTypes())
                counts[type] = counts.GetValueOrDefault(type) + 1;
            if (context.WinningTile.HasValue)
                counts[context.WinningTile.Value] = counts.GetValueOrDefault(context.WinningTile.Value) + 1;

            foreach (var entry in counts.Where(c => c.Value >= 4))
                if (!context.Hand.Melds.Any(m => m.IsKong && m.BaseType == entry.Key)) return true;
            return false;
        }

        private static int visibleCopies(ScoringContext context)
        {
            var type = context.WinningTile!.Value;
            var count = context.Hand.AllTypes().Count(t => t == type)
                        + context.DiscardPile.Count(t => t == type)
                        + context.OtherMelds.SelectMany(m => m.Types).Count(t => t == type);
            return count;
        }

        private static IEnumerable<string> pureDoubleChows(List<ShapeGroup> chows) =>
            chows.GroupBy(c => (c.Base.Suit, c.Base.Rank))
                .Where(g => g.Count() >= 2)
                .Select(g => $"two identical chows starting at {g.Key.Rank} {g.Key.Suit}");

        private static IEnumerable<string> mixedDoubleChows(List<ShapeGroup> chows) =>
            chows.GroupBy(c => c.Base.Rank)
                .Where(g => g.Select(c => c.Base.Suit).Distinct().Count() >= 2)
                .Select(g => $"same-rank chows in different suits at {g.Key}");

        private static IEnumerable<string> shortStraights(List<ShapeGroup> chows) =>
            chows.Where(a => chows.Any(b => b != a && b.Base.Suit == a.Base.Suit && b.Base.Rank == a.Base.Rank + 3))
                .Select(a => $"short straight from {a.Base.Rank} {a.Base.Suit}");

        private static bool twoTerminalChows(List<ShapeGroup> chows) =>
            chows.Any(c => c.Base.Rank == 1) && chows.Any(c => c.Base.Rank == 7 && c.Base.Suit == chows.First(x => x.Base.Rank == 1).Base.Suit);

        // ------------------------------------------------------------ exclusions

        /// <summary>
        /// Implication table from the MCR scoring rules: a higher fan suppresses the lower ones
        /// it necessarily contains. Kept in one place so it can be audited against the rulebook.
        /// </summary>
        private static readonly Dictionary<string, string[]> ExclusionTable = new()
        {
            ["big-four-winds"] = new[] { "big-three-winds", "little-four-winds", "all-pungs", "round-wind-pung", "seat-wind-pung", "pung-of-terminals-or-honors" },
            ["big-three-dragons"] = new[] { "little-three-dragons", "two-dragon-pungs", "dragon-pung" },
            ["nine-gates"] = new[] { "full-flush", "concealed-hand", "edge-wait", "closed-wait", "single-wait", "pung-of-terminals-or-honors" },
            ["four-kongs"] = new[] { "single-wait", "three-kongs" },
            ["seven-shifted-pairs"] = new[] { "full-flush", "concealed-hand", "single-wait" },
            ["seven-pairs"] = new[] { "concealed-hand", "single-wait" },
            ["thirteen-orphans"] = new[] { "concealed-hand", "all-types", "single-wait" },
            ["all-terminals"] = new[] { "no-honors", "all-pungs", "outside-hand", "pung-of-terminals-or-honors" },
            ["little-four-winds"] = new[] { "big-three-winds", "pung-of-terminals-or-honors" },
            ["little-three-dragons"] = new[] { "two-dragon-pungs", "dragon-pung" },
            ["all-honors"] = new[] { "all-pungs", "outside-hand", "pung-of-terminals-or-honors" },
            ["four-concealed-pungs"] = new[] { "concealed-hand", "all-pungs", "three-concealed-pungs", "two-concealed-pungs" },
            ["four-terminal-chows"] = new[] { "full-flush", "all-chows", "seven-pairs", "pure-double-chow", "mixed-double-chow", "two-terminal-chows" },
            ["quadruple-chow"] = new[] { "pure-double-chow", "pure-triple-chow", "tile-hog", "pure-shifted-pungs" },
            ["four-pure-shifted-pungs"] = new[] { "pure-double-chow", "pure-triple-chow", "tile-hog", "pure-shifted-pungs" },
            ["four-pure-shifted-chows"] = new[] { "short-straight" },
            ["three-kongs"] = new[] { "two-concealed-kongs", "two-melded-kongs", "concealed-kong", "melded-kong", "concealed-and-melded-kong" },
            ["all-terminals-and-honors"] = new[] { "pung-of-terminals-or-honors", "outside-hand", "all-pungs" },
            ["greater-honors-knitted"] = new[] { "concealed-hand", "fully-concealed", "all-types", "single-wait", "lesser-honors-knitted" },
            ["lesser-honors-knitted"] = new[] { "concealed-hand", "fully-concealed", "all-types", "single-wait" },
            ["all-even-pungs"] = new[] { "all-pungs", "all-simples" },
            ["full-flush"] = new[] { "no-honors" },
            ["pure-triple-chow"] = new[] { "pure-shifted-pungs", "pure-double-chow" },
            ["pure-shifted-pungs"] = new[] { "pure-triple-chow" },
            ["upper-tiles"] = new[] { "no-honors" },
            ["middle-tiles"] = new[] { "no-honors" },
            ["lower-tiles"] = new[] { "no-honors" },
            ["pure-straight"] = new[] { "short-straight" },
            ["three-suited-terminal-chows"] = new[] { "all-chows", "two-terminal-chows", "mixed-double-chow", "pure-double-chow" },
            ["all-fives"] = new[] { "all-simples" },
            ["triple-pung"] = new[] { "double-pung" },
            ["three-concealed-pungs"] = new[] { "two-concealed-pungs" },
            ["knitted-straight"] = new[] { "edge-wait", "closed-wait" },
            ["upper-four"] = new[] { "no-honors" },
            ["lower-four"] = new[] { "no-honors" },
            ["reversible-tiles"] = new[] { "one-voided-suit" },
            ["mixed-triple-chow"] = new[] { "mixed-double-chow" },
            ["chicken-hand"] = new[] { "*" },
            ["last-tile-draw"] = new[] { "self-drawn" },
            ["out-with-replacement"] = new[] { "self-drawn" },
            ["robbing-kong"] = new[] { "last-tile", "last-tile-draw", "last-tile-claim" },
            ["two-concealed-kongs"] = new[] { "concealed-kong" },
            ["all-pungs"] = new[] { "all-chows" },
            ["half-flush"] = new[] { "no-honors" },
            ["melded-hand"] = new[] { "single-wait", "concealed-hand", "fully-concealed" },
            ["two-dragon-pungs"] = new[] { "dragon-pung" },
            ["fully-concealed"] = new[] { "self-drawn", "concealed-hand" },
            ["two-melded-kongs"] = new[] { "melded-kong" },
            ["concealed-and-melded-kong"] = new[] { "concealed-kong", "melded-kong" },
            ["last-tile"] = new[] { "robbing-kong" },
            ["all-chows"] = new[] { "no-honors" }
        };

        private static List<ScoredFan> ApplyExclusions(List<ScoredFan> fans)
        {
            var kept = new List<ScoredFan>();
            foreach (var fan in fans.OrderByDescending(f => f.Fan.Points))
            {
                var conflicting = kept.Any(k => Excludes(k.Fan.Id, fan.Fan.Id) || Excludes(fan.Fan.Id, k.Fan.Id));
                if (!conflicting) kept.Add(fan);
            }
            return kept;
        }

        private static bool Excludes(string first, string second)
        {
            if (!ExclusionTable.TryGetValue(first, out var list)) return false;
            return list.Contains("*") || list.Contains(second);
        }
    }
}
