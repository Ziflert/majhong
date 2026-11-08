using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>
    /// Small lifecycle facade for the desktop online mode. It deliberately owns no Unity objects:
    /// the view only edits <see cref="Settings"/>, calls StartHost or Connect, and pumps it from
    /// Update. The table protocol remains the same for a host and a remote player.
    /// </summary>
    public sealed class OnlineLobby : IDisposable
    {
        private TcpTableHost? host;
        private TcpTableLink? link;
        private TableClient? client;

        public OnlineLobby(string playerName = "player")
        {
            Settings = new LobbySettings(playerName);
        }

        public LobbySettings Settings { get; }
        public bool IsHost => host != null;
        public bool IsConnected => client?.IsOpen == true;
        public bool IsRunning => host?.IsRunning == true;
        public TableClient? Client => client;
        public TableServer? Server => host?.Server;
        public string Status { get; private set; } = "offline";
        public Exception? LastError { get; private set; }

        /// <summary>Starts listening. Port zero asks the OS for a free port.</summary>
        public int StartHost()
        {
            Stop();
            try
            {
                host = new TcpTableHost(Settings.Port, CreateServer(), IPAddress.Any);
                host.Start();
                Status = $"hosting:{host.Port.ToString(CultureInfo.InvariantCulture)}";
                LastError = null;
                return host.Port;
            }
            catch (Exception error) when (error is SocketException or InvalidOperationException)
            {
                LastError = error;
                Status = "error";
                Stop();
                return -1;
            }
        }

        /// <summary>Connects one player to a running desktop host.</summary>
        public bool Connect()
        {
            DisconnectClient();
            try
            {
                link = TcpTableLink.Connect(Settings.Host, Settings.Port);
                client = new TableClient(link, Settings.PlayerName);
                Status = "connecting";
                LastError = null;
                return true;
            }
            catch (Exception error) when (error is System.Net.Sockets.SocketException or InvalidOperationException)
            {
                LastError = error;
                Status = "error";
                DisconnectClient();
                return false;
            }
        }

        /// <summary>Runs both halves without creating a worker thread.</summary>
        public int Pump()
        {
            var moved = 0;
            if (host != null) moved += host.Pump();
            if (client != null) moved += client.Poll();
            if (client?.Seat >= 0) Status = client.IsHandOver ? "hand-finished" : $"seat:{client.Seat}";
            return moved;
        }

        public void AddBot(int seat, BotLevel level = BotLevel.Strategic) => host?.AddBot(seat, level, Settings.Seed);

        private TableServer CreateServer() => new(Settings.Dealer, Settings.Seed, Settings.RoundWind,
            Settings.Variant, Settings.Clock);

        public void DisconnectClient()
        {
            client = null;
            link?.Close();
            link = null;
        }

        public void Stop()
        {
            DisconnectClient();
            host?.Stop();
            host = null;
            Status = "offline";
        }

        public void Dispose() => Stop();
    }

    /// <summary>Validated, serialisable lobby choices. No socket code belongs in a menu widget.</summary>
    public sealed class LobbySettings
    {
        public LobbySettings(string playerName = "player") => PlayerName = playerName;

        public string PlayerName { get; set; }
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 39123;
        public int Dealer { get; set; }
        public int Seed { get; set; } = 12345;
        public Wind RoundWind { get; set; } = Wind.East;
        public RuleVariant Variant { get; set; } = RuleVariant.Default;
        public ClockLimits Clock { get; set; } = ClockLimits.Rules;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(PlayerName)) { error = "player name is empty"; return false; }
            if (PlayerName.IndexOfAny(new[] { '\r', '\n', '|' }) >= 0) { error = "player name contains a forbidden character"; return false; }
            if (string.IsNullOrWhiteSpace(Host)) { error = "host is empty"; return false; }
            if (Port is < 0 or > 65535) { error = "port must be between 0 and 65535"; return false; }
            if (Dealer is < 0 or >= 4) { error = "dealer must be a seat from 0 to 3"; return false; }
            error = string.Empty;
            return true;
        }
    }
}
