using System.Collections;
using System.Linq;
using Mahjong.Controls;
using Mahjong.Rules;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// Checks the action bar itself: buttons are built from legal actions, carry readable captions,
    /// can be picked with a ray and disappear when the prompt closes. The bar is built by the same
    /// code the game uses, so these checks cover the 3D part that headless tests cannot reach.
    /// </summary>
    public sealed class ActionBarTests
    {
        private static Tile Tile(TileSuit suit, int rank, int id) => new(new TileType(suit, rank), id);

        private static LegalAction Action(ActionKind kind, Tile? tile = null, params Tile[] required) =>
            new(kind, 1, tile, required, $"{kind} of {tile?.Type}");

        private static ActionPrompt SamplePrompt()
        {
            var discard = Tile(TileSuit.Characters, 5, 40);
            return ActionPrompt.FromLegalActions(new[]
            {
                Action(ActionKind.Pass),
                Action(ActionKind.Chow, discard, Tile(TileSuit.Characters, 3, 1), Tile(TileSuit.Characters, 4, 2)),
                Action(ActionKind.Pung, discard, Tile(TileSuit.Characters, 5, 3), Tile(TileSuit.Characters, 5, 4)),
                Action(ActionKind.Win, discard)
            });
        }

        [UnityTest]
        public IEnumerator BarBuildsReadableButtonsForEveryOption()
        {
            var cameraObject = new GameObject("BarTestCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            var bar = ActionBar.Create(cameraObject.transform);
            var prompt = SamplePrompt();
            bar.Show(prompt);
            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(bar.IsOpen, "The bar did not open for a prompt with decisions.");
            Assert.AreEqual(prompt.Count, bar.Buttons.Count, "Every option must have a button.");

            foreach (var button in bar.Buttons)
            {
                var caption = button.GetComponentInChildren<TextMesh>();
                Assert.IsNotNull(caption, $"Button {button.name} has no TextMesh caption.");
                Assert.IsNotNull(caption!.font, $"Button {button.name} has no font, its caption will not render.");
                Assert.IsNotEmpty(caption.text, $"Button {button.name} has an empty caption.");
                StringAssert.Contains(button.Label, caption.text, "The caption must show the button label.");

                Assert.IsNotNull(button.GetComponent<Collider>(),
                    $"Button {button.name} has no collider and cannot be clicked.");
            }

            // The button root owns the collider, so a ray from the camera through the button centre
            // must hit it. Built from world points: in batch mode the Game view has no real size,
            // so screen point arithmetic is not reliable (checked separately below).
            var first = bar.Buttons[0];
            var origin = camera.transform.position;
            var target = first.transform.position;
            var ray = new Ray(origin, (target - origin).normalized);

            var collider = first.GetComponent<Collider>();
            var hits = Physics.RaycastAll(ray, 5f);
            Debug.Log($"[bar] first={first.name} world={target} collider={collider} bounds={collider?.bounds}"
                      + $" active={first.gameObject.activeInHierarchy} hits={hits.Length}"
                      + $" [{string.Join(", ", hits.Select(h => h.collider.name + " d=" + h.distance))}]");

            Assert.IsTrue(bar.TryPick(ray, out var picked), "A ray through the middle of a button did not hit it.");
            Assert.AreEqual(first.Action.Kind, picked.Kind, "The bar picked the wrong action.");

            var hovered = bar.Hover(ray);
            Assert.IsNotNull(hovered, "Hovering a button returned no option.");
            Assert.AreEqual(first.Label, hovered!.Label);

            bar.SetHint(hovered.Hint);
            Assert.AreEqual(hovered.Hint, bar.Hint.text, "The hint line does not show the engine description.");

            // The bar must stay in front of the camera and inside the frame.
            var viewport = camera.WorldToViewportPoint(first.transform.position);
            Debug.Log($"[bar] screen={Screen.width}x{Screen.height} viewport={viewport} buttons={bar.Buttons.Count}"
                      + $" collider={first.GetComponent<Collider>()?.bounds} autoSync={Physics.autoSyncTransforms}");
            Assert.Greater(viewport.z, 0f, "The bar is behind the camera and invisible.");
            Assert.IsTrue(viewport.x is > 0f and < 1f && viewport.y is > 0f and < 1f,
                $"The bar is outside the frame at viewport {viewport}.");

            if (Screen.width > 1 && Screen.height > 1)
            {
                var screenRay = camera.ScreenPointToRay(camera.WorldToScreenPoint(first.transform.position));
                Assert.IsTrue(bar.TryPick(screenRay, out _),
                    "A click on screen coordinates of a button did not reach it.");
            }

            bar.Hide();
            yield return null;
            Assert.AreEqual(0, bar.Buttons.Count, "Hiding the bar must destroy its buttons.");
            StringAssert.AreEqualIgnoringCase(string.Empty, bar.Hint.text, "Hiding the bar must clear the hint.");

            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BarIgnoresRaysThatMiss()
        {
            var cameraObject = new GameObject("BarMissCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var bar = ActionBar.Create(cameraObject.transform);
            bar.Show(SamplePrompt());
            yield return null;

            // straight backwards, away from the bar
            Assert.IsFalse(bar.TryPick(new Ray(Vector3.zero, Vector3.back), out _),
                "A ray pointing away from the bar must not pick a button.");
            Assert.IsNull(bar.Hover(new Ray(Vector3.zero, Vector3.back)));

            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameKeepsTheBarInSyncWithThePrompt()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongTestHost").AddComponent<MahjongGame>();
            // The welcome screen holds the table at launch; a test of the table starts its own game.
            game.SkipWelcome();

            var previousScale = Time.timeScale;
            Time.timeScale = 6f;
            yield return null;
            yield return null;
            Time.timeScale = previousScale;

            Assert.IsNotNull(game.Bar, "The game did not create the action bar.");
            Assert.AreEqual(game.Prompt.IsOpen, game.Bar.IsOpen,
                "The bar is out of sync with the prompt the engine offered.");
            Assert.IsTrue(game.Prompt.Options.All(option => option.Action.Kind != ActionKind.Discard),
                "Discarding is a tile click and must not appear as a button.");
            Assert.IsTrue(game.Prompt.Options.All(option => option.Action.Kind != ActionKind.Draw),
                "Drawing is automatic and must not appear as a button.");
        }
    }
}
