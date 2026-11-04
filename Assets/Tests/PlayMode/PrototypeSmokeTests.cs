using System.Collections;
using System.Linq;
using Mahjong.Localization;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// Runs the 3D prototype for a few frames in play mode and checks the things that
    /// compilation cannot check: scene bootstrap, generated table, tile meshes, text labels
    /// and the fact that the session actually reaches the human turn.
    /// </summary>
    public sealed class PrototypeSmokeTests
    {
        [UnityTest]
        public IEnumerator TableIsBuiltAndSessionStarts()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongTestHost").AddComponent<MahjongGame>();

            // one frame for Start(), one more for the first Update()
            yield return null;
            yield return null;

            // Tiles slide out of the wall into their places, so nothing is where it belongs yet. The
            // board is animated in scaled time and batch mode runs very fast, hence the boost.
            var previousScale = Time.timeScale;
            Time.timeScale = 30f;
            var settleFrames = 0;
            while (settleFrames++ < 600 && Settled() == false) yield return null;
            Time.timeScale = previousScale;

            var camera = Camera.main;
            Assert.IsNotNull(camera, "Camera.main is missing: nothing will render.");
            Assert.IsNotNull(camera!.GetComponent<Camera>(), "MainCamera has no Camera component.");

            Assert.IsNotNull(GameObject.Find("Table"), "The table cube was not created.");
            Assert.IsNotNull(Object.FindAnyObjectByType<Light>(), "No light in the scene.");

            // Queried after the settle loop: a rebuild can destroy a tile while the test holds the array.
            var tiles = Object.FindObjectsByType<TileObject>(FindObjectsSortMode.None);
            Assert.IsTrue(tiles.All(tile => tile.IsSettled), "The table did not settle.");
            Assert.GreaterOrEqual(tiles.Length, 50,
                $"Expected the human hand, three opponent hands and the wall-facing tiles, got {tiles.Length}.");

            var faceUp = tiles.Where(tile => tile.IsFaceUp).ToList();
            var faceDown = tiles.Where(tile => !tile.IsFaceUp).ToList();
            Assert.GreaterOrEqual(faceUp.Count, 13, "The human hand is not laid out.");
            // Three opponents hold thirteen tiles each minus three per opened meld, so the count comes
            // from the engine instead of a hard-coded 39: a bot that claimed a discard holds fewer.
            Assert.AreEqual(game.OpponentConcealedTileCount, faceDown.Count,
                "The opponents' concealed tiles are not drawn face down.");
            Assert.IsTrue(faceDown.All(tile => string.IsNullOrEmpty(LabelOf(tile))),
                "A face-down tile shows its face label.");

            // The atlas is the visible face in the normal build; keep checking the TextMesh fallback
            // as well, including when it is inactive behind the atlas.
            var label = faceUp[0].GetComponentInChildren<TextMesh>(true);
            Assert.IsNotNull(label, "A face-up tile has no TextMesh label.");
            Assert.IsNotNull(label!.font,
                "TextMesh has no font, so no tile label will be visible on screen.");
            Assert.IsNotEmpty(label.text, "A face-up tile label is empty.");
            StringAssert.AreEqualIgnoringCase(TileLabel.Short(faceUp[0].Type), label.text,
                "Tile label text does not match the tile type.");

            var status = game.Status;
            Assert.IsNotNull(status, "The status line was not created.");
            Assert.IsNotNull(status!.font, "The status TextMesh has no font.");
            // The interface is Russian by default, so the assertions ask the catalogue rather than
            // hard-coding English words: the HUD must use the texts of the active language.
            StringAssert.Contains(Loc.Format("hud.wall", game.WallRemaining), status.text,
                "Status text does not describe the wall:\n" + status.text);
            StringAssert.Contains(Loc.Get("hud.points").Split('{')[0].Trim(), status.text,
                "Status text does not show points:\n" + status.text);
            StringAssert.Contains(Loc.Get("hud.header").Split('{')[0].Trim(), status.text,
                "Status text does not describe the session:\n" + status.text);

            Debug.Log("[smoke] status:\n" + status.text);
            Debug.Log($"[smoke] tiles: {tiles.Length} (face up {faceUp.Count}, face down {faceDown.Count})");

            // the hand row on screen must follow the engine order: suit first, then rank
            var handRow = Object.FindObjectsByType<TileObject>(FindObjectsSortMode.None)
                .Where(tile => tile.gameObject.name == "hand")
                .OrderBy(tile => tile.transform.position.x)
                .Select(tile => tile.Type)
                .ToList();
            Assert.IsNotEmpty(handRow, "The human hand row is empty.");
            var labels = string.Join(" ", handRow.Select(TileLabel.Short));
            Debug.Log("[smoke] hand left to right: " + labels);
            for (var i = 1; i < handRow.Count; i++)
            {
                var previous = handRow[i - 1];
                var current = handRow[i];
                var ordered = previous.Suit < current.Suit
                              || (previous.Suit == current.Suit && previous.Rank <= current.Rank);
                Assert.IsTrue(ordered,
                    $"Hand is out of order at position {i}: {TileLabel.Short(previous)} before {TileLabel.Short(current)} ({labels}).");
            }
        }

        [UnityTest]
        public IEnumerator BotsPlayUntilTheHumanTurn()
        {
            var previousScale = Time.timeScale;
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongTestHost").AddComponent<MahjongGame>();

            // botDelay is 0.45 s per action; speed time up so several turns happen quickly
            Time.timeScale = 6f;
            yield return null;

            var status = game.Status;
            var wallAtStart = game.WallRemaining;
            Assert.Greater(wallAtStart, 0, "The wall count was not parsed from the status line.");

            var reachedHuman = false;
            var frames = 0;
            while (frames++ < 240 && !reachedHuman)
            {
                yield return null;
                reachedHuman = game.IsYourTurn || game.HandClock.IsRunning || game.DiscardTileCount > 0;
                if (reachedHuman) break;
            }

            Time.timeScale = previousScale;

            Assert.IsTrue(reachedHuman,
                "After two simulated seconds the table is still not in a turn: the game loop is stuck.");
            Debug.Log($"[smoke] reached a turn, wall at start {wallAtStart}, wall now {game.WallRemaining}"
                      + "\n" + status.text);
        }

        private static bool Settled() =>
            !Object.FindObjectsByType<TileObject>(FindObjectsSortMode.None).Any(tile => !tile.IsSettled);

        private static string LabelOf(TileObject tile)
        {
            var label = tile.GetComponentInChildren<TextMesh>();
            return label == null ? string.Empty : label.text;
        }

    }
}
