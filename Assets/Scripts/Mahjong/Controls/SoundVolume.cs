using System;
using System.Globalization;
using Mahjong.Localization;

namespace Mahjong.Controls
{
    /// <summary>
    /// How loud the table is (4.10): a ladder from silence to full volume, the text the rows print and
    /// the one place where a string from the environment becomes a number.
    ///
    /// A table is built with sound on at three quarters, not at full volume: a knock in the ear on the
    /// first discard is the fastest way to make a player mute the game for good. The first step of the
    /// ladder is silence, so "turn the sound off" is the same setting as "quieter" and needs no second
    /// checkbox.
    /// </summary>
    public static class SoundVolumeSettings
    {
        /// <summary>The steps a row walks: off, then a quarter at a time up to full volume.</summary>
        public static readonly double[] Steps = { 0d, 0.25d, 0.5d, 0.75d, 1d };

        /// <summary>Three quarters of full volume: audible, and not in the way of reading the table.</summary>
        public const double Default = 0.75d;

        /// <summary>Anything at or below this is silence, not a very quiet knock.</summary>
        private const double Silence = 0.0001d;

        /// <summary>True when the setting means "no sound at all"; the player then skips the clip.</summary>
        public static bool IsMuted(double volume) => volume <= Silence;

        /// <summary>Keeps a volume inside 0…1, which is what an audio source takes.</summary>
        public static double Clamp(double volume) =>
            double.IsNaN(volume) ? Default : Math.Clamp(volume, 0d, 1d);

        /// <summary>The step of <see cref="Steps"/> closest to a volume read from a file or an environment.</summary>
        public static int IndexOf(double volume)
        {
            var best = 0;
            for (var index = 0; index < Steps.Length; index++)
                if (Math.Abs(Steps[index] - volume) < Math.Abs(Steps[best] - volume)) best = index;
            return best;
        }

        /// <summary>Steps one notch up or down; the value stays where it is at either end.</summary>
        public static double Step(double volume, int delta)
        {
            if (delta == 0) return volume;
            var index = Math.Clamp(IndexOf(volume) + Math.Sign(delta), 0, Steps.Length - 1);
            return Steps[index];
        }

        /// <summary>
        /// How a row prints the setting: silence has a word of its own, a volume is a percentage —
        /// a number and a sign, the same in both languages, so it is not a catalogue entry.
        /// </summary>
        public static string Text(double volume)
        {
            if (IsMuted(volume)) return Loc.Get("sound.off");
            var percent = (int)Math.Round(Clamp(volume) * 100d);
            return percent.ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        /// A volume from text: "off"/"0"/"выкл" is silence, "50%" and "0.5" are the same half volume,
        /// "on"/"full" is full volume. Null when the text means nothing, so a broken variable keeps the
        /// default instead of stopping the table from starting.
        /// </summary>
        public static double? TryParse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var value = text!.Trim().ToLowerInvariant();
            if (value.EndsWith("%", StringComparison.Ordinal)) value = value[..^1].Trim();

            switch (value)
            {
                case "off" or "mute" or "muted" or "выкл" or "нет":
                    return 0d;
                case "on" or "full" or "max" or "вкл":
                    return 1d;
            }

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return null;

            // A bare number above one is read as a percentage, so "75" and "0.75" mean the same thing.
            if (number > 1d) number /= 100d;
            return Clamp(number);
        }
    }
}
