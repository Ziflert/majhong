using System;
using System.Collections;
using System.IO;
using Mahjong.Controls;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The settings file (8.11) on the running prototype: a choice made on screen is written to disk at
    /// once and comes back as the same table, and a capture run can point the file somewhere else.
    ///
    /// The format itself — every ladder, every broken line, the order defaults → file → environment —
    /// is checked headless (`settings: …` in the rules self-test). What needs a screen is the one thing
    /// the rules assembly cannot know: that stepping a row really reaches the disk.
    /// </summary>
    public sealed class SettingsTests
    {
        private static MahjongGame Game()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongSettingsTestHost").AddComponent<MahjongGame>();
            game.SkipWelcome();
            return game;
        }

        /// <summary>A file of this test's own, so nobody's real settings are touched.</summary>
        private static string TestSettingsPath() =>
            Path.Combine(Path.GetTempPath(), "mahjong-settings-playtest", "settings.txt");

        private static void ClearTestFile()
        {
            try
            {
                var folder = Path.GetDirectoryName(TestSettingsPath());
                if (folder != null && Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                // A leftover file of a previous run is harmless: the test overwrites it.
            }
        }

        [UnityTest]
        public IEnumerator TheChoiceOnTheScreenReachesTheFile()
        {
            var game = Game();
            yield return null;
            yield return null;

            var previous = Environment.GetEnvironmentVariable("MAHJONG_SETTINGS");
            var themeBefore = game.Settings.Theme;
            var scaleBefore = game.Settings.UiScale;
            var volumeBefore = game.Settings.SoundVolume;
            var path = TestSettingsPath();

            try
            {
                ClearTestFile();
                Environment.SetEnvironmentVariable("MAHJONG_SETTINGS", path);
                Assert.AreEqual(path, MahjongGame.SettingsPath(), "MAHJONG_SETTINGS does not name the settings file.");

                game.OpenTitle();
                yield return null;

                // Walk to the palette row and step it with the same call the player's key makes.
                for (var step = 0; step < game.Title.RowCount && game.Title.Selected != TitleItem.Look; step++)
                    game.TitleMove(1);
                Assert.AreEqual(TitleItem.Look, game.Title.Selected, "The welcome screen has no theme row.");
                Assert.IsTrue(game.TitleAdjust(1), "The theme row cannot be stepped.");
                yield return null;

                Assert.IsTrue(File.Exists(path), "Stepping a setting did not write the settings file.");

                var written = SettingsFile.Read(path);
                Assert.AreEqual(game.Settings.Theme.Id, written.Theme.Id,
                    "The file does not hold the palette the player just chose.");
                Assert.AreEqual(game.Settings.UiScale, written.UiScale,
                    "The file does not hold the size of the interface.");
                Assert.AreEqual(game.Settings.SoundVolume, written.SoundVolume,
                    "The file does not hold the volume of the table.");
                Assert.AreEqual(game.Settings.Variant.Id, written.Variant.Id,
                    "The file does not hold the profile the table plays by.");
                Assert.AreEqual(game.Settings.Language, written.Language,
                    "The file does not hold the language of the interface.");

                // The size row as well: the file of the same run holds both steps, not only the last one.
                game.Settings.UiScale = UiThemeSettings.StepScale(game.Settings.UiScale, 1);
                game.SaveSettings();
                Assert.AreEqual(game.Settings.UiScale, SettingsFile.Read(path).UiScale,
                    "The size written by the second save is not the size in effect.");

                var text = File.ReadAllText(path);
                Assert.IsTrue(text.Contains($"theme={game.Settings.Theme.Id}", StringComparison.Ordinal),
                    "The file does not name the palette in the form the reader understands.");
                Assert.IsTrue(text.Contains($"version={SettingsFile.Version}", StringComparison.Ordinal),
                    "The file does not carry its format version.");
            }
            finally
            {
                game.CloseTitle();
                game.Settings.Theme = themeBefore;
                game.Settings.UiScale = scaleBefore;
                game.Settings.SoundVolume = volumeBefore;
                game.PaintLook();
                Environment.SetEnvironmentVariable("MAHJONG_SETTINGS", previous);
                ClearTestFile();
            }

            yield return null;
        }

        /// <summary>
        /// A file written by a run and read by the next one: the same palette, size and volume. This is
        /// the whole point of 8.11 — the choice outliving the process — checked without a second launch.
        /// </summary>
        [UnityTest]
        public IEnumerator TheFileOpensTheNextRunAsTheLastOneWasLeft()
        {
            var game = Game();
            yield return null;

            var previous = Environment.GetEnvironmentVariable("MAHJONG_SETTINGS");
            var path = TestSettingsPath();
            try
            {
                ClearTestFile();
                Environment.SetEnvironmentVariable("MAHJONG_SETTINGS", path);

                var chosen = new GameSettings
                {
                    Language = Mahjong.Localization.Language.En,
                    Theme = UiThemes.Ivory,
                    UiScale = 1.3d,
                    SoundVolume = 0d,
                    Variant = Mahjong.Rules.RuleVariant.Classic144,
                    ClockPace = ClockPace.Unlimited
                };
                chosen.SetSpeedIndex(1);
                Assert.IsTrue(SettingsFile.Write(path, chosen), "The settings file could not be written.");

                // What the next launch does: the file first, then the environment on top of it.
                var next = GameSettings.FromEnvironment(Mahjong.Bots.BotLevel.Strategic, SettingsFile.Read(path));
                Assert.AreEqual("ivory", next.Theme.Id, "The next run does not open on the chosen palette.");
                Assert.AreEqual(1.3d, next.UiScale, "The next run does not open on the chosen size.");
                Assert.AreEqual(0d, next.SoundVolume, "The next run does not open with the sound off.");
                Assert.AreEqual(Mahjong.Localization.Language.En, next.Language,
                    "The next run does not open in the chosen language.");
                Assert.AreEqual("classic144", next.Variant.Id, "The next run does not open on the chosen rules.");
                Assert.AreEqual(ClockPace.Unlimited, next.ClockPace, "The next run does not open on the chosen windows.");
                Assert.AreEqual(0.5d, next.Speed, "The next run does not open on the chosen bot speed.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("MAHJONG_SETTINGS", previous);
                ClearTestFile();
            }

            yield return null;
        }
    }
}
