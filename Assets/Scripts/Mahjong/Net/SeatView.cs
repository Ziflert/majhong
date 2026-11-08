using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>What one player may know about another: melds, flowers and discards are public.</summary>
    public sealed record PlayerView(
        int Seat,
        Wind Wind,
        int ConcealedCount,
        IReadOnlyList<Meld> Melds,
        IReadOnlyList<Tile> Flowers,
        IReadOnlyList<Tile> Discards);

    /// <summary>
    /// Who won the hand, as much of it as the projection carries: the seat, the way it was won and the
    /// two numbers of the score. The fan-by-fan breakdown is a settlement of its own and travels in the
    /// same message as the payments (docs/multiplayer-draft.md, section 4).
    /// </summary>
    public sealed record WinSummary(int Player, ClaimSource Source, int TotalPoints, int HandPoints, int? Discarder)
    {
        public static WinSummary Of(WinResult result) => new(
            result.PlayerIndex, result.Source, result.Score.TotalPoints, result.Score.HandPoints, result.DiscarderPlayer);

        public override string ToString() => $"P{Player} wins {TotalPoints} points by {Source}";
    }

    /// <summary>
    /// The table as one seat is allowed to see it. This is the only shape in which hand state leaves
    /// the server: the concealed tiles of the other three players never appear here, only their count.
    ///
    /// Everything a client needs to draw the table is present, so no client ever needs the whole
    /// <see cref="GameState"/> — that is what keeps a network game honest (see docs/multiplayer-draft.md).
    /// <see cref="Describe"/> and <see cref="Read"/> are the two directions of the wire form, and a
    /// projection that is written and read back must describe itself the same way.
    /// </summary>
    public sealed record SeatView(
        int Seat,
        Wind SeatWind,
        Wind RoundWind,
        GamePhase Phase,
        int CurrentPlayer,
        int Dealer,
        int WallRemaining,
        IReadOnlyList<Tile> Concealed,
        IReadOnlyList<Meld> Melds,
        IReadOnlyList<Tile> Flowers,
        IReadOnlyList<PlayerView> Players,
        IReadOnlyList<LegalAction> Offers,
        bool WinForbidden,
        WinResult? Result,
        bool EndedInDraw)
    {
        /// <summary>Own tiles in a stable order, so two equal states give equal text.</summary>
        public string ConcealedNotation => string.Join(",", Concealed
            .OrderBy(tile => tile.InstanceId)
            .Select(TileNotation.Format));

        /// <summary>The win as the wire carries it (no fan breakdown); null while the hand runs.</summary>
        public WinSummary? Winner => Result != null ? WinSummary.Of(Result) : null;

        /// <summary>Builds the projection for one seat out of the authoritative state.</summary>
        public static SeatView For(GameState state, int seat)
        {
            if (seat is < 0 or > 3) throw new System.ArgumentOutOfRangeException(nameof(seat));

            var players = new List<PlayerView>(4);
            for (var player = 0; player < 4; player++)
            {
                var hand = state.Players[player];
                players.Add(new PlayerView(
                    player,
                    state.SeatWindOf(player),
                    hand.Concealed.Count,
                    hand.Melds.ToList(),
                    hand.Flowers.ToList(),
                    state.Discards.Where(d => d.Player == player).Select(d => d.Tile).ToList()));
            }

            var own = state.Players[seat];
            return new SeatView(
                seat,
                state.SeatWindOf(seat),
                state.RoundWind,
                state.Phase,
                state.CurrentPlayer,
                state.Dealer,
                state.Wall.Remaining,
                own.Concealed.ToList(),
                own.Melds.ToList(),
                own.Flowers.ToList(),
                players,
                state.GetLegalActions(seat).ToList(),
                state.IsWinForbidden(seat),
                state.Result,
                state.EndedInDraw);
        }

        /// <summary>
        /// The wire form: one <c>key|fields</c> line per fact. Contains no concealed tile of another
        /// seat, which is exactly what the self-test asserts by searching the text for their identities.
        ///
        /// Every field is escaped, so the text can hold a sentence (the description of an offer) without
        /// breaking the line apart; <see cref="Read"/> undoes it.
        /// </summary>
        public string Describe()
        {
            var text = new StringBuilder();
            void Line(string key, params object[] fields)
            {
                text.Append(key);
                foreach (var field in fields) text.Append('|').Append(WireMessage.Escape(F(field)));
                text.Append('\n');
            }

            Line("table", Seat, TileNotation.Format(TileType.WindTile(SeatWind)),
                TileNotation.Format(TileType.WindTile(RoundWind)), Phase, CurrentPlayer, Dealer, WallRemaining);
            foreach (var tile in Concealed.OrderBy(tile => tile.InstanceId)) Line("hand", TileNotation.Format(tile));

            foreach (var player in Players)
            {
                Line("player", player.Seat, TileNotation.Format(TileType.WindTile(player.Wind)), player.ConcealedCount);
                foreach (var meld in player.Melds)
                    Line("meld", player.Seat, meld.Kind, TileNotation.Format(meld.Tiles),
                        meld.ClaimedFromPlayer?.ToString(CultureInfo.InvariantCulture) ?? "-",
                        meld.AddedToMeldIndex?.ToString(CultureInfo.InvariantCulture) ?? "-");
                foreach (var tile in player.Flowers) Line("flower", player.Seat, TileNotation.Format(tile));
                foreach (var tile in player.Discards) Line("discard", player.Seat, TileNotation.Format(tile));
            }

            foreach (var offer in Offers)
                Line("offer", ActionCodec.KindCode(offer.Kind), offer.Tile.HasValue ? TileNotation.Format(offer.Tile.Value) : "-",
                    offer.RequiredTiles.Count > 0 ? TileNotation.Format(offer.RequiredTiles) : "-", offer.Points, offer.Description);

            if (WinForbidden) Line("forbidden", true);
            if (EndedInDraw) Line("draw", true);
            if (Winner != null)
                Line("result", Winner.Player, Winner.Source, Winner.TotalPoints, Winner.HandPoints,
                    Winner.Discarder?.ToString(CultureInfo.InvariantCulture) ?? "-");

            return text.ToString();

            static string F(object value) => value switch
            {
                null => "-",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "-"
            };
        }

        /// <summary>
        /// Rebuilds a projection from its wire text. Lines this version does not know are skipped, so a
        /// newer server can add a fact without breaking an older client — the same rule the settings
        /// file follows.
        /// </summary>
        public static SeatView Read(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            var seat = 0;
            var seatWind = Wind.East;
            var roundWind = Wind.East;
            var phase = GamePhase.NotStarted;
            var current = 0;
            var dealer = 0;
            var wall = 0;
            var concealed = new List<Tile>();
            var players = new List<PlayerView>();
            var melds = new Dictionary<int, List<Meld>>();
            var flowers = new Dictionary<int, List<Tile>>();
            var discards = new Dictionary<int, List<Tile>>();
            var offers = new List<LegalAction>();
            var forbidden = false;
            var draw = false;
            WinSummary? winner = null;

            foreach (var raw in text.Split('\n'))
            {
                if (raw.Trim().Length == 0) continue;
                var fields = raw.Split('|').Select(WireMessage.Unescape).ToList();
                switch (fields[0])
                {
                    case "table":
                        seat = Int(fields, 1);
                        seatWind = WindAt(fields, 2);
                        roundWind = WindAt(fields, 3);
                        phase = PhaseAt(fields, 4);
                        current = Int(fields, 5);
                        dealer = Int(fields, 6);
                        wall = Int(fields, 7);
                        break;
                    case "hand":
                        if (TileNotation.TryParseTile(Field(fields, 1), out var tile)) concealed.Add(tile);
                        break;
                    case "player":
                        players.Add(new PlayerView(Int(fields, 1), WindAt(fields, 2), Int(fields, 3),
                            Array.Empty<Meld>(), Array.Empty<Tile>(), Array.Empty<Tile>()));
                        break;
                    case "meld":
                        Add(melds, Int(fields, 1), ReadMeld(fields));
                        break;
                    case "flower":
                        if (TileNotation.TryParseTile(Field(fields, 2), out var flower)) Add(flowers, Int(fields, 1), flower);
                        break;
                    case "discard":
                        if (TileNotation.TryParseTile(Field(fields, 2), out var discarded)) Add(discards, Int(fields, 1), discarded);
                        break;
                    case "offer":
                        offers.Add(ReadOffer(fields, seat));
                        break;
                    case "forbidden":
                        forbidden = true;
                        break;
                    case "draw":
                        draw = true;
                        break;
                    case "result":
                        winner = new WinSummary(Int(fields, 1), SourceAt(fields, 2), Int(fields, 3), Int(fields, 4),
                            fields.Count > 5 && fields[5] != "-" ? int.Parse(fields[5], CultureInfo.InvariantCulture) : null);
                        break;
                }
            }

            var views = players
                .Select(player => player with
                {
                    Melds = melds.TryGetValue(player.Seat, out var own) ? own : Array.Empty<Meld>(),
                    Flowers = flowers.TryGetValue(player.Seat, out var ownFlowers) ? ownFlowers : Array.Empty<Tile>(),
                    Discards = discards.TryGetValue(player.Seat, out var ownDiscards) ? ownDiscards : Array.Empty<Tile>()
                })
                .OrderBy(player => player.Seat)
                .ToList();

            var mine = views.FirstOrDefault(player => player.Seat == seat);
            var result = winner == null
                ? null
                : new WinResult(winner.Player, winner.Source,
                    new ScoreResult(true, winner.TotalPoints, winner.HandPoints, Array.Empty<ScoredFan>(),
                        HandAnalysis.Fail("The fan breakdown travels with the settlement.")),
                    winner.Discarder);

            return new SeatView(seat, seatWind, roundWind, phase, current, dealer, wall, concealed,
                mine?.Melds ?? Array.Empty<Meld>(), mine?.Flowers ?? Array.Empty<Tile>(), views, offers,
                forbidden, result, draw);

            static string Field(IReadOnlyList<string> fields, int index) => index < fields.Count ? fields[index] : string.Empty;

            static int Int(IReadOnlyList<string> fields, int index) =>
                int.TryParse(Field(fields, index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

            static Wind WindAt(IReadOnlyList<string> fields, int index) =>
                TileNotation.TryParse(Field(fields, index), out var type) && type.IsWind ? (Wind)type.Rank : Wind.East;

            static GamePhase PhaseAt(IReadOnlyList<string> fields, int index) =>
                Enum.TryParse<GamePhase>(Field(fields, index), out var value) ? value : GamePhase.NotStarted;

            static ClaimSource SourceAt(IReadOnlyList<string> fields, int index) =>
                Enum.TryParse<ClaimSource>(Field(fields, index), out var value) ? value : ClaimSource.SelfDraw;

            static void Add<T>(Dictionary<int, List<T>> map, int key, T value)
            {
                if (!map.TryGetValue(key, out var list)) map[key] = list = new List<T>();
                list.Add(value);
            }

            static Meld ReadMeld(IReadOnlyList<string> fields)
            {
                var kind = Enum.TryParse<MeldKind>(Field(fields, 2), out var value) ? value : MeldKind.Chow;
                var tiles = new List<Tile>();
                foreach (var part in Field(fields, 3).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (TileNotation.TryParseTile(part, out var tile)) tiles.Add(tile);

                int? from = Field(fields, 4) is { Length: > 0 } text && text != "-"
                    ? int.Parse(text, CultureInfo.InvariantCulture)
                    : null;
                int? added = fields.Count > 5 && Field(fields, 5) is { Length: > 0 } addedText && addedText != "-"
                    ? int.Parse(addedText, CultureInfo.InvariantCulture)
                    : null;
                return new Meld(kind, tiles, from, added);
            }

            static LegalAction ReadOffer(IReadOnlyList<string> fields, int seat)
            {
                ActionCodec.TryParseKind(Field(fields, 1), out var kind);
                Tile? tile = TileNotation.TryParseTile(Field(fields, 2), out var face) ? face : null;
                var required = new List<Tile>();
                foreach (var part in Field(fields, 3).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (TileNotation.TryParseTile(part, out var held)) required.Add(held);

                return new LegalAction(kind, seat, tile, required, Field(fields, 5), Int(fields, 4));
            }
        }

        /// <summary>True when this seat may act: the engine offers it at least one move.</summary>
        public bool HasMove => Offers.Count > 0;

        /// <summary>True when the seat has a decision that is not just "pass".</summary>
        public bool HasRealChoice => Offers.Any(offer => offer.Kind != ActionKind.Pass);

        public override string ToString() =>
            $"seat {Seat} ({SeatWind}) {Phase}, wall {WallRemaining}, {Concealed.Count} concealed";
    }
}
