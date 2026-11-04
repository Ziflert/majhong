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
    /// The table as one seat is allowed to see it. This is the only shape in which hand state leaves
    /// the server: the concealed tiles of the other three players never appear here, only their count.
    ///
    /// Everything a client needs to draw the table is present, so no client ever needs the whole
    /// <see cref="GameState"/> — that is what keeps a network game honest (see docs/multiplayer-draft.md).
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
        /// </summary>
        public string Describe()
        {
            var text = new StringBuilder();
            void Line(string key, params object[] fields) =>
                text.Append(key).Append('|').AppendJoin('|', fields.Select(F)
                ).Append('\n');

            Line("seat", Seat);
            Line("wind", TileNotation.Format(TileType.WindTile(SeatWind)));
            Line("round", TileNotation.Format(TileType.WindTile(RoundWind)));
            Line("phase", Phase);
            Line("turn", CurrentPlayer);
            Line("dealer", Dealer);
            Line("wall", WallRemaining);
            foreach (var tile in Concealed.OrderBy(tile => tile.InstanceId)) Line("hand", TileNotation.Format(tile));
            foreach (var meld in Melds) Line("meld", meld.Kind, TileNotation.Format(meld.Tiles), meld.ClaimedFromPlayer?.ToString() ?? "-");
            foreach (var tile in Flowers) Line("flower", TileNotation.Format(tile));

            foreach (var player in Players)
            {
                Line("player", player.Seat, player.ConcealedCount, player.Melds.Count, player.Flowers.Count,
                    string.Join(" ", player.Melds.Select(meld => meld.Kind)));
                foreach (var tile in player.Discards) Line("discard", player.Seat, TileNotation.Format(tile));
            }

            foreach (var offer in Offers) Line("offer", ActionCodec.Encode(offer), offer.Kind, offer.Points);
            if (WinForbidden) Line("forbidden", WinForbidden);
            if (EndedInDraw) Line("draw", EndedInDraw);
            if (Result != null)
                Line("result", Result.PlayerIndex, Result.Source, Result.Score.TotalPoints, Result.DiscarderPlayer?.ToString() ?? "-");

            return text.ToString();

            static string F(object value) => value switch
            {
                null => "-",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "-"
            };
        }

        /// <summary>True when this seat may act: the engine offers it at least one move.</summary>
        public bool HasMove => Offers.Count > 0;

        public override string ToString() =>
            $"seat {Seat} ({SeatWind}) {Phase}, wall {WallRemaining}, {Concealed.Count} concealed";
    }
}
