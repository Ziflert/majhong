using System.Globalization;
using Mahjong.Localization;

namespace Mahjong.Controls
{
    /// <summary>
    /// Which deal a new game starts from (8.7). A seed used to be a field of the scene, so every launch
    /// of the prototype dealt the same first hand: fine for a capture run, wrong for a player. The screen
    /// now offers two values, and both are worth having — a fresh game every time, and the game the table
    /// dealt last, which is what a playtester needs to show a bug twice.
    ///
    /// Knows nothing about Unity; the game asks <see cref="Resolve"/> for the seed a session starts with.
    /// </summary>
    public static class DealSeedSettings
    {
        /// <summary>Zero is not a seed: the table deals a fresh game whenever the player asks for one.</summary>
        public const int Random = 0;

        /// <summary>A new game is a fresh one until the player chooses to repeat a deal.</summary>
        public const int Default = Random;

        /// <summary>The widest seed <see cref="Resolve"/> hands out; six digits still read on the row.</summary>
        public const int Widest = 999999;

        /// <summary>True when the row says "случайная": the seed is chosen when the game starts.</summary>
        public static bool IsRandom(int seed) => seed <= Random;

        /// <summary>How the row prints the setting: "случайная" or the number of the deal.</summary>
        public static string Text(int seed) => IsRandom(seed)
            ? Loc.Get("seed.random")
            : seed.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Steps between the two values: "случайная" becomes the deal the table remembers, a number goes
        /// back to a fresh one. A table that has not dealt anything yet repeats the very first seed.
        /// </summary>
        public static int Step(int seed, int delta, int remembered)
        {
            if (delta == 0) return seed;
            return IsRandom(seed) ? (remembered > 0 ? remembered : 1) : Random;
        }

        /// <summary>
        /// A setting from a text: "random" / "0", or the number of a deal. Null when the text means
        /// nothing, so a mistyped variable keeps the table's own default instead of stopping the game.
        /// </summary>
        public static int? TryParse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var text = raw.Trim().ToLowerInvariant();
            if (text is "random" or "any" or "fresh" or "случайная" or "0") return Random;

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed) && seed > 0
                ? seed
                : null;
        }

        /// <summary>
        /// The seed a new session is dealt and its bots are built from: the chosen one, or a fresh one
        /// from the clock when the row says the deal is random.
        /// </summary>
        public static int Resolve(int seed) => IsRandom(seed)
            ? new System.Random().Next(1, Widest + 1)
            : seed;
    }
}
