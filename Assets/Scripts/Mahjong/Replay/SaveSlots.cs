using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mahjong.Localization;
using Mahjong.Rules;

namespace Mahjong.Replay
{
    /// <summary>
    /// One saved game as a line a screen can show: where the file is, what is in it and whether it can
    /// be opened at all. The welcome screen lists these rows for "load game", and the "continue" row
    /// takes the newest one, so both read the same catalogue instead of scanning the folder twice.
    ///
    /// No Unity here: the folder is passed in, and the checks that pin the listing down run headless.
    /// </summary>
    public sealed record SaveSlot(string Path, DateTime? Written, int Hands, int Moves,
        RuleVariant Variant, string Problem)
    {
        /// <summary>File name without the extension; that is what a player recognises a save by.</summary>
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

        /// <summary>True when the file parses and holds at least one dealt hand.</summary>
        public bool IsOpenable => Problem.Length == 0 && Hands > 0;

        /// <summary>"5 рук · Классика 144" — what the row shows next to the file name.</summary>
        public string Summary() => IsOpenable
            ? $"{HandsText(Hands)} · {(Loc.Current == Language.Ru ? Variant.NameRu : Variant.NameEn)}"
            : Loc.Get("title.save.broken");

        /// <summary>"1 рука", "2 руки", "5 рук" — a count a player reads without counting twice.</summary>
        public static string HandsText(int hands)
        {
            if (Loc.Current != Language.Ru)
                return Loc.Format(hands == 1 ? "title.save.hand" : "title.save.hands", hands);

            // Russian counts by the last digit, except the teens: 11..14 are all "рук".
            var teens = hands % 100 is >= 11 and <= 14;
            var tail = hands % 10;
            return Loc.Format(teens
                    ? "title.save.hands"
                    : tail switch
                    {
                        1 => "title.save.hand",
                        2 or 3 or 4 => "title.save.hands234",
                        _ => "title.save.hands"
                    },
                hands);
        }

        public override string ToString() => $"{Name}: {Summary()}";
    }

    /// <summary>
    /// The saves of a folder as the welcome screen sees them. A file that cannot be read is listed
    /// rather than hidden: a player looking for a save that disappeared should see why it is not there.
    /// </summary>
    public static class SaveSlots
    {
        /// <summary>Extension of a saved game; <see cref="SessionLog"/> writes plain text under it.</summary>
        public const string Extension = ".mahjong";

        /// <summary>Every save in the folder, newest first; an empty list when the folder is not there.</summary>
        public static IReadOnlyList<SaveSlot> Scan(string directory)
        {
            var slots = new List<SaveSlot>();
            if (string.IsNullOrWhiteSpace(directory)) return slots;

            string[] files;
            try
            {
                if (!Directory.Exists(directory)) return slots;
                files = Directory.GetFiles(directory, "*" + Extension);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                return slots;
            }

            foreach (var file in files) slots.Add(Read(file));

            // Newest first: "continue" is the top row and the first thing a player looks at.
            return slots
                .OrderByDescending(slot => slot.Written ?? DateTime.MinValue)
                .ThenBy(slot => slot.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>The newest save that can actually be opened, or an empty path when there is none.</summary>
        public static string Newest(string directory) =>
            Scan(directory).FirstOrDefault(slot => slot.IsOpenable)?.Path ?? string.Empty;

        /// <summary>
        /// One save read as a catalogue entry. A file that does not parse keeps the reason, so the
        /// screen can say "broken" instead of quietly offering a row that would fail when picked.
        /// </summary>
        public static SaveSlot Read(string path)
        {
            DateTime? written;
            try
            {
                written = File.GetLastWriteTimeUtc(path);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                written = null;
            }


            try
            {
                var log = SessionLog.Load(path);
                return new SaveSlot(path, written, log.Hands.Count, log.MoveCount,
                    log.Variant, log.Hands.Count > 0 ? string.Empty : "no hands");
            }
            catch (Exception failure)
                when (failure is IOException or FormatException or UnauthorizedAccessException
                      or ArgumentException or NotSupportedException)
            {
                return new SaveSlot(path, written, 0, 0, RuleVariant.Default, failure.Message);
            }
        }
    }
}
