using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>Something that happened at the table; a client needs it to animate a move.</summary>
    public sealed record TableEvent(string Kind, int Seat, Tile? Tile = null, string Detail = "")
    {
        public static TableEvent Draw(int seat, Tile tile) => new("draw", seat, tile);
        public static TableEvent Discard(int seat, Tile tile) => new("discard", seat, tile);
        public static TableEvent Meld(int seat, MeldKind kind) => new("meld", seat, null, kind.ToString());
        public static TableEvent Flower(int seat, Tile tile) => new("flower", seat, tile);
        public static TableEvent Turn(int seat) => new("turn", seat);
        public static TableEvent HandEnd(string detail) => new("handEnd", -1, null, detail);

        public override string ToString() =>
            $"{(Seat < 0 ? "-" : $"P{Seat}")} {Kind}{(Tile.HasValue ? " " + TileNotation.Format(Tile.Value) : string.Empty)}"
            + (Detail.Length > 0 ? $": {Detail}" : string.Empty);
    }

    /// <summary>Answer to one move: either the new projection, or a refusal that changed nothing.</summary>
    public sealed record TableReply(bool Accepted, string Reason, SeatView View, IReadOnlyList<TableEvent> Events)
    {
        public override string ToString() => Accepted ? $"accepted, {Events.Count} event(s)" : $"rejected: {Reason}";
    }

    /// <summary>
    /// The authoritative table of docs/multiplayer-draft.md: one <see cref="GameState"/> is the single
    /// source of truth, players state intentions as wire lines, and a move is applied only when the
    /// engine itself offers exactly that move to that seat.
    ///
    /// Nothing here depends on Unity, so the whole network contract — projection, refusal, log, replay —
    /// is checked headless.
    /// </summary>
    public sealed class AuthoritativeTable
    {
        private readonly List<TableEvent> events = new();
        private readonly List<(int Seat, string Wire)> log = new();

        public AuthoritativeTable(int dealer = 0, int? seed = null, Wind roundWind = Wind.East)
        {
            Dealer = dealer;
            Seed = seed;
            RoundWind = roundWind;
            State = new GameState();
            State.StartHand(dealer, seed, roundWind);
            RecordHandStart();
        }

        public GameState State { get; private set; }
        public int Dealer { get; private set; }
        public int? Seed { get; private set; }
        public Wind RoundWind { get; private set; }

        /// <summary>Everything that happened since the table was created, in order.</summary>
        public IReadOnlyList<TableEvent> Events => events;

        /// <summary>Accepted moves in order: the replay log.</summary>
        public IReadOnlyList<(int Seat, string Wire)> Log => log;

        /// <summary>The log as text lines <c>seat|wire</c>; this is what a file or a replay stores.</summary>
        public string LogText => string.Join("\n", log.Select(entry => $"{entry.Seat}|{entry.Wire}"));

        /// <summary>Projection for one seat. There is no way to get the whole state out of this class.</summary>
        public SeatView ViewFor(int seat) => SeatView.For(State, seat);

        /// <summary>
        /// Applies a move stated as a wire line. A move the engine does not offer is refused with a
        /// reason and leaves the state untouched: that check is the whole server-side authority.
        /// </summary>
        public TableReply Accept(int seat, string wire)
        {
            if (seat is < 0 or > 3)
                return Reject(seat, $"seat {seat} does not exist");

            if (State.Phase == GamePhase.HandFinished)
                return Reject(seat, "the hand is over");

            if (!ActionCodec.TryDecode(wire, out var intent, out var error))
                return Reject(seat, error);

            var legal = State.GetLegalActions(seat);
            var action = ActionCodec.Resolve(legal, intent);
            if (action == null)
                return Reject(seat, $"{wire} is not offered to seat {seat}");

            var before = Snapshot.Of(State);
            State.Apply(action);
            log.Add((seat, wire));
            Collect(before, action);
            return new TableReply(true, string.Empty, ViewFor(seat), events.ToList());
        }

        /// <summary>Deals the next hand on the same table; the log keeps growing across hands.</summary>
        public SeatView StartNextHand(int dealer, int? seed = null, Wind? roundWind = null)
        {
            Dealer = dealer;
            Seed = seed;
            if (roundWind.HasValue) RoundWind = roundWind.Value;
            State.StartHand(dealer, seed, RoundWind);
            RecordHandStart();
            return ViewFor(0);
        }

        private void RecordHandStart()
        {
            events.Add(new TableEvent("handStart", -1, null, $"dealer {Dealer}, seed {Seed?.ToString() ?? "random"}"));
            for (var player = 0; player < 4; player++)
                events.Add(new TableEvent("deal", player, null, $"{State.Players[player].Concealed.Count} tiles"));
        }

        private TableReply Reject(int seat, string reason) =>
            new(false, reason, SeatView.For(State, Math.Clamp(seat, 0, 3)), Array.Empty<TableEvent>());

        /// <summary>Derives the visible events of one applied move by comparing the state around it.</summary>
        private void Collect(Snapshot before, LegalAction action)
        {
            var after = Snapshot.Of(State);

            for (var player = 0; player < 4; player++)
            {
                if (after.MeldCounts[player] > before.MeldCounts[player])
                {
                    var meld = State.Players[player].Melds[^1];
                    events.Add(TableEvent.Meld(player, meld.Kind));
                }

                if (after.FlowerCounts[player] > before.FlowerCounts[player])
                {
                    var flower = State.Players[player].Flowers[^1];
                    events.Add(TableEvent.Flower(player, flower));
                }

                if (after.ConcealedCounts[player] > before.ConcealedCounts[player] && State.LastDraw.HasValue)
                    events.Add(TableEvent.Draw(player, State.LastDraw.Value));
            }

            if (State.Discards.Count > before.Discards)
            {
                var last = State.Discards[^1];
                events.Add(TableEvent.Discard(last.Player, last.Tile));
            }

            if (State.Phase != before.Phase)
                events.Add(TableEvent.Turn(State.CurrentPlayer));

            if (State.Phase == GamePhase.HandFinished && before.Phase != GamePhase.HandFinished)
                events.Add(TableEvent.HandEnd(State.Result != null ? $"win P{State.Result.PlayerIndex}" : "draw"));
        }

        /// <summary>
        /// Rebuilds a table by replaying a log onto a fresh deal. The deal is deterministic from the
        /// seed, so the replay needs the log and nothing else — no wall is ever sent over the wire.
        /// </summary>
        public static AuthoritativeTable Replay(int dealer, int? seed, Wind roundWind, IEnumerable<string> logLines)
        {
            var table = new AuthoritativeTable(dealer, seed, roundWind);
            foreach (var line in logLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|', 2);
                if (parts.Length != 2)
                    throw new FormatException($"Not a log line: {line}");

                var seat = int.Parse(parts[0].Trim());
                var reply = table.Accept(seat, parts[1]);
                if (!reply.Accepted)
                    throw new InvalidOperationException($"Replay diverged at '{line}': {reply.Reason}");
            }

            return table;
        }

        /// <summary>Projections of all four seats; tests compare them to prove nothing leaks.</summary>
        public IReadOnlyList<SeatView> AllViews() => Enumerable.Range(0, 4).Select(ViewFor).ToList();

        /// <summary>The parts of the state an event is derived from.</summary>
        private sealed class Snapshot
        {
            private Snapshot(int discards, int[] concealedCounts, int[] meldCounts, int[] flowerCounts, GamePhase phase)
            {
                Discards = discards;
                ConcealedCounts = concealedCounts;
                MeldCounts = meldCounts;
                FlowerCounts = flowerCounts;
                Phase = phase;
            }

            public int Discards { get; }
            public int[] ConcealedCounts { get; }
            public int[] MeldCounts { get; }
            public int[] FlowerCounts { get; }
            public GamePhase Phase { get; }

            public static Snapshot Of(GameState state) => new(
                state.Discards.Count,
                Enumerable.Range(0, 4).Select(p => state.Players[p].Concealed.Count).ToArray(),
                Enumerable.Range(0, 4).Select(p => state.Players[p].Melds.Count).ToArray(),
                Enumerable.Range(0, 4).Select(p => state.Players[p].Flowers.Count).ToArray(),
                state.Phase);
        }
    }
}
