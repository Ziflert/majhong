using System;
using System.Globalization;
using Mahjong.Bots;
using Mahjong.Localization;
using Mahjong.Rules;

namespace Mahjong.Controls
{
    /// <summary>
    /// What the player can change about the table: the interface language, how strong the bots are and
    /// how fast they play.
    ///
    /// A setting has one place where it is parsed and one place where its default is written down, so
    /// a capture run, a test and the menu cannot disagree about what "level 2" or "speed 2" means.
    /// Knows nothing about Unity, so the whole thing is checked headless.
    /// </summary>
    public sealed class GameSettings
    {
        /// <summary>
        /// The rules profile of the table. Chosen by <c>MAHJONG_VARIANT</c> until the table screen
        /// offers the presets on screen (docs/rules-classic.md, "Настройки стола", этап 2).
        /// </summary>
        public RuleVariant Variant { get; set; } = RuleVariant.Default;

        /// <summary>Optional sporting-hand potential overlay; off by default to preserve immersion.</summary>
        public HandPotentialMode Potential { get; set; } = HandPotentialMode.Off;

        /// <summary>Interface composition; independent from the selected colour palette.</summary>
        public UiProfile Profile { get; set; } = UiProfiles.Default;

        /// <summary>Interface font preset. The Unity layer resolves the preset to a font with Cyrillic.</summary>
        public string Font { get; set; } = UiFontSettings.Default;
        public double FontSize { get; set; } = 1d;

        /// <summary>Speeds the menu steps through; the delay between bot moves is divided by this.</summary>
        public static readonly double[] Speeds = { 0.25, 0.5, 1, 2, 4 };

        /// <summary>Index of 1× in <see cref="Speeds"/>; the game starts here.</summary>
        private const int NormalSpeed = 2;

        public Language Language { get; set; }

        public BotLevel Level { get; set; } = BotLevel.Strategic;

        /// <summary>Index into <see cref="Speeds"/>; 1× by default.</summary>
        public int SpeedIndex { get; private set; } = NormalSpeed;

        /// <summary>
        /// How long the result screen waits before the next hand: 3 / 5 / 10 seconds or only by the
        /// button (У-7). The table used to deal again three seconds after the hand ended, which was
        /// too fast to read a classic settlement.
        /// </summary>
        public ResultAdvance Result { get; set; } = ResultAdvanceSettings.Default;

        /// <summary>
        /// How much time the player gets for his decisions (У-1). The rules' own windows are 20 / 10 / 3
        /// seconds; a slower table and a table without limits are house rules, and the HUD says so.
        /// </summary>
        public ClockPace ClockPace { get; set; } = ClockPaceSettings.Default;

        /// <summary>The three windows of a hand that the chosen pace stands for.</summary>
        public ClockLimits Windows => ClockPaceSettings.Limits(ClockPace);

        /// <summary>
        /// How long the session runs (8.7). The rules give a full game 150 minutes (3.5.6); a shorter
        /// table and a table without a clock are house rules, and the HUD says so. A zero limit means
        /// the sixteen hands decide the game on their own.
        /// </summary>
        public TimeSpan SessionLimit { get; set; } = SessionLengthSettings.Default;

        /// <summary>
        /// The deal a new game starts from (8.7): <see cref="DealSeedSettings.Random"/> means a fresh one
        /// every time, a number repeats the deal remembered in <see cref="LastSeed"/>.
        /// </summary>
        public int Seed { get; set; } = DealSeedSettings.Default;

        /// <summary>The seed of the last game the table dealt; the welcome screen steps the deal row back to it.</summary>
        public int LastSeed { get; set; }

        /// <summary>
        /// How loud the table is (4.10): 0 is silence, 1 is full volume. The ladder and its text live in
        /// <see cref="SoundVolumeSettings"/>; the player reads the value off this field and nothing else.
        /// </summary>
        public double SoundVolume { get; set; } = SoundVolumeSettings.Default;

        /// <summary>
        /// How loud the music under the table is (4.10): its own setting, on the same ladder, so a player
        /// who wants the tiles and no tune can have exactly that.
        /// </summary>
        public double MusicVolume { get; set; } = MusicVolumeSettings.Default;

        /// <summary>
        /// The palette the interface is painted with. A name from <see cref="UiThemes.All"/>, not a set
        /// of colours: a player chooses a look, and every panel, the cloth of the table and the tiles
        /// follow it.
        /// </summary>
        public UiTheme Theme { get; set; } = UiThemeSettings.DefaultTheme;

        /// <summary>
        /// How big the interface is drawn, one step of <see cref="UiThemeSettings.Scales"/>. The layout
        /// numbers stay written for 100% and every panel multiplies them by this.
        /// </summary>
        public double UiScale { get; set; } = UiThemeSettings.DefaultScale;

        /// <summary>How much faster than normal the bots play; never zero, so a delay cannot vanish.</summary>
        public double Speed => Speeds[SpeedIndex];

        /// <summary>One bot move every <c>baseDelay / Speed</c> seconds.</summary>
        public double ScaledDelay(double baseDelay) => baseDelay / Speed;

        public void SetSpeedIndex(int index) => SpeedIndex = Math.Clamp(index, 0, Speeds.Length - 1);

        /// <summary>Steps the speed one notch up; false at the top.</summary>
        public bool Faster()
        {
            if (SpeedIndex >= Speeds.Length - 1) return false;
            SpeedIndex++;
            return true;
        }

        /// <summary>Steps the speed one notch down; false at the bottom.</summary>
        public bool Slower()
        {
            if (SpeedIndex <= 0) return false;
            SpeedIndex--;
            return true;
        }

        /// <summary>
        /// The same three settings from the environment, so a capture run or a test can ask for a
        /// language, a bot level and a bot speed without touching the scene. Values that make no sense
        /// are ignored instead of crashing the table: a wrong variable must not stop the game from
        /// starting.
        ///
        /// <paramref name="basis"/> is what sits under the environment — the settings read from the
        /// player's file (8.11). A variable that is not set then leaves the file's value alone; without
        /// a basis the fresh table's own defaults apply.
        /// </summary>
        public static GameSettings FromEnvironment(BotLevel fallbackLevel = BotLevel.Strategic,
            GameSettings? basis = null)
        {
            var settings = basis ?? new GameSettings
            {
                Language = Loc.PreferredLanguage,
                Level = fallbackLevel
            };

            // MAHJONG_LANG=ru switches a capture run; a file that already holds a language keeps it.
            if (Loc.TryParseLanguage(Environment.GetEnvironmentVariable("MAHJONG_LANG")) is { } language)
                settings.Language = language;

            if (FromLevelText(Environment.GetEnvironmentVariable("MAHJONG_BOT_LEVEL")) is { } level)
                settings.Level = level;

            if (RuleVariant.TryParse(Environment.GetEnvironmentVariable("MAHJONG_VARIANT")) is { } variant)
                settings.Variant = variant;

            if (UiProfiles.TryParse(Environment.GetEnvironmentVariable("MAHJONG_UI_PROFILE")) is { } profile)
                settings.Profile = profile;

            var speedText = Environment.GetEnvironmentVariable("MAHJONG_BOT_SPEED");
            // Invariant culture: "0.5" must mean half speed even under a Russian locale, where the
            // comma is the decimal separator and TryParse would otherwise refuse the string.
            if (double.TryParse(speedText, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                settings.SetSpeedIndex(NearestSpeed(speed));

            // MAHJONG_RESULT_DELAY=0 holds the result screen until the button is pressed, which is
            // what a capture run needs; nonsense keeps the five-second default.
            var resultText = Environment.GetEnvironmentVariable("MAHJONG_RESULT_DELAY");
            if (ResultAdvanceSettings.TryParse(resultText ?? string.Empty) is { } advance) settings.Result = advance;

            // MAHJONG_CLOCK=rules|calm|unlimited (or a triple of seconds) opens the windows of a
            // capture run without touching the scene; nonsense keeps the rules' own windows.
            var clockText = Environment.GetEnvironmentVariable("MAHJONG_CLOCK");
            if (ClockPaceSettings.TryParse(clockText ?? string.Empty) is { } pace) settings.ClockPace = pace;

            // MAHJONG_SESSION_MINUTES=0.2 shortens the session so the time-up path can be seen in a
            // capture run; "unlimited" leaves the table without a clock. Nonsense keeps 150 minutes.
            var sessionText = Environment.GetEnvironmentVariable("MAHJONG_SESSION_MINUTES");
            if (SessionLengthSettings.TryParse(sessionText ?? string.Empty) is { } limit) settings.SessionLimit = limit;

            // MAHJONG_SEED=12345 repeats a game exactly: the same deal, the same bots, the same hands.
            var seedText = Environment.GetEnvironmentVariable("MAHJONG_SEED");
            if (DealSeedSettings.TryParse(seedText ?? string.Empty) is { } seed) settings.Seed = seed;

            // MAHJONG_SOUND=off|0.3|70% sets the volume of a capture run; the table is built with
            // sound on, so a run that must stay quiet has to say so.
            var soundText = Environment.GetEnvironmentVariable("MAHJONG_SOUND");
            if (SoundVolumeSettings.TryParse(soundText) is { } volume) settings.SoundVolume = volume;

            // MAHJONG_MUSIC=off silences the theme of a capture run; it is the same ladder as the sound.
            var musicText = Environment.GetEnvironmentVariable("MAHJONG_MUSIC");
            if (MusicVolumeSettings.TryParse(musicText) is { } music) settings.MusicVolume = music;

            // MAHJONG_THEME=ivory paints a capture run in the light palette and MAHJONG_UI_SCALE=1.15
            // draws the interface bigger; both are look settings, and nonsense keeps the default.
            var themeText = Environment.GetEnvironmentVariable("MAHJONG_THEME");
            if (UiThemeSettings.TryParseTheme(themeText) is { } theme) settings.Theme = theme;

            var potentialText = Environment.GetEnvironmentVariable("MAHJONG_POTENTIAL");
            if (HandPotentialModeSettings.TryParse(potentialText) is { } potential)
                settings.Potential = potential;

            var scaleText = Environment.GetEnvironmentVariable("MAHJONG_UI_SCALE");
            if (UiThemeSettings.TryParseScale(scaleText) is { } scale) settings.UiScale = scale;

            return settings;
        }

        /// <summary>Level from "1".."3" or from its name; null when the text means nothing.</summary>
        public static BotLevel? FromLevelText(string? text) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
                ? level switch
                {
                    1 => BotLevel.Fast,
                    2 => BotLevel.Cautious,
                    3 => BotLevel.Strategic,
                    _ => null
                }
                : text?.Trim().ToLowerInvariant() switch
                {
                    "fast" or "speed" => BotLevel.Fast,
                    "cautious" or "danger" => BotLevel.Cautious,
                    "strategic" or "smart" => BotLevel.Strategic,
                    _ => null
                };

        /// <summary>The step of <see cref="Speeds"/> closest to a multiplier read from the environment.</summary>
        private static int NearestSpeed(double speed)
        {
            var best = NormalSpeed;
            for (var index = 0; index < Speeds.Length; index++)
                if (Math.Abs(Speeds[index] - speed) < Math.Abs(Speeds[best] - speed)) best = index;
            return best;
        }

        /// <summary>The multiplier as the menu prints it: ×0.5, ×1, ×4.</summary>
        public string SpeedText() => "×" + Speed.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
