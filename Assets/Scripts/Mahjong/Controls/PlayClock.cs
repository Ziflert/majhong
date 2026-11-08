using System;

namespace Mahjong.Controls
{
    /// <summary>Which timed window of a hand is running.</summary>
    public enum ClockWindow
    {
        /// <summary>Nobody is on the clock.</summary>
        Idle,

        /// <summary>Rule 3.5.7.6: twenty seconds to arrange the tiles before the first discard.</summary>
        Arrange,

        /// <summary>Rule 3.6.4: ten seconds to discard after a draw or a meld.</summary>
        Discard,

        /// <summary>Rule 3.6.7: three seconds to claim a discard.</summary>
        Claim
    }

    /// <summary>
    /// The timed windows of a hand (3.5.7.6, 3.6.4, 3.6.7): twenty seconds to arrange the deal, ten
    /// seconds to discard and three seconds to speak a claim. Timing only, no Unity: the view feeds it
    /// the frame delta and the tests feed it whatever they like, so "the window closes after three
    /// seconds" is checked without waiting three seconds.
    ///
    /// The clock never decides anything by itself — <see cref="ClockExpiry"/> turns an expired window
    /// into an engine action.
    /// </summary>
    public sealed class PlayClock
    {
        /// <summary>Rule 3.5.7.6: 20 seconds to arrange the tiles up to the first discard.</summary>
        public static readonly TimeSpan ArrangeLimit = ClockLimits.Rules.Arrange;

        /// <summary>Rule 3.6.4: a tile has to be discarded within 10 seconds.</summary>
        public static readonly TimeSpan DiscardLimit = ClockLimits.Rules.Discard;

        /// <summary>Rule 3.6.7: a pung or kong has to be spoken within 3 seconds of the discard.</summary>
        public static readonly TimeSpan ClaimLimit = ClockLimits.Rules.Claim;

        /// <summary>The limit the rules give to a window; <see cref="ClockWindow.Idle"/> has none.</summary>
        public static TimeSpan LimitOf(ClockWindow window) => ClockLimits.Rules.LimitOf(window);

        public ClockWindow Window { get; private set; } = ClockWindow.Idle;

        /// <summary>How long the current window lasts; zero when idle or when the window has no limit.</summary>
        public TimeSpan Limit { get; private set; }

        /// <summary>How long the current window has been open; clamped at the limit.</summary>
        public TimeSpan Elapsed { get; private set; }

        public bool IsRunning => Window != ClockWindow.Idle;

        /// <summary>
        /// True when the open window counts down. A window without a limit (<see cref="ClockLimits.Unlimited"/>)
        /// stays open until the player acts, so nothing about it ever runs out.
        /// </summary>
        public bool IsLimited => IsRunning && Limit > TimeSpan.Zero;

        /// <summary>Time left in the window; zero when idle, unlimited or already expired.</summary>
        public TimeSpan Remaining => !IsLimited || Elapsed >= Limit ? TimeSpan.Zero : Limit - Elapsed;

        /// <summary>True from the moment the limit is reached until the window is restarted or stopped.</summary>
        public bool IsExpired => IsLimited && Elapsed >= Limit;

        /// <summary>How much of the window is used up, from zero to one; used by UI strips.</summary>
        public double Fraction => !IsLimited ? 0d : Math.Min(1d, Elapsed.TotalSeconds / Limit.TotalSeconds);

        /// <summary>Seconds left, rounded up, for a countdown line: 3, 2, 1, 0.</summary>
        public int SecondsLeft => IsLimited ? (int)Math.Ceiling(Remaining.TotalSeconds) : 0;

        /// <summary>Opens a window with the limit the rules give it, discarding any previous window.</summary>
        public void Restart(ClockWindow window) => Restart(window, LimitOf(window));

        /// <summary>
        /// Opens a window with the limits the player chose (У-1). A zero limit opens the window without
        /// a countdown: the table then waits for the player instead of playing for him.
        /// </summary>
        public void Restart(ClockWindow window, ClockLimits limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));

            var limit = limits.LimitOf(window);
            if (limit > TimeSpan.Zero) Restart(window, limit);
            else Set(window, TimeSpan.Zero);
        }

        /// <summary>Opens a window with an explicit limit; tests and shortened demos use this.</summary>
        public void Restart(ClockWindow window, TimeSpan limit)
        {
            if (limit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(limit));
            Set(window, limit);
        }

        private void Set(ClockWindow window, TimeSpan limit)
        {
            if (window == ClockWindow.Idle) throw new ArgumentOutOfRangeException(nameof(window), "An idle window has no limit.");

            Window = window;
            Limit = limit;
            Elapsed = TimeSpan.Zero;
        }

        /// <summary>Closes the window; the clock is no longer running and no longer expired.</summary>
        public void Stop()
        {
            Window = ClockWindow.Idle;
            Limit = TimeSpan.Zero;
            Elapsed = TimeSpan.Zero;
        }

        /// <summary>
        /// Advances the window by <paramref name="delta"/>. Returns true only on the tick that crosses
        /// the limit, so the caller acts once and the following ticks stay quiet. A negative or zero
        /// delta does nothing.
        /// </summary>
        public bool Tick(TimeSpan delta)
        {
            if (!IsLimited || delta <= TimeSpan.Zero || IsExpired) return false;

            Elapsed += delta;
            if (Elapsed < Limit) return false;

            Elapsed = Limit;
            return true;
        }

        public override string ToString() => !IsRunning ? "idle"
            : IsLimited ? $"{Window} {SecondsLeft}s/{Limit.TotalSeconds:0}s"
            : $"{Window} without a limit";
    }
}
