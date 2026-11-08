using System;
using System.Collections.Generic;

namespace Mahjong.Net
{
    /// <summary>
    /// One end of a connection to the table: messages go out as text, whatever the connection itself is.
    /// The rest of the network layer never sees a socket, so the same conversation is checked in memory,
    /// played in one process and later carried over TCP or a WebSocket (docs/multiplayer-draft.md, M2).
    /// </summary>
    public interface ITableLink
    {
        /// <summary>True while the other end can still be written to.</summary>
        bool IsOpen { get; }

        /// <summary>How many payloads this end has handed to the other side.</summary>
        int Sent { get; }

        /// <summary>Sends one payload; the link does the framing the wire would do.</summary>
        void Send(string payload);

        /// <summary>Everything the other end has sent since the last call, in order; may be empty.</summary>
        IReadOnlyList<string> Receive();

        void Close();
    }

    /// <summary>
    /// Two ends of a link inside one process. The payload still goes through the byte framing of a real
    /// connection — including a run where it is handed over in pieces of three bytes — so a client that
    /// works over this link works over a socket, and a message cut in half is caught by a test rather
    /// than by a player.
    /// </summary>
    public sealed class LoopbackLink : ITableLink
    {
        private readonly Queue<string> inbox = new();
        private readonly FrameReader reader = new();
        private readonly int fragmentSize;
        private LoopbackLink? peer;
        private bool open = true;

        private LoopbackLink(int fragmentSize) => this.fragmentSize = fragmentSize;

        public bool IsOpen => open && peer != null && peer.open;

        public int Sent { get; private set; }

        /// <summary>How many messages were handed over in pieces; zero when the link is not fragmented.</summary>
        public int Pieces { get; private set; }

        /// <summary>Two ends of one connection. <paramref name="fragment"/> splits every frame in three bytes.</summary>
        public static (ITableLink Left, ITableLink Right) Create(bool fragment = false)
        {
            var left = new LoopbackLink(fragment ? 3 : 0);
            var right = new LoopbackLink(fragment ? 3 : 0);
            left.peer = right;
            right.peer = left;
            return (left, right);
        }

        public void Send(string payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (!IsOpen) throw new InvalidOperationException("The link is closed.");

            Sent++;
            var frame = WireFrame.Pack(payload);
            if (fragmentSize <= 0)
            {
                Deliver(frame, 0, frame.Length);
                return;
            }

            for (var offset = 0; offset < frame.Length; offset += fragmentSize)
            {
                var size = Math.Min(fragmentSize, frame.Length - offset);
                Deliver(frame, offset, size);
                Pieces++;
            }
        }

        private void Deliver(byte[] frame, int offset, int size)
        {
            foreach (var payload in peer!.reader.Push(frame, offset, size)) peer.inbox.Enqueue(payload);
        }

        public IReadOnlyList<string> Receive()
        {
            if (inbox.Count == 0) return Array.Empty<string>();

            var payloads = new List<string>(inbox.Count);
            while (inbox.Count > 0) payloads.Add(inbox.Dequeue());
            return payloads;
        }

        public void Close()
        {
            open = false;
            inbox.Clear();
        }
    }
}
