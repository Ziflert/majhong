using System;
using System.Globalization;
using Mahjong.Localization;

namespace Mahjong.Controls
{
    /// <summary>
    /// How much time the player gets for each decision of a hand (У-1). The rules give the windows
    /// themselves — twenty seconds to arrange the deal, ten to discard, three to claim (3.5.7.6, 3.6.4,
    /// 3.6.7) — so anything else is a house rule and the table has to say so.
    /// </summary>
    public enum ClockPace
    {
        /// <summary>The windows of the rulebook: 20 / 10 / 3 seconds.</summary>
        Rules = 0,

        /// <summary>Roomier windows for a relaxed table: 30 / 20 / 10 seconds.</summary>
        Calm = 1,

        /// <summary>No limit at all: a window stays open until the player acts.</summary>
        Unlimited = 2
    }

    /// <summary>
    /// The three windows of a hand as numbers. A zero window means "no limit", not "no time": the
    /// window opens and waits for the player instead of running out.
    ///
    /// Knows nothing about Unity or about the clock that runs it, so the presets are checked headless.
    /// </summary>
    public sealed record ClockLimits(TimeSpan Arrange, TimeSpan Discard, TimeSpan Claim)
    {
        /// <summary>Rules 3.5.7.6, 3.6.4 and 3.6.7: the windows a sporting table is played by.</summary>
        public static readonly ClockLimits Rules = new(
            TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3));

        /// <summary>A slower table: long enough to think about the first discard and about a claim.</summary>
        public static readonly ClockLimits Calm = new(
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10));

        /// <summary>Nothing runs out; the table waits for the player.</summary>
        public static readonly ClockLimits Unlimited = new(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

        /// <summary>True when no window of this set runs out at all.</summary>
        public bool IsUnlimited => Arrange <= TimeSpan.Zero && Discard <= TimeSpan.Zero && Claim <= TimeSpan.Zero;

        /// <summary>The limit of one window; <see cref="ClockWindow.Idle"/> never has one.</summary>
        public TimeSpan LimitOf(ClockWindow window) => window switch
        {
            ClockWindow.Arrange => Arrange,
            ClockWindow.Discard => Discard,
            ClockWindow.Claim => Claim,
            _ => TimeSpan.Zero
        };
    }

    /// <summary>
    /// The ladder of paces the menu and the welcome screen step through, and the text and parsing that
    /// go with it — one place, the way <see cref="ResultAdvanceSettings"/> owns the waiting time of the
    /// result screen.
    /// </summary>
    public static class ClockPaceSettings
    {
        /// <summary>The order of the rows: the rules' own windows, a slower table, and no limit.</summary>
        public static readonly ClockPace[] Steps = { ClockPace.Rules, ClockPace.Calm, ClockPace.Unlimited };

        /// <summary>A table is played by the rules until the player chooses otherwise.</summary>
        public const ClockPace Default = ClockPace.Rules;

        /// <summary>The windows a pace stands for.</summary>
        public static ClockLimits Limits(ClockPace pace) => pace switch
        {
            ClockPace.Calm => ClockLimits.Calm,
            ClockPace.Unlimited => ClockLimits.Unlimited,
            _ => ClockLimits.Rules
        };

        /// <summary>True for the pace the rulebook describes; anything else is a house rule.</summary>
        public static bool FollowsRules(ClockPace pace) => pace == ClockPace.Rules;

        /// <summary>How a row prints the setting: "по правилам (20/10/3)", "без ограничения".</summary>
        public static string Text(ClockPace pace) => Loc.Get(pace switch
        {
            ClockPace.Calm => "clock.pace.calm",
            ClockPace.Unlimited => "clock.pace.unlimited",
            _ => "clock.pace.rules"
        });

        /// <summary>Position of the pace on the ladder; an unknown value lands on the default.</summary>
        public static int IndexOf(ClockPace pace)
        {
            var index = Array.IndexOf(Steps, pace);
            return index >= 0 ? index : Array.IndexOf(Steps, Default);
        }

        /// <summary>Steps one notch along the ladder; it stops at both ends instead of wrapping.</summary>
        public static ClockPace Step(ClockPace pace, int delta)
        {
            if (delta == 0) return pace;
            var index = Math.Clamp(IndexOf(pace) + Math.Sign(delta), 0, Steps.Length - 1);
            return Steps[index];
        }

        /// <summary>
        /// A pace from a text: "rules" / "20/10/3", "calm" / "30/20/10", "unlimited" / "0". Null when
        /// the text means nothing, so the caller keeps its own default instead of guessing.
        /// </summary>
        public static ClockPace? TryParse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var text = raw.Trim().ToLowerInvariant();
            switch (text)
            {
                case "rules" or "sport" or "sports" or "default" or "по правилам":
                    return ClockPace.Rules;
                case "calm" or "slow" or "relaxed" or "спокойно":
                    return ClockPace.Calm;
                case "unlimited" or "none" or "off" or "no limit" or "без ограничения":
                    return ClockPace.Unlimited;
            }

            // A triple of seconds is the setting written out: "20/10/3" or "30/20/10".
            var parts = text.Split('/');
            if (parts.Length != 3) return null;

            var seconds = new int[3];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds[i]))
                    return null;
                if (seconds[i] < 0) return null;
            }

            if (new ClockLimits(TimeSpan.FromSeconds(seconds[0]), TimeSpan.FromSeconds(seconds[1]),
                    TimeSpan.FromSeconds(seconds[2])).IsUnlimited)
                return ClockPace.Unlimited;

            return seconds[0] == 20 && seconds[1] == 10 && seconds[2] == 3 ? ClockPace.Rules
                : seconds[0] == 30 && seconds[1] == 20 && seconds[2] == 10 ? ClockPace.Calm
                : (ClockPace?)null;
        }
    }
}
