using System;
using System.Globalization;
using Mahjong.Localization;
using Mahjong.Rules;

namespace Mahjong.Controls
{
    /// <summary>
    /// How long a game session runs (8.7). The rulebook gives a full game — four rounds of four hands —
    /// one hundred and fifty minutes (3.5.6), so a shorter table is a house rule and has to say so.
    /// A zero limit is a table without a clock at all: the sixteen hands decide the game, nothing else.
    ///
    /// Knows nothing about Unity, so the ladder, the text and the parsing are checked headless; the
    /// welcome screen only shows <see cref="Text"/> and steps <see cref="Step"/>.
    /// </summary>
    public static class SessionLengthSettings
    {
        /// <summary>The ladder the welcome screen steps through: the rules' own length down to no clock.</summary>
        public static readonly TimeSpan[] Steps =
        {
            GameSession.DefaultTimeLimit,
            TimeSpan.FromMinutes(90),
            TimeSpan.FromMinutes(60),
            TimeSpan.FromMinutes(30),
            TimeSpan.Zero
        };

        /// <summary>A table is played by the rules until the player chooses otherwise.</summary>
        public static readonly TimeSpan Default = GameSession.DefaultTimeLimit;

        /// <summary>True when the session has no clock: the table plays all sixteen hands and stops.</summary>
        public static bool IsUnlimited(TimeSpan limit) => limit <= TimeSpan.Zero;

        /// <summary>True for the length the rulebook gives a session (3.5.6); anything else is a house rule.</summary>
        public static bool FollowsRules(TimeSpan limit) => !IsUnlimited(limit) && limit == GameSession.DefaultTimeLimit;

        /// <summary>How a row prints the setting: "по правилам (150 мин)", "60 мин", "без ограничения".</summary>
        public static string Text(TimeSpan limit)
        {
            if (IsUnlimited(limit)) return Loc.Get("session.limit.unlimited");

            var minutes = MinutesText(limit);
            return FollowsRules(limit)
                ? Loc.Format("session.limit.rules", minutes)
                : Loc.Format("session.limit.minutes", minutes);
        }

        /// <summary>Position of a length on the ladder: the same step, or the nearest one.</summary>
        public static int IndexOf(TimeSpan limit)
        {
            var best = 0;
            for (var index = 0; index < Steps.Length; index++)
            {
                if (Steps[index] == limit) return index;
                if (Math.Abs((Steps[index] - limit).TotalSeconds) < Math.Abs((Steps[best] - limit).TotalSeconds))
                    best = index;
            }
            return best;
        }

        /// <summary>Steps one notch along the ladder; it stops at both ends instead of wrapping.</summary>
        public static TimeSpan Step(TimeSpan limit, int delta)
        {
            if (delta == 0) return limit;
            var index = Math.Clamp(IndexOf(limit) + Math.Sign(delta), 0, Steps.Length - 1);
            return Steps[index];
        }

        /// <summary>
        /// A length from a text: "rules" / "150", "90", "60", "30", "unlimited" / "0", and minutes written
        /// out — "0.2", "20min", "45 мин". A fraction of a minute is allowed so a capture run can watch the
        /// clock run out in seconds. Null when the text means nothing, so the caller keeps its own default.
        /// </summary>
        public static TimeSpan? TryParse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var text = raw.Trim().ToLowerInvariant();
            switch (text)
            {
                case "rules" or "sport" or "sports" or "default" or "по правилам":
                    return Default;
                case "unlimited" or "none" or "off" or "no limit" or "clockless" or "без ограничения":
                    return TimeSpan.Zero;
            }

            // "20min", "20 минут" and "20m" are the same number of minutes as "20".
            foreach (var suffix in new[] { "minutes", "minute", "mins", "min", "минут", "мин", "m", "м" })
            {
                if (!text.EndsWith(suffix, StringComparison.Ordinal)) continue;
                text = text[..^suffix.Length].Trim();
                break;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes))
                return null;
            if (minutes < 0) return null;
            return minutes == 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(minutes);
        }

        /// <summary>The length as the row prints it: whole minutes for the ladder, exact minutes otherwise.</summary>
        private static string MinutesText(TimeSpan limit) =>
            limit.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
