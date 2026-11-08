using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Mahjong.Controls;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>What a message on the wire is for (docs/multiplayer-draft.md, section 4).</summary>
    public enum WireKind
    {
        /// <summary>Client → server: "seat me at this table".</summary>
        Hello,

        /// <summary>Server → client: which seat you have, which rules are played, how far the table is.</summary>
        Welcome,

        /// <summary>Server → client: the whole projection of that seat.</summary>
        View,

        /// <summary>Server → client: which window is open and how much of it is left.</summary>
        Clock,

        /// <summary>Server → client: one thing that happened, for the animation.</summary>
        Event,

        /// <summary>Client → server: the move I want to play.</summary>
        Act,

        /// <summary>Server → client: your move was played.</summary>
        Accepted,

        /// <summary>Server → client: your move was not played, and here is why.</summary>
        Rejected,

        /// <summary>Client → server: I was away, catch me up from event N.</summary>
        Resume,

        /// <summary>Server → client: you are caught up, from event N to event M.</summary>
        Resumed,

        /// <summary>Server → client: the table did something the seat has to hear about (the clock ran out).</summary>
        Notice,

        /// <summary>Server → client: the hand has been settled and these payments belong on the score sheet.</summary>
        Settled,

        /// <summary>Server → client: there is no place for you (or the table is over).</summary>
        Refused
    }

    /// <summary>
    /// One message as a line of fields: <c>kind|field|field…</c>. Fields are joined with <c>|</c>, so a
    /// field that contains one is escaped — a refusal reason is a sentence, and the sentence can have
    /// anything in it.
    ///
    /// Deliberately shallow: the wire is text because a bug report, a log line and a test are all text,
    /// and the project already stores moves and tiles as text (<see cref="ActionCodec"/>,
    /// <see cref="TileNotation"/>). A JSON serialiser can be put on top of this later without changing
    /// the conversation.
    /// </summary>
    public sealed record WireMessage(WireKind Kind, IReadOnlyList<string> Fields)
    {
        public static WireMessage Of(WireKind kind, params string[] fields) => new(kind, fields);

        public static string Code(WireKind kind) => kind switch
        {
            WireKind.Hello => "hello",
            WireKind.Welcome => "welcome",
            WireKind.View => "view",
            WireKind.Clock => "clock",
            WireKind.Event => "event",
            WireKind.Act => "act",
            WireKind.Accepted => "accepted",
            WireKind.Rejected => "rejected",
            WireKind.Resume => "resume",
            WireKind.Resumed => "resumed",
            WireKind.Notice => "notice",
            WireKind.Settled => "settled",
            WireKind.Refused => "refused",
            _ => kind.ToString().ToLowerInvariant()
        };

        public static bool TryParseCode(string? code, out WireKind kind)
        {
            switch ((code ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "hello": kind = WireKind.Hello; return true;
                case "welcome": kind = WireKind.Welcome; return true;
                case "view": kind = WireKind.View; return true;
                case "clock": kind = WireKind.Clock; return true;
                case "event": kind = WireKind.Event; return true;
                case "act": kind = WireKind.Act; return true;
                case "accepted": kind = WireKind.Accepted; return true;
                case "rejected": kind = WireKind.Rejected; return true;
                case "resume": kind = WireKind.Resume; return true;
                case "resumed": kind = WireKind.Resumed; return true;
                case "notice": kind = WireKind.Notice; return true;
                case "settled": kind = WireKind.Settled; return true;
                case "refused": kind = WireKind.Refused; return true;
                default: kind = default; return false;
            }
        }

        public string Encode()
        {
            var text = new StringBuilder(Code(Kind));
            foreach (var field in Fields) text.Append('|').Append(Escape(field));
            return text.ToString();
        }

        public static WireMessage Parse(string payload) =>
            TryParse(payload, out var message, out var error) ? message : throw new FormatException(error);

        public static bool TryParse(string? payload, out WireMessage message, out string error)
        {
            message = Of(WireKind.Refused, "unreadable");
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(payload))
            {
                error = "empty message";
                return false;
            }

            var parts = payload.Trim().Split('|');
            if (!TryParseCode(parts[0], out var kind))
            {
                error = $"unknown message: {parts[0]}";
                return false;
            }

            var fields = new List<string>(Math.Max(0, parts.Length - 1));
            for (var index = 1; index < parts.Length; index++) fields.Add(Unescape(parts[index]));

            message = new WireMessage(kind, fields);
            return true;
        }

        /// <summary>Field at <paramref name="index"/>, or an empty string when the message is short.</summary>
        public string Field(int index) => index >= 0 && index < Fields.Count ? Fields[index] : string.Empty;

        public int Int(int index, int fallback = 0) =>
            int.TryParse(Field(index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

        public int? IntOrNull(int index) =>
            int.TryParse(Field(index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

        public override string ToString() => Encode();

        // ------------------------------------------------------------ escaping

        /// <summary>Escapes the separator, the escape character itself and the line breaks.</summary>
        public static string Escape(string field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;
            if (field.IndexOfAny(new[] { '%', '|', '\n', '\r' }) < 0) return field;

            var text = new StringBuilder(field.Length + 8);
            foreach (var c in field)
            {
                switch (c)
                {
                    case '%': text.Append("%25"); break;
                    case '|': text.Append("%7C"); break;
                    case '\n': text.Append("%0A"); break;
                    case '\r': text.Append("%0D"); break;
                    default: text.Append(c); break;
                }
            }
            return text.ToString();
        }

        public static string Unescape(string field)
        {
            if (string.IsNullOrEmpty(field) || field.IndexOf('%') < 0) return field;

            var text = new StringBuilder(field.Length);
            for (var index = 0; index < field.Length; index++)
            {
                if (field[index] == '%' && index + 2 < field.Length
                    && Uri.IsHexDigit(field[index + 1]) && Uri.IsHexDigit(field[index + 2]))
                {
                    var code = Convert.ToInt32(field.Substring(index + 1, 2), 16);
                    text.Append((char)code);
                    index += 2;
                    continue;
                }
                text.Append(field[index]);
            }
            return text.ToString();
        }

        // ----------------------------------------------------------- factories

        public static WireMessage Hello(string name) => Of(WireKind.Hello, name);

        public static WireMessage Welcome(int seat, Wind wind, int dealer, int? seed, string rules, int events, int moves) =>
            Of(WireKind.Welcome, seat.ToString(CultureInfo.InvariantCulture),
                TileNotation.Format(TileType.WindTile(wind)), dealer.ToString(CultureInfo.InvariantCulture),
                seed?.ToString(CultureInfo.InvariantCulture) ?? "-", rules,
                events.ToString(CultureInfo.InvariantCulture), moves.ToString(CultureInfo.InvariantCulture));

        public static WireMessage ViewOf(int seat, int moveIndex, string projection) =>
            Of(WireKind.View, seat.ToString(CultureInfo.InvariantCulture),
                moveIndex.ToString(CultureInfo.InvariantCulture), projection);

        public static WireMessage ClockOf(SeatClock? clock, TimeSpan now) => clock == null
            ? Of(WireKind.Clock, "-", "-", "-")
            : Of(WireKind.Clock, clock.Seat.ToString(CultureInfo.InvariantCulture), clock.Window.ToString(),
                clock.IsLimited ? ((long)Math.Ceiling(clock.Remaining(now).TotalMilliseconds)).ToString(CultureInfo.InvariantCulture) : "-");

        public static WireMessage EventOf(int index, TableEvent entry) => Of(WireKind.Event,
            index.ToString(CultureInfo.InvariantCulture), entry.Kind, entry.Seat.ToString(CultureInfo.InvariantCulture),
            entry.Tile.HasValue ? TileNotation.Format(entry.Tile.Value) : "-", entry.Detail);

        public static WireMessage ActOf(int requestId, string wire) =>
            Of(WireKind.Act, requestId.ToString(CultureInfo.InvariantCulture), wire);

        public static WireMessage AcceptedOf(int requestId, int moveIndex) =>
            Of(WireKind.Accepted, requestId.ToString(CultureInfo.InvariantCulture), moveIndex.ToString(CultureInfo.InvariantCulture));

        public static WireMessage RejectedOf(int requestId, string reason) =>
            Of(WireKind.Rejected, requestId.ToString(CultureInfo.InvariantCulture), reason);

        public static WireMessage ResumeFrom(int eventsSeen) =>
            Of(WireKind.Resume, eventsSeen.ToString(CultureInfo.InvariantCulture));

        public static WireMessage ResumedFrom(int eventsFrom, int eventsTo, int moveIndex) => Of(WireKind.Resumed,
            eventsFrom.ToString(CultureInfo.InvariantCulture), eventsTo.ToString(CultureInfo.InvariantCulture),
            moveIndex.ToString(CultureInfo.InvariantCulture));

        public static WireMessage Notice(string text) => Of(WireKind.Notice, text);

        public static WireMessage Refuse(string reason) => Of(WireKind.Refused, reason);

        /// <summary>Transports the completed hand's settlement without exposing server state.</summary>
        public static WireMessage SettledOf(Settlement settlement)
        {
            var payments = string.Join(';', settlement.Payments.Select(payment =>
                string.Join(',', payment.FromPlayer, payment.ToPlayer, payment.Points)));
            return Of(WireKind.Settled,
                settlement.WinnerPlayer.ToString(CultureInfo.InvariantCulture),
                settlement.HandPoints.ToString(CultureInfo.InvariantCulture),
                settlement.WinnerGain.ToString(CultureInfo.InvariantCulture),
                settlement.SelfDrawn ? "self" : "discard",
                settlement.Explanation,
                payments);
        }

        /// <summary>Reads the move an <c>act</c> message carries back into the move intent.</summary>
        public static bool TryReadAct(WireMessage message, out int requestId, out string wire, out string error)
        {
            requestId = message.Int(0, -1);
            wire = message.Field(1);
            error = string.Empty;

            if (message.Kind != WireKind.Act)
            {
                error = $"not an act message: {message.Kind}";
                return false;
            }
            if (wire.Length == 0)
            {
                error = "the act message carries no move";
                return false;
            }
            return true;
        }
    }
}
