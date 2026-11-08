using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Mahjong.Net
{
    /// <summary>
    /// TCP implementation of <see cref="ITableLink"/>. The protocol above this class is unchanged:
    /// framed UTF-8 payloads go through a stream, and <see cref="Receive"/> only returns complete
    /// messages that are already available. It never gives the game a socket or a partial payload.
    /// </summary>
    public sealed class TcpTableLink : ITableLink
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly FrameReader reader = new();
        private readonly object sendLock = new();
        private bool open = true;

        private TcpTableLink(TcpClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            client.NoDelay = true;
            stream = client.GetStream();
        }

        public bool IsOpen => open && client.Connected;
        public int Sent { get; private set; }

        /// <summary>Connects to a table host. The call returns after the TCP handshake.</summary>
        public static TcpTableLink Connect(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("A host is required.", nameof(host));
            if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            var client = new TcpClient();
            client.Connect(host, port);
            return new TcpTableLink(client);
        }

        /// <summary>Wraps an already accepted TCP connection on the server side.</summary>
        public static TcpTableLink Accept(TcpClient client) => new(client);

        public void Send(string payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            EnsureOpen();
            var frame = WireFrame.Pack(payload);
            lock (sendLock)
            {
                try
                {
                    stream.Write(frame, 0, frame.Length);
                    stream.Flush();
                    Sent++;
                }
                catch (IOException)
                {
                    Close();
                    throw;
                }
                catch (SocketException)
                {
                    Close();
                    throw;
                }
            }
        }

        public IReadOnlyList<string> Receive()
        {
            EnsureOpen();
            var messages = new List<string>();
            try
            {
                while (stream.DataAvailable)
                {
                    var bytes = new byte[4096];
                    var read = stream.Read(bytes, 0, bytes.Length);
                    if (read == 0)
                    {
                        Close();
                        break;
                    }

                    foreach (var payload in reader.Push(bytes, 0, read)) messages.Add(payload);
                    if (read < bytes.Length) break;
                }
            }
            catch (IOException)
            {
                Close();
            }
            catch (SocketException)
            {
                Close();
            }

            return messages;
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            try { stream.Close(); } catch (IOException) { }
            try { client.Close(); } catch (SocketException) { }
        }

        private void EnsureOpen()
        {
            if (!IsOpen) throw new InvalidOperationException("The TCP link is closed.");
        }
    }

    /// <summary>Small non-blocking acceptor used by a host process or the Unity lobby.</summary>
    public sealed class TcpTableListener : IDisposable
    {
        private readonly TcpListener listener;

        public TcpTableListener(IPAddress address, int port)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            listener = new TcpListener(address, port);
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public bool IsListening { get; private set; }

        public void Start()
        {
            if (IsListening) return;
            listener.Start();
            IsListening = true;
        }

        /// <summary>Returns a connection immediately, or null when no client is waiting.</summary>
        public TcpTableLink? AcceptPending()
        {
            if (!IsListening || !listener.Pending()) return null;
            return TcpTableLink.Accept(listener.AcceptTcpClient());
        }

        public void Stop()
        {
            if (!IsListening) return;
            listener.Stop();
            IsListening = false;
        }

        public void Dispose() => Stop();
    }
}
