using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mahjong.Controls;

namespace Mahjong.Net
{
    /// <summary>
    /// The window one seat has to answer in, as the server counts it: which kind of decision it is and
    /// when it runs out. The deadline is a reading of the server clock, never of the client's, so a
    /// player cannot buy time by turning his own clock back (M1 of docs/multiplayer-draft.md).
    /// </summary>
    public sealed record SeatClock(int Seat, ClockWindow Window, TimeSpan? Deadline)
    {
        /// <summary>True when the window counts down; an unlimited window waits for the seat instead.</summary>
        public bool IsLimited => Deadline.HasValue;

        /// <summary>Time left in the window; zero when unlimited or already run out.</summary>
        public TimeSpan Remaining(TimeSpan now) =>
            Deadline.HasValue && Deadline.Value > now ? Deadline.Value - now : TimeSpan.Zero;

        /// <summary>True from the moment the deadline is reached.</summary>
        public bool HasRunOut(TimeSpan now) => Deadline.HasValue && now >= Deadline.Value;

        /// <summary>Seconds left, rounded up, for a countdown line: 3, 2, 1, 0.</summary>
        public int SecondsLeft(TimeSpan now) => (int)Math.Ceiling(Remaining(now).TotalSeconds);

        public override string ToString() => IsLimited
            ? $"P{Seat} {Window} until {Deadline!.Value.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}s"
            : $"P{Seat} {Window} without a limit";
    }

    /// <summary>
    /// The clocks of the table, one window per seat: twenty seconds to arrange the deal, ten to discard,
    /// three to speak a claim (3.5.7.6, 3.6.4, 3.6.7) — or whatever pace the table was set up with
    /// (У-1). Two seats are usually on the clock at once, because a claim window is open to all four
    /// seats at the same time and each of them may speak.
    ///
    /// The clock decides nothing by itself: <see cref="TableServer"/> asks it who has run out and lets
    /// <see cref="ClockExpiry"/> pick the move, exactly as the single-machine game does.
    /// </summary>
    public sealed class TableClock
    {
        public const int Seats = 4;

        private readonly SeatClock?[] seats = new SeatClock?[Seats];

        public TableClock(ClockLimits? limits = null) => Limits = limits ?? ClockLimits.Rules;

        /// <summary>The windows this table runs by; <see cref="ClockLimits.Unlimited"/> never runs out.</summary>
        public ClockLimits Limits { get; }

        /// <summary>True when no window of this table ever runs out.</summary>
        public bool IsUnlimited => Limits.IsUnlimited;

        /// <summary>The seats that are on the clock right now, in seat order.</summary>
        public IReadOnlyList<SeatClock> Armed =>
            seats.Where(seat => seat != null).Select(seat => seat!).ToList();

        /// <summary>
        /// Opens (or restarts) the window of one seat: the deadline is <paramref name="now"/> plus the
        /// limit of that kind of decision. An unlimited table opens the window with no deadline.
        /// </summary>
        public void Arm(int seat, ClockWindow window, TimeSpan now)
        {
            if (seat is < 0 or >= Seats) throw new ArgumentOutOfRangeException(nameof(seat));
            if (window == ClockWindow.Idle) throw new ArgumentOutOfRangeException(nameof(window), "An idle window has no limit.");

            var limit = Limits.LimitOf(window);
            seats[seat] = limit > TimeSpan.Zero
                ? new SeatClock(seat, window, now + limit)
                : new SeatClock(seat, window, null);
        }

        public void Disarm(int seat)
        {
            if (seat is < 0 or >= Seats) throw new ArgumentOutOfRangeException(nameof(seat));
            seats[seat] = null;
        }

        public void DisarmAll()
        {
            for (var seat = 0; seat < Seats; seat++) seats[seat] = null;
        }

        public bool IsArmed(int seat) => seat is >= 0 and < Seats && seats[seat] != null;

        public SeatClock? Of(int seat) => seat is >= 0 and < Seats ? seats[seat] : null;

        /// <summary>
        /// The windows that have run out at <paramref name="now"/>, in seat order. A window that runs
        /// out stays armed until someone disarms it, so a caller that forgets to act sees it again on
        /// the next tick — that is deliberate: silence must not be able to stop the table.
        /// </summary>
        public IReadOnlyList<SeatClock> RunOut(TimeSpan now) =>
            Armed.Where(clock => clock.HasRunOut(now)).ToList();

        /// <summary>The clocks as text lines, for a log; contains no hidden information.</summary>
        public string Describe(TimeSpan now) =>
            string.Join("\n", Armed.Select(clock => clock.IsLimited
                ? $"clock|P{clock.Seat}|{clock.Window}|{clock.Remaining(now).TotalMilliseconds:0}"
                : $"clock|P{clock.Seat}|{clock.Window}|-"));
    }
}
