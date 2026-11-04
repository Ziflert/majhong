using System;

namespace Mahjong.Rules
{
    /// <summary>
    /// Where a tile of the wall stands: which side of the square it belongs to, which stack, and
    /// whether it is the top or the bottom tile of that stack. Sides are seats (0 is the screen
    /// player's own wall), stacks are counted from that player's right end, and <c>Level</c> is 1
    /// for the top tile and 0 for the bottom one, because a stack is taken top first.
    /// </summary>
    public readonly struct WallSlot : IEquatable<WallSlot>
    {
        public WallSlot(int side, int stack, int level)
        {
            Side = side;
            Stack = stack;
            Level = level;
        }

        public int Side { get; }
        public int Stack { get; }
        public int Level { get; }

        public bool IsTop => Level == WallGeometry.TilesPerStack - 1;

        public override string ToString() => $"side {Side}, stack {Stack + 1}, {(IsTop ? "top" : "bottom")}";

        public bool Equals(WallSlot other) => Side == other.Side && Stack == other.Stack && Level == other.Level;
        public override bool Equals(object? obj) => obj is WallSlot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Side, Stack, Level);
    }

    /// <summary>
    /// The shape of the wall as the rules describe it (3.5.7.4): every player takes 36 tiles and
    /// builds 18 stacks of two in front of himself, and the four walls form a square.
    ///
    /// The whole wall is one closed ring of 144 tiles and the deal walks that ring. Walking clockwise
    /// as seen from above means a player's own wall is taken from his right end to his left end, then
    /// the wall on his left, and so on — while the players themselves take turns counter-clockwise.
    /// That is why <see cref="RingStart"/> counts sides backwards: 0, 3, 2, 1.
    /// </summary>
    public static class WallGeometry
    {
        public const int Sides = 4;
        public const int StacksPerSide = 18;
        public const int TilesPerStack = 2;
        public const int TilesPerSide = StacksPerSide * TilesPerStack;
        public const int TileCount = Sides * TilesPerSide;

        /// <summary>Where a side's wall starts in the ring: the end counted as that player's right end.</summary>
        public static int RingStart(int side) => Mod(-side, Sides) * TilesPerSide;

        /// <summary>Ring position of one tile; <paramref name="level"/> is 1 for the top of a stack.</summary>
        public static int RingIndex(int side, int stack, int level) =>
            RingStart(side) + Mod(stack, StacksPerSide) * TilesPerStack
                            + (TilesPerStack - 1 - Mod(level, TilesPerStack));

        /// <summary>The tile that stands at a ring position.</summary>
        public static WallSlot SlotAt(int ring)
        {
            var position = Mod(ring, TileCount);
            var ringSide = position / TilesPerSide;
            var within = position % TilesPerSide;
            return new WallSlot(Mod(-ringSide, Sides), within / TilesPerStack,
                TilesPerStack - 1 - within % TilesPerStack);
        }

        /// <summary>
        /// Rule 3.5.7.5: count the sum of both throws in stacks from the right end of the second
        /// thrower's wall and break there. The live wall starts with the tile after that stack, so a
        /// sum past the end of his own wall carries on into the next one.
        /// </summary>
        public static int BreakIndex(int thrower, int stacks) =>
            Mod(RingStart(thrower) + stacks * TilesPerStack, TileCount);

        private static int Mod(int value, int m)
        {
            var result = value % m;
            return result < 0 ? result + m : result;
        }
    }
}
