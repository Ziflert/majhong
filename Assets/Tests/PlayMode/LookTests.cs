using System.Collections;
using Mahjong.Controls;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The look of the interface (8.10) on the running prototype: one palette paints the whole screen —
    /// the welcome page, the table, the HUD, the tray and the tiles — and one number sizes it.
    ///
    /// The palettes themselves are checked headless (`look: …` in the rules self-test): here only what
    /// needs a screen is checked, namely that every surface asks <see cref="UiStyle"/> for its colour and
    /// that a step of the setting shows at once instead of after a restart.
    /// </summary>
    public sealed class LookTests
    {
        private static MahjongGame Game()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongLookTestHost").AddComponent<MahjongGame>();
            game.SkipWelcome();
            return game;
        }

        /// <summary>Puts the look of the table back the way the test found it.</summary>
        private static void Restore(MahjongGame game, UiTheme theme, double scale)
        {
            game.Settings.Theme = theme;
            game.Settings.UiScale = scale;
            game.PaintLook();
        }

        [UnityTest]
        public IEnumerator TheTableFollowsThePalette()
        {
            var game = Game();
            yield return null;

            var themeBefore = game.Settings.Theme;
            var scaleBefore = game.Settings.UiScale;

            try
            {
                game.Settings.Theme = UiThemes.Ivory;
                game.Settings.UiScale = UiThemeSettings.DefaultScale;
                game.PaintLook();
                yield return null;

                Assert.AreEqual("ivory", UiStyle.Theme.Id, "The palette in effect did not change.");

                var camera = Camera.main;
                Assert.AreEqual(UiStyle.ToColor(UiThemes.Ivory.Of(UiRole.Background)), camera!.backgroundColor,
                    "The camera still clears to the background of the old palette.");
                Assert.AreEqual(UiStyle.Of(UiRole.Wood), game.TableSurface.material.color,
                    "The wooden rim of the table kept the colour of the old palette.");
                Assert.AreEqual(UiStyle.Of(UiRole.Felt), game.Cloth.material.color,
                    "The cloth kept the colour of the old palette.");
                Assert.AreEqual(UiStyle.Of(UiRole.TextMain), game.Status.color,
                    "The HUD text kept the colour of the old palette.");

                // The skins are baked per palette: a tray tile has to be repainted in new textures, not
                // in the ones that were baked for the palette the game started with.
                foreach (var tile in game.Controls.Tiles)
                    Assert.AreEqual(UiSkins.Texture(tile.Face.Skin), tile.Face.Renderer.sharedMaterial.mainTexture,
                        "A tray tile is still drawn with the skin of the old palette.");

                var before = game.Settings.Theme;
                game.Settings.Theme = UiThemes.Default;
                game.PaintLook();
                yield return null;
                Assert.AreNotEqual(before.Of(UiRole.Felt), UiStyle.Of(UiRole.Felt),
                    "The two palettes do not differ, so this test proves nothing.");
            }
            finally
            {
                Restore(game, themeBefore, scaleBefore);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheThemeRowRepaintsTheWelcomePageAtOnce()
        {
            var game = Game();
            yield return null;
            yield return null;

            var themeBefore = game.Settings.Theme;
            var scaleBefore = game.Settings.UiScale;

            try
            {
                game.OpenTitle();
                yield return null;

                var paper = game.TitlePanel.Backdrop.material.color;
                Assert.AreEqual(UiStyle.Of(UiRole.Surface, 0.97f), paper,
                    "The welcome page is not painted with the palette in effect.");

                // Walk to the theme row and step it: the page has to repaint itself without a restart.
                for (var step = 0; step < game.Title.RowCount && game.Title.Selected != TitleItem.Look; step++)
                    game.TitleMove(1);
                Assert.AreEqual(TitleItem.Look, game.Title.Selected, "The welcome screen has no theme row.");
                Assert.IsTrue(game.TitleAdjust(1), "The theme row cannot be stepped.");
                yield return null;
                yield return null;

                Assert.AreNotEqual(themeBefore.Id, game.Settings.Theme.Id,
                    "The theme row did not change the palette.");
                Assert.AreNotEqual(paper, game.TitlePanel.Backdrop.material.color,
                    "The welcome page kept the colour of the palette it was painted with.");
                Assert.AreEqual(UiStyle.Of(UiRole.Surface, 0.97f), game.TitlePanel.Backdrop.material.color,
                    "The welcome page does not show the palette it switched to.");
            }
            finally
            {
                game.CloseTitle();
                Restore(game, themeBefore, scaleBefore);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSizeRowDrawsTheInterfaceBigger()
        {
            var game = Game();
            yield return null;
            yield return null;

            var themeBefore = game.Settings.Theme;
            var scaleBefore = game.Settings.UiScale;

            try
            {
                var camera = Camera.main;
                Assert.IsNotNull(camera, "The prototype has no camera to measure the interface against.");
                var fieldOfViewBefore = camera!.fieldOfView;
                var aspectBefore = camera.aspect;
                try
                {
                    // The tray is laid out from the frame and is capped by it, so the size setting is
                    // measured where nothing caps it: the HUD line and the plate behind it.
                    camera.fieldOfView = 34f;
                    camera.aspect = 16f / 9f;

                    game.Settings.UiScale = UiThemeSettings.DefaultScale;
                    game.PaintLook();
                    yield return new WaitForSeconds(0.05f);
                    var textAtHundred = game.Status.characterSize;
                    var plateAtHundred = game.HudPlate.localScale.x;

                    game.Settings.UiScale = 1.15d;
                    game.PaintLook();
                    yield return new WaitForSeconds(0.05f);
                    var textAtLarge = game.Status.characterSize;
                    var plateAtLarge = game.HudPlate.localScale.x;

                    Assert.Greater(textAtHundred, 0f, "The HUD line has no size to compare with.");
                    Assert.AreEqual(1.15f, textAtLarge / textAtHundred, 0.02f,
                        "One step of the size ladder did not draw the HUD line 115 % of its former size.");
                    Assert.Greater(plateAtLarge, plateAtHundred,
                        "The plate behind the HUD did not grow with the text it holds.");

                    // And the tray grows with the setting too, as far as its share of the frame allows:
                    // no tile may leave the screen at the largest step.
                    game.Settings.UiScale = UiThemeSettings.Scales[^1];
                    game.PaintLook();
                    yield return new WaitForSeconds(0.05f);
                    foreach (var tile in game.Controls.Tiles)
                    {
                        var point = camera.WorldToViewportPoint(tile.transform.position);
                        Assert.IsTrue(point.x is > 0f and < 1f && point.y is > 0f and < 1f,
                            $"A tray tile left the frame at {UiThemeSettings.ScaleText(game.Settings.UiScale)}.");
                    }
                }
                finally
                {
                    camera.fieldOfView = fieldOfViewBefore;
                    camera.aspect = aspectBefore;
                }
            }
            finally
            {
                Restore(game, themeBefore, scaleBefore);
            }

            yield return null;
        }
    }
}
