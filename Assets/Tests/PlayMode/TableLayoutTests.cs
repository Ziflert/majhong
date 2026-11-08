using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The table is laid out from constants, so what lies on what is arithmetic, not a screenshot: a
    /// discard block that reaches into its neighbour's block or a sideways tile that runs into the
    /// tile next to it is a wrong number, and a wrong number is cheaper to catch here than in a frame.
    ///
    /// Two findings of the first playtest live in these numbers (У-3): the last tile of one seat's
    /// first discard row lay on the first tile of its neighbour's, and the claimed tile of a meld —
    /// turned sideways, so 0.072 long instead of 0.052 — overlapped the tile beside it.
    /// </summary>
    public sealed class TableLayoutTests
    {
        /// <summary>Flat footprint of a tile on the felt: width along x and length along z before the turn.</summary>
        private static Rect Footprint(Vector3 centre, float rotationY)
        {
            var quarterTurned = Mathf.Abs(Mathf.DeltaAngle(rotationY, 0f)) > 45f
                                && Mathf.Abs(Mathf.DeltaAngle(rotationY, 180f)) > 45f;
            var width = quarterTurned ? TileObject.Length : TileObject.Width;
            var length = quarterTurned ? TileObject.Width : TileObject.Length;
            return new Rect(centre.x - width * 0.5f, centre.z - length * 0.5f, width, length);
        }

        private static bool Overlaps(Rect a, Rect b, float tolerance = 0.0005f) =>
            a.xMin < b.xMax - tolerance && b.xMin < a.xMax - tolerance
            && a.yMin < b.yMax - tolerance && b.yMin < a.yMax - tolerance;

        [Test]
        public void DiscardBlocksOfNeighbouringSeatsDoNotTouch()
        {
            // Nineteen discards is a long hand; a seat cannot have many more, and the block grows inward
            // a row at a time, so this covers every row it will ever have.
            for (var seat = 0; seat < 4; seat++)
            for (var other = seat + 1; other < 4; other++)
            for (var mine = 0; mine < 19; mine++)
            for (var theirs = 0; theirs < 19; theirs++)
            {
                var a = Footprint(TableLayout.DiscardPosition(seat, mine), TableLayout.DiscardRotationY(seat));
                var b = Footprint(TableLayout.DiscardPosition(other, theirs), TableLayout.DiscardRotationY(other));
                Assert.IsFalse(Overlaps(a, b),
                    $"Discard {mine} of seat {seat} lies on discard {theirs} of seat {other}.");
            }
        }

        [Test]
        public void TilesInsideAMeldKeepTheirDistance()
        {
            // Every tile of a meld can be the claimed one — the one that is turned sideways — so every
            // tile is checked in both positions among four tiles, which is the longest meld.
            for (var seat = 0; seat < 4; seat++)
            for (var sideways = 0; sideways < 4; sideways++)
            for (var index = 0; index < 3; index++)
            {
                var claim = Footprint(TableLayout.MeldPosition(seat, index, sideways),
                    TableLayout.HandRotationY(seat) + 90f);

                for (var left = 0; left < 4; left++)
                {
                    if (left == sideways) continue;

                    var neighbour = Footprint(TableLayout.MeldPosition(seat, index, left),
                        TableLayout.HandRotationY(seat));
                    Assert.IsFalse(Overlaps(neighbour, claim),
                        $"Tile {left} of meld {index} of seat {seat} runs into the sideways tile beside it.");
                }
            }
        }

        [Test]
        public void MeldsOfOneSeatDoNotTouch()
        {
            for (var seat = 0; seat < 4; seat++)
            for (var first = 0; first < 3; first++)
            {
                var lastTileOfFirst = Footprint(TableLayout.MeldPosition(seat, first, 3),
                    TableLayout.HandRotationY(seat) + 90f);
                var firstTileOfNext = Footprint(TableLayout.MeldPosition(seat, first + 1, 0),
                    TableLayout.HandRotationY(seat));
                Assert.IsFalse(Overlaps(lastTileOfFirst, firstTileOfNext),
                    $"Meld {first} and meld {first + 1} of seat {seat} run into each other.");
                // The melds of a seat line up along that seat's own row, and that row runs on x for the
                // near and far seats and on z for the side ones: the gap is the distance along the row
                // minus a length of tile from each side, the sideways claim and the upright tile after
                // it. Reading it off xMin/xMax only ever worked for two of the four seats.
                var step = TableLayout.MeldPosition(seat, first + 1, 0)
                           - TableLayout.MeldPosition(seat, first, 3);
                var along = Mathf.Abs(step.x) + Mathf.Abs(step.z);
                Assert.Greater(along - TileObject.Length, 0.01f,
                    $"Melds {first} and {first + 1} of seat {seat} are too close to tell apart.");
            }
        }

        [Test]
        public void MeldsStayClearOfTheDiscardsAndTheWall()
        {
            var wallInnerEdge = TableLayout.WallCenter - TileObject.Width * 0.5f;
            var meldOuterEdge = TableLayout.MeldDistance + TileObject.Length * 0.5f;

            for (var seat = 0; seat < 4; seat++)
            {
                Assert.Less(meldOuterEdge, wallInnerEdge, $"The melds of seat {seat} run under the wall.");

                // Twenty-four discards are four rows; the block grows inward and then stops.
                var lastDiscard = Footprint(TableLayout.DiscardPosition(seat, 23),
                    TableLayout.DiscardRotationY(seat));
                var closestDiscardEdge = seat switch
                {
                    0 => Mathf.Abs(lastDiscard.yMin),
                    2 => lastDiscard.yMax,
                    3 => Mathf.Abs(lastDiscard.xMin),
                    _ => lastDiscard.xMax
                };
                Assert.Less(closestDiscardEdge, TableLayout.MeldDistance - TileObject.Length * 0.5f,
                    $"The discard block of seat {seat} reaches into its own melds.");

                // The flowers lie between the melds and the wall and must not land on either.
                Assert.Greater(TableLayout.FlowerDistance - TileObject.Width * 0.5f, meldOuterEdge,
                    $"A flower of seat {seat} lies on the melds.");
                Assert.Less(TableLayout.FlowerDistance + TileObject.Width * 0.5f, wallInnerEdge,
                    $"A flower of seat {seat} stands under the wall.");
            }
        }

        [Test]
        public void TheNewestMeldOfASeatStandsHigher()
        {
            // A meld is declared by a bot as often as by the player, so the new one has to announce
            // itself: it stands a step higher than the melds before it. Only the last one — a lift
            // on all of them would break the row apart and say nothing about which is new.
            Assert.Greater(TableLayout.MeldStand(0, 1), 0f, "A seat's only meld is its newest and must stand higher.");
            Assert.AreEqual(0f, TableLayout.MeldStand(0, 2), "The older meld must lie flat.");
            Assert.Greater(TableLayout.MeldStand(1, 2), 0f, "The second meld must stand higher than the first.");
            Assert.Greater(TableLayout.MeldStand(2, 3), 0f, "The third meld must stand higher than the two before it.");

            // The step has to be seen from the camera, so it is worth a visible part of a tile: a
            // hairline lift reads as nothing at all from the far side of the table.
            Assert.GreaterOrEqual(TableLayout.MeldLift, TileObject.Height * 0.4f,
                "The newest meld must stand high enough to be seen.");
        }

        [Test]
        public void MeldRowOfFourStaysOnTheFelt()
        {
            for (var seat = 0; seat < 4; seat++)
            {
                var corner = TableLayout.MeldPosition(seat, 3, 3);
                // The centre is not the whole tile: half a tile length hangs past it, and past the felt
                // is what a player sees as a meld lying on the table edge.
                var edge = TableLayout.FeltHalf - TileObject.Length * 0.5f;
                Assert.LessOrEqual(Mathf.Abs(corner.x), edge,
                    $"The fourth meld of seat {seat} hangs off the felt.");
                Assert.LessOrEqual(Mathf.Abs(corner.z), edge,
                    $"The fourth meld of seat {seat} hangs off the felt.");
            }
        }
    }
}
