using System.Collections.Generic;

namespace Mahjong.Rules
{
    /// <summary>A scoring element ("fan"). Points follow section 3.8 of the WMCC rules.</summary>
    public sealed record Fan(string Id, string NameRu, string NameEn, int Points);

    public sealed record ScoredFan(Fan Fan, string Evidence);

    /// <summary>The complete fan catalogue from section 3.8.1.</summary>
    public static class Fans
    {
        public static readonly Fan BigFourWinds = new("big-four-winds", "Большие четыре ветра", "Big Four Winds", 88);
        public static readonly Fan BigThreeDragons = new("big-three-dragons", "Большие три дракона", "Big Three Dragons", 88);
        public static readonly Fan AllGreen = new("all-green", "Все зеленые", "All Green", 88);
        public static readonly Fan NineGates = new("nine-gates", "Девять врат", "Nine Gates", 88);
        public static readonly Fan FourKongs = new("four-kongs", "Четыре конга", "Four Kongs", 88);
        public static readonly Fan SevenShiftedPairs = new("seven-shifted-pairs", "Семь смещённых пар", "Seven Shifted Pairs", 88);
        public static readonly Fan ThirteenOrphans = new("thirteen-orphans", "Тринадцать сирот", "Thirteen Orphans", 88);

        public static readonly Fan AllTerminals = new("all-terminals", "Все терминальные", "All Terminals", 64);
        public static readonly Fan LittleFourWinds = new("little-four-winds", "Малые четыре ветра", "Little Four Winds", 64);
        public static readonly Fan LittleThreeDragons = new("little-three-dragons", "Малые три дракона", "Little Three Dragons", 64);
        public static readonly Fan AllHonors = new("all-honors", "Все благородные", "All Honors", 64);
        public static readonly Fan FourConcealedPungs = new("four-concealed-pungs", "Четыре закрытых панга", "Four Concealed Pungs", 64);

        public static readonly Fan FourTerminalChows = new("four-terminal-chows", "Чистые терминальные чоу", "Four Terminal Chows", 48);
        public static readonly Fan QuadrupleChow = new("quadruple-chow", "Четверное чоу", "Quadruple Chow", 48);
        public static readonly Fan FourPureShiftedPungs = new("four-pure-shifted-pungs", "Четыре чистых смещённых панга", "Four Pure Shifted Pungs", 48);

        public static readonly Fan FourPureShiftedChows = new("four-pure-shifted-chows", "Четыре чистых смещённых чоу", "Four Pure Shifted Chows", 32);
        public static readonly Fan ThreeKongs = new("three-kongs", "Три конга", "Three Kongs", 32);
        public static readonly Fan AllTerminalsAndHonors = new("all-terminals-and-honors", "Все терминальные и благородные", "All Terminals and Honors", 32);

        public static readonly Fan SevenPairs = new("seven-pairs", "Семь пар", "Seven Pairs", 24);
        public static readonly Fan GreaterHonorsKnitted = new("greater-honors-knitted", "Большие благородные и переплетённые", "Greater Honors and Knitted Tiles", 24);
        public static readonly Fan AllEvenPungs = new("all-even-pungs", "Все чётные панги", "All Even Pungs", 24);
        public static readonly Fan FullFlush = new("full-flush", "Полное изобилие", "Full Flush", 24);
        public static readonly Fan PureTripleChow = new("pure-triple-chow", "Чистое тройное чоу", "Pure Triple Chow", 24);
        public static readonly Fan PureShiftedPungs = new("pure-shifted-pungs", "Чистые смещённые панги", "Pure Shifted Pungs", 24);
        public static readonly Fan UpperTiles = new("upper-tiles", "Верхние кости", "Upper Tiles", 24);
        public static readonly Fan MiddleTiles = new("middle-tiles", "Средние кости", "Middle Tiles", 24);
        public static readonly Fan LowerTiles = new("lower-tiles", "Нижние кости", "Lower Tiles", 24);

        public static readonly Fan PureStraight = new("pure-straight", "Чистый ряд", "Pure Straight", 16);
        public static readonly Fan ThreeSuitedTerminalChows = new("three-suited-terminal-chows", "Три масти и терминальные чоу", "Three-Suited Terminal Chows", 16);
        public static readonly Fan PureShiftedChows = new("pure-shifted-chows", "Чистые смещённые чоу", "Pure Shifted Chows", 16);
        public static readonly Fan AllFives = new("all-fives", "Все пятёрки", "All Fives", 16);
        public static readonly Fan TriplePung = new("triple-pung", "Тройной панг", "Triple Pung", 16);
        public static readonly Fan ThreeConcealedPungs = new("three-concealed-pungs", "Три закрытых панга", "Three Concealed Pungs", 16);

        public static readonly Fan LesserHonorsKnitted = new("lesser-honors-knitted", "Малые благородные и переплетённые", "Lesser Honors and Knitted Tiles", 12);
        public static readonly Fan KnittedStraight = new("knitted-straight", "Переплетённый ряд", "Knitted Straight", 12);
        public static readonly Fan UpperFour = new("upper-four", "Четыре верхних", "Upper Four", 12);
        public static readonly Fan LowerFour = new("lower-four", "Четыре нижних", "Lower Four", 12);
        public static readonly Fan BigThreeWinds = new("big-three-winds", "Большие три ветра", "Big Three Winds", 12);

        public static readonly Fan MixedStraight = new("mixed-straight", "Смешанный ряд", "Mixed Straight", 8);
        public static readonly Fan ReversibleTiles = new("reversible-tiles", "Симметричные кости", "Reversible Tiles", 8);
        public static readonly Fan MixedTripleChow = new("mixed-triple-chow", "Смешанное тройное чоу", "Mixed Triple Chow", 8);
        public static readonly Fan MixedShiftedPungs = new("mixed-shifted-pungs", "Смешанные смещённые панги", "Mixed Shifted Pungs", 8);
        public static readonly Fan ChickenHand = new("chicken-hand", "Цыплячья рука", "Chicken Hand", 8);
        public static readonly Fan LastTileDraw = new("last-tile-draw", "Последняя кость со стены", "Last Tile Draw", 8);
        public static readonly Fan LastTileClaim = new("last-tile-claim", "Последний снос", "Last Tile Claim", 8);
        public static readonly Fan OutWithReplacement = new("out-with-replacement", "Выигрыш замещающей костью", "Out with Replacement Tile", 8);
        public static readonly Fan RobbingKong = new("robbing-kong", "Ограбление конга", "Robbing the Kong", 8);
        public static readonly Fan TwoConcealedKongs = new("two-concealed-kongs", "Два закрытых конга", "Two Concealed Kongs", 8);

        public static readonly Fan AllPungs = new("all-pungs", "Все панги", "All Pungs", 6);
        public static readonly Fan HalfFlush = new("half-flush", "Пол-изобилия", "Half Flush", 6);
        public static readonly Fan MixedShiftedChows = new("mixed-shifted-chows", "Смешанные смещённые чоу", "Mixed Shifted Chows", 6);
        public static readonly Fan AllTypes = new("all-types", "Все типы", "All Types", 6);
        public static readonly Fan MeldedHand = new("melded-hand", "Открытая рука", "Melded Hand", 6);
        public static readonly Fan TwoDragonPungs = new("two-dragon-pungs", "Два панга драконов", "Two Dragon Pungs", 6);

        public static readonly Fan OutsideHand = new("outside-hand", "Внешняя рука", "Outside Hand", 4);
        public static readonly Fan FullyConcealed = new("fully-concealed", "Полностью закрытая рука", "Fully Concealed Hand", 4);
        public static readonly Fan TwoMeldedKongs = new("two-melded-kongs", "Два открытых конга", "Two Melded Kongs", 4);
        public static readonly Fan LastTile = new("last-tile", "Последняя кость", "Last Tile", 4);

        public static readonly Fan DragonPung = new("dragon-pung", "Панг драконов", "Dragon Pung", 2);
        public static readonly Fan RoundWindPung = new("round-wind-pung", "Преимущественный ветер", "Round Wind Pung", 2);
        public static readonly Fan SeatWindPung = new("seat-wind-pung", "Ветер места", "Seat Wind Pung", 2);
        public static readonly Fan ConcealedHand = new("concealed-hand", "Закрытая рука", "Concealed Hand", 2);
        public static readonly Fan AllChows = new("all-chows", "Все чоу", "All Chows", 2);
        public static readonly Fan TileHog = new("tile-hog", "Четыре врозь", "Tile Hog", 2);
        public static readonly Fan DoublePung = new("double-pung", "Двойной панг", "Double Pung", 2);
        public static readonly Fan TwoConcealedPungs = new("two-concealed-pungs", "Два закрытых панга", "Two Concealed Pungs", 2);
        public static readonly Fan ConcealedKong = new("concealed-kong", "Закрытый конг", "Concealed Kong", 2);
        public static readonly Fan AllSimples = new("all-simples", "Все простые", "All Simples", 2);

        public static readonly Fan PureDoubleChow = new("pure-double-chow", "Чистое двойное чоу", "Pure Double Chow", 1);
        public static readonly Fan MixedDoubleChow = new("mixed-double-chow", "Смешанное двойное чоу", "Mixed Double Chow", 1);
        public static readonly Fan ShortStraight = new("short-straight", "Короткий ряд", "Short Straight", 1);
        public static readonly Fan TwoTerminalChows = new("two-terminal-chows", "Два терминальных чоу", "Two Terminal Chows", 1);
        public static readonly Fan PungOfTerminalsOrHonors = new("pung-of-terminals-or-honors", "Панг терминальных или благородных", "Pung of Terminals or Honors", 1);
        public static readonly Fan MeldedKong = new("melded-kong", "Открытый конг", "Melded Kong", 1);
        public static readonly Fan OneVoidedSuit = new("one-voided-suit", "Пропущенная масть", "One Voided Suit", 1);
        public static readonly Fan NoHonors = new("no-honors", "Без благородных", "No Honors", 1);
        public static readonly Fan EdgeWait = new("edge-wait", "Крайнее ожидание", "Edge Wait", 1);
        public static readonly Fan ClosedWait = new("closed-wait", "Закрытое ожидание", "Closed Wait", 1);
        public static readonly Fan SingleWait = new("single-wait", "Ожидание единственной", "Single Wait", 1);
        public static readonly Fan SelfDrawn = new("self-drawn", "Выигрыш со стены", "Self-Drawn", 1);
        public static readonly Fan Flower = new("flower", "Цветок", "Flower Tile", 1);

        public static IReadOnlyList<Fan> All { get; } = new[]
        {
            BigFourWinds, BigThreeDragons, AllGreen, NineGates, FourKongs, SevenShiftedPairs, ThirteenOrphans,
            AllTerminals, LittleFourWinds, LittleThreeDragons, AllHonors, FourConcealedPungs,
            FourTerminalChows, QuadrupleChow, FourPureShiftedPungs,
            FourPureShiftedChows, ThreeKongs, AllTerminalsAndHonors,
            SevenPairs, GreaterHonorsKnitted, AllEvenPungs, FullFlush, PureTripleChow, PureShiftedPungs,
            UpperTiles, MiddleTiles, LowerTiles,
            PureStraight, ThreeSuitedTerminalChows, PureShiftedChows, AllFives, TriplePung, ThreeConcealedPungs,
            LesserHonorsKnitted, KnittedStraight, UpperFour, LowerFour, BigThreeWinds,
            MixedStraight, ReversibleTiles, MixedTripleChow, MixedShiftedPungs, ChickenHand, LastTileDraw,
            LastTileClaim, OutWithReplacement, RobbingKong, TwoConcealedKongs,
            AllPungs, HalfFlush, MixedShiftedChows, AllTypes, MeldedHand, TwoDragonPungs,
            OutsideHand, FullyConcealed, TwoMeldedKongs, LastTile,
            DragonPung, RoundWindPung, SeatWindPung, ConcealedHand, AllChows, TileHog, DoublePung,
            TwoConcealedPungs, ConcealedKong, AllSimples,
            PureDoubleChow, MixedDoubleChow, ShortStraight, TwoTerminalChows, PungOfTerminalsOrHonors,
            MeldedKong, OneVoidedSuit, NoHonors, EdgeWait, ClosedWait, SingleWait, SelfDrawn, Flower
        };

        public static Fan ById(string id)
        {
            foreach (var fan in All)
                if (fan.Id == id) return fan;
            throw new KeyNotFoundException($"Unknown fan id: {id}");
        }
    }
}
