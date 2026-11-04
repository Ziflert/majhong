using System;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Localization;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the in-game menu: the settings (language, bot strength, bot speed) parse from the
    /// environment, step within their bounds and scale the bot delay; the menu rows step the right
    /// setting and report the command a pick should run.
    ///
    /// Kept in its own file and wired into <see cref="RulesSelfTest.RunAll"/>, so the behaviour of the
    /// menu is checked headless and inside Unity, with only the drawing left to the play-mode tests.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void MenuGroups(SelfTestReport report)
        {
            Run(report, "menu: the settings parse, step and scale the bot delay", () => SettingsParse(report));
            Run(report, "menu: the rows step the settings and report their commands", () => MenuRows(report));
        }

        private static void SettingsParse(SelfTestReport report)
        {
            var defaults = new GameSettings();
            report.Equal(1.0, defaults.Speed, "the game starts at normal speed");
            report.Equal(BotLevel.Strategic, defaults.Level, "the strongest bots are the default");
            report.Equal("×1", defaults.SpeedText(), "normal speed prints as ×1");
            report.Equal(0.45, defaults.ScaledDelay(0.45), "normal speed changes nothing about the delay");

            // Levels come from the menu, from a number and from their name; nonsense changes nothing.
            report.Equal(BotLevel.Fast, GameSettings.FromLevelText("1"), "\"1\" is the fast level");
            report.Equal(BotLevel.Cautious, GameSettings.FromLevelText("2"), "\"2\" is the cautious level");
            report.Equal(BotLevel.Strategic, GameSettings.FromLevelText("3"), "\"3\" is the strategic level");
            report.Equal(BotLevel.Fast, GameSettings.FromLevelText("fast"), "the level can be named");
            report.Equal<BotLevel?>(null, GameSettings.FromLevelText("9"), "there is no level 9");
            report.Equal<BotLevel?>(null, GameSettings.FromLevelText(""), "an empty level is no level");
            report.Equal<BotLevel?>(null, GameSettings.FromLevelText(null), "a missing level is no level");

            // Speed steps through the fixed ladder and stops at both ends instead of wrapping.
            var stepped = new GameSettings();
            report.Check(stepped.Faster(), "speed steps up from normal");
            report.Equal(2.0, stepped.Speed, "one step up from normal is double speed");
            report.Equal(0.225, stepped.ScaledDelay(0.45), "double speed halves the bot delay");
            while (stepped.Faster()) { }
            report.Equal(GameSettings.Speeds[^1], stepped.Speed, "speed stops at the top of the ladder");
            while (stepped.Slower()) { }
            report.Equal(GameSettings.Speeds[0], stepped.Speed, "speed stops at the bottom of the ladder");
            report.Check(stepped.ScaledDelay(0.45) > 0.45, "the slowest speed waits longer than normal");

            stepped.SetSpeedIndex(99);
            report.Equal(GameSettings.Speeds[^1], stepped.Speed, "an index past the ladder is clamped");
            stepped.SetSpeedIndex(-5);
            report.Equal(GameSettings.Speeds[0], stepped.Speed, "a negative index is clamped");
            report.Equal(1.0, new GameSettings().Speed, "a fresh setting is still at normal speed");

            // The environment gives a capture run or a test the same three settings.
            var previousLevel = Environment.GetEnvironmentVariable("MAHJONG_BOT_LEVEL");
            var previousSpeed = Environment.GetEnvironmentVariable("MAHJONG_BOT_SPEED");
            try
            {
                Environment.SetEnvironmentVariable("MAHJONG_BOT_LEVEL", "2");
                Environment.SetEnvironmentVariable("MAHJONG_BOT_SPEED", "0.5");
                var fromEnvironment = GameSettings.FromEnvironment(BotLevel.Strategic);
                report.Equal(BotLevel.Cautious, fromEnvironment.Level, "MAHJONG_BOT_LEVEL=2 is the cautious level");
                report.Equal(0.5, fromEnvironment.Speed, "MAHJONG_BOT_SPEED=0.5 is half speed");
                report.Equal(0.9, fromEnvironment.ScaledDelay(0.45), "half speed doubles the bot delay");
                report.Equal("×0.5", fromEnvironment.SpeedText(), "half speed prints as ×0.5");

                Environment.SetEnvironmentVariable("MAHJONG_BOT_LEVEL", "какой-то текст");
                Environment.SetEnvironmentVariable("MAHJONG_BOT_SPEED", "быстро");
                var nonsense = GameSettings.FromEnvironment(BotLevel.Fast);
                report.Equal(BotLevel.Fast, nonsense.Level, "a broken level keeps the caller's own default");
                report.Equal(1.0, nonsense.Speed, "a broken speed keeps normal speed");
            }
            finally
            {
                Environment.SetEnvironmentVariable("MAHJONG_BOT_LEVEL", previousLevel);
                Environment.SetEnvironmentVariable("MAHJONG_BOT_SPEED", previousSpeed);
            }
        }

        private static void MenuRows(SelfTestReport report)
        {
            var settings = new GameSettings { Language = Language.Ru, Level = BotLevel.Fast };
            var menu = new GameMenu(settings);

            report.Equal(GameMenu.TotalRows, menu.RowCount, "the panel and the model agree on the row count");
            report.Equal(MenuItem.Language, menu.Selected, "the menu opens on the language row");
            report.Equal(Loc.Get("menu.value.ru"), menu.RowAt(0).Value, "the language row shows the language");

            // Language: a step flips it and the row says so.
            report.Check(menu.Adjust(1), "the language row can be stepped");
            report.Equal(Language.En, settings.Language, "stepping the language row flips the language");
            report.Equal(Loc.Get("menu.value.en"), menu.RowAt(0).Value, "the row follows the new language");
            report.Check(!menu.Adjust(0), "a step of zero changes nothing");

            // The cursor wraps at both ends instead of getting stuck.
            menu.Move(-1);
            report.Equal(MenuItem.Close, menu.Selected, "one row up from the top is the last row");
            menu.Move(1);
            report.Equal(MenuItem.Language, menu.Selected, "one row down from the last row is the top");

            // Speed: steps through the ladder, and picking the row steps it too.
            menu.Move(1);
            report.Equal(MenuItem.Speed, menu.Selected, "the second row is the speed");
            report.Equal("×1", menu.RowAt(1).Value, "the speed row shows normal speed");
            report.Check(menu.Adjust(1), "the speed row can be stepped");
            report.Equal(2.0, settings.Speed, "stepping the speed row speeds the bots up");
            report.Equal(MenuCommand.None, menu.Activate(), "picking a setting row opens nothing");
            report.Equal(4.0, settings.Speed, "picking a setting row steps it");
            report.Check(!menu.Adjust(1), "the speed row stops at the top");

            // Level: three settings, no fourth.
            settings.SetSpeedIndex(0);
            menu.Move(1);
            report.Equal(MenuItem.Level, menu.Selected, "the third row is the bot strength");
            report.Equal(Loc.Get("menu.value.fast"), menu.RowAt(2).Value, "the level row shows the level");
            report.Check(menu.Adjust(1), "the level row can be stepped");
            report.Equal(BotLevel.Cautious, settings.Level, "stepping the level row strengthens the bots");
            menu.Adjust(1);
            report.Equal(BotLevel.Strategic, settings.Level, "the level reaches the strongest bots");
            report.Check(!menu.Adjust(1), "there is no fourth level to step into");

            // Commands: the rows that open something say what they open.
            menu.Move(1);
            report.Equal(MenuCommand.Save, menu.Activate(), "the fourth row saves");
            report.Equal(MenuCommand.Resume, menu.RowAt(4).Command, "the fifth row resumes");
            report.Equal(MenuCommand.Review, menu.RowAt(5).Command, "the sixth row opens the review");
            report.Equal(MenuCommand.Rules, menu.RowAt(6).Command, "the seventh row opens the rules");
            menu.Move(4);
            report.Equal(MenuItem.Close, menu.Selected, "the last row closes the menu");
            report.Equal(MenuCommand.Close, menu.Activate(), "the last row asks to close");

            report.Equal(GameMenu.TotalRows, menu.AllRows().Count, "every row can be listed for the panel");
        }
    }
}
