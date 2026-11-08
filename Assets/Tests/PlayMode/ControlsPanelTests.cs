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
    /// The on-screen tray of buttons that mirrors the hot keys (У-6): six tiles with readable
    /// captions, a hot key in the corner of each, colliders that a ray from the camera can hit,
    /// hover feedback, and the same actions the keyboard would trigger. Built from the same
    /// primitives as the action bar, so it covers the 3D part headless tests cannot reach.
    /// </summary>
    public sealed class ControlsPanelTests
    {
        private static readonly string[] RequiredKeys =
        {
            "controls.rulebook",
            "controls.menu",
            "controls.replay",
            "controls.save",
            "controls.language",
            "controls.freeplay"
        };

        private static Camera NewCamera(string name)
        {
            var cameraObject = new GameObject(name);
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            // The game's own camera is a narrow 34° view; at Unity's default 60° a row placed for the
            // wide frame still nearly fits the narrow one, which is how the tray went off the screen
            // with every test green.
            camera.fieldOfView = 34f;
            camera.aspect = 16f / 9f;
            return camera;
        }

        [UnityTest]
        public IEnumerator TrayBuildsSixTilesWithReadableCaptions()
        {
            var camera = NewCamera("TrayCamera");
            var tray = ControlsPanel.Create(camera.transform);
            yield return null;

            Assert.AreEqual(6, tray.Count, "The tray must show six controls.");
            Assert.AreEqual(6, tray.Tiles.Count, "Every control must have a tile.");

            Assert.IsTrue(RequiredKeys.All(Loc.Has), "All tray keys must have translations in both languages.");
            foreach (var tile in tray.Tiles)
            {
                var caption = tile.GetComponentInChildren<TextMesh>();
                Assert.IsNotNull(caption, $"Tile {tile.name} has no TextMesh caption.");
                Assert.IsNotNull(caption!.font, $"Tile {tile.name} has no font, its caption will not render.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(tile.Label),
                    $"Tile {tile.name} has an empty caption.");

                // The hot key is printed in the corner of the tile, not inside the caption: the face is
                // narrow and "Сохранить · F5" would have to shrink below what a player can read.
                Assert.AreEqual(tile.Key.ToString() == "Escape" ? "Esc" : tile.Key.ToString(),
                    tile.KeyLabel, $"The tile {tile.name} must print its hot key in the corner.");

                // A caption that is wider than its face runs over the next button.
                Assert.LessOrEqual(tile.CaptionWidth, ControlTile.TileWidth,
                    $"The caption of {tile.name} (\"{tile.Label}\") is wider than the tile.");

                Assert.IsNotNull(tile.GetComponent<Collider>(),
                    $"Tile {tile.name} has no collider and cannot be clicked.");
            }

            // All six tiles must lie on the same row, on the bottom edge of the frame and inside it.
            var positions = tray.Tiles.Select(t => t.transform.position).ToList();
            var first = positions[0];
            for (var i = 1; i < positions.Count; i++)
            {
                Assert.AreEqual(first.z, positions[i].z,
                    $"Tile {i} is not on the same plane as the first.");
                Assert.AreEqual(first.y, positions[i].y,
                    $"Tile {i} is not on the same row as the first.");
                var viewport = camera.WorldToViewportPoint(positions[i]);
                Assert.IsTrue(viewport.z > 0f, $"Tile {i} is behind the camera.");
                Assert.IsTrue(viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f,
                    $"Tile {i} hangs outside the frame at viewport {viewport}.");
            }

            // The row rides the bottom edge of the frame, not the middle of the table: a tray floating
            // over the hand is as useless as one below the screen. And it stays in its corner, because
            // the hand of the player stands in the middle of the bottom edge.
            var bottom = camera.WorldToViewportPoint(first).y;
            Assert.Less(bottom, 0.25f, $"The tray stands at {bottom} of the frame, not on its bottom edge.");
            Assert.Greater(bottom, 0.01f, $"The tray is cut off by the bottom edge of the frame ({bottom}).");
            Assert.Less(camera.WorldToViewportPoint(positions[^1]).x, 0.6f,
                "The tray reaches into the middle of the bottom edge, where the hand stands.");

            Object.Destroy(camera.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayCaptionsShareOneSize()
        {
            var camera = NewCamera("TrayCaptionCamera");
            var tray = ControlsPanel.Create(camera.transform);
            yield return null;

            // "Правила" and "Язык" are different lengths: sizing each caption to its own face gave the
            // row three sizes of label and made short buttons look like a different kind of button.
            var sizes = tray.Tiles
                .Select(tile => tile.transform.Find("caption").GetComponent<TextMesh>().characterSize)
                .Distinct().ToList();
            Assert.AreEqual(1, sizes.Count,
                $"The tray draws its captions in {sizes.Count} sizes: {string.Join(", ", sizes)}.");

            // The shared size is the one the longest caption needs, so no caption overruns its face.
            Assert.IsTrue(tray.Tiles.All(tile => tile.CaptionWidth <= ControlTile.TileWidth),
                "A caption is wider than its face at the shared size.");

            Object.Destroy(camera.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayStaysInsideANarrowFrame()
        {
            // A window can be tall and narrow or short and wide: a row that keeps the size it was built
            // with runs off the sides of the narrower one.
            var camera = NewCamera("TrayNarrowCamera");
            camera.aspect = 4f / 3f;
            var tray = ControlsPanel.Create(camera.transform);
            yield return null;
            yield return null;

            foreach (var tile in tray.Tiles)
            {
                var viewport = camera.WorldToViewportPoint(tile.transform.position);
                Assert.IsTrue(viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f,
                    $"Tile {tile.name} hangs outside a 4:3 frame at {viewport}.");
            }

            Object.Destroy(camera.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayTilesCanBePickedWithARay()
        {
            var camera = NewCamera("TrayPickCamera");
            var tray = ControlsPanel.Create(camera.transform);
            yield return null;

            for (var i = 0; i < tray.Tiles.Count; i++)
            {
                var tile = tray.Tiles[i];
                var origin = camera.transform.position;
                var target = tile.transform.position;
                var ray = new Ray(origin, (target - origin).normalized);

                Assert.IsTrue(tray.TryPick(ray, out var picked),
                    $"A ray through the middle of tile {i} did not hit it.");
                Assert.AreEqual(tile.Type, picked.Type,
                    $"The tray picked the wrong control on tile {i}.");
                Assert.AreEqual(tile.Key, picked.Key,
                    $"The tray reported the wrong key on tile {i}.");

                var hovered = tray.Hover(ray);
                Assert.IsNotNull(hovered, $"Hovering tile {i} returned no control.");
                Assert.AreEqual(tile.Type, hovered!.Type, $"Hover returned the wrong control on tile {i}.");
            }

            Object.Destroy(camera.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayIgnoresRaysThatMiss()
        {
            var camera = NewCamera("TrayMissCamera");
            var tray = ControlsPanel.Create(camera.transform);
            yield return null;

            Assert.IsFalse(tray.TryPick(new Ray(Vector3.zero, Vector3.back), out _),
                "A ray pointing away from the tray must not pick a tile.");
            Assert.IsNull(tray.Hover(new Ray(Vector3.zero, Vector3.back)));

            Object.Destroy(camera.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayTilesCanSetHoveredAndEnabled()
        {
            var camera = NewCamera("TrayStateCamera");
            var tray = ControlsPanel.Create(camera.transform);
            yield return null;

            var saveTile = tray.Tiles.Single(t => t.Type == ControlType.Save);

            // Save is disabled while no hand is played: the tile wears the disabled skin of the
            // interface, which is a palette role and not a colour written into the tile (4.10, 8.10).
            Assert.IsFalse(saveTile.IsEnabled, "A save tile while no hand is played must be disabled.");
            Assert.AreEqual(UiSkin.ButtonDisabled, saveTile.Face.Skin,
                "A disabled tray tile must wear the disabled skin.");

            // Hovering a disabled tile keeps it disabled but highlights the face.
            var ray = new Ray(camera.transform.position,
                (saveTile.transform.position - camera.transform.position).normalized);
            tray.Hover(ray);
            Assert.IsTrue(saveTile.IsHovered, "Hovering a tile has to be remembered by the tile.");
            Assert.AreEqual(UiSkin.ButtonHover, saveTile.Face.Skin,
                "Hovering must change the skin even on a disabled tile.");

            tray.Hide();
            yield return null;
            Assert.AreEqual(0, tray.Tiles.Count, "Hiding the tray must destroy its tiles.");

            Object.Destroy(camera.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayComesBackWithItsTilesAfterBeingHidden()
        {
            var camera = NewCamera("TrayReturnCamera");
            var tray = ControlsPanel.Create(camera.transform);
            yield return null;

            // The tray is switched off with the GameObject while a full-screen page is up: a row that
            // does not build itself again comes back empty, and the buttons are gone for good.
            tray.gameObject.SetActive(false);
            yield return null;
            Assert.AreEqual(0, tray.Tiles.Count, "A hidden tray must not keep its tiles.");

            tray.gameObject.SetActive(true);
            yield return null;
            Assert.AreEqual(6, tray.Tiles.Count, "The tray must rebuild its six tiles when it is shown again.");
            Assert.IsTrue(tray.Tiles.All(t => t.GetComponent<Collider>() != null),
                "The rebuilt tiles must be clickable again.");

            Object.Destroy(camera.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayRebuildsItsCaptionsWhenTheLanguageChanges()
        {
            var camera = NewCamera("TrayLanguageCamera");
            var tray = ControlsPanel.Create(camera.transform);
            yield return null;

            var before = tray.Tiles.Select(t => t.Label).ToList();
            var previous = Loc.Current;
            try
            {
                Loc.Current = previous == Language.Ru ? Language.En : Language.Ru;
                tray.Relabel();
                yield return null;

                var after = tray.Tiles.Select(t => t.Label).ToList();
                Assert.AreEqual(6, after.Count, "The tray must keep its six tiles after a language switch.");
                Assert.AreNotEqual(string.Join("|", before), string.Join("|", after),
                    "The captions of the tray must follow the language of the table.");

                // The hot keys are language-independent: they must survive the rebuild.
                Assert.IsTrue(tray.Tiles.All(t => !string.IsNullOrWhiteSpace(t.KeyLabel)),
                    "Every rebuilt tile must still print its hot key.");
            }
            finally
            {
                Loc.Current = previous;
            }

            Object.Destroy(camera.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheReferenceInterfaceTakesOverTheTable()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null)
            {
                var host = new GameObject("MahjongTestHost");
                game = host.AddComponent<MahjongGame>();
            }

            // The game opens on the welcome screen, and the main interface belongs to the table.
            game.OpenTitle();
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.IsNotNull(game.Ui, "The game did not create the reference interface.");
            Assert.IsFalse(game.Ui.IsOpen,
                "The interface must be hidden while the welcome screen holds the table.");

            game.SkipWelcome();
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(game.Ui.IsOpen,
                "The interface must be shown once the table is dealt.");
            Assert.Greater(game.Ui.Plates.Count, 0,
                "The interface shown after the welcome screen must draw its plates.");

            // The old bottom tray is gone from the composition: it covered the player's hand and
            // reached into the right-hand seat (examples/Эскизы UI Маджонг.html).
            Assert.IsNotNull(game.Controls, "The legacy tray component must still exist for tools.");
            Assert.IsFalse(game.Controls.gameObject.activeSelf,
                "The legacy bottom tray must not be drawn over the table any more.");

            Object.Destroy(game.gameObject);
            yield return null;
        }
    }
}
