using System.Collections.Generic;

namespace Mahjong.Rules
{
    /// <summary>How a finished hand is priced.</summary>
    public enum ScoringMode
    {
        /// <summary>MCR fan scoring from <c>rule.pdf</c>: 82 fans, eight points to win (see <see cref="Scoring"/>).</summary>
        Fans,

        /// <summary>Classic scoring from <c>rule-2.pdf</c>: points per set, doublings, a house limit (see <see cref="ClassicScoring"/>).</summary>
        ClassicPoints
    }

    /// <summary>
    /// The profile of the rules a table is played by: a preset, not a pair of hand-made modes
    /// (docs/rules-classic.md, "Настройки стола"). Two presets are live — the sporting one
    /// (<c>rule.pdf</c>, everything the engine did before this file existed) and classic 144
    /// (<c>rule-2.pdf</c>, the way the player plays at home).
    ///
    /// Every switch here is used by working code: a field is added only together with the code that
    /// honours it and the test that pins it down, so the profile can never claim a rule the engine
    /// does not play. Switches that are still missing (a 136-tile wall, the requesting hand, the
    /// limited hands, the classic penalties) are listed in docs/rules-classic.md as future work and
    /// are deliberately absent from this record.
    /// </summary>
    /// <param name="Id">Stable name used by saves, tools and <c>MAHJONG_VARIANT</c>: <c>sports</c>, <c>classic144</c>.</param>
    /// <param name="MinimumPoints">
    /// Points a hand needs before it may win, flowers excluded: eight in MCR (3.4.22), none in classic
    /// (rule-2.pdf lets any Mah-Jong win).
    /// </param>
    /// <param name="DeadWallTiles">
    /// Tiles at the far end of the wall that are never drawn. Classic stops a hand with fourteen tiles
    /// standing ("14 костей без учёта Свободных"); MCR plays the wall to the last tile (3.4.20).
    /// </param>
    /// <param name="FreeTiles">
    /// Tiles lying on the wall as the free tiles of the classic rules: replacements come from them
    /// first, and two new ones are put on top when both are spent. MCR has none.
    /// </param>
    /// <param name="HouseLimit">
    /// "Дом полон": a combination worth more than this is scored at this value. Zero means no limit.
    /// </param>
    /// <param name="DealerKeepsSeatOnWin">
    /// Classic keeps the dealer when he wins his own hand; MCR passes the dice on every hand (3.4.8).
    /// </param>
    public sealed record RuleVariant(
        string Id,
        string NameRu,
        string NameEn,
        ScoringMode Mode,
        int MinimumPoints,
        int DeadWallTiles,
        int FreeTiles,
        int HouseLimit,
        bool DealerKeepsSeatOnWin,
        int WallStacks = 18,
        bool IncludeFlowers = true)
    {
        /// <summary>Sporting MCR rules: the profile every earlier build played.</summary>
        public static readonly RuleVariant Sports = new(
            "sports", "Спортивный (ВОМ)", "Sporting (WMO)", ScoringMode.Fans,
            MinimumPoints: Scoring.MinimumPoints,
            DeadWallTiles: 0,
            FreeTiles: 0,
            HouseLimit: 0,
            DealerKeepsSeatOnWin: false);

        /// <summary>Classic mahjong with flowers and seasons: 144 tiles, 18 stacks, points and doublings.</summary>
        public static readonly RuleVariant Classic144 = new(
            "classic144", "Классика 144", "Classic 144", ScoringMode.ClassicPoints,
            MinimumPoints: 0,
            DeadWallTiles: 14,
            FreeTiles: 2,
            HouseLimit: 2000,
            DealerKeepsSeatOnWin: true,
            WallStacks: 18,
            IncludeFlowers: true);

        /// <summary>Classic 136-tile table without flowers and seasons.</summary>
        public static readonly RuleVariant Classic136 = new(
            "classic136", "Классика 136", "Classic 136", ScoringMode.ClassicPoints,
            MinimumPoints: 0,
            DeadWallTiles: 14,
            FreeTiles: 2,
            HouseLimit: 2000,
            DealerKeepsSeatOnWin: true,
            WallStacks: 17,
            IncludeFlowers: false);

        /// <summary>The presets as the table offers them, in the order a menu shows them.</summary>
        public static IReadOnlyList<RuleVariant> Presets => new[] { Sports, Classic144, Classic136 };

        /// <summary>Rules of a table that did not ask for any: the sporting ones.</summary>
        public static RuleVariant Default => Sports;

        /// <summary>Profile by id, or null when the text names nothing this build knows.</summary>
        public static RuleVariant? TryParse(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var text = id.Trim().ToLowerInvariant().Replace("-", string.Empty).Replace("_", string.Empty);
            return text switch
            {
                "sports" or "sport" or "mcr" or "wmo" or "wmcc" or "sporting" => Sports,
                "classic144" or "classic" or "classicpoints" or "home" => Classic144,
                "classic136" or "136" or "home136" => Classic136,
                _ => null
            };
        }

        /// <summary>Profile by id; a name this build does not know falls back to the default.</summary>
        public static RuleVariant Parse(string? id) => TryParse(id) ?? Default;

        /// <summary>True when a hand has to reach a minimum before it may win at all.</summary>
        public bool HasMinimumPoints => MinimumPoints > 0;

        /// <summary>True when the wall keeps a dead end and free tiles at the far side.</summary>
        public bool HasFreeTiles => FreeTiles > 0 || DeadWallTiles > 0;

        /// <summary>
        /// Prices one hand by the rules of this profile. The scorer is chosen here and nowhere else,
        /// so the state machine, the bots and the settlement cannot disagree about which table they
        /// are playing.
        /// </summary>
        public ScoreResult Score(ScoringContext context)
        {
            context.MinimumPoints = MinimumPoints;
            return Mode == ScoringMode.Fans
                ? Scoring.Evaluate(context)
                : ClassicScoring.Evaluate(context, HouseLimit);
        }
    }
}
