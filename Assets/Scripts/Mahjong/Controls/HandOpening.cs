using System;
using Mahjong.Rules;

namespace Mahjong.Controls
{
    /// <summary>The beats of the opening: two dice throws, the break, then the deal (3.5.7.5).</summary>
    public enum OpeningStep
    {
        FirstRoll = 0,
        SecondRoll = 1,
        Break = 2,
        Deal = 3,
        Done = 4
    }

    /// <summary>
    /// The opening of a hand as timing and nothing else: how long each beat lasts, which one is
    /// running, and how long the table still has to wait before the tiles may leave the wall.
    ///
    /// The engine deals at once, because the rules care about the result and not about the show. The
    /// table needs the show: dice are thrown twice, the wall is broken, and only then are the tiles
    /// taken. Keeping the timing here — away from Unity — means the order of the beats is checked
    /// headless and the play-mode tests only have to look at the screen.
    /// </summary>
    public sealed class HandOpening
    {
        /// <summary>How long each beat lasts, in seconds: first throw, second throw, break, deal.</summary>
        public static readonly double[] BeatSeconds = { 0.9, 0.9, 0.6, 1.1 };

        /// <summary>Beat lengths can be scaled for a capture run or a test; 1 is the table's own pace.</summary>
        public double Pace { get; set; } = 1.0;

        /// <summary>
        /// The table takes a speed, not a length: <c>MAHJONG_OPENING_SPEED=2</c> shows the opening twice
        /// as fast and <c>0</c> skips it. Turning a speed into <see cref="Pace"/> in one place keeps the
        /// table and the tests from disagreeing about which way the number goes.
        /// </summary>
        public static double PaceFromSpeed(double speed) => speed > 0d ? 1d / speed : 0d;

        public HandOpening(DiceRoll first, DiceRoll second, int thrower, int breakStacks)
        {
            First = first;
            Second = second;
            Thrower = thrower;
            BreakStacks = breakStacks;
        }

        /// <summary>
        /// An opening that is already over: the state a table holds before its first hand, so nothing
        /// that reads the opening has to guard against null. <see cref="Skip"/> is the way to reach it.
        /// </summary>
        public static HandOpening Finished()
        {
            var opening = new HandOpening(new DiceRoll(1, 1), new DiceRoll(1, 1), 0, 0);
            opening.Skip();
            return opening;
        }

        public DiceRoll First { get; }
        public DiceRoll Second { get; }

        /// <summary>Seat that threw the dice the second time: the first sum counts round the table.</summary>
        public int Thrower { get; }

        /// <summary>Stacks counted from that seat's right end before the wall breaks.</summary>
        public int BreakStacks { get; }

        public OpeningStep Step { get; private set; } = OpeningStep.FirstRoll;

        /// <summary>Seconds spent in the current beat; the table animates the dice from it.</summary>
        public double ElapsedInStep { get; private set; }

        public bool IsDone => Step == OpeningStep.Done;

        /// <summary>True while the dice are still being thrown and no tile may leave the wall.</summary>
        public bool IsRunning => !IsDone;

        /// <summary>Length of the running beat, in seconds.</summary>
        public double StepLength => IsDone ? 0d : BeatSeconds[(int)Step] * Pace;

        /// <summary>How much of the running beat is left.</summary>
        public double StepRemaining => Math.Max(0d, StepLength - ElapsedInStep);

        /// <summary>How long the table still waits before the first tile is taken from the wall.</summary>
        public double TimeUntilDeal
        {
            get
            {
                var wait = 0d;
                for (var step = (int)Step; step < (int)OpeningStep.Deal; step++)
                {
                    var length = BeatSeconds[step] * Pace;
                    wait += step == (int)Step ? Math.Max(0d, length - ElapsedInStep) : length;
                }

                return wait;
            }
        }

        /// <summary>
        /// Moves the opening forward. Returns true when the beat changed, so the caller can react once
        /// per beat instead of every frame.
        /// </summary>
        public bool Advance(double seconds)
        {
            if (IsDone || seconds <= 0d) return false;

            var changed = false;
            ElapsedInStep += seconds;
            while (!IsDone && ElapsedInStep >= StepLength)
            {
                ElapsedInStep -= StepLength;
                Step = (OpeningStep)((int)Step + 1);
                changed = true;
                if (IsDone) ElapsedInStep = 0d;
            }

            return changed;
        }

        /// <summary>Jumps to the end without waiting: a capture run and a test must not sleep.</summary>
        public void Skip()
        {
            Step = OpeningStep.Done;
            ElapsedInStep = 0d;
        }

        /// <summary>Seconds into the current beat, clamped: the dice animation reads it as 0..1.</summary>
        public double Progress => IsDone || StepLength <= 0d ? 1d : Math.Clamp(ElapsedInStep / StepLength, 0d, 1d);
    }
}
