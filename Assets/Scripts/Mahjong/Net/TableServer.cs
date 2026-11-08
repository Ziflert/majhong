using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>
    /// The table as a server sees it: one <see cref="AuthoritativeTable"/> is the single source of truth,
    /// every seat talks to it in move lines, and nobody ever gets the state itself — only the projection
    /// of their own seat (docs/multiplayer-draft.md).
    ///
    /// Three things live here that a single machine does not need:
    /// <list type="bullet">
    /// <item>the clocks of the table are the server's, so a claim does not wait for a client that stopped
    /// answering, and a move that arrives after the deadline is not played (M1);</item>
    /// <item>seats are attached and detached over links, and a seat that comes back is caught up from
    /// the projection and the events it missed (M2);</item>
    /// <item>a seat played by a bot answers at once, so filling a networked table with bots does not mean
    /// waiting ten seconds a turn.</item>
    /// </list>
    ///
    /// Time is not read from the machine: the caller advances it with <see cref="Tick"/>, so a test can
    /// let three seconds of a claim window pass in one line.
    /// </summary>
    public sealed class TableServer
    {
        public const int Seats = 4;

        private readonly AuthoritativeTable table;
        private readonly ITableLink?[] links = new ITableLink?[Seats];
        private readonly string?[] names = new string?[Seats];
        private readonly IMahjongBot?[] bots = new IMahjongBot?[Seats];
        private readonly GameSession session;
        private int announced;

        public TableServer(int dealer = 0, int? seed = null, Wind roundWind = Wind.East,
            RuleVariant? variant = null, ClockLimits? limits = null)
        {
            table = new AuthoritativeTable(dealer, seed, roundWind, variant);
            session = new GameSession(GameSession.DefaultTimeLimit, variant, dealer);
            session.StartNextHand(seed);
            Clock = new TableClock(limits);
            Now = TimeSpan.Zero;
            announced = table.Events.Count;
            RefreshWindows();
        }

        public AuthoritativeTable Table => table;
        public GameState State => table.State;
        public TableClock Clock { get; }

        /// <summary>The reading of the server clock the table is playing by.</summary>
        public TimeSpan Now { get; private set; }

        /// <summary>How many moves the table has played: the position a client is caught up to.</summary>
        public int MoveIndex => table.Log.Count;

        /// <summary>How many things have happened since the table was created.</summary>
        public int EventCount => table.Events.Count;

        public int Dealer => table.Dealer;
        public int? Seed => table.Seed;
        public RuleVariant Variant => table.Variant;
        /// <summary>The public session ledger mirrored from hands settled by this table.</summary>
        public GameSession Session => session;

        /// <summary>Seats with a link attached, in seat order.</summary>
        public IReadOnlyList<int> Seated => Enumerable.Range(0, Seats).Where(IsSeated).ToList();

        public bool IsSeated(int seat) => seat is >= 0 and < Seats && links[seat]?.IsOpen == true;

        public string? NameOf(int seat) => seat is >= 0 and < Seats ? names[seat] : null;

        /// <summary>
        /// Attaches a link to a seat and introduces the table to it: which seat it has, which rules are
        /// played, the projection of that seat and the window it has. A full table refuses politely.
        /// </summary>
        public int Attach(ITableLink link, string name = "player", int seat = -1)
        {
            if (link == null) throw new ArgumentNullException(nameof(link));

            var free = seat >= 0 ? seat : FirstFreeSeat();
            if (free is < 0 or >= Seats || IsSeated(free))
            {
                // The link stays open long enough for the refusal to be read; the client closes it.
                link.Send(WireMessage.Refuse(seat >= 0 ? $"seat {seat} is taken" : "the table is full").Encode());
                return -1;
            }

            links[free] = link;
            names[free] = name;
            Push(free, WelcomeFor(free));
            Push(free, ViewFor(free));
            Push(free, WireMessage.ClockOf(Clock.Of(free), Now));
            Push(free, WireMessage.SessionOf(SessionMessage()));
            if (session.IsFinished) Push(free, WireMessage.StandingsOf(session.Standings()));
            return free;
        }

        /// <summary>Closes the link of a seat; the table plays on without it.</summary>
        public void Detach(int seat)
        {
            if (seat is < 0 or >= Seats) return;
            links[seat]?.Close();
            links[seat] = null;
            names[seat] = null;
            Clock.Disarm(seat);
        }

        /// <summary>Gives a seat to a bot: it answers instantly, with or without a link.</summary>
        public void SetBot(int seat, IMahjongBot? bot, bool replace = true)
        {
            if (seat is < 0 or >= Seats) throw new ArgumentOutOfRangeException(nameof(seat));
            if (!replace && bots[seat] != null) return;
            bots[seat] = bot;
        }

        public bool IsBot(int seat) => seat is >= 0 and < Seats && bots[seat] != null;

        /// <summary>
        /// Reads every attached link, answers what the seats ask and lets the bots answer theirs. The
        /// table clock is checked first: a move that comes after its window has run out is too late,
        /// and the table has already played the window for that seat.
        /// </summary>
        public int Poll()
        {
            Expire();
            var read = 0;
            for (var seat = 0; seat < Seats; seat++)
            {
                var link = links[seat];
                if (link == null) continue;
                if (!link.IsOpen)
                {
                    Detach(seat);
                    continue;
                }

                foreach (var payload in link.Receive())
                {
                    Handle(seat, payload);
                    read++;
                }
            }

            RunBots();
            Publish();
            return read;
        }

        /// <summary>Advances the server clock by <paramref name="delta"/> and plays whatever ran out.</summary>
        public void Tick(TimeSpan delta)
        {
            if (delta < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delta), "A clock does not run backwards.");
            Now += delta;
            session.AdvanceClock(delta);
            if (session.IsTimeUp && !session.IsFinished)
                session.StopEarly();
            Expire();
            RunBots();
            Publish();
        }

        /// <summary>Deals the next hand on the same table; every seat gets a fresh projection.</summary>
        public void StartNextHand(int dealer, int? seed = null, Wind? roundWind = null)
        {
            if (session.IsFinished) throw new InvalidOperationException("The network session is over.");
            table.StartNextHand(dealer, seed, roundWind);
            session.StartNextHand(seed);
            Clock.DisarmAll();
            RefreshWindows();
            RunBots();
            Publish();
        }

        public void Close()
        {
            for (var seat = 0; seat < Seats; seat++) Detach(seat);
        }

        // ------------------------------------------------------------------ clock

        /// <summary>
        /// The windows that ran out are played by the table itself: silence is a pass in a claim window,
        /// and a discard that nobody made is made for the seat by its own bot (see
        /// <see cref="ClockExpiry"/>). Hu and kongs are never declared for a silent seat.
        /// </summary>
        private void Expire()
        {
            var expired = Clock.RunOut(Now);
            if (expired.Count == 0) return;

            foreach (var clock in expired)
            {
                if (State.Phase == GamePhase.HandFinished) break;

                Clock.Disarm(clock.Seat);
                var legal = State.GetLegalActions(clock.Seat);
                var action = ClockExpiry.Choose(State, clock.Seat, legal, bots[clock.Seat]);
                if (action == null) continue;

                var reply = table.Accept(clock.Seat, ActionCodec.Encode(action));
                if (!reply.Accepted) continue;

                // The seats have to know why a move appeared out of nowhere.
                Push(clock.Seat, WireMessage.Notice($"P{clock.Seat} ran out of time: {ActionCodec.Encode(action)}"));
                foreach (var other in Seated.Where(seat => seat != clock.Seat))
                    Push(other, WireMessage.Notice($"P{clock.Seat} ran out of time: {ActionCodec.Encode(action)}"));
            }
        }

        private void RefreshWindows()
        {
            for (var seat = 0; seat < Seats; seat++)
            {
                if (!NeedsDecision(State, State.GetLegalActions(seat)))
                {
                    Clock.Disarm(seat);
                    continue;
                }

                if (Clock.IsArmed(seat)) continue;
                Clock.Arm(seat, WindowFor(State), Now);
            }
        }

        /// <summary>
        /// True when the seat has a decision that stops the table: it is on turn, or it holds a claim
        /// that is more than a pass. A seat that may only pass is not on the clock, because the window
        /// closes without it.
        /// </summary>
        private static bool NeedsDecision(GameState state, IReadOnlyList<LegalAction> offers)
        {
            if (offers.Count == 0) return false;

            // Taking a tile is not a choice: the table draws for the seat instead of timing it.
            if (offers.All(offer => offer.Kind == ActionKind.Draw)) return false;

            return state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery
                ? offers.Any(offer => offer.Kind != ActionKind.Pass)
                : true;
        }

        private static ClockWindow WindowFor(GameState state) => state.Phase switch
        {
            GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery => ClockWindow.Claim,
            GamePhase.AwaitingDiscard when state.CurrentPlayer == state.Dealer && state.Discards.Count == 0 => ClockWindow.Arrange,
            _ => ClockWindow.Discard
        };

        // ------------------------------------------------------------------- bots

        /// <summary>Lets every seat played by a bot play until a person has to decide or the hand ends.</summary>
        private void RunBots()
        {
            for (var guard = 0; guard < 4000; guard++)
            {
                AutoDraw();
                if (State.Phase == GamePhase.HandFinished) return;

                var actor = BotOnTurn();
                if (actor < 0) return;

                var offers = State.GetLegalActions(actor);
                var choice = bots[actor]!.Choose(State, actor, offers);
                var reply = table.Accept(actor, ActionCodec.Encode(choice));
                if (!reply.Accepted) return;
            }
        }

        /// <summary>
        /// Takes the tile for whoever is on turn. A draw is forced — the engine offers nothing else — so
        /// waiting for a seat to ask for it would only mean waiting for a window that can do nothing.
        /// </summary>
        private void AutoDraw()
        {
            for (var guard = 0; guard < 8; guard++)
            {
                if (State.Phase != GamePhase.AwaitingDraw) return;

                var offers = State.GetLegalActions(State.CurrentPlayer);
                var draw = offers.FirstOrDefault(offer => offer.Kind == ActionKind.Draw);
                if (draw == null) return;
                if (!table.Accept(State.CurrentPlayer, ActionCodec.Encode(draw)).Accepted) return;
            }
        }

        private int BotOnTurn()
        {
            if (State.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
            {
                for (var seat = 0; seat < Seats; seat++)
                {
                    if (bots[seat] == null) continue;
                    if (!NeedsDecision(State, State.GetLegalActions(seat))) continue;
                    return seat;
                }
                return -1;
            }

            var current = State.CurrentPlayer;
            if (bots[current] == null) return -1;
            return NeedsDecision(State, State.GetLegalActions(current)) ? current : -1;
        }

        // -------------------------------------------------------------- messages

        private void Handle(int seat, string payload)
        {
            if (!WireMessage.TryParse(payload, out var message, out var error))
            {
                Push(seat, WireMessage.RejectedOf(-1, error));
                return;
            }

            switch (message.Kind)
            {
                case WireKind.Act:
                    HandleAct(seat, message);
                    return;

                case WireKind.Resume:
                    HandleResume(seat, message);
                    return;

                case WireKind.Hello:
                    Push(seat, WelcomeFor(seat));
                    Push(seat, ViewFor(seat));
                    Push(seat, WireMessage.ClockOf(Clock.Of(seat), Now));
                    Push(seat, WireMessage.SessionOf(SessionMessage()));
                    if (session.IsFinished) Push(seat, WireMessage.StandingsOf(session.Standings()));
                    return;

                default:
                    Push(seat, WireMessage.RejectedOf(-1,
                        $"the table does not understand {WireMessage.Code(message.Kind)}"));
                    return;
            }
        }

        private void HandleAct(int seat, WireMessage message)
        {
            if (!WireMessage.TryReadAct(message, out var requestId, out var wire, out var error))
            {
                Push(seat, WireMessage.RejectedOf(requestId, error));
                return;
            }

            if (Clock.Of(seat) is { } clock && clock.HasRunOut(Now))
            {
                Push(seat, WireMessage.RejectedOf(requestId, "the window of this seat has already run out"));
                return;
            }

            var reply = table.Accept(seat, wire);
            if (!reply.Accepted)
            {
                Push(seat, WireMessage.RejectedOf(requestId, reply.Reason));
                return;
            }

            Push(seat, WireMessage.AcceptedOf(requestId, MoveIndex));
        }

        /// <summary>A seat that was away is caught up: the events it missed, then the projection itself.</summary>
        private void HandleResume(int seat, WireMessage message)
        {
            var seen = Math.Clamp(message.Int(0), 0, table.Events.Count);
            for (var index = seen; index < table.Events.Count; index++)
                Push(seat, WireMessage.EventOf(index, table.Events[index]));

            Push(seat, WireMessage.ResumedFrom(seen, table.Events.Count, MoveIndex));
            Push(seat, ViewFor(seat));
            Push(seat, WireMessage.ClockOf(Clock.Of(seat), Now));
        }

        /// <summary>Sends what changed: the events nobody has seen, the projection and the clock of every seat.</summary>
        private void Publish()
        {
            for (; announced < table.Events.Count; announced++)
            {
                var entry = table.Events[announced];
                var message = WireMessage.EventOf(announced, entry);
                foreach (var seat in Seated) Push(seat, message);
                if (entry.Kind == "handEnd")
                {
                    var settlement = Settlements.ForState(State, Variant);
                    session.RecordExternalHand(settlement, settlement.WinnerPlayer);
                    foreach (var seat in Seated)
                    {
                        Push(seat, WireMessage.SettledOf(settlement));
                        Push(seat, WireMessage.SessionOf(SessionMessage()));
                        if (session.IsFinished) Push(seat, WireMessage.StandingsOf(session.Standings()));
                    }
                }
            }

            RefreshWindows();
            foreach (var seat in Seated)
            {
                Push(seat, ViewFor(seat));
                Push(seat, WireMessage.ClockOf(Clock.Of(seat), Now));
            }
        }

        private WireMessage WelcomeFor(int seat) => WireMessage.Welcome(
            seat,
            State.SeatWindOf(seat),
            Dealer,
            Seed,
            Variant.Id,
            table.Events.Count,
            MoveIndex);

        private WireMessage ViewFor(int seat) =>
            WireMessage.ViewOf(seat, MoveIndex, SeatView.For(State, seat).Describe());

        private SessionSnapshot SessionMessage() => new(
            session.HandsPlayed,
            GameSession.TotalHands,
            session.RoundWind,
            session.Dealer,
            session.TimeLimit,
            session.TimeLeft,
            session.IsFinished,
            session.Scores.ToArray());

        private void Push(int seat, WireMessage message)
        {
            var link = links[seat];
            if (link == null || !link.IsOpen) return;
            link.Send(message.Encode());
        }

        private int FirstFreeSeat()
        {
            for (var seat = 0; seat < Seats; seat++)
                if (!IsSeated(seat)) return seat;
            return -1;
        }

        public override string ToString() =>
            $"table of {Seated.Count} seat(s), {MoveIndex} moves, {Now.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s";
    }
}
