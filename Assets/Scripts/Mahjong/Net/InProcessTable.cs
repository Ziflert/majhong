using System;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>
    /// A whole table inside one process: the authoritative server plus the seats that are played by
    /// bots. Humans state their moves exactly like bots do — through
    /// <see cref="AuthoritativeTable.Accept"/> — so the same validation applies to a click, to a bot and
    /// to a move arriving over a wire.
    ///
    /// This is the "in-process transport" of the multiplayer draft: it needs no socket, and it is what
    /// lets four seats play a hand with nothing but move lines in the self-test.
    /// </summary>
    public sealed class InProcessTable
    {
        public const int Seats = 4;

        private readonly AuthoritativeTable table;
        private readonly IMahjongBot?[] bots = new IMahjongBot?[Seats];

        public InProcessTable(int dealer, int? seed, Wind roundWind = Wind.East,
            BotLevel level = BotLevel.Strategic, params int[] botSeats)
        {
            table = new AuthoritativeTable(dealer, seed, roundWind);
            var random = new Random(seed ?? 0);
            foreach (var seat in botSeats)
            {
                if (seat is < 0 or >= Seats) throw new ArgumentOutOfRangeException(nameof(botSeats));
                bots[seat] = new HeuristicBot(random.Next(), level);
            }
        }

        public AuthoritativeTable Server => table;
        public GameState State => table.State;
        public bool IsBot(int seat) => bots[seat] != null;

        public SeatView View(int seat) => table.ViewFor(seat);

        /// <summary>A human (or a test, or a wire client) states a move.</summary>
        public TableReply Act(int seat, string wire) => table.Accept(seat, wire);

        /// <summary>
        /// Lets every bot seat play until a human seat has to decide or the hand ends. Returns the
        /// number of moves played, so a caller can tell "the table is waiting for a person" from
        /// "the bots could not move".
        /// </summary>
        public int Pump(int maxMoves = 2000)
        {
            var played = 0;
            for (var step = 0; step < maxMoves; step++)
            {
                if (State.Phase == GamePhase.HandFinished) break;

                if (State.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                {
                    var answered = false;
                    for (var seat = 0; seat < Seats; seat++)
                    {
                        if (!IsBot(seat)) continue;

                        var options = State.GetLegalActions(seat);
                        if (options.Count == 0) continue;

                        var choice = bots[seat]!.Choose(State, seat, options);
                        var reply = Act(seat, ActionCodec.Encode(choice));
                        if (!reply.Accepted) throw new InvalidOperationException($"Bot P{seat} was refused: {reply.Reason}");
                        played++;
                        answered = true;
                        break;
                    }

                    if (!answered) break;
                    continue;
                }

                var current = State.CurrentPlayer;
                if (!IsBot(current)) break;

                var legal = State.GetLegalActions(current);
                if (legal.Count == 0) break;

                var move = bots[current]!.Choose(State, current, legal);
                var acceptance = Act(current, ActionCodec.Encode(move));
                if (!acceptance.Accepted) throw new InvalidOperationException($"Bot P{current} was refused: {acceptance.Reason}");
                played++;
            }

            return played;
        }

        /// <summary>Plays the rest of the hand, human seats included, using the bots as a stand-in.</summary>
        public int PlayOut(int maxMoves = 4000)
        {
            var played = 0;
            var random = new Random(SeedOfTable());
            // A seat without a bot gets a stand-in; it is created once so the stand-in keeps its own
            // notion of the hand instead of rolling new dice on every move.
            var stand = new IMahjongBot?[Seats];
            for (var seat = 0; seat < Seats; seat++) stand[seat] = bots[seat];

            IMahjongBot Player(int seat) =>
                stand[seat] ??= new HeuristicBot(random.Next(), BotLevel.Strategic);

            for (var step = 0; step < maxMoves; step++)
            {
                if (State.Phase == GamePhase.HandFinished) break;

                if (State.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)
                {
                    var answered = false;
                    for (var seat = 0; seat < Seats; seat++)
                    {
                        var options = State.GetLegalActions(seat);
                        if (options.Count == 0) continue;

                        var reply = Act(seat, ActionCodec.Encode(Player(seat).Choose(State, seat, options)));
                        if (!reply.Accepted) throw new InvalidOperationException($"P{seat} was refused: {reply.Reason}");
                        played++;
                        answered = true;
                        break;
                    }

                    if (!answered) break;
                    continue;
                }

                var current = State.CurrentPlayer;
                var legal = State.GetLegalActions(current);
                if (legal.Count == 0) break;

                var acceptance = Act(current, ActionCodec.Encode(Player(current).Choose(State, current, legal)));
                if (!acceptance.Accepted) throw new InvalidOperationException($"P{current} was refused: {acceptance.Reason}");
                played++;
            }

            return played;
        }

        private int SeedOfTable() => table.Seed ?? 0;

        /// <summary>Projection of one seat as the client would print it.</summary>
        public string DescribeSeat(int seat) => table.ViewFor(seat).Describe();

        /// <summary>Projections of all seats.</summary>
        public string[] DescribeAllSeats() => Enumerable.Range(0, Seats).Select(DescribeSeat).ToArray();
    }
}
