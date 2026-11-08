using System;
using System.Collections.Generic;
using Mahjong.Localization;

namespace Mahjong.Controls
{
    /// <summary>
    /// How long the result screen waits before it deals the next hand. The values are the number of
    /// seconds, so a setting read from a file or an environment variable has nothing to look up;
    /// <see cref="Manual"/> means the table waits for the player's "next hand" button and nothing else.
    /// </summary>
    public enum ResultAdvance
    {
        Manual = 0,
        Three = 3,
        Five = 5,
        Ten = 10
    }

    /// <summary>
    /// The ladder of waiting times the menu steps through, and the text and parsing that go with it.
    /// One place decides what "3" means, the same way <see cref="GameSettings"/> owns the bot speed.
    /// </summary>
    public static class ResultAdvanceSettings
    {
        /// <summary>The order of the menu row: three seconds, five, ten, and then only by button.</summary>
        public static readonly ResultAdvance[] Steps =
        {
            ResultAdvance.Three, ResultAdvance.Five, ResultAdvance.Ten, ResultAdvance.Manual
        };

        /// <summary>Five seconds: long enough to read a classic settlement, short enough to play on.</summary>
        public const ResultAdvance Default = ResultAdvance.Five;

        public static float Seconds(ResultAdvance advance) =>
            advance == ResultAdvance.Manual ? 0f : (int)advance;

        /// <summary>How the menu row prints the setting: "5 с" or "по кнопке".</summary>
        public static string Text(ResultAdvance advance) => advance == ResultAdvance.Manual
            ? Loc.Get("menu.value.manual")
            : Loc.Format("menu.value.seconds", (int)advance);

        /// <summary>Position of the setting on the ladder; an unknown value lands on the default.</summary>
        public static int IndexOf(ResultAdvance advance)
        {
            var index = Array.IndexOf(Steps, advance);
            return index >= 0 ? index : Array.IndexOf(Steps, Default);
        }

        /// <summary>Steps one notch along the ladder; it stops at both ends instead of wrapping.</summary>
        public static ResultAdvance Step(ResultAdvance advance, int delta)
        {
            if (delta == 0) return advance;
            var index = Math.Clamp(IndexOf(advance) + Math.Sign(delta), 0, Steps.Length - 1);
            return Steps[index];
        }

        /// <summary>
        /// A setting from a text: "0", "3", "5", "10", "manual", "off" or "3s". Null when the text means
        /// nothing, so the caller keeps its own default instead of guessing.
        /// </summary>
        public static ResultAdvance? TryParse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var text = raw.Trim().ToLowerInvariant();
            switch (text)
            {
                case "manual" or "off" or "none" or "button" or "кнопка":
                    return ResultAdvance.Manual;
            }

            // "3s" and "3 с" are the same number of seconds as "3".
            text = text.TrimEnd('s', 'с', ' ').TrimEnd();
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                return null;
            if (seconds <= 0) return ResultAdvance.Manual;

            foreach (var step in new[] { ResultAdvance.Three, ResultAdvance.Five, ResultAdvance.Ten })
                if (Math.Abs((int)step - seconds) < 0.001) return step;

            return null;
        }
    }

    /// <summary>
    /// What the result screen of one hand says: the winner, the breakdown of the hand as it was paid,
    /// and — after the last hand of a round or of the session — the table of points.
    /// <see cref="CanContinue"/> is false once there is no next hand left to deal.
    /// </summary>
    public sealed record HandResultContent(
        string Title,
        IReadOnlyList<string> Lines,
        IReadOnlyList<string> Standings,
        bool CanContinue);

    /// <summary>
    /// The end-of-hand screen as a model: what it says, how long it waits and when it is over. The
    /// panel only draws what this returns, so the behaviour — a countdown that pauses with the rest of
    /// the table, a hand with no next deal to go to — is checked headless, without a scene.
    /// </summary>
    public sealed class ResultScreen
    {
        public bool IsOpen { get; private set; }

        /// <summary>The waiting time this screen was opened with; a setting changed later applies to the next hand.</summary>
        public ResultAdvance Advance { get; private set; } = ResultAdvanceSettings.Default;

        /// <summary>Seconds left before the next deal; zero in <see cref="ResultAdvance.Manual"/>.</summary>
        public float SecondsLeft { get; private set; }

        public string Title { get; private set; } = string.Empty;

        /// <summary>The hand as it was paid: the breakdown, the transfers and the points after the hand.</summary>
        public IReadOnlyList<string> Lines { get; private set; } = Array.Empty<string>();

        /// <summary>The table of points, filled only at the end of a round or of the session.</summary>
        public IReadOnlyList<string> Standings { get; private set; } = Array.Empty<string>();

        /// <summary>False on the last hand of the session: there is nothing left to deal.</summary>
        public bool CanContinue { get; private set; } = true;

        /// <summary>True when the screen counts down at all; the button works either way.</summary>
        public bool CountsDown => IsOpen && Advance != ResultAdvance.Manual;

        /// <summary>True when the wait is over and the next hand should be dealt.</summary>
        public bool Expired => CountsDown && SecondsLeft <= 0f;

        /// <summary>Caption of the button that deals the next hand.</summary>
        public string Button => Loc.Get("result.next");

        /// <summary>What the keys do, said differently while the table waits or has nothing left to deal.</summary>
        public string Hint => Loc.Get(!CanContinue
            ? "result.hintLast"
            : Advance == ResultAdvance.Manual ? "result.hintManual" : "result.hint");

        /// <summary>The countdown as it is drawn: "Дальше через 4 с" or "переход по кнопке".</summary>
        public string Countdown => CountsDown
            ? Loc.Format("result.countdown", Math.Max(0, (int)Math.Ceiling(SecondsLeft)))
            : Loc.Get("result.manual");

        /// <summary>Opens the screen with the content of a finished hand and starts the wait.</summary>
        public void Show(HandResultContent content, ResultAdvance advance)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));

            Title = content.Title;
            Lines = content.Lines;
            Standings = content.Standings;
            CanContinue = content.CanContinue;
            Advance = advance;
            SecondsLeft = ResultAdvanceSettings.Seconds(advance);
            IsOpen = true;
        }

        /// <summary>Closes the screen; the table deals the next hand from here.</summary>
        public void Hide()
        {
            IsOpen = false;
            SecondsLeft = 0f;
        }

        /// <summary>
        /// Replaces the text of an open screen without touching its countdown: the language can be
        /// switched while a hand is still on the screen, and a half-read wait must not start over.
        /// </summary>
        public void Relocalize(HandResultContent content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (!IsOpen) return;

            Title = content.Title;
            Lines = content.Lines;
            Standings = content.Standings;
            CanContinue = content.CanContinue;
        }

        /// <summary>
        /// Runs the wait; true when it has just run out. The caller stops calling when the table is
        /// paused (menu, rulebook, review), which is what keeps the screen on the same second.
        /// </summary>
        public bool Tick(float deltaSeconds)
        {
            if (!CountsDown) return false;
            if (deltaSeconds > 0f) SecondsLeft = Math.Max(0f, SecondsLeft - deltaSeconds);
            return SecondsLeft <= 0f;
        }

        /// <summary>Every text line of the screen in drawing order: the hand, then the table of points.</summary>
        public IReadOnlyList<string> AllLines()
        {
            var lines = new List<string>(Lines.Count + Standings.Count);
            lines.AddRange(Lines);
            lines.AddRange(Standings);
            return lines;
        }
    }
}
