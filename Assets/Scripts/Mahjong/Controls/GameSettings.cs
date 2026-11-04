using System;
using System.Globalization;
using Mahjong.Bots;
using Mahjong.Localization;

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
        /// <summary>Speeds the menu steps through; the delay between bot moves is divided by this.</summary>
        public static readonly double[] Speeds = { 0.25, 0.5, 1, 2, 4 };

        /// <summary>Index of 1× in <see cref="Speeds"/>; the game starts here.</summary>
        private const int NormalSpeed = 2;

        public Language Language { get; set; }

        public BotLevel Level { get; set; } = BotLevel.Strategic;

        /// <summary>Index into <see cref="Speeds"/>; 1× by default.</summary>
        public int SpeedIndex { get; private set; } = NormalSpeed;

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
        /// </summary>
        public static GameSettings FromEnvironment(BotLevel fallbackLevel = BotLevel.Strategic)
        {
            var settings = new GameSettings
            {
                Language = Loc.PreferredLanguage,
                Level = FromLevelText(Environment.GetEnvironmentVariable("MAHJONG_BOT_LEVEL")) ?? fallbackLevel
            };

            var speedText = Environment.GetEnvironmentVariable("MAHJONG_BOT_SPEED");
            // Invariant culture: "0.5" must mean half speed even under a Russian locale, where the
            // comma is the decimal separator and TryParse would otherwise refuse the string.
            if (double.TryParse(speedText, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                settings.SetSpeedIndex(NearestSpeed(speed));

            return settings;
        }

        /// <summary>Level from "1".."3" or from its name; null when the text means nothing.</summary>
        public static BotLevel? FromLevelText(string text) =>
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
