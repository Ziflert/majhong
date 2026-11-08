using System;
using System.Net;
using Mahjong.Bots;

namespace Mahjong.Net
{
    /// <summary>
    /// Desktop host: accepts TCP links and hands them to the authoritative table. Call <see cref="Pump"/>
    /// from the application's main loop; it does not create a worker thread or touch Unity objects.
    /// </summary>
    public sealed class TcpTableHost : IDisposable
    {
        private readonly TcpTableListener listener;

        public TcpTableHost(int port, TableServer? server = null, IPAddress? address = null)
        {
            Server = server ?? new TableServer();
            listener = new TcpTableListener(address ?? IPAddress.Any, port);
        }

        public TableServer Server { get; }
        public int Port => listener.Port;
        public bool IsRunning => listener.IsListening;

        public void Start() => listener.Start();

        /// <summary>Accepts pending clients and runs one server pump. Returns messages processed.</summary>
        public int Pump()
        {
            while (listener.AcceptPending() is { } link)
                Server.Attach(link);
            return Server.Poll();
        }

        public void AddBot(int seat, BotLevel level = BotLevel.Strategic, int? seed = null) =>
            Server.SetBot(seat, new HeuristicBot(seed ?? 17 + seat, level));

        public void Stop()
        {
            listener.Stop();
            Server.Close();
        }

        public void Dispose() => Stop();
    }
}
