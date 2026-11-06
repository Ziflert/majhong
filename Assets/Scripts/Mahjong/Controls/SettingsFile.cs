using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Mahjong.Localization;
using Mahjong.Rules;

namespace Mahjong.Controls
{
    /// <summary>
    /// The settings of the table as a file on disk (8.11): the language, the bots, the windows, the
    /// length of the session, the seed, the sound and the look, so the next run opens the table the way
    /// the last one was left.
    ///
    /// Until now everything lived in memory and in <c>MAHJONG_*</c>, which meant a player chose a
    /// palette, a volume and a language for every single game. The file is plain text, one
    /// <c>key=value</c> per line: a player can read it, a build can carry it, and a test can write one
    /// without a save machine.
    ///
    /// Two rules keep it from ever stopping the game: an unknown key is ignored (a file written by a
    /// newer build still opens), and a value that does not parse keeps the default (a mistyped line
    /// costs one setting, not the table). The order of precedence stays defaults → file → environment,
    /// so <c>MAHJONG_THEME=ivory</c> still paints a capture run over whatever the player chose.
    ///
    /// Knows nothing about Unity, so the whole format is checked headless.
    /// </summary>
    public static class SettingsFile
    {
        /// <summary>The format of the file; a newer number is read as far as the keys are understood.</summary>
        public const int Version = 1;

        /// <summary>Name of the file next to the saves.</summary>
        public const string FileName = "settings.txt";

        /// <summary>Header the game writes; a comment for anyone who opens the file by hand.</summary>
        private const string Header =
            "# Mahjong: the table as the player left it. Every line is setting=value; a line that\n" +
            "# does not parse keeps the default, so this file can be edited or deleted safely.";

        /// <summary>Every key the file may carry, in the order the screens show the settings.</summary>
        public static readonly IReadOnlyList<string> Keys = new[]
        {
            "rules", "language", "bots", "speed", "result", "clock", "session", "seed", "last_seed",
            "sound", "music", "profile", "potential", "theme", "scale"
        };

        /// <summary>The settings file of a folder: <c>&lt;folder&gt;/settings.txt</c>.</summary>
        public static string PathIn(string directory) => Path.Combine(directory, FileName);

        /// <summary>
        /// The settings as the text of the file. Every value is written in the form the parser reads
        /// back — the profile id, <c>ru</c>/<c>en</c>, the level number, the multiplier, the pace name,
        /// the minutes, the seed, the volume and then the palette and the size.
        /// </summary>
        public static string Save(GameSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var text = new StringBuilder();
            text.AppendLine(Header);
            text.Append("version=").AppendLine(Version.ToString(CultureInfo.InvariantCulture));
            text.Append("rules=").AppendLine(settings.Variant.Id);
            text.Append("language=").AppendLine(Loc.LanguageId(settings.Language));
            text.Append("bots=").AppendLine(((int)settings.Level).ToString(CultureInfo.InvariantCulture));
            text.Append("speed=").AppendLine(settings.Speed.ToString("0.##", CultureInfo.InvariantCulture));
            text.Append("result=").AppendLine(settings.Result == ResultAdvance.Manual
                ? "manual"
                : ((int)settings.Result).ToString(CultureInfo.InvariantCulture));
            text.Append("clock=").AppendLine(PaceId(settings.ClockPace));
            text.Append("session=").AppendLine(SessionLengthSettings.IsUnlimited(settings.SessionLimit)
                ? "unlimited"
                : settings.SessionLimit.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture));
            text.Append("seed=").AppendLine(settings.Seed.ToString(CultureInfo.InvariantCulture));
            text.Append("last_seed=").AppendLine(settings.LastSeed.ToString(CultureInfo.InvariantCulture));
            text.Append("sound=").AppendLine(SoundVolumeSettings.Text(settings.SoundVolume));
            text.Append("music=").AppendLine(MusicVolumeSettings.Text(settings.MusicVolume));
            text.Append("profile=").AppendLine(settings.Profile.Id);
            text.Append("potential=").AppendLine(HandPotentialModeSettings.Id(settings.Potential));
            text.Append("theme=").AppendLine(settings.Theme.Id);
            text.Append("scale=").AppendLine(UiThemeSettings.ScaleText(settings.UiScale));
            return text.ToString();
        }

        /// <summary>
        /// The settings a text describes: the defaults of a fresh table, then every line that makes
        /// sense. A line with an unknown key, no <c>=</c> or a value this build does not understand is
        /// skipped, and the setting keeps what it had.
        /// </summary>
        public static GameSettings Load(string text)
        {
            // A table that has never been configured speaks Russian, the language of the rulebook in
            // the repository; the file (or MAHJONG_LANG, through FromEnvironment) may say otherwise.
            var settings = new GameSettings { Language = Loc.PreferredLanguage };
            Apply(text, settings);
            return settings;
        }

        /// <summary>
        /// Puts the lines of a text onto settings that already exist. Separate from <see cref="Load"/>
        /// so the environment can sit on top of the file without a second copy of the parser.
        /// </summary>
        public static void Apply(string text, GameSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(text)) return;

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim().TrimEnd('\r');
                if (trimmed.Length == 0 || trimmed[0] == '#') continue;

                var split = trimmed.IndexOf('=');
                if (split <= 0) continue;

                var key = trimmed[..split].Trim().ToLowerInvariant();
                var value = trimmed[(split + 1)..].Trim();
                ApplyOne(key, value, settings);
            }
        }

        /// <summary>
        /// The settings stored at <paramref name="path"/>. A missing, unreadable or broken file gives a
        /// fresh table instead of an exception: a player who loses the file loses the settings, not the
        /// game.
        /// </summary>
        public static GameSettings Read(string path)
        {
            string text;
            try
            {
                text = string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? string.Empty : File.ReadAllText(path);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
                                                or ArgumentException or NotSupportedException)
            {
                text = string.Empty;
            }

            return Load(text);
        }

        /// <summary>
        /// Writes the settings to <paramref name="path"/>, creating the folder. False when the file
        /// could not be written: the table keeps running, the next run just starts from the defaults.
        ///
        /// The text is written to a neighbouring temporary file first and moved over the old one, so an
        /// interrupted write leaves the previous settings in place rather than half a file.
        /// </summary>
        public static bool Write(string path, GameSettings settings) =>
            WriteText(path, Save(settings));

        /// <summary>Writes an already formatted text; used by <see cref="Write"/> and by the tests.</summary>
        public static bool WriteText(string path, string text)
        {
            if (string.IsNullOrWhiteSpace(path) || text == null) return false;

            var temporary = path + ".tmp";
            try
            {
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                File.WriteAllText(temporary, text);
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
                return true;
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException
                                                or ArgumentException or NotSupportedException)
            {
                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                {
                    // Nothing to do: the temporary file is harmless and the next write overwrites it.
                }

                return false;
            }
        }

        /// <summary>One key onto one field; a value that does not parse changes nothing.</summary>
        private static void ApplyOne(string key, string value, GameSettings settings)
        {
            switch (key)
            {
                case "version":
                    // The version is a note to the next build, not a gate: keys are read as they come.
                    break;
                case "rules":
                    if (Rules.RuleVariant.TryParse(value) is { } variant) settings.Variant = variant;
                    break;
                case "language":
                    if (Loc.TryParseLanguage(value) is { } language) settings.Language = language;
                    break;
                case "bots":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bots))
                        settings.Level = bots switch
                        {
                            1 => Bots.BotLevel.Fast,
                            2 => Bots.BotLevel.Cautious,
                            3 => Bots.BotLevel.Strategic,
                            _ => settings.Level
                        };
                    break;
                case "speed":
                    // "0.5" must mean half speed under a Russian locale too, hence the invariant culture.
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                        settings.SetSpeedIndex(NearestSpeed(speed));
                    break;
                case "result":
                    if (ResultAdvanceSettings.TryParse(value) is { } advance) settings.Result = advance;
                    break;
                case "clock":
                    if (ClockPaceSettings.TryParse(value) is { } pace) settings.ClockPace = pace;
                    break;
                case "session":
                    if (SessionLengthSettings.TryParse(value) is { } limit) settings.SessionLimit = limit;
                    break;
                case "seed":
                    if (DealSeedSettings.TryParse(value) is { } seed) settings.Seed = seed;
                    break;
                case "last_seed":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var last))
                        settings.LastSeed = last;
                    break;
                case "sound":
                    if (SoundVolumeSettings.TryParse(value) is { } volume) settings.SoundVolume = volume;
                    break;
                case "music":
                    if (MusicVolumeSettings.TryParse(value) is { } music) settings.MusicVolume = music;
                    break;
                case "profile":
                    if (UiProfiles.TryParse(value) is { } profile) settings.Profile = profile;
                    break;
                case "potential":
                    if (HandPotentialModeSettings.TryParse(value) is { } potential)
                        settings.Potential = potential;
                    break;
                case "theme":
                    if (UiThemeSettings.TryParseTheme(value) is { } theme) settings.Theme = theme;
                    break;
                case "scale":
                    if (UiThemeSettings.TryParseScale(value) is { } scale) settings.UiScale = scale;
                    break;
            }
        }

        /// <summary>Index of the multiplier closest to a value read from the file.</summary>
        private static int NearestSpeed(double speed)
        {
            var best = 0;
            for (var index = 0; index < GameSettings.Speeds.Length; index++)
                if (Math.Abs(GameSettings.Speeds[index] - speed) <
                    Math.Abs(GameSettings.Speeds[best] - speed))
                    best = index;
            return best;
        }

        /// <summary>The pace as the file writes it: <c>rules</c>, <c>calm</c> or <c>unlimited</c>.</summary>
        private static string PaceId(ClockPace pace) => pace switch
        {
            ClockPace.Calm => "calm",
            ClockPace.Unlimited => "unlimited",
            _ => "rules"
        };
    }
}
