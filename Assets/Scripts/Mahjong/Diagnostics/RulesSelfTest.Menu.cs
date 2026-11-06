using System;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;

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
            Run(report, "result: the end-of-hand screen waits, holds and closes", () => ResultScreenRules(report));
            Run(report, "clock: the windows step from the rules to no limit", () => ClockWindowRules(report));
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

            // Result screen (У-7): the ladder of waiting times, and picking the row steps it too.
            menu.Move(1);
            report.Equal(MenuItem.Result, menu.Selected, "the fourth row is the end-of-hand screen");
            report.Equal(ResultAdvanceSettings.Text(ResultAdvance.Five), menu.RowAt(3).Value,
                "the result row shows the waiting time");
            report.Check(menu.Adjust(1), "the result row can be stepped");
            report.Equal(ResultAdvance.Ten, settings.Result, "stepping the result row waits longer");
            while (menu.Adjust(1)) { }
            report.Equal(ResultAdvance.Manual, settings.Result, "the ladder ends at the button-only setting");
            report.Check(!menu.Adjust(1), "there is nothing past the button-only setting");
            while (menu.Adjust(-1)) { }
            report.Equal(ResultAdvance.Three, settings.Result, "the ladder starts at three seconds");
            settings.Result = ResultAdvanceSettings.Default;

            // The clock row (У-1): the rules' own 20/10/3, a slower table, and no limit at all.
            menu.Move(1);
            report.Equal(MenuItem.Clock, menu.Selected, "the fifth row is the time the player gets");
            report.Equal(ClockPaceSettings.Text(ClockPace.Rules), menu.RowAt(4).Value,
                "the clock row starts on the windows of the rulebook");
            report.Check(menu.Adjust(1), "the clock row can be stepped");
            report.Equal(ClockPace.Calm, settings.ClockPace, "a step up gives the player more time");
            while (menu.Adjust(1)) { }
            report.Equal(ClockPace.Unlimited, settings.ClockPace, "the ladder ends without a limit");
            report.Check(!menu.Adjust(1), "there is nothing past a window without a limit");
            report.Equal(MenuCommand.None, menu.Activate(), "picking the clock row is not a command");
            report.Equal(ClockPace.Unlimited, settings.ClockPace, "picking a setting row that is at its end steps nothing");
            while (menu.Adjust(-1)) { }
            report.Equal(ClockPace.Rules, settings.ClockPace, "the ladder starts at the rules' own windows");

            // The sound of the table (4.10): one row, one ladder from silence to full volume.
            menu.Move(1);
            report.Equal(MenuItem.Sound, menu.Selected, "the sixth row is the sound of the table");
            report.Equal(SoundVolumeSettings.Text(settings.SoundVolume), menu.RowAt(5).Value,
                "the sound row shows the volume the table is played at");
            report.Check(menu.Adjust(-1), "the sound row can be stepped");
            report.Check(settings.SoundVolume < SoundVolumeSettings.Default,
                "stepping the sound row makes the table quieter");

            // Commands: the rows that open something say what they open.
            menu.Move(1);
            report.Equal(MenuCommand.Save, menu.Activate(), "the seventh row saves");
            report.Equal(MenuCommand.Resume, menu.RowAt(7).Command, "the eighth row resumes");
            report.Equal(MenuCommand.Review, menu.RowAt(8).Command, "the ninth row opens the review");
            report.Equal(MenuCommand.Rules, menu.RowAt(9).Command, "the tenth row opens the rules");
            menu.Move(4);
            report.Equal(MenuItem.Close, menu.Selected, "the last row closes the menu");
            report.Equal(MenuCommand.Close, menu.Activate(), "the last row asks to close");

            report.Equal(GameMenu.TotalRows, menu.AllRows().Count, "every row can be listed for the panel");
        }

        /// <summary>
        /// У-1: the windows of a hand are a table setting now. The rules give 20 / 10 / 3 seconds, a
        /// slower table gets 30 / 20 / 10, and a table without limits never runs a window out — the
        /// last case is a house rule, so the HUD has to say so. Checked on the settings and on the clock
        /// itself, with no waiting: the view feeds the clock frame deltas.
        /// </summary>
        private static void ClockWindowRules(SelfTestReport report)
        {
            // The default is the rulebook's own pace, and it is the same numbers PlayClock carries.
            var settings = new GameSettings();
            report.Equal(ClockPace.Rules, settings.ClockPace, "a new table plays by the rules' own windows");
            report.Equal(PlayClock.ArrangeLimit, settings.Windows.Arrange, "twenty seconds to arrange the deal");
            report.Equal(PlayClock.DiscardLimit, settings.Windows.Discard, "ten seconds to discard");
            report.Equal(PlayClock.ClaimLimit, settings.Windows.Claim, "three seconds to claim");
            report.Check(ClockPaceSettings.FollowsRules(ClockPace.Rules), "the rules' pace is the rules' pace");
            report.Check(!ClockPaceSettings.FollowsRules(ClockPace.Calm), "a slower table is a house rule");
            report.Check(!ClockPaceSettings.FollowsRules(ClockPace.Unlimited), "no limits are a house rule too");

            // The ladder: rules, then a slower table, then no limit, and it stops at both ends.
            report.Equal(ClockPace.Calm, ClockPaceSettings.Step(ClockPace.Rules, 1), "a step up gives more time");
            report.Equal(ClockPace.Unlimited, ClockPaceSettings.Step(ClockPace.Calm, 1), "and then there is no limit");
            report.Equal(ClockPace.Unlimited, ClockPaceSettings.Step(ClockPace.Unlimited, 3), "the ladder does not wrap");
            report.Equal(ClockPace.Rules, ClockPaceSettings.Step(ClockPace.Rules, -4), "the ladder stops at the rules");
            report.Equal(ClockPace.Calm, ClockPaceSettings.Step(ClockPace.Calm, 0), "a step of zero changes nothing");
            report.Equal(TimeSpan.FromSeconds(30), ClockLimits.Calm.Arrange, "the slower table arranges for thirty seconds");
            report.Equal(TimeSpan.FromSeconds(20), ClockLimits.Calm.Discard, "and discards within twenty");
            report.Equal(TimeSpan.FromSeconds(10), ClockLimits.Calm.Claim, "and claims within ten");
            report.Check(ClockLimits.Unlimited.IsUnlimited, "the third preset has no limit at all");
            report.Check(!ClockLimits.Rules.IsUnlimited, "the rules' windows are limits");
            report.Equal(TimeSpan.Zero, ClockLimits.Unlimited.LimitOf(ClockWindow.Claim),
                "an unlimited table has no claim window to run out");
            report.Equal(TimeSpan.Zero, ClockLimits.Rules.LimitOf(ClockWindow.Idle), "an idle window has no limit");

            // MAHJONG_CLOCK goes through one parse, like every other table setting.
            report.Equal<ClockPace?>(ClockPace.Rules, ClockPaceSettings.TryParse("rules"), "the pace can be named");
            report.Equal<ClockPace?>(ClockPace.Rules, ClockPaceSettings.TryParse("20/10/3"),
                "the rules' windows can be written out");
            report.Equal<ClockPace?>(ClockPace.Calm, ClockPaceSettings.TryParse("calm"), "\"calm\" is the slower table");
            report.Equal<ClockPace?>(ClockPace.Calm, ClockPaceSettings.TryParse("30/20/10"), "and so is its triple");
            report.Equal<ClockPace?>(ClockPace.Unlimited, ClockPaceSettings.TryParse("unlimited"), "no limit can be named");
            report.Equal<ClockPace?>(ClockPace.Unlimited, ClockPaceSettings.TryParse("0/0/0"), "or written out as zeros");
            report.Equal<ClockPace?>(null, ClockPaceSettings.TryParse("12/8/5"),
                "a pace that is on no ladder keeps the default instead of guessing");
            report.Equal<ClockPace?>(null, ClockPaceSettings.TryParse("быстро"),
                "a broken value must not stop the table from starting");
            report.Equal<ClockPace?>(null, ClockPaceSettings.TryParse(null), "an empty value means nothing");

            // The clock itself: an unlimited window opens, never counts down and never expires.
            var clock = new PlayClock();
            clock.Restart(ClockWindow.Discard, ClockLimits.Unlimited);
            report.Check(clock.IsRunning, "a window without a limit still opens");
            report.Check(!clock.IsLimited, "and it does not count down");
            report.Equal(0, clock.SecondsLeft, "a window without a limit shows no seconds");
            report.Check(!clock.Tick(TimeSpan.FromSeconds(3600)), "an hour does not expire it");
            report.Check(!clock.IsExpired, "a window without a limit is never expired");
            report.Equal(0d, clock.Fraction, "a window without a limit has no progress to show");

            // A slower table simply starts the same window with a bigger limit.
            clock.Restart(ClockWindow.Claim, ClockLimits.Calm);
            report.Equal(10, clock.SecondsLeft, "the slower table gives ten seconds to claim");
            report.Check(clock.IsLimited, "a limited window counts down");
            report.Check(!clock.Tick(TimeSpan.FromSeconds(9.5)), "nine and a half seconds do not end it");
            report.Check(clock.Tick(TimeSpan.FromSeconds(0.5)), "ten seconds end the claim window");

            // The setting reaches the table: it is what the HUD note and the clock read.
            settings.ClockPace = ClockPace.Calm;
            report.Equal(TimeSpan.FromSeconds(30), settings.Windows.Arrange, "the table reads the pace it was given");
            settings.ClockPace = ClockPace.Unlimited;
            report.Check(settings.Windows.IsUnlimited, "a table without limits is what the clock is opened with");

            var previousClock = Environment.GetEnvironmentVariable("MAHJONG_CLOCK");
            try
            {
                Environment.SetEnvironmentVariable("MAHJONG_CLOCK", "calm");
                report.Equal(ClockPace.Calm, GameSettings.FromEnvironment().ClockPace,
                    "MAHJONG_CLOCK=calm opens a slower table for a capture run");
                Environment.SetEnvironmentVariable("MAHJONG_CLOCK", "без ограничения");
                report.Equal(ClockPace.Unlimited, GameSettings.FromEnvironment().ClockPace,
                    "the pace can be asked for in Russian too");
                Environment.SetEnvironmentVariable("MAHJONG_CLOCK", "7/7/7");
                report.Equal(ClockPace.Rules, GameSettings.FromEnvironment().ClockPace,
                    "a broken value keeps the rules' own windows");
            }
            finally
            {
                Environment.SetEnvironmentVariable("MAHJONG_CLOCK", previousClock);
            }
        }

        /// <summary>
        /// У-7: the hand no longer ends with three seconds of HUD text. The screen that replaced it is a
        /// model of its own — what it says, how long it waits and when the wait is over — so its
        /// behaviour is checked here, with only the drawing left to the play-mode tests.
        /// </summary>
        private static void ResultScreenRules(SelfTestReport report)
        {
            // The default and the ladder: three, five, ten seconds and then only the button.
            report.Equal(ResultAdvance.Five, new GameSettings().Result,
                "the table waits five seconds before it deals again");
            report.Equal(5f, ResultAdvanceSettings.Seconds(ResultAdvance.Five), "five seconds is five seconds");
            report.Equal(0f, ResultAdvanceSettings.Seconds(ResultAdvance.Manual),
                "the button-only setting does not wait at all");
            report.Equal(ResultAdvance.Ten, ResultAdvanceSettings.Step(ResultAdvance.Five, 1),
                "a step up waits longer");
            report.Equal(ResultAdvance.Three, ResultAdvanceSettings.Step(ResultAdvance.Five, -1),
                "a step down waits less");
            report.Equal(ResultAdvance.Three, ResultAdvanceSettings.Step(ResultAdvance.Three, -1),
                "the ladder stops at three seconds");
            report.Equal(ResultAdvance.Manual, ResultAdvanceSettings.Step(ResultAdvance.Ten, 1),
                "the last rung of the ladder is the button");
            report.Equal(ResultAdvance.Manual, ResultAdvanceSettings.Step(ResultAdvance.Manual, 5),
                "the ladder does not wrap round to three seconds");
            report.Equal(ResultAdvance.Five, ResultAdvanceSettings.Step(ResultAdvance.Five, 0),
                "a step of zero changes nothing");

            // MAHJONG_RESULT_DELAY goes through one parse, like every other setting.
            report.Equal<ResultAdvance?>(ResultAdvance.Manual, ResultAdvanceSettings.TryParse("0"),
                "\"0\" holds the screen until the button");
            report.Equal<ResultAdvance?>(ResultAdvance.Manual, ResultAdvanceSettings.TryParse("manual"),
                "the setting can be named");
            report.Equal<ResultAdvance?>(ResultAdvance.Ten, ResultAdvanceSettings.TryParse("10"),
                "\"10\" is ten seconds");
            report.Equal<ResultAdvance?>(ResultAdvance.Three, ResultAdvanceSettings.TryParse("3s"),
                "\"3s\" is three seconds as well");
            report.Equal<ResultAdvance?>(null, ResultAdvanceSettings.TryParse("7"),
                "seven seconds is not on the ladder, so the default stays");
            report.Equal<ResultAdvance?>(null, ResultAdvanceSettings.TryParse("быстро"),
                "a broken value keeps the default instead of stopping the table");

            // The screen itself: it holds the hand it was given and runs its own countdown.
            var screen = new ResultScreen();
            report.Check(!screen.IsOpen, "the screen starts closed");
            var content = new HandResultContent("Выиграл игрок 2",
                new[] { "32 очка", "Выплачено: 96" }, new[] { "Итоговая таблица", "1. Игрок 1 — 44" }, true);
            screen.Show(content, ResultAdvance.Three);
            report.Check(screen.IsOpen, "showing the content opens the screen");
            report.Equal("Выиграл игрок 2", screen.Title, "the title is the one the hand produced");
            report.Equal(4, screen.AllLines().Count, "the hand and the table of points are drawn together");
            report.Equal(3f, screen.SecondsLeft, "the wait starts at the setting it was opened with");
            report.Check(!screen.Tick(1.5f), "half the wait is not the end of it");
            report.Equal(1.5f, screen.SecondsLeft, "the countdown follows the table, not the wall clock");
            report.Check(screen.Tick(2f), "the wait is over when it has run out");
            report.Check(screen.Expired, "an expired screen asks for the next hand");
            report.Equal(0f, screen.SecondsLeft, "the countdown never goes below zero");
            screen.Hide();
            report.Check(!screen.IsOpen && !screen.Expired, "a closed screen asks for nothing");

            // The button-only setting never runs out, and the last hand of a session offers no next one.
            var manual = new ResultScreen();
            manual.Show(content with { CanContinue = false }, ResultAdvance.Manual);
            report.Check(!manual.CountsDown, "the button-only setting does not count down");
            report.Check(!manual.Tick(3600f), "an hour changes nothing when the table waits for the button");
            report.Check(!manual.CanContinue, "the last hand of the session has no next hand to deal");
            report.Check(manual.Hint != screen.Hint, "a screen with nothing to deal says so");

            // The content of a real hand: winner, what it was worth, the transfers and the points after.
            var game = new GameSession();
            game.StartNextHand(seed: 4242);
            PlayHandToEnd(game.State!, new Random(9));
            game.CompleteHand();
            var hand = Text.HandResult(game, humanSeat: 0);
            report.Check(hand.Title.Length > 0, "a settled hand names its winner");
            report.Check(hand.Lines.Count >= 2, "the result lists the hand and the points after it");
            report.Check(hand.Lines.Any(line => line.Contains("P1=")), "the points after the hand are shown");
            report.Check(hand.CanContinue, "the first hand of a session is not the last one");
            report.Check(hand.Standings.Count == 0, "a table of points belongs to the end of a round, not to every hand");
        }
    }
}
