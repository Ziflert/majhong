using System.Collections;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The hop and the staggered start are what make a kong visible and an opening hand read as a
    /// deal. They are checked on a single tile, without the table: the timing is the whole behaviour.
    /// </summary>
    public sealed class TileAnimationTests
    {
        private Transform host = null!;

        [SetUp]
        public void SetUp() => host = new GameObject("AnimationHost").transform;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.Destroy(host.gameObject);
        }

        private TileObject Tile() => TileObject.Create(host, "tile");

        [UnityTest]
        public IEnumerator HopLiftsTheTileAndSettlesBack()
        {
            var tile = Tile();
            tile.SetPosition(Vector3.zero, 0f);
            var resting = tile.transform.position.y;

            tile.Pulse(height: 0.05f, duration: 0.2f);
            Assert.IsFalse(tile.IsSettled, "A hop has to count as motion, otherwise the table is called settled mid-animation.");

            // Batch mode runs far faster than real time, so the hop is played at a boosted rate
            // instead of sleeping for a fifth of a second.
            var previousScale = Time.timeScale;
            Time.timeScale = 30f;
            var peak = resting;
            var frames = 0;
            while (frames++ < 600 && !tile.IsSettled)
            {
                yield return null;
                peak = Mathf.Max(peak, tile.transform.position.y);
            }
            Time.timeScale = previousScale;

            Assert.IsTrue(tile.IsSettled, "The hop never finished.");
            Assert.Greater(peak, resting + 0.005f, "The tile was supposed to rise, it stayed flat.");
            Assert.AreEqual(resting, tile.transform.position.y, 1e-4f, "The tile did not come back to the table.");
        }

        [UnityTest]
        public IEnumerator HopWaitsForTheLanding()
        {
            var pulsed = Tile();
            pulsed.SetPosition(Vector3.zero, 0f);
            var plain = Tile();
            plain.SetPosition(Vector3.zero, 0f);

            // Two identical moves, only one of them hops after landing: while travelling the two tiles
            // must stay side by side, otherwise the hop is fighting the slide (both write the position).
            var target = new Vector3(0.3f, 0f, 0f);
            pulsed.SetTarget(target, 0f);
            plain.SetTarget(target, 0f);
            var landing = pulsed.LandingDelay;
            Assert.Greater(landing, 0f, "A long move must take time, otherwise there is nothing to wait for.");
            pulsed.Pulse(height: 0.05f, duration: 0.2f, delay: landing);

            var previousScale = Time.timeScale;
            Time.timeScale = 10f;
            var frames = 0;
            var travelled = false;
            while (frames++ < 600 && pulsed.transform.position.x < target.x - 0.05f)
            {
                Assert.AreEqual(plain.transform.position.y, pulsed.transform.position.y, 1e-4f,
                    "The hop started before the tile landed.");
                yield return null;
                travelled = true;
            }

            Assert.IsTrue(travelled, "The tile never moved towards its target.");
            Assert.IsTrue(pulsed.transform.position.x > 0.05f, "The pulsed tile was left at the start.");

            // After the landing the hop is what separates the two tiles.
            frames = 0;
            var peak = 0f;
            while (frames++ < 600 && !pulsed.IsSettled)
            {
                yield return null;
                peak = Mathf.Max(peak, Mathf.Abs(pulsed.transform.position.x - plain.transform.position.x) +
                                        Mathf.Abs(pulsed.transform.position.y - plain.transform.position.y));
            }
            Time.timeScale = previousScale;

            Assert.Greater(peak, 0.005f, "The scheduled hop never lifted the tile after it landed.");
        }

        [UnityTest]
        public IEnumerator ExtraDelayPostponesTheStart()
        {
            var tile = Tile();
            tile.SetPosition(Vector3.zero, 0f);

            var target = new Vector3(0.3f, 0f, 0f);
            tile.SetTarget(target, 0f, extraDelay: 0.25f);

            // Two frames are far less than the delay, so a tile that does not honour it would be off.
            yield return null;
            yield return null;
            Assert.Less(tile.transform.position.x, 0.05f, "The tile ignored the extra delay and started moving at once.");
            Assert.IsFalse(tile.IsSettled, "A tile in the middle of a delayed move is not settled.");

            yield return null;
        }
    }
}
