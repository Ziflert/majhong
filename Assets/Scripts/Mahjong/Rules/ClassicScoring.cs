using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>One scored element of a classic hand: a set, the pair, a bonus or a flower.</summary>
    public sealed record ClassicPart(string Id, string NameRu, string NameEn, int Points, string Evidence);

    /// <summary>One doubling: the whole sum of the combination is multiplied by two.</summary>
    public sealed record ClassicDoubling(string Id, string NameRu, string NameEn, string Evidence);

    /// <summary>
    /// What a combination is worth under <c>rule-2.pdf</c> (section V): the base sum of its sets and
    /// bonuses, the doublings it earned, and the value after them.
    /// </summary>
    public sealed record ClassicScore(
        int BasePoints,
        IReadOnlyList<ClassicPart> Parts,
        IReadOnlyList<ClassicDoubling> Doublings,
        int Value,
        bool AtLimit)
    {
        /// <summary>How many times the sum was doubled: the value is <c>BasePoints * 2^count</c>.</summary>
        public int DoublingCount => Doublings.Count;
    }

    /// <summary>One declared or decomposed set of a classic hand.</summary>
    internal sealed class ClassicGroup
    {
        public GroupKind Kind { get; init; }
        public TileType Base { get; init; }
        public bool IsKong { get; init; }
        public bool IsConcealed { get; init; }
    }

    /// <summary>A reading of a hand: its sets and its pair.</summary>
    internal sealed class ClassicShape
    {
        public List<ClassicGroup> Groups { get; } = new();
        public TileType? Pair { get; init; }
        public bool HasPair => Pair.HasValue;
    }

    /// <summary>
    /// Classic scoring from <c>rule-2.pdf</c> (docs/rules-classic.md): points per combination, the
    /// twenty points for Mah-Jong, doublings and a house limit.
    ///
    /// It is a second scorer next to <see cref="Scoring"/>, not a rewrite of it: the MCR fan table and
    /// this table answer different questions, and a hand may win under one profile and not under the
    /// other. The profile picks the scorer (<see cref="RuleVariant.Score"/>).
    ///
    /// Readings the rulebook leaves open, fixed here and in docs/rules-classic.md:
    /// a doubling multiplies the sum by two and doublings never add ("удвоение трижды" is ×8, which is
    /// what the fourth worked example of the rulebook does: 26 → 208);
    /// a pair of the seat wind that is also the round wind scores four;
    /// a hand won on a discard keeps its concealed sets concealed — only declared melds are open,
    /// which is how the second worked example reads (2 + 20 open, 4 + 20 closed).
    /// </summary>
    public static class ClassicScoring
    {
        /// <summary>Every Mah-Jong is worth twenty points before its sets are counted.</summary>
        public const int MahJongPoints = 20;

        /// <summary>Any flower or season is worth four points.</summary>
        public const int FlowerPoints = 4;

        /// <summary>The winning tile came from the wall.</summary>
        public const int FromTheWallPoints = 2;

        /// <summary>The winning tile was the only one that could finish the hand.</summary>
        public const int SingleWaitPoints = 2;

        /// <summary>
        /// Prices a finished hand. A classic win needs four sets and a pair — the rulebook knows no
        /// other shape, so the special patterns of MCR never win here.
        /// </summary>
        public static ScoreResult Evaluate(ScoringContext context, int houseLimit = 0)
        {
            var analysis = HandSolver.Analyse(context.Hand, context.WinningTile, context.WinningTileInHand);
            if (!analysis.IsComplete)
                return new ScoreResult(false, 0, 0, Array.Empty<ScoredFan>(), analysis, analysis.Error);
            if (analysis.Decompositions.Count == 0)
                return new ScoreResult(false, 0, 0, Array.Empty<ScoredFan>(), analysis,
                    "Classic mahjong is four sets and a pair; these tiles have no such split.");

            ScoreResult? best = null;
            foreach (var shape in Shapes(context.Hand, analysis))
            {
                var classic = ScoreShape(shape, context, analysis.Wait, houseLimit);
                var candidate = new ScoreResult(true, classic.Value, classic.BasePoints,
                    Array.Empty<ScoredFan>(), analysis, null, classic);
                if (best == null || candidate.TotalPoints > best.TotalPoints) best = candidate;
            }

            return best!;
        }

        /// <summary>
        /// What the combination of any seat is worth, winner or not. The settlement compares the three
        /// losers by exactly this number, so every seat needs a value: the rulebook counts flowers and
        /// their doublings for all four players (section VIII).
        /// </summary>
        public static ClassicScore ValueOf(PlayerHand hand, Wind seatWind, Wind roundWind, int houseLimit = 0)
        {
            var fixedGroups = new List<ClassicGroup>();
            foreach (var meld in hand.Melds)
                fixedGroups.Add(new ClassicGroup
                {
                    Kind = meld.Kind == MeldKind.Chow ? GroupKind.Chow : GroupKind.Pung,
                    Base = meld.BaseType,
                    IsKong = meld.IsKong,
                    IsConcealed = !meld.IsOpen
                });

            var faces = new List<TileType>();
            var counts = new List<int>();
            var indexOf = new Dictionary<TileType, int>();
            foreach (var type in hand.Concealed.Select(tile => tile.Type).OrderBy(type => type, FaceOrder.Instance))
            {
                if (!indexOf.TryGetValue(type, out var index))
                {
                    index = faces.Count;
                    indexOf[type] = index;
                    faces.Add(type);
                    counts.Add(0);
                }
                counts[index]++;
            }

            var best = -1;
            var bestGroups = new List<ClassicGroup>();
            TileType? bestPair = null;
            var working = new List<ClassicGroup>();
            LooseSearch(faces, counts, indexOf, 0, working, null, seatWind, roundWind,
                ref best, ref bestGroups, ref bestPair);

            // The declared melds are worth their points too: a loser who claimed a kong before the
            // hand ran out settles by the whole combination, not by the tiles left in his hand.
            var shape = new ClassicShape { Pair = bestPair };
            shape.Groups.AddRange(fixedGroups);
            shape.Groups.AddRange(bestGroups);

            var parts = new List<ClassicPart>();
            var points = 0;
            foreach (var group in shape.Groups) AddGroup(parts, ref points, group);
            AddPair(parts, ref points, shape.Pair, seatWind, roundWind);
            AddFlowers(parts, ref points, hand);

            var doublings = new List<ClassicDoubling>();
            ShapeDoublings(shape, seatWind, roundWind, 0, null, winner: false, doublings);
            FlowerDoublings(hand, seatWind, doublings);

            return Build(points, parts, doublings, houseLimit);
        }

        /// <summary>
        /// Walks the concealed tiles into sets, one optional pair and a few unused tiles, keeping the
        /// reading that is worth the most. Thirteen or fourteen tiles make the search trivial, and it
        /// runs once per seat at the end of a hand.
        /// </summary>
        private static void LooseSearch(List<TileType> faces, List<int> counts, Dictionary<TileType, int> indexOf,
            int start, List<ClassicGroup> groups, TileType? pair, Wind seatWind, Wind roundWind,
            ref int best, ref List<ClassicGroup> bestGroups, ref TileType? bestPair)
        {
            var index = start;
            while (index < faces.Count && counts[index] == 0) index++;

            if (index >= faces.Count)
            {
                var value = PairPoints(pair, seatWind, roundWind);
                foreach (var group in groups) value += SetPoints(group);
                if (value > best)
                {
                    best = value;
                    bestGroups = new List<ClassicGroup>(groups);
                    bestPair = pair;
                }
                return;
            }

            var face = faces[index];

            if (counts[index] >= 3)
            {
                counts[index] -= 3;
                groups.Add(new ClassicGroup { Kind = GroupKind.Pung, Base = face, IsConcealed = true });
                LooseSearch(faces, counts, indexOf, index, groups, pair, seatWind, roundWind,
                    ref best, ref bestGroups, ref bestPair);
                groups.RemoveAt(groups.Count - 1);
                counts[index] += 3;
            }

            if (face.IsNumbered && face.Rank <= 7)
            {
                var second = new TileType(face.Suit, face.Rank + 1);
                var third = new TileType(face.Suit, face.Rank + 2);
                if (indexOf.TryGetValue(second, out var secondIndex) && indexOf.TryGetValue(third, out var thirdIndex)
                    && counts[secondIndex] > 0 && counts[thirdIndex] > 0)
                {
                    counts[index]--;
                    counts[secondIndex]--;
                    counts[thirdIndex]--;
                    groups.Add(new ClassicGroup { Kind = GroupKind.Chow, Base = face, IsConcealed = true });
                    LooseSearch(faces, counts, indexOf, index, groups, pair, seatWind, roundWind,
                        ref best, ref bestGroups, ref bestPair);
                    groups.RemoveAt(groups.Count - 1);
                    counts[index]++;
                    counts[secondIndex]++;
                    counts[thirdIndex]++;
                }
            }

            if (pair == null && counts[index] >= 2)
            {
                counts[index] -= 2;
                LooseSearch(faces, counts, indexOf, index, groups, face, seatWind, roundWind,
                    ref best, ref bestGroups, ref bestPair);
                counts[index] += 2;
            }

            counts[index]--;
            LooseSearch(faces, counts, indexOf, index, groups, pair, seatWind, roundWind,
                ref best, ref bestGroups, ref bestPair);
            counts[index]++;
        }

        private static ClassicScore ScoreShape(ClassicShape shape, ScoringContext context, WaitType wait, int houseLimit)
        {
            var parts = new List<ClassicPart>();
            var points = 0;
            foreach (var group in shape.Groups) AddGroup(parts, ref points, group);
            AddPair(parts, ref points, shape.Pair, context.SeatWind, context.RoundWind);

            var withoutBonus = points;
            parts.Add(new ClassicPart("mahjong", "Ма-Джонг", "Mah-Jong", MahJongPoints, "four sets and a pair"));
            points += MahJongPoints;

            if (context.Source != ClaimSource.Discard)
            {
                parts.Add(new ClassicPart("from-the-wall", "Кость со стены", "Tile from the wall",
                    FromTheWallPoints, "the winning tile was drawn"));
                points += FromTheWallPoints;
            }

            if (wait != WaitType.None)
            {
                parts.Add(new ClassicPart("single-wait", "Единственное ожидание", "Single wait",
                    SingleWaitPoints, $"the hand waited on one tile only ({wait})"));
                points += SingleWaitPoints;
            }

            AddFlowers(parts, ref points, context.Hand);

            var doublings = new List<ClassicDoubling>();
            ShapeDoublings(shape, context.SeatWind, context.RoundWind, withoutBonus, context, winner: true, doublings);
            FlowerDoublings(context.Hand, context.SeatWind, doublings);

            return Build(points, parts, doublings, houseLimit);
        }

        // ------------------------------------------------------------ set values

        /// <summary>
        /// The table of section V: a chow is worth nothing, a simple set is worth half a terminal or
        /// honour set, a kong is worth four pungs, and a concealed set is worth double an open one.
        /// </summary>
        internal static int SetPoints(ClassicGroup group)
        {
            if (group.Kind == GroupKind.Chow) return 0;
            var unit = group.Base.IsNumbered && group.Base.Rank is >= 2 and <= 8 ? 2 : 4;
            return unit * (group.IsConcealed ? 2 : 1) * (group.IsKong ? 4 : 1);
        }

        /// <summary>
        /// A pair of dragons is worth two, and so is a pair of the seat wind and a pair of the round
        /// wind. A wind that is both scores both rows.
        /// </summary>
        public static int PairPoints(TileType? pair, Wind seatWind, Wind roundWind)
        {
            if (!pair.HasValue) return 0;
            var type = pair.Value;
            if (type.IsDragon) return 2;
            if (!type.IsWind) return 0;
            var wind = (Wind)type.Rank;
            var points = 0;
            if (wind == seatWind) points += 2;
            if (wind == roundWind) points += 2;
            return points;
        }

        private static void AddGroup(List<ClassicPart> parts, ref int points, ClassicGroup group)
        {
            var value = SetPoints(group);
            points += value;
            if (value == 0) return;

            var (id, ru, en) = GroupNames(group);
            parts.Add(new ClassicPart(id, ru, en, value, group.Base.ToString()));
        }

        private static (string Id, string Ru, string En) GroupNames(ClassicGroup group)
        {
            var kong = group.IsKong;
            if (group.Base.IsDragon)
                return kong ? ("kong-dragons", "Четвёрка Драконов", "Kong of dragons")
                            : ("pung-dragons", "Тройка Драконов", "Pung of dragons");
            if (group.Base.IsWind)
                return kong ? ("kong-winds", "Четвёрка Ветров", "Kong of winds")
                            : ("pung-winds", "Тройка Ветров", "Pung of winds");
            if (group.Base.IsNumbered && group.Base.Rank is 1 or 9)
                return kong ? ("kong-terminals", "Четвёрка Единиц и Девяток", "Kong of terminals")
                            : ("pung-terminals", "Тройка Единиц и Девяток", "Pung of terminals");
            return kong ? ("kong-simples", "Четвёрка в мастях", "Kong of simples")
                        : ("pung-simples", "Тройка в мастях", "Pung of simples");
        }

        private static void AddPair(List<ClassicPart> parts, ref int points, TileType? pair, Wind seatWind, Wind roundWind)
        {
            var value = PairPoints(pair, seatWind, roundWind);
            points += value;
            if (value == 0 || !pair.HasValue) return;

            var id = pair.Value.IsDragon ? "pair-dragons" : "pair-winds";
            var ru = pair.Value.IsDragon ? "Пара Драконов" : "Пара Ветров";
            var en = pair.Value.IsDragon ? "Pair of dragons" : "Pair of winds";
            parts.Add(new ClassicPart(id, ru, en, value, pair.Value.ToString()));
        }

        private static void AddFlowers(List<ClassicPart> parts, ref int points, PlayerHand hand)
        {
            foreach (var flower in hand.Flowers)
            {
                parts.Add(new ClassicPart("flower", "Цветок или сезон", "Flower or season",
                    FlowerPoints, flower.Type.ToString()));
                points += FlowerPoints;
            }
        }

        // ------------------------------------------------------------- doublings

        private static void ShapeDoublings(ClassicShape shape, Wind seatWind, Wind roundWind, int withoutBonus,
            ScoringContext? context, bool winner, List<ClassicDoubling> into)
        {
            var pungs = shape.Groups.Where(g => g.Kind == GroupKind.Pung).ToList();
            var all = shape.Groups.Select(g => g.Base).ToList();
            if (shape.Pair.HasValue) all.Add(shape.Pair.Value);
            var numberedSuits = all.Where(t => t.IsNumbered).Select(t => t.Suit).Distinct().ToList();
            var hasHonors = all.Any(t => t.IsHonor);

            foreach (var dragon in pungs.Where(g => g.Base.IsDragon))
                into.Add(new ClassicDoubling("dragons", "Драконы", "Dragons", dragon.Base.ToString()));

            foreach (var wind in pungs.Where(g => g.Base.IsWind && (Wind)g.Base.Rank == seatWind))
                into.Add(new ClassicDoubling("own-wind", "Свой ветер", "Own wind", wind.Base.ToString()));

            foreach (var wind in pungs.Where(g => g.Base.IsWind && (Wind)g.Base.Rank == roundWind))
                into.Add(new ClassicDoubling("round-wind", "Ветер раунда", "Round wind", wind.Base.ToString()));

            if (numberedSuits.Count == 1)
            {
                if (!hasHonors)
                    for (var i = 0; i < 3; i++)
                        into.Add(new ClassicDoubling("pure-suit-thrice", "Чистая масть трижды", "Pure suit, thrice",
                            $"only {numberedSuits[0]}"));
                else
                    into.Add(new ClassicDoubling("pure-suit", "Чистая масть", "Pure suit",
                        $"only {numberedSuits[0]} plus honours"));
            }

            if (all.Count > 0 && all.All(t => t.IsTerminalOrHonor))
                into.Add(new ClassicDoubling("terminals-and-honors", "Только единицы, девятки и ветры с драконами",
                    "Only terminals and honours", "no simple tiles"));

            if (all.Count > 0 && all.All(t => t.IsHonor))
                for (var i = 0; i < 3; i++)
                    into.Add(new ClassicDoubling("honors-only-thrice", "Только ветры и драконы трижды",
                        "Only winds and dragons, thrice", "no suited tiles"));

            if (winner && pungs.Count == shape.Groups.Count && shape.Groups.Count > 0)
                into.Add(new ClassicDoubling("all-pungs", "Ни одной последовательности", "No chows",
                    "every set is a pung or a kong"));

            if (winner && withoutBonus == 0)
                into.Add(new ClassicDoubling("no-points", "Нет очков (мизер)", "No points",
                    "the sets and the pair are worth nothing"));

            if (!winner || context == null) return;

            if (context.Source is ClaimSource.KongReplacement or ClaimSource.FlowerReplacement)
                into.Add(new ClassicDoubling("free-tile", "Комбинация завершена Свободной костью",
                    "Finished with a free tile", "the winning tile came from the far end of the wall"));

            if (context.LastTileOfWall)
                into.Add(new ClassicDoubling("last-tile", "Последняя допустимая кость", "Last possible tile",
                    "the winning tile was the last one the wall could give"));

            if (context.RobbedKong)
                into.Add(new ClassicDoubling("robbing-kong", "Ограбление открытого конга", "Robbing the kong",
                    "the winning tile was taken from a declared kong"));

            var windSets = pungs.Count(g => g.Base.IsWind);
            if (windSets >= 3 && shape.Pair.HasValue && shape.Pair.Value.IsWind)
                into.Add(new ClassicDoubling("three-winds", "Три группы Ветров и пара Ветров",
                    "Three wind sets and a wind pair", $"{windSets} wind sets"));

            var dragonSets = pungs.Count(g => g.Base.IsDragon);
            if (dragonSets >= 2 && shape.Pair.HasValue && shape.Pair.Value.IsDragon)
                into.Add(new ClassicDoubling("two-dragons", "Две группы Драконов и пара Драконов",
                    "Two dragon sets and a dragon pair", $"{dragonSets} dragon sets"));
        }

        /// <summary>
        /// Section VIII: any flower is four points, the flower of your own seat doubles the sum, and a
        /// full bouquet doubles it twice. Flowers count whether the hand won or not, so this runs for
        /// every seat.
        /// </summary>
        private static void FlowerDoublings(PlayerHand hand, Wind seatWind, List<ClassicDoubling> into)
        {
            var kinds = hand.Flowers.Select(flower => (FlowerKind)flower.Type.Rank).ToList();
            if (kinds.Count == 0) return;

            var (season, flower) = OwnBonuses(seatWind);
            var own = kinds.Contains(season) ? season : kinds.Contains(flower) ? flower : (FlowerKind?)null;
            if (own.HasValue)
                into.Add(new ClassicDoubling("own-flower", "Свой цветок или сезон", "Own flower or season",
                    own.Value.ToString()));

            if (kinds.Contains(FlowerKind.Spring) && kinds.Contains(FlowerKind.Summer)
                && kinds.Contains(FlowerKind.Autumn) && kinds.Contains(FlowerKind.Winter))
                for (var i = 0; i < 2; i++)
                    into.Add(new ClassicDoubling("bouquet-seasons", "Полный букет сезонов", "Full bouquet of seasons",
                        "all four seasons"));

            if (kinds.Contains(FlowerKind.Plum) && kinds.Contains(FlowerKind.Orchid)
                && kinds.Contains(FlowerKind.Bamboo) && kinds.Contains(FlowerKind.Chrysanthemum))
                for (var i = 0; i < 2; i++)
                    into.Add(new ClassicDoubling("bouquet-flowers", "Полный букет цветов", "Full bouquet of flowers",
                        "all four flowers"));
        }

        /// <summary>Which season and which flower belong to a seat (section VIII).</summary>
        public static (FlowerKind Season, FlowerKind Flower) OwnBonuses(Wind wind) => wind switch
        {
            Wind.East => (FlowerKind.Spring, FlowerKind.Plum),
            Wind.South => (FlowerKind.Summer, FlowerKind.Orchid),
            Wind.West => (FlowerKind.Autumn, FlowerKind.Chrysanthemum),
            _ => (FlowerKind.Winter, FlowerKind.Bamboo)
        };

        private static ClassicScore Build(int basePoints, List<ClassicPart> parts,
            List<ClassicDoubling> doublings, int houseLimit)
        {
            var value = basePoints;
            foreach (var _ in doublings)
                value = value > int.MaxValue / 2 ? int.MaxValue : value * 2;

            var atLimit = houseLimit > 0 && value > houseLimit;
            if (atLimit) value = houseLimit;

            return new ClassicScore(basePoints, parts, doublings, value, atLimit);
        }

        private static IEnumerable<ClassicShape> Shapes(PlayerHand hand, HandAnalysis analysis)
        {
            foreach (var decomposition in analysis.Decompositions)
            {
                var shape = new ClassicShape { Pair = decomposition.Pair };
                foreach (var meld in hand.Melds)
                    shape.Groups.Add(new ClassicGroup
                    {
                        Kind = meld.Kind == MeldKind.Chow ? GroupKind.Chow : GroupKind.Pung,
                        Base = meld.BaseType,
                        IsKong = meld.IsKong,
                        IsConcealed = !meld.IsOpen
                    });
                foreach (var group in decomposition.Groups)
                    shape.Groups.Add(new ClassicGroup { Kind = group.Kind, Base = group.Base, IsConcealed = true });
                yield return shape;
            }
        }

        /// <summary>Suits, then winds, then dragons — the order the search walks the distinct faces in.</summary>
        private sealed class FaceOrder : IComparer<TileType>
        {
            public static readonly FaceOrder Instance = new();

            public int Compare(TileType left, TileType right)
            {
                if (left.Suit != right.Suit) return left.Suit.CompareTo(right.Suit);
                return left.Rank.CompareTo(right.Rank);
            }
        }
    }
}
