using System;
using System.Collections.Generic;

namespace Mahjong.Rules
{
    /// <summary>Two dice thrown by the dealer, and the second throw used to find the break.</summary>
    public readonly struct DiceRoll
    {
        public DiceRoll(int first, int second)
        {
            First = first;
            Second = second;
        }

        public int First { get; }
        public int Second { get; }
        public int Sum => First + Second;
        public override string ToString() => $"{First}+{Second}={Sum}";
    }

    public static class Dice
    {
        public static DiceRoll Roll(Random random) => new(random.Next(1, 7), random.Next(1, 7));

        /// <summary>
        /// Rule 3.5.7.5: the first sum, counted counter-clockwise from the dealer,
        /// selects who throws the dice the second time.
        /// </summary>
        public static int ThrowerOffset(int sum)
        {
            return sum switch
            {
                5 or 9 => 0,
                2 or 6 or 10 => 1,
                3 or 7 or 11 => 2,
                4 or 8 or 12 => 3,
                _ => throw new ArgumentOutOfRangeException(nameof(sum))
            };
        }

        /// <summary>Tiles to count from the right end of the thrower's wall before the break.</summary>
        public static int BreakStacks(DiceRoll first, DiceRoll second) => first.Sum + second.Sum;
    }
}
