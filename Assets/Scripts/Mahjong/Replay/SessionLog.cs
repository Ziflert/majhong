using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mahjong.Net;
using Mahjong.Rules;

namespace Mahjong.Replay
{
    /// <summary>
    /// The saved game: the move log of every hand together with what each hand was dealt from.
    ///
    /// The file is the wire format of the network contract plus one header line per hand, so a save
    /// reads as text and a hand replays from its seed alone — the wall is never stored (see
    /// <see cref="AuthoritativeTable.Replay"/>). Nothing here depends on Unity, so the save format is
    /// checked headless together with the rest of the network contract.
    /// </summary>
    public sealed class SessionLog
    {
        /// <summary>The first line of every save file.</summary>
        public const string Header = "majhong-save v1";

        /// <summary>One dealt hand: where the deal came from and what the players did with it.</summary>
        public sealed class HandRecord
        {
            public HandRecord(int dealer, int? seed, Wind roundWind)
            {
                Dealer = dealer;
                Seed = seed;
                RoundWind = roundWind;
            }

            public int Dealer { get; }
            public int? Seed { get; }
            public Wind RoundWind { get; }

            /// <summary>Accepted moves in order, exactly as the table accepted them.</summary>
            public List<(int Seat, string Wire)> Moves { get; } = new();

            /// <summary>Free-form comment lines that travelled with this hand; kept on a round trip.</summary>
            public List<string> Notes { get; } = new();

            /// <summary>The hand as the replay consumes it: one <c>seat|wire</c> line per move.</summary>
            public IEnumerable<string> LogLines() => Moves.Select(move => $"{move.Seat}|{move.Wire}");
        }

        /// <summary>
        /// Session-wide facts a resumed game needs: the clock as it stood at the save and the limit it
        /// runs against (rule 3.5.6). Scores are not stored — they are recomputed from the replayed
        /// hands, so a save cannot claim a score its moves do not produce.
        /// </summary>
        public sealed record SessionInfo(TimeSpan Elapsed, TimeSpan TimeLimit);

        private readonly List<HandRecord> hands = new();
        private readonly List<string> notes = new();

        /// <summary>Session-wide facts; null for a file written before they were stored.</summary>
        public SessionInfo? Info { get; set; }

        public IReadOnlyList<HandRecord> Hands => hands;

        /// <summary>Comment lines that sit before the first hand; kept on a round trip.</summary>
        public IReadOnlyList<string> Notes => notes;

        public int MoveCount => hands.Sum(hand => hand.Moves.Count);
        public bool IsEmpty => hands.Count == 0;

        /// <summary>The hand moves are recorded into; null before the first deal.</summary>
        public HandRecord? CurrentHand => hands.Count == 0 ? null : hands[^1];

        /// <summary>Opens a hand record; moves recorded next belong to this deal.</summary>
        public void BeginHand(int dealer, int? seed, Wind roundWind) =>
            hands.Add(new HandRecord(dealer, seed, roundWind));

        /// <summary>Records one applied action in the hand being played.</summary>
        public void Record(int seat, LegalAction action) => RecordWire(seat, ActionCodec.Encode(action));

        /// <summary>Records one move as its wire line.</summary>
        public void RecordWire(int seat, string wire)
        {
            if (hands.Count == 0)
                throw new InvalidOperationException("a move needs a dealt hand: call BeginHand first");
            if (seat is < 0 or > 3)
                throw new ArgumentOutOfRangeException(nameof(seat), $"seat {seat} does not exist");
            hands[^1].Moves.Add((seat, wire));
        }

        /// <summary>The whole save as text; parsing this text back gives an identical log.</summary>
        public string SaveText()
        {
            var lines = new List<string> { Header };
            if (Info != null)
                lines.Add($"session|elapsed={Seconds(Info.Elapsed)}|limit={Seconds(Info.TimeLimit)}");
            lines.AddRange(notes.Select(note => "# " + note));
            foreach (var hand in hands)
            {
                var seed = hand.Seed?.ToString(CultureInfo.InvariantCulture) ?? "random";
                lines.Add($"hand|{hand.Dealer}|{seed}|{hand.RoundWind}");
                lines.AddRange(hand.Notes.Select(note => "# " + note));
                lines.AddRange(hand.LogLines());
            }

            return string.Join("\n", lines) + "\n";
        }

        /// <summary>
        /// Reads a save. A broken file is refused with the line that broke it, because a save that
        /// silently loses moves is worse than one that does not load.
        /// </summary>
        public static SessionLog Parse(string text)
        {
            var log = new SessionLog();
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var sawHeader = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var number = i + 1;
                var line = lines[i].Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("#"))
                {
                    var note = line.Substring(1).Trim();
                    (log.CurrentHand?.Notes ?? log.notes).Add(note);
                    continue;
                }

                if (!sawHeader)
                {
                    if (line != Header)
                        throw new FormatException($"line {number}: not a mahjong save (expected '{Header}')");
                    sawHeader = true;
                    continue;
                }

                if (line.StartsWith("hand|"))
                {
                    log.hands.Add(ParseHand(line, number));
                    continue;
                }

                if (line.StartsWith("session|"))
                {
                    log.Info = ParseSession(line, number);
                    continue;
                }

                var parts = line.Split('|');
                if (parts.Length != 4)
                    throw new FormatException($"line {number}: not a move (expected 'seat|kind|tile|required')");
                if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seat)
                    || seat is < 0 or > 3)
                    throw new FormatException($"line {number}: '{parts[0]}' is not a seat");

                var wire = $"{parts[1]}|{parts[2]}|{parts[3]}";
                if (!ActionCodec.TryDecode(wire, out _, out var error))
                    throw new FormatException($"line {number}: {error}");

                if (log.CurrentHand == null)
                    throw new FormatException($"line {number}: a move before any deal");
                log.RecordWire(seat, wire);
            }

            if (!sawHeader)
                throw new FormatException("not a mahjong save: no header line");
            return log;
        }

        public void Save(string path)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, SaveText());
        }

        public static SessionLog Load(string path) => Parse(File.ReadAllText(path));

        /// <summary>Rebuilds every hand from its seed and returns the finished tables, one per hand.</summary>
        public IReadOnlyList<AuthoritativeTable> Replay()
        {
            var tables = new List<AuthoritativeTable>();
            for (var i = 0; i < hands.Count; i++) tables.Add(ReplayHand(i));
            return tables;
        }

        /// <summary>
        /// Rebuilds one hand from its seed. A move the engine would not have accepted stops the
        /// replay loudly: a save that replays into a different game is a broken save.
        /// </summary>
        public AuthoritativeTable ReplayHand(int index)
        {
            if (index < 0 || index >= hands.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"there is no hand {index + 1}");
            var hand = hands[index];
            try
            {
                return AuthoritativeTable.Replay(hand.Dealer, hand.Seed, hand.RoundWind, hand.LogLines());
            }
            catch (Exception failure) when (failure is FormatException or InvalidOperationException)
            {
                throw new InvalidOperationException($"hand {index + 1} does not replay: {failure.Message}", failure);
            }
        }

        private static string Seconds(TimeSpan span) =>
            span.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>
        /// Reads the session line: <c>session|elapsed=seconds|limit=seconds</c>. Keys that this version
        /// does not know are skipped, so an older build can still read a newer save.
        /// </summary>
        private static SessionInfo ParseSession(string line, int number)
        {
            var elapsed = GameSession.DefaultTimeLimit;
            var limit = GameSession.DefaultTimeLimit;
            var sawElapsed = false;

            foreach (var field in line.Split('|').Skip(1))
            {
                var parts = field.Split('=', 2);
                if (parts.Length != 2) continue;
                if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var seconds) || seconds < 0)
                    throw new FormatException($"line {number}: '{parts[1]}' is not a number of seconds");

                switch (parts[0].Trim())
                {
                    case "elapsed":
                        elapsed = TimeSpan.FromSeconds(seconds);
                        sawElapsed = true;
                        break;
                    case "limit":
                        limit = TimeSpan.FromSeconds(seconds);
                        break;
                }
            }

            if (!sawElapsed)
                throw new FormatException($"line {number}: the session line has no elapsed time");
            return new SessionInfo(elapsed, limit);
        }

        private static HandRecord ParseHand(string line, int number)
        {
            var parts = line.Split('|');
            if (parts.Length != 4)
                throw new FormatException($"line {number}: not a hand header (expected 'hand|dealer|seed|wind')");

            if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dealer)
                || dealer is < 0 or > 3)
                throw new FormatException($"line {number}: '{parts[1]}' is not a dealer seat");

            var seedText = parts[2].Trim();
            int? seed = null;
            if (seedText.Length > 0 && !seedText.Equals("random", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    throw new FormatException($"line {number}: '{seedText}' is not a seed");
                seed = parsed;
            }

            if (!Enum.TryParse(parts[3].Trim(), true, out Wind roundWind))
                throw new FormatException($"line {number}: '{parts[3]}' is not a round wind");

            return new HandRecord(dealer, seed, roundWind);
        }
    }
}
