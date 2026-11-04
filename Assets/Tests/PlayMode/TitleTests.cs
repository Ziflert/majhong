using System;
using System.Collections;
using System.IO;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Replay;
using Mahjong.Rules;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The welcome screen inside the prototype (У-2): the game opens on it, the rows carry the settings
    /// a table is created with, picking a row either starts a game, opens the rulebook or honestly says
    /// the mode is not built yet, and the saved games the screen offers are the ones that open.
    ///
    /// The rows and their meaning are checked headless (`title: …` groups of the rules self-test); this
    /// is the drawing, the frame, the clicks and the way the welcome screen hands the table over.
    /// </summary>
    public sealed class TitleTests
    {
        private static MahjongGame Game()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<MahjongGame>();
            return existing != null ? existing : new GameObject("MahjongTitleTestHost").AddComponent<MahjongGame>();
        }

        /// <summary>Opens the welcome screen in a known language, so the captions can be compared.</summary>
        private static void OpenTitleIn(MahjongGame game, Language language)
        {
            game.Settings.Language = language;
            Loc.Current = language;
            game.OpenTitle();
        }

        /// <summary>Puts the cursor on a row of the open welcome screen; the cursor wraps, so this ends.</summary>
        private static void Select(MahjongGame game, TitleItem item)
        {
            for (var step = 0; step < game.Title.RowCount && game.Title.Selected != item; step++) game.TitleMove(1);
            Assert.AreEqual(item, game.Title.Selected, "the row could not be reached on the screen");
        }

        /// <summary>
        /// A fresh table, built the way the player builds it: no leftovers from the tests that ran before.
        /// </summary>
        private static IEnumerator FreshTable(Action<MahjongGame> assertions)
        {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<MahjongGame>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var game = new GameObject("MahjongTitleTestHost").AddComponent<MahjongGame>();
            yield return null;
            yield return null;

            assertions(game);
        }

        [UnityTest]
        public IEnumerator TheGameOpensOnTheWelcomeScreen()
        {
            yield return FreshTable(game =>
            {
                Assert.IsTrue(game.IsTitleOpen, "The game dealt a hand instead of showing the welcome screen.");
                Assert.IsNull(game.State, "A hand was dealt behind the welcome screen.");
                Assert.IsTrue(game.TitlePanel.gameObject.activeInHierarchy, "The welcome screen is not on the screen.");
                Assert.IsTrue(game.TitlePanel.IsOpen, "The welcome screen is not open as far as the panel knows.");
                Assert.IsFalse(game.Status.gameObject.activeInHierarchy,
                    "The table HUD is drawn over the welcome screen.");
                Assert.AreEqual(TitleItem.NewGame, game.Title.Selected, "The cursor does not start on a new game.");
            });

            // The suite shares one table: leave a live game for the tests that follow.
            var game = Game();
            game.SkipWelcome();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheRowsCarryTheirCaptionsAndFitTheFrame()
        {
            var game = Game();
            OpenTitleIn(game, Language.Ru);
            yield return null;

            Assert.AreEqual(TitleScreen.MaxRows, game.TitlePanel.RowCount,
                "The panel does not have a strip for every row the model can show.");
            Assert.IsNotEmpty(game.TitlePanel.Title.text, "The welcome screen has no name of the game.");
            StringAssert.IsMatch("[А-Яа-яЁё]", game.TitlePanel.Title.text, "The game name is not in Russian.");
            StringAssert.Contains("▸", game.TitlePanel.RowText(game.Title.Index),
                "The cursor does not mark the row it is on: " + game.TitlePanel.RowText(game.Title.Index));
            StringAssert.Contains(Loc.Get("title.new"), game.TitlePanel.RowText(0),
                "The first row is not a new game: " + game.TitlePanel.RowText(0));
            StringAssert.Contains(Loc.Get("title.quit"), game.TitlePanel.RowText(game.Title.RowCount - 1),
                "The last row is not the way out.");

            // Every caption sits on its own strip and the rows run down the panel.
            for (var row = 0; row < game.Title.RowCount; row++)
            {
                Assert.AreEqual(game.TitlePanel.RowStripHeight(row), game.TitlePanel.RowLabelHeight(row), 0.005f,
                    $"The caption of row {row} is not on its strip.");
                Assert.IsNotEmpty(game.TitlePanel.RowText(row), $"Row {row} carries no caption.");
                if (row > 0)
                    Assert.Less(game.TitlePanel.RowStripHeight(row), game.TitlePanel.RowStripHeight(row - 1),
                        $"Row {row} is not below row {row - 1}.");
            }

            // The panel is measured against the camera, so it has to stay inside the frame.
            var camera = Camera.main;
            Assert.IsNotNull(camera, "The prototype has no camera to measure the welcome screen against.");
            foreach (var corner in new[]
                     {
                         new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                         new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                     })
            {
                var point = camera!.WorldToViewportPoint(game.TitlePanel.Backdrop.transform.TransformPoint(corner));
                Assert.Greater(point.z, 0f, "The welcome screen is behind the camera.");
                Assert.IsTrue(point.x is >= 0f and <= 1f && point.y is >= 0f and <= 1f,
                    $"The welcome screen hangs off the frame at {point.x:0.00} x {point.y:0.00}.");
            }

            // A row is a hit box of its own: a ray down the middle of the frame lands on a row.
            var ray = camera!.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            Assert.IsTrue(game.TitlePanel.TryPick(ray, out var picked), "No row answers a click in the middle.");
            Assert.IsTrue(picked >= 0 && picked < game.Title.RowCount, $"The click landed on row {picked}.");
        }

        [UnityTest]
        public IEnumerator TheSettingsOnTheScreenReachTheTable()
        {
            var game = Game();
            OpenTitleIn(game, Language.Ru);
            yield return null;

            // The rules profile is the setting the whole table is created with.
            Select(game, TitleItem.Variant);
            StringAssert.Contains(Text.VariantName(game.Settings.Variant), game.TitlePanel.RowText(game.Title.Index),
                "The profile row does not show the profile the table would be played by: "
                + game.TitlePanel.RowText(game.Title.Index));

            game.TitleAdjust(1);
            yield return null;
            Assert.AreEqual(RuleVariant.Classic144.Id, game.Settings.Variant.Id,
                "Stepping the profile on the screen did not change the rules of the table.");
            StringAssert.Contains(Text.VariantName(RuleVariant.Classic144), game.TitlePanel.RowText(game.Title.Index),
                "The profile row does not print the preset it was stepped to.");

            // The language changes the words on the screen at once.
            Select(game, TitleItem.Language);
            var spoken = Loc.Current;
            game.TitleAdjust(1);
            yield return null;
            Assert.AreNotEqual(spoken, Loc.Current, "The language row does not change the interface language.");
            StringAssert.Contains(Loc.Get("title.new"), game.TitlePanel.RowText(0),
                "The rows are still written in the language the table left behind: "
                + game.TitlePanel.RowText(0));

            // Put the table back the way it was: the suite shares one game.
            game.Settings.Variant = RuleVariant.Sports;
            game.Settings.Language = spoken;
            Loc.Current = spoken;
            game.OpenTitle();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PickingANewGameDealsTheHandItPromised()
        {
            var game = Game();
            OpenTitleIn(game, Language.Ru);
            yield return null;

            Select(game, TitleItem.Variant);
            game.TitleAdjust(1);
            Assert.AreEqual(RuleVariant.Classic144.Id, game.Settings.Variant.Id, "The profile did not change.");

            Select(game, TitleItem.NewGame);
            game.TitleActivate();
            yield return null;
            yield return null;

            Assert.IsFalse(game.IsTitleOpen, "The welcome screen stayed on after the game started.");
            Assert.IsFalse(game.TitlePanel.gameObject.activeInHierarchy, "The welcome screen is still drawn.");
            Assert.IsNotNull(game.State, "Picking a new game did not deal a hand.");
            Assert.AreEqual(RuleVariant.Classic144, game.Session.Variant, "The hand is not played by the chosen rules.");
            Assert.IsTrue(game.Status.gameObject.activeInHierarchy, "The table HUD did not come back.");

            // A second pick of the same row deals a new game: points and the log start over.
            game.OpenTitle();
            Select(game, TitleItem.NewGame);
            game.TitleActivate();
            yield return null;
            Assert.AreEqual(1, game.MoveLog.Hands.Count, "A new game did not start a log with a single deal.");
            Assert.AreEqual(0, game.HandsPlayed, "A new game did not reset the points of the table.");

            game.Settings.Variant = RuleVariant.Sports;
        }

        [UnityTest]
        public IEnumerator TheRulebookOpensFromTheWelcomeScreenAndComesBack()
        {
            var game = Game();
            OpenTitleIn(game, Language.Ru);
            yield return null;

            Select(game, TitleItem.Rules);
            game.TitleActivate();
            yield return null;

            Assert.IsTrue(game.Rules.IsOpen, "The rulebook row did not open the rulebook.");
            Assert.IsFalse(game.TitlePanel.gameObject.activeInHierarchy,
                "The rows are drawn over the rulebook page.");

            game.Rules.Close();
            yield return null;

            Assert.IsTrue(game.TitlePanel.gameObject.activeInHierarchy,
                "The welcome screen did not come back when the rulebook was closed.");
            Assert.IsTrue(game.IsTitleOpen, "The welcome screen forgot that it was open.");
        }

        [UnityTest]
        public IEnumerator AnUnbuiltModeSaysSoInsteadOfStarting()
        {
            var game = Game();
            OpenTitleIn(game, Language.Ru);
            yield return null;

            var hands = game.HandsPlayed;
            var moves = game.MoveLog.MoveCount;
            foreach (var item in new[] { TitleItem.LocalGame, TitleItem.Multiplayer })
            {
                Select(game, item);
                game.TitleActivate();
                yield return null;

                Assert.IsTrue(game.IsTitleOpen, $"The {item} row started something the engine cannot play.");
                Assert.AreEqual(hands, game.HandsPlayed, $"The {item} row dealt a new hand.");
                Assert.AreEqual(moves, game.MoveLog.MoveCount, $"The {item} row played on a table nobody can see.");
                Assert.IsNotEmpty(game.TitlePanel.Notice.text,
                    $"The {item} row did nothing and said nothing about it.");
            }
        }

        [UnityTest]
        public IEnumerator ContinueOpensTheSaveTheScreenOffers()
        {
            var folder = Path.Combine(Path.GetTempPath(), "mahjong-title-playmode");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);

            var previousDirectory = Environment.GetEnvironmentVariable("MAHJONG_REPLAY_DIR");
            Environment.SetEnvironmentVariable("MAHJONG_REPLAY_DIR", folder);
            try
            {
                // A save is a deal with its moves; a deal alone is enough to continue from.
                var log = new SessionLog { Variant = RuleVariant.Sports };
                log.Info = new SessionLog.SessionInfo(TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(150));
                log.BeginHand(0, 4242, Wind.East);
                log.Save(Path.Combine(folder, "welcome.mahjong"));

                var game = Game();
                OpenTitleIn(game, Language.Ru);
                yield return null;

                Assert.IsTrue(game.Title.CanContinue, "The screen does not see the saved game in the folder.");
                Select(game, TitleItem.Continue);
                StringAssert.Contains(SaveSlot.HandsText(1), game.TitlePanel.RowText(game.Title.Index),
                    "The continue row does not say what it would open: "
                    + game.TitlePanel.RowText(game.Title.Index));

                game.TitleActivate();
                yield return null;
                yield return null;

                Assert.IsFalse(game.IsTitleOpen, "Continue left the welcome screen open.");
                Assert.IsNotNull(game.State, "Continue did not bring the saved hand back to the table.");
                Assert.AreEqual(1, game.MoveLog.Hands.Count, "The resumed game lost its deal.");

                // Leave a live game for the tests that follow.
                game.StartNewGame(7);
                yield return null;
            }
            finally
            {
                Environment.SetEnvironmentVariable("MAHJONG_REPLAY_DIR", previousDirectory);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
