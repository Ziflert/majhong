using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// Every place a tile can stand on the table, in one file: the wall square of four walls of
    /// eighteen stacks (3.5.7.4), the hand row of a seat, the meld row, the discard blocks and the
    /// seat plates.
    ///
    /// A real table is laid out from the outside in — player, hand, wall, discards — so the wall sits
    /// between a player's hand and the middle of the table. Every distance below is in those same
    /// units, which is why the wall is not laid out from constants of its own.
    /// </summary>
    public static class TableLayout
    {
        /// <summary>Half the felt; both hand rows and the wall square fit inside it.</summary>
        public const float FeltHalf = 0.85f;

        /// <summary>Height of the felt top, where tiles rest.</summary>
        public const float TileY = 0.01f;

        /// <summary>Height of the second tile of a stack: it rests on the lower one.</summary>
        public const float StackStep = TileObject.Height;

        /// <summary>Gap between two neighbouring stacks of the same wall.</summary>
        private const float StackGap = 0.004f;

        /// <summary>Distance between the centres of two neighbouring stacks along a wall.</summary>
        public const float StackPitch = TileObject.Length + StackGap;

        /// <summary>Length of one wall: eighteen stacks, which is the side of the square.</summary>
        public const float WallLength = (WallGeometry.StacksPerSide - 1) * StackPitch + TileObject.Length;

        /// <summary>
        /// Distance from the middle of the table to the centre line of the near and far walls. Those
        /// two run the full length of the square and the side walls fit between them, which is how
        /// the four walls meet at the corners on a real table instead of overlapping there.
        /// </summary>
        public const float WallCenter = WallLength * 0.5f + TileObject.Width * 0.5f;

        /// <summary>Where a seat's hand row stands: outside its wall, near the edge of the felt.</summary>
        public const float HandDistance = FeltHalf - TileObject.Width * 0.5f - 0.02f;

        /// <summary>Distance between two neighbouring tiles of a hand or a discard row.</summary>
        public const float TilePitch = 0.062f;

        /// <summary>Where the melds of a seat stand: inside the wall, in front of the player.</summary>
        public const float MeldDistance = WallCenter - 0.21f;

        /// <summary>Where the seat plates stand: outside the felt, so they never cover a tile.</summary>
        public const float PlateDistance = FeltHalf + 0.02f;

        /// <summary>Width of a seat's hand row; the HUD and the tests use it to centre the hand.</summary>
        public const float HandRowStart = -0.40f;

        /// <summary>Centre of a wall tile on the table, from the slot the engine gave it.</summary>
        public static Vector3 WallPosition(WallSlot slot)
        {
            var along = -WallLength * 0.5f + TileObject.Length * 0.5f + slot.Stack * StackPitch;
            var height = TileY + slot.Level * StackStep;
            return slot.Side switch
            {
                // The near and far walls run along the table's width, the side walls along its depth.
                0 => new Vector3(along, height, -WallCenter),
                2 => new Vector3(-along, height, WallCenter),
                3 => new Vector3(-WallCenter, height, along),
                _ => new Vector3(WallCenter, height, -along)
            };
        }

        /// <summary>
        /// A wall tile lies face down with its long side along its own wall: the side walls need no
        /// turn, the near and far walls a quarter turn.
        /// </summary>
        public static float WallRotationY(WallSlot slot) => slot.Side is 1 or 3 ? 0f : 90f;

        /// <summary>Centre of one tile of a seat's hand row.</summary>
        public static Vector3 HandPosition(int seat, int index) => RowPosition(seat, index, HandDistance);

        /// <summary>Which way a seat's hand tiles point: each hand faces the player it belongs to.</summary>
        public static float HandRotationY(int seat) => seat * 90f;

        /// <summary>Centre of one tile of a seat's meld row, which stands inside the wall.</summary>
        public static Vector3 MeldPosition(int seat, int index, int tileIndex)
        {
            // Melds line up side by side along the seat's own row, one tile apart from each other.
            var spread = index * (TilePitch * 4.6f - TilePitch) + tileIndex * TilePitch;
            return RowPosition(seat, 0, MeldDistance) + RowDirection(seat) * spread;
        }

        /// <summary>
        /// Where a seat's exposed flowers lie: face up, between the melds and the wall, at the far end
        /// of the seat's own row. A flower is out of the hand but never out of the game (3.5.7.6).
        /// </summary>
        public static Vector3 FlowerPosition(int seat, int index)
        {
            var start = RowPosition(seat, 0, WallCenter - 0.105f);
            return start + RowDirection(seat) * (WallGeometry.StacksPerSide - 1 - index);
        }

        /// <summary>Centre of one tile of a seat's discard block: rows of six, as on a real table.</summary>
        public static Vector3 DiscardPosition(int seat, int index)
        {
            const int perRow = 6;
            var column = index % perRow;
            var row = index / perRow;
            var inward = 0.10f + row * TilePitch * 1.25f;
            var spread = (column - (perRow - 1) * 0.5f) * TilePitch;
            return seat switch
            {
                0 => new Vector3(spread, TileY, -inward - 0.08f),
                2 => new Vector3(-spread, TileY, inward + 0.08f),
                3 => new Vector3(-inward - 0.08f, TileY, -spread),
                _ => new Vector3(inward + 0.08f, TileY, spread)
            };
        }

        /// <summary>Which way the tiles of a seat's discard block lie: across its own side.</summary>
        public static float DiscardRotationY(int seat) => seat * 90f;

        /// <summary>Where the wind plate of a seat stands.</summary>
        public static Vector3 PlatePosition(int seat) => seat switch
        {
            0 => new Vector3(0f, 0f, -PlateDistance),
            2 => new Vector3(0f, 0f, PlateDistance),
            3 => new Vector3(-PlateDistance, 0f, 0f),
            _ => new Vector3(PlateDistance, 0f, 0f)
        };

        /// <summary>Direction a seat's row of tiles grows in.</summary>
        private static Vector3 RowDirection(int seat) => seat switch
        {
            0 => new Vector3(TilePitch, 0f, 0f),
            2 => new Vector3(-TilePitch, 0f, 0f),
            3 => new Vector3(0f, 0f, -TilePitch),
            _ => new Vector3(0f, 0f, TilePitch)
        };

        /// <summary>Centre of the tile at <paramref name="index"/> in a row that lies <paramref name="distance"/> from the middle.</summary>
        private static Vector3 RowPosition(int seat, int index, float distance)
        {
            var start = seat switch
            {
                0 => new Vector3(HandRowStart, TileY, -distance),
                2 => new Vector3(-HandRowStart, TileY, distance),
                3 => new Vector3(-distance, TileY, -HandRowStart),
                _ => new Vector3(distance, TileY, HandRowStart)
            };
            return start + RowDirection(seat) * index;
        }
    }
}
