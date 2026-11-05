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
    /// captions, a hot key on each, colliders that a ray from the camera can hit, hover feedback,
    /// and the same actions the keyboard would trigger. Built from the same primitives as the
    /// action bar, so it covers the 3D part headless tests cannot reach.
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

        [UnityTest]
        public IEnumerator TrayBuildsSixTilesWithReadableCaptions()
        {
            var cameraObject = new GameObject("TrayCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

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
                Assert.IsFalse(string.IsNullOrWhiteSpace(caption.text),
                    $"Tile {tile.name} has an empty caption.");
                StringAssert.Contains(tile.Key.ToString(), caption.text,
                    $"The caption of {tile.name} must show the hot key.");

                Assert.IsNotNull(tile.GetComponent<Collider>(),
                    $"Tile {tile.name} has no collider and cannot be clicked.");
            }

            // All six tiles must lie on the same row and stay inside the frame.
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

            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayTilesCanBePickedWithARay()
        {
            var cameraObject = new GameObject("TrayPickCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

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

            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayIgnoresRaysThatMiss()
        {
            var cameraObject = new GameObject("TrayMissCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            var tray = ControlsPanel.Create(camera.transform);
            yield return null;

            Assert.IsFalse(tray.TryPick(new Ray(Vector3.zero, Vector3.back), out _),
                "A ray pointing away from the tray must not pick a tile.");
            Assert.IsNull(tray.Hover(new Ray(Vector3.zero, Vector3.back)));

            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayTilesCanSetHoveredAndEnabled()
        {
            var cameraObject = new GameObject("TrayStateCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -5f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            var tray = ControlsPanel.Create(camera.transform);
            yield return null;

            var saveTile = tray.Tiles.Single(t => t.Type == ControlType.Save);
            var rulebookTile = tray.Tiles.Single(t => t.Type == ControlType.Rulebook);

            // Save is disabled while no hand is played: check the grey colours.
            var saveRenderer = saveTile.GetComponent<Renderer>();
            var saveCaption = saveTile.GetComponentInChildren<TextMesh>();
            Assert.IsNotNull(saveRenderer);
            Assert.IsNotNull(saveCaption);
            // A disabled tile uses the disabled face colour defined in ControlTile.
            var expectedDisabled = new Color(0.55f, 0.56f, 0.55f);
            Assert.AreEqual(expectedDisabled, saveRenderer.material.color,
                "A save tile while no hand is played must be disabled.");

            // Hovering a disabled tile keeps it disabled but highlights the face.
            var ray = new Ray(camera.transform.position,
                (saveTile.transform.position - camera.transform.position).normalized);
            tray.Hover(ray);
            Assert.AreEqual(Color.green, saveRenderer.material.color,
                "Hovering must change the face colour even on a disabled tile.");

            tray.Hide();
            yield return null;
            Assert.AreEqual(0, tray.Tiles.Count, "Hiding the tray must destroy its tiles.");

            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayIsInvisibleWhileTheWelcomeScreenIsUp()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null)
            {
                var host = new GameObject("MahjongTestHost");
                game = host.AddComponent<MahjongGame>();
            }
            game.SkipWelcome();
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.IsNotNull(game.Controls, "The game did not create the controls panel.");
            Assert.IsFalse(game.Controls.gameObject.activeSelf,
                "The tray must be hidden while the welcome screen holds the table.");

            game.OpenTitle();
            yield return null;
            Assert.IsFalse(game.Controls.gameObject.activeSelf,
                "The tray must stay hidden while the welcome screen is up.");

            game.SkipWelcome();
            yield return null;
            Assert.IsTrue(game.Controls.gameObject.activeSelf,
                "The tray must be shown once the table is dealt.");

            Object.Destroy(game.gameObject);
            yield return null;
        }
    }
}
