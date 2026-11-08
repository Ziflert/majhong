using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mahjong.Controls;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>
    /// One player's end of the table. It draws from nothing but what the server sent: the projection of
    /// its own seat, the events for the animation and the window it has left. It never holds a
    /// <see cref="GameState"/>, which is the whole point — the client cannot know a tile it was not
    /// shown, so it cannot invent a move the server would accept.
    ///
    /// Moves leave as <c>act</c> messages with a request id; the answer comes back either
    /// <c>accepted</c> (with the position it was played at) or <c>rejected</c> (with the reason).
    /// </summary>
    public sealed class TableClient
    {
        private readonly ITableLink link;
        private readonly List<TableEvent> events = new();
        private readonly List<string> notices = new();
        private readonly List<string> rejects = new();
        private readonly List<Settlement> settlements = new();
        private int nextRequest = 1;

        public TableClient(ITableLink link, string name = "player")
        {
            this.link = link ?? throw new ArgumentNullException(nameof(link));
            Name = name ?? "player";
            link.Send(WireMessage.Hello(Name).Encode());
        }

        public string Name { get; }

        /// <summary>The seat the server gave this client; minus one until the welcome arrives.</summary>
        public int Seat { get; private set; } = -1;

        public Wind Wind { get; private set; } = Wind.East;
        public int Dealer { get; private set; }
        public int? Seed { get; private set; }
        public string? RulesId { get; private set; }

        /// <summary>The last projection of this seat. Null until the server introduces the table.</summary>
        public SeatView? View { get; private set; }

        /// <summary>The position the last projection belongs to; it only grows.</summary>
        public int MoveIndex { get; private set; }

        /// <summary>How many events this client knows about — the number it sends back on a resume.</summary>
        public int EventsSeen { get; private set; }

        public IReadOnlyList<TableEvent> Events => events;
        public IReadOnlyList<string> Notices => notices;

        /// <summary>Settlements received from the authoritative table, in hand order.</summary>
        public IReadOnlyList<Settlement> Settlements => settlements;

        /// <summary>Why the last move was refused; empty when the last one was played.</summary>
        public string LastReject => rejects.Count > 0 ? rejects[^1] : string.Empty;

        public IReadOnlyList<string> Rejects => rejects;

        public int Accepted { get; private set; }
        public int Refused { get; private set; }

        /// <summary>True when the server refused the seat itself (a full table).</summary>
        public string Refusal { get; private set; } = string.Empty;

        /// <summary>Which window this seat had in the last clock message; idle when it is not on the clock.</summary>
        public ClockWindow ClockWindow { get; private set; } = ClockWindow.Idle;

        /// <summary>What was left of that window when the server said so; zero when unlimited or idle.</summary>
        public TimeSpan ClockLeft { get; private set; }

        public bool OnClock => ClockWindow != ClockWindow.Idle;

        public bool IsOpen => link.IsOpen;

        public bool IsMyTurn => View != null && View.CurrentPlayer == Seat
                                && View.Phase is GamePhase.AwaitingDraw or GamePhase.AwaitingDiscard;

        /// <summary>The moves the server offers this seat right now.</summary>
        public IReadOnlyList<LegalAction> Offers => View?.Offers ?? Array.Empty<LegalAction>();

        /// <summary>The offers that are more than a pass, in the order the server gave them.</summary>
        public IReadOnlyList<LegalAction> Choices => Offers.Where(offer => offer.Kind != ActionKind.Pass).ToList();

        public bool IsHandOver => View?.Phase == GamePhase.HandFinished;

        public WinSummary? Winner => View?.Winner;

        /// <summary>Reads everything the server has sent since the last call; returns how many messages it was.</summary>
        public int Poll()
        {
            var count = 0;
            foreach (var payload in link.Receive())
            {
                Apply(payload);
                count++;
            }

            return count;
        }

        /// <summary>States a move: the wire line, the same one the log and the replay use.</summary>
        public int Act(string wire)
        {
            if (string.IsNullOrWhiteSpace(wire)) throw new ArgumentException("A move needs a wire line.", nameof(wire));
            var request = nextRequest++;
            link.Send(WireMessage.ActOf(request, wire).Encode());
            return request;
        }

        public int Play(LegalAction action) => Act(ActionCodec.Encode(action));

        /// <summary>Asks for the projection again after a connection came back.</summary>
        public void Resume() => link.Send(WireMessage.ResumeFrom(EventsSeen).Encode());

        private void Apply(string payload)
        {
            if (!WireMessage.TryParse(payload, out var message, out var error))
            {
                rejects.Add(error);
                return;
            }

            switch (message.Kind)
            {
                case WireKind.Welcome:
                    Seat = message.Int(0, -1);
                    Wind = message.Field(1).Length > 0 && TileNotation.TryParse(message.Field(1), out var wind) && wind.IsWind
                        ? (Wind)wind.Rank
                        : Wind.East;
                    Dealer = message.Int(2);
                    Seed = message.IntOrNull(3);
                    RulesId = message.Field(4);
                    EventsSeen = Math.Max(EventsSeen, message.Int(5));
                    MoveIndex = Math.Max(MoveIndex, message.Int(6));
                    return;

                case WireKind.View:
                    MoveIndex = Math.Max(MoveIndex, message.Int(1));
                    View = SeatView.Read(message.Field(2));
                    return;

                case WireKind.Clock:
                    if (message.Field(1) is { Length: > 0 } window && Enum.TryParse<ClockWindow>(window, out var kind))
                        ClockWindow = kind;
                    else
                        ClockWindow = ClockWindow.Idle;
                    ClockLeft = message.IntOrNull(2) is { } left
                        ? TimeSpan.FromMilliseconds(left)
                        : TimeSpan.Zero;
                    return;

                case WireKind.Event:
                    events.Add(ReadEvent(message));
                    EventsSeen = Math.Max(EventsSeen, message.Int(0) + 1);
                    return;

                case WireKind.Notice:
                    notices.Add(message.Field(0));
                    return;

                case WireKind.Settled:
                    if (TryReadSettlement(message, out var settlement)) settlements.Add(settlement);
                    return;

                case WireKind.Accepted:
                    Accepted++;
                    return;

                case WireKind.Rejected:
                    Refused++;
                    rejects.Add(message.Field(1));
                    return;

                case WireKind.Resumed:
                    MoveIndex = Math.Max(MoveIndex, message.Int(2));
                    return;

                case WireKind.Refused:
                    Refusal = message.Field(0);
                    return;
            }
        }

        private static bool TryReadSettlement(WireMessage message, out Settlement settlement)
        {
            settlement = Mahjong.Rules.Settlements.ForDraw();
            if (!int.TryParse(message.Field(0), NumberStyles.Integer, CultureInfo.InvariantCulture, out var winner)
                || !int.TryParse(message.Field(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var handPoints)
                || !int.TryParse(message.Field(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gain)) return false;
            var selfDrawn = string.Equals(message.Field(3), "self", StringComparison.OrdinalIgnoreCase);
            var payments = new List<Payment>();
            foreach (var item in message.Field(5).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = item.Split(',');
                if (fields.Length != 3
                    || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var from)
                    || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var to)
                    || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var points)) return false;
                payments.Add(new Payment(from, to, points));
            }
            settlement = new Settlement(winner, handPoints, selfDrawn, payments, gain, message.Field(4));
            return true;
        }

        private static TableEvent ReadEvent(WireMessage message)
        {
            Tile? tile = TileNotation.TryParseTile(message.Field(3), out var face) ? face : null;
            return new TableEvent(message.Field(1), message.Int(2, -1), tile, message.Field(4));
        }

        public override string ToString() => Seat < 0
            ? $"{Name}: waiting for a seat ({MoveIndex} moves)"
            : $"{Name}: P{Seat} {Wind}, {View?.Phase.ToString() ?? "no view"}, "
              + $"{ClockWindow}{(OnClock ? " " + ClockLeft.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "s" : string.Empty)}";
    }
}
