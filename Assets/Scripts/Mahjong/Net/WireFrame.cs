using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Mahjong.Net
{
    /// <summary>
    /// One message on a byte stream. A stream has no messages in it, only bytes: the length prefix is
    /// what turns them back into messages, and it has to be the sender's own count, because a socket
    /// hands a reader whatever happened to arrive — half a message, three messages, or a message cut
    /// in the middle of a tile name.
    ///
    /// The frame is <c>&lt;length in bytes&gt;\n&lt;payload as UTF-8&gt;</c>. The prefix is ASCII and
    /// the length counts bytes, so a payload with Cyrillic in it (a player name, a refusal reason) still
    /// frames correctly.
    /// </summary>
    public static class WireFrame
    {
        /// <summary>The longest payload the table accepts; a longer one is a broken or hostile stream.</summary>
        public const int MaxPayloadBytes = 1 << 20;

        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static byte[] Pack(string payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            var body = Utf8.GetBytes(payload);
            if (body.Length > MaxPayloadBytes)
                throw new ArgumentOutOfRangeException(nameof(payload), $"A payload of {body.Length} bytes is too long.");

            var header = Utf8.GetBytes(body.Length.ToString(CultureInfo.InvariantCulture) + "\n");
            var frame = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, frame, 0, header.Length);
            Buffer.BlockCopy(body, 0, frame, header.Length, body.Length);
            return frame;
        }

        /// <summary>Text of a whole chunk of frames — what a log or a test prints.</summary>
        public static string Describe(IEnumerable<string> payloads)
        {
            var text = new StringBuilder();
            foreach (var payload in payloads) text.Append(payload.Replace("\n", "\\n")).Append('\n');
            return text.ToString();
        }
    }

    /// <summary>
    /// Turns the bytes of a stream into payloads. Feed it every chunk as it arrives; it keeps the
    /// unfinished tail until the rest comes and returns only whole messages, in order.
    ///
    /// A stream that makes no sense — a header that is not a number, a payload longer than
    /// <see cref="WireFrame.MaxPayloadBytes"/> — throws <see cref="FormatException"/>: the transport
    /// cannot resynchronise a stream it no longer understands, so it has to be closed.
    /// </summary>
    public sealed class FrameReader
    {
        private byte[] buffer = new byte[1024];
        private int count;

        /// <summary>Bytes of the message that has not arrived yet; zero between messages.</summary>
        public int Pending => count;

        /// <summary>How many whole payloads came out of the reader so far.</summary>
        public int Delivered { get; private set; }

        public IReadOnlyList<string> Push(byte[] chunk) => Push(chunk, 0, chunk?.Length ?? 0);

        public IReadOnlyList<string> Push(byte[] chunk, int offset, int length)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            if (offset < 0 || length < 0 || offset + length > chunk.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            Append(chunk, offset, length);

            var payloads = new List<string>();
            while (true)
            {
                var newline = Array.IndexOf(buffer, (byte)'\n', 0, count);
                if (newline < 0)
                {
                    // No header yet. A header is a few digits; anything longer is a broken stream.
                    if (count > 12) throw new FormatException("No length prefix in the first twelve bytes.");
                    break;
                }

                if (!int.TryParse(Encoding.ASCII.GetString(buffer, 0, newline), NumberStyles.None,
                        CultureInfo.InvariantCulture, out var size) || size < 0)
                    throw new FormatException($"Not a frame length: '{Encoding.ASCII.GetString(buffer, 0, newline)}'");
                if (size > WireFrame.MaxPayloadBytes)
                    throw new FormatException($"A frame of {size} bytes is too long.");

                var start = newline + 1;
                if (count - start < size) break;

                payloads.Add(Encoding.UTF8.GetString(buffer, start, size));
                Delivered++;
                Consume(start + size);
            }

            return payloads;
        }

        private void Append(byte[] chunk, int offset, int length)
        {
            if (count + length > buffer.Length)
            {
                var grown = new byte[Math.Max(buffer.Length * 2, count + length)];
                Buffer.BlockCopy(buffer, 0, grown, 0, count);
                buffer = grown;
            }

            Buffer.BlockCopy(chunk, offset, buffer, count, length);
            count += length;
        }

        private void Consume(int bytes)
        {
            count -= bytes;
            if (count > 0) Buffer.BlockCopy(buffer, bytes, buffer, 0, count);
        }
    }
}
