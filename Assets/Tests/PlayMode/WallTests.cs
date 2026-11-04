using System.Collections;
using System.Linq;
using Mahjong.Rules;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The table as a real mahjong table: the whole set stands as four walls of eighteen two-tile
    /// stacks (3.5.7.4), the dice are thrown into the middle, the wall is broken at the counted stack
    /// (3.5.7.5) and a dealt tile is seen leaving its own place in the wall instead of appearing out of
    /// thin air. Whatever nobody took is still standing when the hand is over.
    /// </summary>
    public sealed class WallTests
    {
        private MahjongGame game = null!;

        /// <summary>
        /// Counting a wall means counting a wall that has just been built, so these tests keep a table
        /// of their own instead of sharing the one the other play-mode tests hand each other. The
        /// shared game is dropped along with the table and the sun, which are not children of it: two
        /// half-played tables on screen would make "the wall stands on four sides" mean nothing.
        /// </summary>
        [SetUp]
        public void FreshTable()
        {
            foreach (var existing in Object.FindObjectsByType<MahjongGame>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                Object.DestroyImmediate(existing.gameObject);

            for (var leftover = GameObject.Find("Table"); leftover != null; leftover = GameObject.Find("Table"))
                Object.DestroyImmediate(leftover);
            for (var leftover = GameObject.Find("Sun"); leftover != null; leftover = GameObject.Find("Sun"))
                Object.DestroyImmediate(leftover);

            game = new GameObject("MahjongWallTestHost").AddComponent<MahjongGame>();
        }

        [TearDown]
        public void ClearTable()
        {
            if (game != null) Object.Destroy(game.gameObject);
        }

        private static TileObject[] Tiles() =>
            Object.FindObjectsByType<TileObject>(FindObjectsSortMode.None);

        /// <summary>Which side of the wall square a point belongs to, or -1 if it is not on the square.</summary>
        private static int WallSideOf(Vector3 position)
        {
            var nearFar = Mathf.Abs(Mathf.Abs(position.z) - TableLayout.WallCenter);
            var leftRight = Mathf.Abs(Mathf.Abs(position.x) - TableLayout.WallCenter);
            if (nearFar < 0.01f) return position.z < 0f ? 0 : 2;
            if (leftRight < 0.01f) return position.x < 0f ? 3 : 1;
            return -1;
        }

        private static bool OnTheWallSquare(Vector3 position) => WallSideOf(position) >= 0;

        /// <summary>Lets the dice land and the deal finish; the opening runs in scaled time.</summary>
        private static IEnumerator FinishOpening(MahjongGame game)
        {
            var previousScale = Time.timeScale;
            Time.timeScale = 30f;
            var frames = 0;
            while (frames++ < 3000 && game.Opening.IsRunning) yield return null;
            // And then everything that was dealt has to arrive.
            frames = 0;
            while (frames++ < 3000 && Tiles().Any(tile => !tile.IsSettled)) yield return null;
            Time.timeScale = previousScale;
        }

        [UnityTest]
        public IEnumerator TheWallStandsAsFourWallsOfEighteenStacks()
        {
            yield return null;
            yield return null;
            yield return FinishOpening(game);

            var wall = Tiles().Where(tile => tile.name == "wall").ToList();
            Assert.AreEqual(game.WallRemaining, wall.Count,
                "The tiles standing in the wall are not exactly the tiles the engine has left.");
            Assert.IsTrue(wall.All(tile => !tile.IsFaceUp), "A wall tile shows its face.");
            Assert.IsTrue(wall.All(tile => tile.IsSettled), "A wall tile has not arrived at its place.");

            // Two tiles high: a stack is a bottom and a top tile, never a third one.
            var heights = wall.Select(tile => tile.transform.position.y).Distinct()
                .OrderBy(y => y).ToList();
            Assert.AreEqual(2, heights.Count, "The wall is not two tiles high.");
            Assert.AreEqual(TileObject.Height, heights[1] - heights[0], 0.002f,
                "The two levels of the wall are not one tile apart.");

            // Four walls: every standing tile lies on one of the four sides of the square.
            var sides = new int[4];
            foreach (var tile in wall)
            {
                var side = WallSideOf(tile.transform.position);
                if (side < 0) Assert.Fail($"A wall tile stands outside the square at {tile.transform.position}.");
                sides[side]++;
            }

            Assert.AreEqual(4, sides.Count(count => count > 0), "The wall is not closed on four sides.");
            // Every wall started the hand with eighteen stacks of two; the deal takes from one place
            // in the ring, so no side may be empty and none may hold more than its thirty-six tiles.
            Assert.IsTrue(sides.All(count => count <= WallGeometry.TilesPerSide),
                "A side of the wall holds more tiles than it was built with.");
        }

        [UnityTest]
        public IEnumerator ADealtTileLeavesItsPlaceInTheWall()
        {
            yield return null;
            yield return null;

            // Before the dice have landed, every view on the table was spawned in its slot in the
            // wall. Everything that is dealt afterwards has to be one of these very objects, standing
            // where it stood: nothing arrives out of thin air.
            var standing = Tiles().Where(tile => tile.name == "wall").ToHashSet();
            var spawnedAt = Tiles().ToDictionary(tile => tile, tile => tile.transform.position);
            Assert.AreEqual(game.WallRemaining, standing.Count,
                "The wall before the deal is not the whole wall.");

            yield return FinishOpening(game);

            var hand = Tiles().Where(tile => tile.name == "hand").ToList();
            Assert.IsNotEmpty(hand, "The human hand did not get any tiles.");
            Assert.IsTrue(hand.All(spawnedAt.ContainsKey),
                "A hand tile was created instead of taken out of the wall.");
            Assert.IsTrue(hand.All(tile => OnTheWallSquare(spawnedAt[tile])),
                "A hand tile never stood in the wall: the deal did not look like a deal.");
            Assert.AreEqual(spawnedAt.Count, Tiles().Length,
                "The table lost or gained tile objects while dealing: it no longer holds one set.");
            // Rule 3.5.7.5: 53 tiles are dealt, and every flower it brings is replaced from the far
            // end of the same wall, so the wall is short exactly those.
            var flowers = Enumerable.Range(0, 4).Sum(seat => game.State.Players[seat].Flowers.Count);
            Assert.AreEqual(WallGeometry.TileCount - 53 - flowers, game.State.Wall.Remaining,
                "The deal did not take 53 tiles (plus one per flower) out of a wall of 144.");
            Assert.IsTrue(hand.All(tile => tile.IsSettled), "A dealt tile never arrived in the hand.");
            Assert.IsTrue(hand.All(tile => tile.transform.position.z < -TableLayout.WallCenter),
                "A hand tile did not reach the hand row outside the wall.");

            // And what nobody took stays where it stands: the wall is still there, with holes in it.
            var stillStanding = Tiles().Where(tile => tile.name == "wall").ToList();
            Assert.AreEqual(game.WallRemaining, stillStanding.Count,
                "The wall does not show what the engine has left.");
            Assert.IsTrue(stillStanding.All(standing.Contains),
                "A tile that was dealt is still drawn in the wall.");
        }

        [UnityTest]
        public IEnumerator TheDiceShowWhatTheEngineThrewAndMarkTheBreak()
        {
            yield return null;
            yield return null;
            yield return FinishOpening(game);

            // The second throw is what the table is left showing: it is the one that places the break.
            var second = game.State.BreakRoll;
            Assert.AreEqual(second.First, game.Dice.ShownFirst, "The left die does not show the second throw.");
            Assert.AreEqual(second.Second, game.Dice.ShownSecond, "The right die does not show the second throw.");
            Assert.AreEqual(game.State.DealerRoll.Sum + second.Sum, game.State.BreakStacks,
                "The break is not counted from both throws.");
            Assert.AreEqual(game.State.BreakStacks, game.Opening.BreakStacks,
                "The opening and the engine disagree about where the wall breaks.");
            Assert.AreEqual(game.State.DiceThrower, game.Opening.Thrower,
                "The opening and the engine disagree about who threw the dice.");

            // The break is counted in stacks from the second thrower's right end. His own wall holds
            // eighteen of them, so a sum past that runs on into the wall to his left: the side of the
            // break is his own wall only while the count fits inside it (3.5.7.5).
            var slot = WallGeometry.SlotAt(game.State.Wall.BreakIndex);
            var expectedSide = game.State.BreakStacks < WallGeometry.StacksPerSide
                ? game.State.DiceThrower
                : (game.State.DiceThrower + 3) % 4;
            Assert.AreEqual(expectedSide, slot.Side,
                "The break is not where counting from the thrower's right end lands.");
            Assert.IsNotNull(game.BreakMarker, "The table has no break marker.");
            Assert.IsTrue(game.BreakMarker.gameObject.activeInHierarchy,
                "The break marker is not shown at the start of a hand.");

            var marker = game.BreakMarker.position;
            var tile = TableLayout.WallPosition(slot);
            if (slot.Side is 0 or 2)
            {
                Assert.AreEqual(tile.x, marker.x, 0.01f, "The break marker is at the wrong stack.");
                Assert.Less(Mathf.Abs(marker.z), Mathf.Abs(tile.z),
                    "The break marker does not lie on the table side of the wall.");
            }
            else
            {
                Assert.AreEqual(tile.z, marker.z, 0.01f, "The break marker is at the wrong stack.");
                Assert.Less(Mathf.Abs(marker.x), Mathf.Abs(tile.x),
                    "The break marker does not lie on the table side of the wall.");
            }
        }
    }
}
