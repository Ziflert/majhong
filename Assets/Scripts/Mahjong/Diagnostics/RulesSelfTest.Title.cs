using System;
using System.IO;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Replay;
using Mahjong.Rules;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the welcome screen (У-2): the rows a player sees on launch, what a step and a pick do,
    /// which rows are honestly marked as not built yet, and the saved games the "continue" and "load"
    /// rows read. The screen is a model, so all of this runs headless; only the drawing is left to the
    /// play-mode tests.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void TitleGroups(SelfTestReport report)
        {
            Run(report, "title: the rows a player sees on launch", () => TitleRows(report));
            Run(report, "title: settings step within their presets and stop at the ends", () => TitleSettings(report));
            Run(report, "title: continue and load read the saved games of the folder", () => TitleSaves(report));
        }

        private static void TitleRows(SelfTestReport report)
        {
            var settings = new GameSettings();
            var title = new TitleScreen(settings);

            report.Equal(TitlePage.Main, title.Page, "the screen opens on the main page");
            report.Equal(7, title.RowCount, "the main page keeps only the launch actions");
            report.Equal(TitleItem.NewGame, title.Selected, "the cursor starts on the new game");
            report.Equal("", title.Status, "a fresh screen says nothing under the rows");

            // The order a player reads: the ways into a game first, the table under them, the door last.
            var items = title.AllRows().Select(row => row.Item).ToList();
            report.Equal(TitleItem.NewGame, items[0], "a new game is the first row");
            report.Equal(TitleItem.Continue, items[1], "the game in progress follows it");
            report.Equal(TitleItem.LoadGame, items[2], "then the list of saved games");
            report.Equal(TitleItem.Quit, items[items.Count - 1], "quitting is the last row");

            // The unfinished modes are hidden in the normal build.
            report.Check(!items.Contains(TitleItem.LocalGame), "local mode is hidden from the launch page");
            report.Check(!items.Contains(TitleItem.Multiplayer), "multiplayer is hidden from the launch page");

            title.Move(4);
            report.Equal(TitleItem.Settings, title.Selected, "the cursor reaches the settings page");
            report.Equal(TitleCommand.None, title.Activate(), "settings opens without starting a game");
            report.Equal(TitlePage.Settings, title.Page, "settings opens its own page");
            title.Back();

            // The cursor wraps, so one key press always lands on a row.
            title.Move(-4);
            report.Equal(TitleItem.Quit, title.Selected, "the cursor wraps over the top");
            title.Move(1);
            report.Equal(TitleItem.NewGame, title.Selected, "and over the bottom");

            title.Move(-1);
            report.Equal(TitleItem.Quit, title.Selected, "the last row asks to quit");
            report.Equal(TitleCommand.Quit, title.Activate(), "and picking it asks the game to quit");

            // With nothing saved, "continue" says so instead of opening a file that is not there.
            title.Move(1);
            report.Equal(TitleItem.Continue, title.Selected, "the cursor is on continue");
            report.Check(!title.CanContinue, "an empty folder gives nothing to continue");
            report.Equal(TitleCommand.None, title.Activate(), "continue does nothing without a save");
            report.Equal(Loc.Get("title.noSave"), title.Status, "and says there is nothing saved");

            // A new game is the one command that always works, and it needs no file.
            title.Move(-1);
            report.Equal(TitleCommand.NewGame, title.Activate(), "picking a new game deals a hand");

            // The rulebook is readable before the game starts.
            title.Move(-1);
            report.Equal(TitleItem.Rules, title.Selected, "the rulebook has its own row");
            report.Equal(TitleCommand.Rules, title.Activate(), "and the row opens it");
        }

        private static void TitleSettings(SelfTestReport report)
        {
            var settings = new GameSettings();
            var title = new TitleScreen(settings);
            title.OpenSettings();

            // The settings page starts with the game section; its first adjustable row is bot strength.
            title.Move(1);
            report.Equal(TitleItem.Level, title.Selected, "the settings page starts with bot strength");
            while (title.Selected != TitleItem.Variant) title.Move(1);
            report.Equal(Text.VariantName(RuleVariant.Sports), title.RowAt(title.Index).Value,
                "the screen shows the profile the table will be played by");

            report.Check(title.Adjust(1), "the profile steps to the next preset");
            report.Equal(RuleVariant.Classic144.Id, settings.Variant.Id, "stepping right picks the classic rules");
            report.Check(!title.Adjust(1), "there is no third preset to step into");
            report.Equal(RuleVariant.Classic144.Id, settings.Variant.Id, "the profile stays on the last preset");
            report.Check(title.Adjust(-1), "the profile steps back");
            report.Equal(RuleVariant.Sports.Id, settings.Variant.Id, "and the sporting rules are back");

            // The remaining game settings step the same way the in-game menu steps them.
            title.Move(1);
            report.Equal(TitleItem.Speed, title.Selected, "the tempo follows bot strength");
            var spoken = settings.Language;
            report.Check(title.Adjust(1), "the language steps");
            report.Check(settings.Language != spoken, "and the interface changes the language it speaks");

            title.Move(1);
            report.Equal(TitleItem.Result, title.Selected, "then the result screen");
            while (title.Adjust(1)) { }
            report.Equal(BotLevel.Strategic, settings.Level, "the strongest bots are the top of the ladder");
            while (title.Adjust(-1)) { }
            report.Equal(BotLevel.Fast, settings.Level, "and the weakest are the bottom");

            title.Move(1);
            report.Equal(TitleItem.Clock, title.Selected, "then the clock");
            report.Check(title.Adjust(1), "the tempo steps");
            report.Equal(2.0, settings.Speed, "one step up is double speed");

            title.Move(1);
            report.Equal(TitleItem.Result, title.Selected, "and the result screen after the tempo");
            report.Check(title.Adjust(-1), "the result screen steps");

            title.Move(1);
            report.Equal(TitleItem.Clock, title.Selected, "the time for a decision has its own row last (У-1)");
            report.Equal(ClockPaceSettings.Text(ClockPace.Rules), title.RowAt(title.Index).Value,
                "the clock row shows the windows of the rulebook");
            report.Check(title.Adjust(1), "the clock row steps");
            report.Equal(ClockPace.Calm, settings.ClockPace, "a step up gives the player a slower table");
            while (title.Adjust(1)) { }
            report.Equal(ClockPace.Unlimited, settings.ClockPace, "the ladder ends without a limit");
            while (title.Adjust(-1)) { }
            report.Equal(ClockPace.Rules, settings.ClockPace, "and starts at the rules' own windows");

            // How long the whole game runs (8.7): the rules' 150 minutes, shorter house rules, no clock.
            title.Move(1);
            report.Equal(TitleItem.Session, title.Selected, "the length of the game has a row of its own");
            report.Equal(SessionLengthSettings.Text(GameSession.DefaultTimeLimit), title.RowAt(title.Index).Value,
                "the row shows the length of a game by the rulebook (3.5.6)");
            report.Check(title.Adjust(1), "the length row steps");
            report.Equal(TimeSpan.FromMinutes(90), settings.SessionLimit, "one step shorter is ninety minutes");
            while (title.Adjust(1)) { }
            report.Check(SessionLengthSettings.IsUnlimited(settings.SessionLimit),
                "the ladder ends at a table without a clock");
            while (title.Adjust(-1)) { }
            report.Equal(GameSession.DefaultTimeLimit, settings.SessionLimit, "and starts at the rules' own length");

            // The deal (8.7): a fresh game every time, or the game the table dealt last.
            title.Move(1);
            report.Equal(TitleItem.Seed, title.Selected, "the deal follows the length of the game");
            report.Equal(DealSeedSettings.Text(DealSeedSettings.Random), title.RowAt(title.Index).Value,
                "a fresh table deals a different game every time");
            settings.LastSeed = 4242;
            report.Check(title.Adjust(1), "the deal row steps");
            report.Equal(4242, settings.Seed, "stepping it repeats the game the table just dealt");
            report.Equal("4242", title.RowAt(title.Index).Value, "and the row shows the number of that deal");
            report.Check(title.Adjust(-1), "the row steps back");
            report.Check(DealSeedSettings.IsRandom(settings.Seed), "to a fresh deal every time");

            // Picking a setting row steps it: a click on a slider does not need a second gesture.
            var before = settings.Speed;
            title.Move(-4);
            report.Equal(TitleItem.Speed, title.Selected, "back on the tempo row");
            report.Equal(TitleCommand.None, title.Activate(), "picking a setting is not a command");
            report.Check(settings.Speed != before, "picking the tempo row steps the tempo");

            // A preset chosen here is the one the session is created with: the model owns the setting.
            report.Equal(settings.Variant.Id, RuleVariant.Sports.Id, "the preset ends where the test left it");
        }

        private static void TitleSaves(SelfTestReport report)
        {
            var folder = Path.Combine(Path.GetTempPath(), "mahjong-title-selftest");
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Directory.CreateDirectory(folder);

                // An empty folder is not an error: the screen opens and says there is nothing to continue.
                var empty = new TitleScreen(new GameSettings(), SaveSlots.Scan(folder));
                report.Equal(0, empty.Saves.Count, "an empty folder lists no saves");
                report.Equal("", empty.NewestSave, "and there is nothing to continue");
                empty.Move(1);
                report.Equal(TitleItem.Continue, empty.Selected, "the cursor is on continue");
                report.Equal(TitleCommand.None, empty.Activate(), "picking continue does nothing");
                report.Equal(Loc.Get("title.noSave"), empty.Status, "and the screen says there is nothing saved");
                report.Equal("", SaveSlots.Newest(folder), "the newest save of an empty folder is a blank path");

                // Two saves: the newer one is what "continue" takes, the older one is still on the list.
                var older = Path.Combine(folder, "older.mahjong");
                var newer = Path.Combine(folder, "newer.mahjong");
                WriteSave(older, RuleVariant.Sports, hands: 2);
                WriteSave(newer, RuleVariant.Classic144, hands: 5);
                File.SetLastWriteTimeUtc(older, new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(newer, new DateTime(2026, 11, 5, 21, 30, 0, DateTimeKind.Utc));

                var slots = SaveSlots.Scan(folder);
                report.Equal(2, slots.Count, "both saves are listed");
                report.Equal("newer", slots[0].Name, "the newest save is the first row");
                report.Equal(5, slots[0].Hands, "the row knows how many hands the save holds");
                report.Equal(RuleVariant.Classic144.Id, slots[0].Variant.Id, "and the rules it was played by");
                report.Check(slots[0].IsOpenable, "a save that parses can be opened");
                report.Equal(newer, SaveSlots.Newest(folder), "continue takes the newest save");

                var title = new TitleScreen(new GameSettings(), slots);
                report.Check(title.CanContinue, "the screen offers to continue a saved game");
                title.Move(1);
                report.Equal(TitleItem.Continue, title.Selected, "the cursor is on continue");
                report.Equal(TitleCommand.Continue, title.Activate(), "continue asks for the saved game");
                report.Equal(newer, title.PendingSave, "and names the file to load");

                // The load page lists every save and one row to go back.
                while (title.Selected != TitleItem.LoadGame) title.Move(1);
                report.Equal(TitleItem.LoadGame, title.Selected, "the load row is on the launch page");
                report.Equal(TitleCommand.OpenLoad, title.Activate(), "picking it opens the list");
                report.Equal(TitlePage.Load, title.Page, "the load page is open");
                report.Equal(3, title.RowCount, "two saves and a way back");
                report.Equal("newer", title.RowAt(0).Label, "the newest save is the first row of the list");
                report.Equal(TitleItem.Back, title.RowAt(2).Item, "and the last row goes back");
                report.Equal(TitleCommand.LoadSave, title.Activate(), "picking the first row loads that save");
                report.Equal(newer, title.PendingSave, "the row carries the path of the file it shows");

                title.Move(2);
                report.Equal(TitleItem.Back, title.Selected, "the cursor walks to the way back");
                report.Equal(TitleCommand.Back, title.Activate(), "which closes the list");
                report.Equal(TitlePage.Main, title.Page, "the main page is back");
                report.Check(!title.Back(), "there is nothing to go back to from the main page");

                // A file that does not parse is listed with a reason instead of disappearing.
                var broken = Path.Combine(folder, "broken.mahjong");
                File.WriteAllText(broken, "this is not a mahjong save\n");
                File.SetLastWriteTimeUtc(broken, new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc));
                var withBroken = SaveSlots.Scan(folder);
                report.Equal(3, withBroken.Count, "a broken file is still listed");
                var brokenSlot = withBroken.FirstOrDefault(slot => slot.Name == "broken");
                report.Check(brokenSlot != null && !brokenSlot.IsOpenable,
                    "and it is marked as one that cannot be opened");
                report.Check(brokenSlot!.Problem.Length > 0, "the reason the file does not read is kept");
                report.Equal(newer, SaveSlots.Newest(folder), "a broken file is never what continue picks");

                var listing = new TitleScreen(new GameSettings(), withBroken);
                listing.Move(2);
                report.Equal(TitleCommand.OpenLoad, listing.Activate(), "the list of saves opens");
                report.Equal(4, listing.RowCount, "the list shows the broken save too");
                listing.Move(2);
                report.Equal(TitleItem.SaveSlot, listing.Selected, "the cursor can land on it");
                report.Equal(TitleCommand.None, listing.Activate(), "but it cannot be picked");
                report.Equal(Loc.Get("title.save.broken"), listing.Status, "and the screen says why");
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        /// <summary>A save of <paramref name="hands"/> deals with no moves: enough for a list of saves.</summary>
        private static void WriteSave(string path, RuleVariant variant, int hands)
        {
            var log = new SessionLog { Variant = variant };
            log.Info = new SessionLog.SessionInfo(TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(150));
            for (var hand = 0; hand < hands; hand++)
                log.BeginHand(hand % 4, 1000 + hand, Wind.East);
            log.Save(path);
        }
    }
}
