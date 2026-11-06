using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the settings file (8.11): every setting the screens offer survives the trip to disk
    /// and back, a line that makes no sense costs one setting and not the table, an unknown key from a
    /// newer build is ignored, and the environment still sits on top of the file.
    ///
    /// The file is plain text and lives in the rules assembly, so all of this runs headless. What the
    /// play-mode test adds is the one thing that needs Unity: that stepping a row on screen really
    /// writes the file.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void SettingsGroups(SelfTestReport report)
        {
            Run(report, "settings: the file keeps the table as the player left it", () => SettingsRoundTrip(report));
            Run(report, "settings: a line that makes no sense keeps the default", () => SettingsBrokenLines(report));
            Run(report, "settings: the environment sits on top of the file", () => SettingsPrecedence(report));
            Run(report, "settings: a missing or unreadable file gives a fresh table", () => SettingsMissingFile(report));
        }

        /// <summary>
        /// Every setting of every ladder, written and read back: the palette, the size, the volume, the
        /// language, the level, the speed, the result screen, the windows, the length of the session and
        /// the seeds. A setting that does not survive the file is a setting the player chooses twice.
        /// </summary>
        private static void SettingsRoundTrip(SelfTestReport report)
        {
            var settings = new GameSettings { Language = Language.Ru };

            foreach (var variant in RuleVariant.Presets)
            {
                settings.Variant = variant;
                report.Equal(variant.Id, SettingsFile.Load(SettingsFile.Save(settings)).Variant.Id,
                    $"rules {variant.Id} survive the file");
            }

            foreach (var language in new[] { Language.Ru, Language.En })
            {
                settings.Language = language;
                report.Equal(language, SettingsFile.Load(SettingsFile.Save(settings)).Language,
                    $"language {language} survives the file");
            }

            foreach (var level in new[] { BotLevel.Fast, BotLevel.Cautious, BotLevel.Strategic })
            {
                settings.Level = level;
                report.Equal(level, SettingsFile.Load(SettingsFile.Save(settings)).Level,
                    $"bot level {level} survives the file");
            }

            for (var index = 0; index < GameSettings.Speeds.Length; index++)
            {
                settings.SetSpeedIndex(index);
                report.Equal(GameSettings.Speeds[index], SettingsFile.Load(SettingsFile.Save(settings)).Speed,
                    $"bot speed ×{GameSettings.Speeds[index]} survives the file");
            }

            foreach (var advance in ResultAdvanceSettings.Steps)
            {
                settings.Result = advance;
                report.Equal(advance, SettingsFile.Load(SettingsFile.Save(settings)).Result,
                    $"result screen {advance} survives the file");
            }

            foreach (var pace in ClockPaceSettings.Steps)
            {
                settings.ClockPace = pace;
                report.Equal(pace, SettingsFile.Load(SettingsFile.Save(settings)).ClockPace,
                    $"clock pace {pace} survives the file");
            }

            foreach (var length in SessionLengthSettings.Steps)
            {
                settings.SessionLimit = length;
                report.Equal(length, SettingsFile.Load(SettingsFile.Save(settings)).SessionLimit,
                    $"session length {length} survives the file");
            }

            foreach (var seed in new[] { DealSeedSettings.Random, 1, 424242 })
            {
                settings.Seed = seed;
                settings.LastSeed = seed == DealSeedSettings.Random ? 0 : seed;
                var dealt = SettingsFile.Load(SettingsFile.Save(settings));
                report.Equal(seed, dealt.Seed, $"seed {seed} survives the file");
                report.Equal(settings.LastSeed, dealt.LastSeed, $"the last deal {settings.LastSeed} survives the file");
            }

            foreach (var volume in SoundVolumeSettings.Steps)
            {
                settings.SoundVolume = volume;
                report.Equal(volume, SettingsFile.Load(SettingsFile.Save(settings)).SoundVolume,
                    $"volume {volume} survives the file");
            }

            foreach (var theme in UiThemes.All)
            {
                settings.Theme = theme;
                report.Equal(theme.Id, SettingsFile.Load(SettingsFile.Save(settings)).Theme.Id,
                    $"palette {theme.Id} survives the file");
            }

            foreach (var scale in UiThemeSettings.Scales)
            {
                settings.UiScale = scale;
                report.Equal(scale, SettingsFile.Load(SettingsFile.Save(settings)).UiScale,
                    $"size {scale} survives the file");
            }

            // The file is written with a header, a version and exactly the keys the reader knows, so a
            // line that appears in it is a line this build can read back.
            var text = SettingsFile.Save(settings);
            report.Check(text.StartsWith("# ", StringComparison.Ordinal), "the file opens with a comment");
            report.Check(text.Contains($"version={SettingsFile.Version}", StringComparison.Ordinal),
                "the file names its format version");
            foreach (var key in SettingsFile.Keys)
                report.Check(text.Contains(key + "=", StringComparison.Ordinal), $"the file writes {key}");

            // All keys at once: one file, one table, nothing lost between the lines.
            var whole = new GameSettings
            {
                Language = Language.En,
                Variant = RuleVariant.Classic144,
                Level = BotLevel.Cautious,
                Result = ResultAdvance.Manual,
                ClockPace = ClockPace.Calm,
                SessionLimit = TimeSpan.FromMinutes(30),
                Seed = 777,
                LastSeed = 778,
                SoundVolume = 0.25d,
                Theme = UiThemes.Ivory,
                UiScale = 1.3d
            };
            whole.SetSpeedIndex(4);
            var read = SettingsFile.Load(SettingsFile.Save(whole));
            report.Equal(Language.En, read.Language, "one file keeps the language");
            report.Equal("classic144", read.Variant.Id, "one file keeps the profile");
            report.Equal(BotLevel.Cautious, read.Level, "one file keeps the bot level");
            report.Equal(4d, read.Speed, "one file keeps the bot speed");
            report.Equal(ResultAdvance.Manual, read.Result, "one file keeps the result screen");
            report.Equal(ClockPace.Calm, read.ClockPace, "one file keeps the windows");
            report.Equal(TimeSpan.FromMinutes(30), read.SessionLimit, "one file keeps the session length");
            report.Equal(777, read.Seed, "one file keeps the chosen seed");
            report.Equal(778, read.LastSeed, "one file keeps the last deal");
            report.Equal(0.25d, read.SoundVolume, "one file keeps the volume");
            report.Equal("ivory", read.Theme.Id, "one file keeps the palette");
            report.Equal(1.3d, read.UiScale, "one file keeps the size");
        }

        /// <summary>
        /// The file is meant to be edited by hand, so a broken line is an ordinary event: an unknown
        /// key, a missing <c>=</c>, a value from a build that offered a different ladder. Each of them
        /// costs its own setting and nothing else.
        /// </summary>
        private static void SettingsBrokenLines(SelfTestReport report)
        {
            var defaults = new GameSettings { Language = Language.Ru };

            // Nothing at all: an empty file, spaces, comments and a lone newline.
            report.Equal(defaults.Theme.Id, SettingsFile.Load(string.Empty).Theme.Id, "an empty file keeps the palette");
            report.Equal(defaults.UiScale, SettingsFile.Load("   \n\n\t\n").UiScale, "blank lines change nothing");
            report.Equal(Language.Ru, SettingsFile.Load("# theme=ivory\n").Language,
                "a commented-out line is not a setting");

            // Keys this build has never heard of: a newer or older file still opens.
            var unknown = SettingsFile.Load("wobble=3\nversion=99\ntheme=ivory\n");
            report.Equal("ivory", unknown.Theme.Id, "an unknown key does not stop the known ones");
            report.Equal(UiThemes.Default.Id, SettingsFile.Load("favourite=dawn").Theme.Id,
                "a key that is a near miss changes nothing");

            // Values that mean nothing keep what the table had; one bad line does not spoil the rest.
            var broken = new GameSettings { Language = Language.Ru };
            SettingsFile.Apply(
                "theme=какая-то тема\nscale=огромный\nsound=очень громко\nspeed=быстро\nbots=9\n" +
                "clock=семь\nsession=минус десять\nseed=-3\nresult=когда-нибудь\nrules=ничьи\n" +
                "language=латынь\nlast_seed=последний\nsize=100%\n",
                broken);
            report.Equal(UiThemes.Default.Id, broken.Theme.Id, "a broken palette keeps the default");
            report.Equal(UiThemeSettings.DefaultScale, broken.UiScale, "a broken size keeps the default");
            report.Equal(SoundVolumeSettings.Default, broken.SoundVolume, "a broken volume keeps the default");
            report.Equal(GameSettings.Speeds[2], broken.Speed, "a broken speed keeps 1×");
            report.Equal(BotLevel.Strategic, broken.Level, "a level of nine is no level");
            report.Equal(ClockPace.Rules, broken.ClockPace, "a broken pace keeps the rules' windows");
            report.Equal(SessionLengthSettings.Default, broken.SessionLimit, "a broken length keeps the rules' session");
            report.Equal(DealSeedSettings.Default, broken.Seed, "a negative seed keeps the random deal");
            report.Equal(ResultAdvanceSettings.Default, broken.Result, "a broken result screen keeps five seconds");
            report.Equal("sports", broken.Variant.Id, "a broken profile keeps the sporting rules");
            report.Equal(Language.Ru, broken.Language, "a broken language keeps the table's own");
            report.Equal(0, broken.LastSeed, "a broken last deal leaves nothing remembered");

            // Blank values are as broken as nonsense, and a line without "=" is not a setting.
            var blank = new GameSettings { Language = Language.Ru, Theme = UiThemes.Bamboo };
            SettingsFile.Apply("theme=\n=ivory\nplain text\nscale=\n", blank);
            report.Equal("bamboo", blank.Theme.Id, "an empty value keeps the palette it had");
            report.Equal(UiThemeSettings.DefaultScale, blank.UiScale, "an empty size keeps the default");

            // The parser reads the values a hand-written file is likely to hold.
            var friendly = SettingsFile.Load(
                "rules=Classic144\nlanguage=EN\nbots=1\nspeed=0.5\nresult=button\nclock=30/20/10\n" +
                "session=20 min\nseed=random\nsound=50%\ntheme=Слоновая кость\nscale=115\n");
            report.Equal("classic144", friendly.Variant.Id, "the profile is read in any case");
            report.Equal(Language.En, friendly.Language, "the language is read in any case");
            report.Equal(BotLevel.Fast, friendly.Level, "the level is read as a number");
            report.Equal(0.5d, friendly.Speed, "the speed is read as a multiplier");
            report.Equal(ResultAdvance.Manual, friendly.Result, "the result screen has a word for the button");
            report.Equal(ClockPace.Calm, friendly.ClockPace, "the windows are read as a triple");
            report.Equal(TimeSpan.FromMinutes(20), friendly.SessionLimit, "the length is read with its unit");
            report.Equal(DealSeedSettings.Random, friendly.Seed, "the seed has a word for a fresh deal");
            report.Equal(0.5d, friendly.SoundVolume, "the volume is read as a percentage");
            report.Equal("ivory", friendly.Theme.Id, "the palette is read by its Russian name");
            report.Equal(1.15d, friendly.UiScale, "the size is read as a hundred and fifteen");
        }

        /// <summary>
        /// The order the game uses: defaults, then the file, then <c>MAHJONG_*</c>. A variable that is
        /// set wins over the file, a variable that is absent leaves the file alone — that is what makes
        /// a capture run reproducible without erasing the settings of the player whose machine it runs on.
        /// </summary>
        private static void SettingsPrecedence(SelfTestReport report)
        {
            var chosen = new GameSettings
            {
                Language = Language.En,
                Theme = UiThemes.Ivory,
                UiScale = 1.15d,
                Variant = RuleVariant.Classic144,
                Level = BotLevel.Fast,
                ClockPace = ClockPace.Unlimited,
                SoundVolume = 1d
            };
            chosen.SetSpeedIndex(0);

            var names = new[]
            {
                "MAHJONG_LANG", "MAHJONG_THEME", "MAHJONG_UI_SCALE", "MAHJONG_VARIANT", "MAHJONG_BOT_LEVEL",
                "MAHJONG_BOT_SPEED", "MAHJONG_CLOCK", "MAHJONG_SOUND"
            };
            var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
            try
            {
                foreach (var name in names) Environment.SetEnvironmentVariable(name, null);

                // No variable: the file is the table.
                var fromFile = GameSettings.FromEnvironment(BotLevel.Strategic, SettingsFile.Load(SettingsFile.Save(chosen)));
                report.Equal(Language.En, fromFile.Language, "without MAHJONG_LANG the file's language stands");
                report.Equal("ivory", fromFile.Theme.Id, "without MAHJONG_THEME the file's palette stands");
                report.Equal(1.15d, fromFile.UiScale, "without MAHJONG_UI_SCALE the file's size stands");
                report.Equal("classic144", fromFile.Variant.Id, "without MAHJONG_VARIANT the file's profile stands");
                report.Equal(BotLevel.Fast, fromFile.Level, "without MAHJONG_BOT_LEVEL the file's level stands");
                report.Equal(0.25d, fromFile.Speed, "without MAHJONG_BOT_SPEED the file's speed stands");
                report.Equal(ClockPace.Unlimited, fromFile.ClockPace, "without MAHJONG_CLOCK the file's windows stand");
                report.Equal(1d, fromFile.SoundVolume, "without MAHJONG_SOUND the file's volume stands");

                // Every variable set: the capture run wins.
                Environment.SetEnvironmentVariable("MAHJONG_LANG", "ru");
                Environment.SetEnvironmentVariable("MAHJONG_THEME", "bamboo");
                Environment.SetEnvironmentVariable("MAHJONG_UI_SCALE", "85%");
                Environment.SetEnvironmentVariable("MAHJONG_VARIANT", "sports");
                Environment.SetEnvironmentVariable("MAHJONG_BOT_LEVEL", "3");
                Environment.SetEnvironmentVariable("MAHJONG_BOT_SPEED", "2");
                Environment.SetEnvironmentVariable("MAHJONG_CLOCK", "rules");
                Environment.SetEnvironmentVariable("MAHJONG_SOUND", "off");

                var fromRun = GameSettings.FromEnvironment(BotLevel.Strategic, SettingsFile.Load(SettingsFile.Save(chosen)));
                report.Equal(Language.Ru, fromRun.Language, "MAHJONG_LANG beats the file");
                report.Equal("bamboo", fromRun.Theme.Id, "MAHJONG_THEME beats the file");
                report.Equal(0.85d, fromRun.UiScale, "MAHJONG_UI_SCALE beats the file");
                report.Equal("sports", fromRun.Variant.Id, "MAHJONG_VARIANT beats the file");
                report.Equal(BotLevel.Strategic, fromRun.Level, "MAHJONG_BOT_LEVEL beats the file");
                report.Equal(2d, fromRun.Speed, "MAHJONG_BOT_SPEED beats the file");
                report.Equal(ClockPace.Rules, fromRun.ClockPace, "MAHJONG_CLOCK beats the file");
                report.Equal(0d, fromRun.SoundVolume, "MAHJONG_SOUND beats the file");

                // A broken variable is not "set to nothing": the file keeps its value.
                Environment.SetEnvironmentVariable("MAHJONG_THEME", "какая-то тема");
                Environment.SetEnvironmentVariable("MAHJONG_UI_SCALE", "огромный");
                var halfBroken = GameSettings.FromEnvironment(BotLevel.Strategic, SettingsFile.Load(SettingsFile.Save(chosen)));
                report.Equal("ivory", halfBroken.Theme.Id, "a broken MAHJONG_THEME leaves the file's palette");
                report.Equal(1.15d, halfBroken.UiScale, "a broken MAHJONG_UI_SCALE leaves the file's size");
            }
            finally
            {
                foreach (var name in names) Environment.SetEnvironmentVariable(name, previous[name]);
            }
        }

        /// <summary>
        /// A player who never chose anything, or whose file was deleted or corrupted, still gets a table:
        /// defaults and not an exception. The write side is checked the same way — a folder that is not
        /// there is created, and a write that cannot happen reports itself instead of throwing.
        /// </summary>
        private static void SettingsMissingFile(SelfTestReport report)
        {
            var folder = Path.Combine(Path.GetTempPath(), "mahjong-settings-selftest");
            var path = SettingsFile.PathIn(folder);
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);

                report.Equal(UiThemes.Default.Id, SettingsFile.Read(path).Theme.Id,
                    "a missing file gives a fresh table");
                report.Equal(Language.Ru, SettingsFile.Read(string.Empty).Language,
                    "an empty path gives a fresh game's language");

                var chosen = new GameSettings { Language = Language.En, Theme = UiThemes.Dawn, UiScale = 0.85d };
                chosen.SoundVolume = 0.5d;
                report.Check(SettingsFile.Write(path, chosen), "the folder of the file is created");
                report.Check(File.Exists(path), "the settings are on disk");
                report.Check(!File.Exists(path + ".tmp"), "no temporary file is left behind");

                var read = SettingsFile.Read(path);
                report.Equal("dawn", read.Theme.Id, "the file read back holds the palette");
                report.Equal(Language.En, read.Language, "the file read back holds the language");
                report.Equal(0.85d, read.UiScale, "the file read back holds the size");
                report.Equal(0.5d, read.SoundVolume, "the file read back holds the volume");

                // Overwriting keeps one file: a second write replaces the first instead of piling up.
                chosen.Theme = UiThemes.Bamboo;
                report.Check(SettingsFile.Write(path, chosen), "the settings can be rewritten");
                report.Equal("bamboo", SettingsFile.Read(path).Theme.Id, "the rewrite wins over the old file");
                report.Equal(1, Directory.GetFiles(folder).Length, "one settings file, no leftovers");

                // A file that is not text at all is a file with no settings in it, not a crash.
                File.WriteAllBytes(path, new byte[] { 0, 1, 2, 3, 255 });
                report.Equal(UiThemes.Default.Id, SettingsFile.Read(path).Theme.Id,
                    "a file that is not a settings file gives a fresh table");

                // A path that cannot be written reports itself: a lost settings file must not stop a game.
                // A file standing where the folder should be is the cheapest way to make the write fail.
                var blocked = Path.Combine(folder, "blocked");
                File.WriteAllText(blocked, "not a folder");
                report.Check(!SettingsFile.Write(Path.Combine(blocked, SettingsFile.FileName), chosen),
                    "a write that cannot happen is a refusal, not a throw");
                report.Check(!SettingsFile.WriteText(string.Empty, "theme=ivory"), "no path is no write");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(folder)) Directory.Delete(folder, true);
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                {
                    // A leftover temporary folder is harmless.
                }
            }
        }
    }
}
