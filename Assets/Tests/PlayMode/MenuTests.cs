using System.Collections;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The menu inside the running prototype: Esc pauses the hand and shows the rows, the arrows step
    /// the settings and the language changes the words on screen, and picking "back to the table"
    /// returns to the hand where it stopped. The panel is made of scene objects, so it is checked here.
    /// </summary>
    public sealed class MenuTests
    {
        private static MahjongGame Game()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongMenuTestHost").AddComponent<MahjongGame>();
            // The welcome screen holds the table at launch; a test of the menu starts its own game.
            game.SkipWelcome();
            return game;
        }

        [UnityTest]
        public IEnumerator MenuPausesTheHandAndShowsItsRows()
        {
            var game = Game();
            yield return null;
            yield return null;

            Assert.IsFalse(game.IsMenuOpen, "The menu covers the table before anyone asked for it.");

            game.OpenMenu();
            yield return null;

            Assert.IsTrue(game.IsMenuOpen, "Opening the menu did nothing.");
            Assert.IsTrue(game.MenuPanel.gameObject.activeInHierarchy, "The menu object is still disabled.");
            Assert.IsFalse(game.Status.gameObject.activeInHierarchy,
                "The HUD is still drawn on top of the open menu.");
            Assert.AreEqual(GameMenu.TotalRows, game.MenuPanel.RowCount, "The menu lost some of its rows.");
            StringAssert.Contains("▸", game.MenuPanel.RowText(game.Menu.Index),
                "The cursor does not mark the row it is on: " + game.MenuPanel.RowText(game.Menu.Index));
            StringAssert.Contains(Loc.Get("menu.language"), game.MenuPanel.RowText(0),
                "The first row is not the language: " + game.MenuPanel.RowText(0));
            Assert.IsNotEmpty(game.MenuPanel.Title.text, "The menu has no title.");
            StringAssert.IsMatch("[А-Яа-яЁё]", game.MenuPanel.Title.text, "The menu title is not in Russian.");

            // Every caption sits on its own strip and the rows run down the panel: a caption drawn at
            // twice its row's height is exactly the bug the first screenshots showed.
            for (var row = 0; row < game.MenuPanel.RowCount; row++)
            {
                Assert.AreEqual(game.MenuPanel.RowStripHeight(row), game.MenuPanel.RowLabelHeight(row), 0.005f,
                    $"The caption of row {row} is not on its strip.");
                if (row > 0)
                    Assert.Less(game.MenuPanel.RowStripHeight(row), game.MenuPanel.RowStripHeight(row - 1),
                        $"Row {row} is not below row {row - 1}.");
            }

            // The panel is measured against the camera, so it has to stay inside the frame.
            var camera = Camera.main;
            Assert.IsNotNull(camera, "The prototype has no camera to measure the menu against.");
            foreach (var corner in new[]
                     {
                         new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                         new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                     })
            {
                var point = camera!.WorldToViewportPoint(game.MenuPanel.Backdrop.transform.TransformPoint(corner));
                Assert.Greater(point.z, 0f, "The menu is behind the camera.");
                Assert.IsTrue(point.x is >= 0f and <= 1f && point.y is >= 0f and <= 1f,
                    $"The menu hangs off the screen at {point.x:0.00} x {point.y:0.00}.");
            }

            // The hand is paused: no bot moves and no clock while the menu is open.
            var movesBefore = game.MoveLog.MoveCount;
            var clockBefore = game.HandClock.IsRunning;
            for (var frame = 0; frame < 30; frame++) yield return null;
            Assert.AreEqual(movesBefore, game.MoveLog.MoveCount, "The bots played on behind the open menu.");
            Assert.AreEqual(clockBefore, game.HandClock.IsRunning, "The hand clock kept running behind the menu.");

            // Navigation moves the cursor and the highlight follows it.
            var first = game.Menu.Index;
            game.MenuMove(1);
            yield return null;
            Assert.AreEqual((first + 1) % GameMenu.TotalRows, game.Menu.Index, "The cursor did not move.");
            StringAssert.Contains("▸", game.MenuPanel.RowText(game.Menu.Index), "The highlight stayed behind.");

            game.CloseMenu();
            yield return null;
            Assert.IsFalse(game.IsMenuOpen, "The menu did not close.");
            Assert.IsTrue(game.Status.gameObject.activeInHierarchy, "The HUD did not come back after the menu.");
            Assert.IsNotEmpty(game.Status.text, "The table HUD is empty after the menu.");
        }

        [UnityTest]
        public IEnumerator MenuStepsTheSettingsAndChangesTheLanguage()
        {
            var game = Game();
            yield return null;
            yield return null;

            var settings = game.Settings;
            var languageBefore = game.UiLanguage;
            var speedBefore = settings.Speed;

            game.OpenMenu();
            yield return null;

            // Language row: a step flips it and every caption follows at once.
            game.MenuMove(-game.Menu.Index);
            Assert.AreEqual(MenuItem.Language, game.Menu.Selected, "The cursor is not on the language row.");
            Assert.IsTrue(game.MenuAdjust(1), "The language row cannot be stepped.");
            yield return null;
            Assert.AreNotEqual(languageBefore, game.UiLanguage, "The interface language did not change.");
            var expected = game.UiLanguage == Language.Ru ? "Русский" : "English";
            StringAssert.Contains(expected, game.MenuPanel.RowText(0), "The language row shows another language.");
            StringAssert.Contains(Loc.Get("menu.title"), game.MenuPanel.Title.text, "The title did not follow the language.");

            // Speed row: a step changes the setting the bots actually run on.
            game.MenuMove(1);
            Assert.AreEqual(MenuItem.Speed, game.Menu.Selected, "The cursor is not on the speed row.");
            Assert.IsTrue(game.MenuAdjust(1), "The speed row cannot be stepped.");
            yield return null;
            Assert.Greater(settings.Speed, speedBefore, "The bots did not get faster.");
            StringAssert.Contains("×" + settings.Speed.ToString("0.##",
                    System.Globalization.CultureInfo.InvariantCulture), game.MenuPanel.RowText(1),
                "The speed row does not print the new speed: " + game.MenuPanel.RowText(1));

            // Level row: three settings, and the row never claims a fourth.
            game.MenuMove(1);
            Assert.AreEqual(MenuItem.Level, game.Menu.Selected, "The cursor is not on the bot strength row.");
            while (game.MenuAdjust(1)) { }
            Assert.AreEqual(Mahjong.Bots.BotLevel.Strategic, settings.Level, "The strongest level is not reachable.");
            Assert.IsFalse(game.MenuAdjust(1), "The level row stepped past the strongest bots.");

            // Put the table back the way the test found it.
            game.MenuAdjust(-1);
            game.MenuAdjust(-1);
            settings.SetSpeedIndex(2);
            game.CloseMenu();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LanguageKeyAndMenuRowAreOneSetting()
        {
            var game = Game();
            yield return null;
            yield return null;

            var languageBefore = game.UiLanguage;

            // The L key goes through the same setting the menu row prints: after the key the row must
            // name the language the table is actually in, otherwise its next step would change nothing.
            game.ToggleLanguage();
            yield return null;
            var afterKey = game.UiLanguage;
            Assert.AreNotEqual(languageBefore, afterKey, "The language key did nothing.");

            game.OpenMenu();
            yield return null;
            var expected = afterKey == Language.Ru ? "Русский" : "English";
            StringAssert.Contains(expected, game.MenuPanel.RowText(0),
                "The language row disagrees with the language key: " + game.MenuPanel.RowText(0));

            // And the row still steps the language: one step undoes the key.
            Assert.IsTrue(game.MenuAdjust(1), "The language row cannot be stepped after the key.");
            yield return null;
            Assert.AreEqual(languageBefore, game.UiLanguage, "The menu row and the key pull in opposite directions.");

            game.CloseMenu();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuOpensTheRulebookAndTheReview()
        {
            var game = Game();
            yield return null;
            yield return null;

            // The rules row opens the rulebook and leaves the menu closed.
            game.OpenMenu();
            game.MenuMove(-game.Menu.Index);      // the language row
            game.MenuMove(11);                    // the rules row
            Assert.AreEqual(MenuItem.Rules, game.Menu.Selected, "The cursor is not on the rules row.");
            game.MenuActivate();
            yield return null;
            Assert.IsFalse(game.IsMenuOpen, "The menu stayed open over the rulebook.");
            Assert.IsTrue(game.Rules.IsOpen, "The rules row did not open the rulebook.");
            // У-5: a book opened from the menu closes back into the menu.
            game.CloseRules();
            yield return null;
            Assert.IsTrue(game.IsMenuOpen, "The book dropped the player at the table instead of the menu.");
            game.CloseMenu();
            yield return null;

            // The review row opens the review of the game in progress.
            game.OpenMenu();
            game.MenuMove(-game.Menu.Index);
            game.MenuMove(10);
            Assert.AreEqual(MenuItem.Review, game.Menu.Selected, "The cursor is not on the review row.");
            game.MenuActivate();
            yield return null;
            Assert.IsFalse(game.IsMenuOpen, "The menu stayed open over the review.");
            Assert.IsTrue(game.IsReplayOpen, "The review row did not open the review:\n" + game.Status.text);
            game.CloseReplay();
            yield return null;

            // And "back to the table" closes the menu without opening anything.
            game.OpenMenu();
            game.MenuMove(-1);
            Assert.AreEqual(MenuItem.Close, game.Menu.Selected, "The last row is not the way back.");
            game.MenuActivate();
            yield return null;
            Assert.IsFalse(game.IsMenuOpen, "The last row did not close the menu.");
            Assert.IsFalse(game.Rules.IsOpen, "The way back opened the rulebook.");
            Assert.IsFalse(game.IsReplayOpen, "The way back opened the review.");
        }
    }
}
