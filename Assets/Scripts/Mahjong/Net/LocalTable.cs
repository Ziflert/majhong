using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>
    /// A whole networked table inside one process: the server, the links and one client per seat, with a
    /// pump between them. Nothing here is a shortcut — the clients talk over
    /// <see cref="LoopbackLink"/>s, so every message goes through the same framing, and none of them can
    /// reach the state the server owns.
    ///
    /// This is what a self-test drives, what a hot-seat demo can run on, and what a socket transport
    /// replaces one link at a time: attach a TCP link instead of a loopback one and the conversation
    /// above it does not change.
    /// </summary>
    public sealed class LocalTable
    {
        private readonly List<TableClient> clients = new();
        private readonly bool fragment;

        public LocalTable(int dealer = 0, int? seed = null, Wind roundWind = Wind.East,
            RuleVariant? variant = null, ClockLimits? limits = null, bool fragment = false)
        {
            Server = new TableServer(dealer, seed, roundWind, variant, limits);
            this.fragment = fragment;
        }

        public TableServer Server { get; }

        public IReadOnlyList<TableClient> Clients => clients;

        /// <summary>Adds a person at the next free seat (or the seat asked for) and returns their client.</summary>
        public TableClient Connect(string name = "player", int seat = -1)
        {
            var (mine, theirs) = LoopbackLink.Create(fragment);
            Server.Attach(theirs, name, seat);
            var client = new TableClient(mine, name);
            clients.Add(client);
            Pump();
            return client;
        }

        /// <summary>Gives a seat to a bot, which answers at once instead of waiting for the table clock.</summary>
        public void AddBot(int seat, BotLevel level = BotLevel.Strategic, int? seed = null)
        {
            Server.SetBot(seat, new HeuristicBot(seed ?? 17 + seat, level));
            Pump();
        }

        public TableClient? Seat(int seat) => clients.FirstOrDefault(client => client.Seat == seat);

        public TableClient? Named(string name) => clients.FirstOrDefault(client => client.Name == name);

        /// <summary>
        /// Carries messages until nothing is left to carry: clients read, the server answers, clients read
        /// the answer. Returns how many messages travelled.
        /// </summary>
        public int Pump()
        {
            var total = 0;
            for (var round = 0; round < 16; round++)
            {
                var moved = 0;
                foreach (var client in clients) moved += client.Poll();
                moved += Server.Poll();
                total += moved;
                if (moved == 0) break;
            }
            return total;
        }

        /// <summary>Lets <paramref name="delta"/> of the server clock pass and carries the result.</summary>
        public int Tick(TimeSpan delta)
        {
            Server.Tick(delta);
            return Pump();
        }

        public int Tick(int seconds) => Tick(TimeSpan.FromSeconds(seconds));

        /// <summary>The projections of all four seats as the clients see them; a test compares them.</summary>
        public string[] Views() => clients.OrderBy(client => client.Seat).Select(client => client.View?.Describe() ?? string.Empty).ToArray();
    }
}
