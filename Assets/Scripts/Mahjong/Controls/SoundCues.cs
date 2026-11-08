using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Controls
{
    /// <summary>
    /// The sounds of a hand (4.10). One cue per thing that happens at the table; a cue is a name, not a
    /// file, so the sounds ship with the code and a capture run needs no assets.
    /// </summary>
    public enum SoundCue
    {
        /// <summary>The dice of the opening rattle and land (3.5.7.4).</summary>
        Dice = 0,

        /// <summary>The wall is broken at the counted stack (3.5.7.5).</summary>
        Break = 1,

        /// <summary>Tiles leave the wall for the four hands.</summary>
        Deal = 2,

        /// <summary>A tile is taken from the wall.</summary>
        Draw = 3,

        /// <summary>A tile lands in the discard pile — the sound a player hears most often.</summary>
        Discard = 4,

        /// <summary>A chow or a pung is declared.</summary>
        Claim = 5,

        /// <summary>A kong is declared: the same knock, twice as heavy.</summary>
        Kong = 6,

        /// <summary>The human's hand is won.</summary>
        Win = 7,

        /// <summary>Somebody else's hand is won: the same chime, lower and shorter.</summary>
        Defeat = 8,

        /// <summary>The cursor moved to another row, or a value stepped by one notch.</summary>
        UiMove = 9,

        /// <summary>A row was picked and something opened: a screen, a save, a new game.</summary>
        UiSelect = 10,

        /// <summary>A page of the rulebook turned.</summary>
        UiPage = 11,

        /// <summary>The input did nothing: the ladder is at its end or the row is not built yet.</summary>
        UiDenied = 12
    }

    /// <summary>
    /// One noise strike of a cue: the wooden part of a sound. <paramref name="Brightness"/> is the weight
    /// of a fresh sample in a one-pole low pass — 1 is white noise, 0.3 is a dull knock on wood.
    /// </summary>
    public sealed record SoundBurst(double Delay, double Gain, double Decay, double Brightness);

    /// <summary>
    /// One ringing tone of a cue: the chime of a declared meld or of a won hand. A tone is a decaying
    /// sine with a short attack, so it never starts with a click.
    /// </summary>
    public sealed record SoundTone(double Frequency, double Delay, double Gain, double Decay,
        double Attack = 0.004);

    /// <summary>
    /// What a cue is made of: how long it lasts, how loud it peaks (<paramref name="Gain"/> is the peak
    /// of the finished clip, so cues keep their relative loudness), and the strikes and tones inside.
    /// </summary>
    public sealed record SoundRecipe(SoundCue Cue, double Seconds, double Gain,
        IReadOnlyList<SoundBurst> Noise, IReadOnlyList<SoundTone> Tones)
    {
        /// <summary>Short name of the cue, as the clip and the log call it.</summary>
        public string Name => Cue.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// The catalogue of table sounds and the synthesiser that turns a recipe into samples. Nothing here
    /// knows about Unity: a recipe is data, the samples are a float array, and the whole catalogue is
    /// checked headless (lengths, loudness, silence at both ends, determinism). The Unity layer only
    /// wraps the array in an <c>AudioClip</c> (see <c>MahjongView/SoundPlayer.cs</c>).
    ///
    /// Sounds are synthesised instead of shipped because a table has nine short noises and no music yet:
    /// a generated clip cannot go missing from a build, and a test can listen to it without ears.
    /// </summary>
    public static class SoundCues
    {
        /// <summary>Samples per second of every cue; the player creates its clips at the same rate.</summary>
        public const int SampleRate = 44100;

        /// <summary>The catalogue in the order of <see cref="SoundCue"/>.</summary>
        public static readonly IReadOnlyList<SoundRecipe> All = new[]
        {
            // The dice: five knocks on the felt, each softer than the one before, with a bright tick.
            new SoundRecipe(SoundCue.Dice, 0.60, 0.65,
                new[]
                {
                    new SoundBurst(0.000, 1.00, 0.020, 0.62),
                    new SoundBurst(0.085, 0.85, 0.018, 0.60),
                    new SoundBurst(0.155, 0.75, 0.016, 0.58),
                    new SoundBurst(0.230, 0.62, 0.015, 0.56),
                    new SoundBurst(0.300, 0.50, 0.014, 0.54),
                    new SoundBurst(0.360, 0.38, 0.012, 0.52)
                },
                new[] { new SoundTone(1750, 0.000, 0.22, 0.020) }),

            // The break: a heavy knock and the short slide of the loosened stack beside it.
            new SoundRecipe(SoundCue.Break, 0.34, 0.75,
                new[]
                {
                    new SoundBurst(0.000, 1.00, 0.060, 0.34),
                    new SoundBurst(0.055, 0.55, 0.045, 0.28)
                },
                new[] { new SoundTone(420, 0.000, 0.55, 0.070) }),

            // The deal: a steady soft shuffle, sixteen tiles leaving the wall one after another.
            new SoundRecipe(SoundCue.Deal, 0.55, 0.50,
                new[]
                {
                    new SoundBurst(0.000, 0.90, 0.014, 0.66),
                    new SoundBurst(0.032, 0.85, 0.014, 0.64),
                    new SoundBurst(0.064, 0.80, 0.013, 0.62),
                    new SoundBurst(0.096, 0.78, 0.013, 0.62),
                    new SoundBurst(0.128, 0.74, 0.012, 0.60),
                    new SoundBurst(0.160, 0.70, 0.012, 0.60),
                    new SoundBurst(0.192, 0.66, 0.012, 0.58),
                    new SoundBurst(0.224, 0.62, 0.011, 0.58)
                },
                Array.Empty<SoundTone>()),

            // A draw: the quietest sound at the table, so a long hand does not turn into a metronome.
            new SoundRecipe(SoundCue.Draw, 0.11, 0.35,
                new[] { new SoundBurst(0.000, 0.70, 0.018, 0.70) },
                new[] { new SoundTone(1350, 0.000, 0.30, 0.016) }),

            // A discard: one clear plastic knock, the sound the table answers the player with.
            new SoundRecipe(SoundCue.Discard, 0.19, 0.80,
                new[] { new SoundBurst(0.000, 1.00, 0.034, 0.55) },
                new[] { new SoundTone(1050, 0.000, 0.50, 0.030) }),

            // A claim: two rising notes, the same on every table so a claim is recognised by ear.
            new SoundRecipe(SoundCue.Claim, 0.42, 0.60,
                Array.Empty<SoundBurst>(),
                new[]
                {
                    new SoundTone(880.00, 0.000, 0.70, 0.110, 0.005),
                    new SoundTone(1320.00, 0.100, 0.70, 0.170, 0.005)
                }),

            // A kong: the discard knock with a second, lower strike under it.
            new SoundRecipe(SoundCue.Kong, 0.36, 0.85,
                new[]
                {
                    new SoundBurst(0.000, 1.00, 0.048, 0.44),
                    new SoundBurst(0.038, 0.70, 0.042, 0.40)
                },
                new[]
                {
                    new SoundTone(620, 0.000, 0.55, 0.055),
                    new SoundTone(930, 0.000, 0.30, 0.040)
                }),

            // A won hand: C–E–G–C, the four notes of the hand ending.
            new SoundRecipe(SoundCue.Win, 1.10, 0.75,
                Array.Empty<SoundBurst>(),
                new[]
                {
                    new SoundTone(523.25, 0.000, 0.70, 0.400, 0.006),
                    new SoundTone(659.25, 0.110, 0.70, 0.380, 0.006),
                    new SoundTone(783.99, 0.220, 0.70, 0.360, 0.006),
                    new SoundTone(1046.50, 0.330, 0.75, 0.520, 0.006),
                    new SoundTone(261.63, 0.000, 0.30, 0.600, 0.010)
                }),

            // Somebody else's hand: two notes falling, quieter, so the player knows before he reads who won.
            new SoundRecipe(SoundCue.Defeat, 0.80, 0.45,
                Array.Empty<SoundBurst>(),
                new[]
                {
                    new SoundTone(392.00, 0.000, 0.70, 0.240, 0.008),
                    new SoundTone(329.63, 0.180, 0.70, 0.300, 0.008)
                }),

            // The interface (4.10, remainder). The menus, the action tray and the rulebook are not the
            // table: their sounds are shorter, quieter and higher than a tile landing on the felt, so a
            // player never mistakes a row he stepped for a hand he dealt.
            //
            // The cursor: one dry tick, the quietest thing the game says. It sounds on every row and
            // every notch of a value, so it has to be short enough to not become a rattle.
            new SoundRecipe(SoundCue.UiMove, 0.055, 0.30,
                new[] { new SoundBurst(0.000, 1.00, 0.005, 0.88) },
                new[] { new SoundTone(2350.00, 0.000, 0.40, 0.008, 0.001) }),

            // Picking a row: two short rising notes — an answer, not a chime of the table.
            new SoundRecipe(SoundCue.UiSelect, 0.16, 0.42,
                Array.Empty<SoundBurst>(),
                new[]
                {
                    new SoundTone(1174.66, 0.000, 0.55, 0.030, 0.002),
                    new SoundTone(1567.98, 0.040, 0.60, 0.055, 0.002)
                }),

            // A turned page: the brush of paper, low and soft, with a papery edge under it.
            new SoundRecipe(SoundCue.UiPage, 0.30, 0.34,
                new[]
                {
                    new SoundBurst(0.000, 1.00, 0.055, 0.30),
                    new SoundBurst(0.055, 0.60, 0.050, 0.24)
                },
                new[] { new SoundTone(320, 0.000, 0.20, 0.035) }),

            // Nothing happened: a dull low double knock, the sound of a value that is already at its end.
            new SoundRecipe(SoundCue.UiDenied, 0.22, 0.40,
                new[] { new SoundBurst(0.000, 1.00, 0.040, 0.22) },
                new[]
                {
                    new SoundTone(196.00, 0.000, 0.55, 0.070, 0.003),
                    new SoundTone(146.83, 0.022, 0.35, 0.060, 0.003)
                })
        };

        /// <summary>
        /// The cues of the interface: the menus, the action tray, the rulebook and the result screen.
        /// They are a group of their own because the two kinds of sound are checked differently — a
        /// cue of the table may last a second, a click may not.
        /// </summary>
        public static readonly IReadOnlyList<SoundCue> Interface = new[]
        {
            SoundCue.UiMove, SoundCue.UiSelect, SoundCue.UiPage, SoundCue.UiDenied
        };

        /// <summary>True for a cue the interface plays; false for a thing that happens at the table.</summary>
        public static bool IsInterface(SoundCue cue) => Interface.Contains(cue);

        /// <summary>How many cues the table has.</summary>
        public static int Count => All.Count;

        /// <summary>The recipe of one cue.</summary>
        public static SoundRecipe Recipe(SoundCue cue)
        {
            foreach (var recipe in All)
                if (recipe.Cue == cue) return recipe;
            throw new ArgumentOutOfRangeException(nameof(cue), cue, "There is no such sound at the table.");
        }
    }

    /// <summary>
    /// Renders a recipe into samples. Deterministic: the same recipe gives the same array on every run
    /// and on every machine, because the noise comes from a fixed-seed generator of our own — a test can
    /// therefore compare two renders of a cue and expect them to be equal.
    /// </summary>
    public static class SoundSynth
    {
        /// <summary>Seconds of fade at both ends of a clip: a clip that starts or ends mid-swing clicks.</summary>
        private const double Fade = 0.004;

        /// <summary>Renders a cue at the table's own sample rate.</summary>
        public static float[] Render(SoundRecipe recipe) => Render(recipe, SoundCues.SampleRate);

        /// <summary>Renders a cue at any sample rate; the length follows the sample rate exactly.</summary>
        public static float[] Render(SoundRecipe recipe, int sampleRate) =>
            Render(recipe.Seconds, recipe.Gain, recipe.Noise, recipe.Tones, sampleRate, (int)recipe.Cue + 1);

        /// <summary>
        /// Renders a piece of music at the table's own sample rate. Music goes through the same mixer as
        /// a cue — the same noise, the same tones, the same fades at both ends — because a second mixer
        /// would be a second thing to keep in tune. The length of a track is its loop, exactly.
        /// </summary>
        public static float[] Render(MusicTrack track) => Render(track, SoundCues.SampleRate);

        /// <summary>Renders a piece of music at any sample rate.</summary>
        public static float[] Render(MusicTrack track, int sampleRate) =>
            Render(track.Seconds, track.Gain, track.Noise, track.Tones, sampleRate, SeedOf(track.Id));

        /// <summary>The mixer of everything the game says: strikes, tones and the fades of one clip.</summary>
        private static float[] Render(double seconds, double gain, IReadOnlyList<SoundBurst> noise,
            IReadOnlyList<SoundTone> tones, int sampleRate, int seedBase)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));

            var count = Math.Max(1, (int)Math.Round(seconds * sampleRate));
            var mix = new double[count];

            for (var index = 0; index < noise.Count; index++)
                AddBurst(mix, sampleRate, noise[index], SeedOf(seedBase, index));

            foreach (var tone in tones) AddTone(mix, sampleRate, tone);

            // The fades go on before the loudness is measured: a clip whose first strike is its peak
            // would otherwise be normalised against a sample the fade has already pulled down.
            for (var index = 0; index < count; index++) mix[index] *= FadeAt(index, count, sampleRate);

            // The recipe's gain is the peak of the finished clip, not the sum of its layers: cues keep
            // their relative loudness however many strikes and tones were put inside one.
            var peak = 0d;
            for (var index = 0; index < count; index++) peak = Math.Max(peak, Math.Abs(mix[index]));
            var scale = peak > 1e-9 ? gain / peak : 0d;

            var samples = new float[count];
            for (var index = 0; index < count; index++)
            {
                var value = mix[index] * scale;
                // Soft clip as a last line of defence: a clipped sample would be a click, not a limit.
                if (value > 1d) value = 1d;
                else if (value < -1d) value = -1d;
                samples[index] = (float)value;
            }

            return samples;
        }

        /// <summary>Peak of a rendered clip; a cue that renders below this is silent by accident.</summary>
        public static double Peak(IReadOnlyList<float> samples)
        {
            var peak = 0d;
            for (var index = 0; index < samples.Count; index++) peak = Math.Max(peak, Math.Abs(samples[index]));
            return peak;
        }

        /// <summary>The strike of a cue: filtered noise under an exponential envelope.</summary>
        private static void AddBurst(double[] mix, int sampleRate, SoundBurst burst, uint seed)
        {
            var start = (int)Math.Round(burst.Delay * sampleRate);
            if (start >= mix.Length || burst.Decay <= 0d) return;

            var noise = new Noise32(seed);
            var brightness = Math.Clamp(burst.Brightness, 0.02, 1d);
            var low = 0d;
            for (var index = start; index < mix.Length; index++)
            {
                var time = (index - start) / (double)sampleRate;
                // One-pole low pass: brightness 1 keeps the sample as it is, 0.3 keeps only the body.
                low += brightness * (noise.Next() - low);
                mix[index] += low * burst.Gain * Math.Exp(-time / burst.Decay);
            }
        }

        /// <summary>The ringing part of a cue: a decaying sine with a short attack.</summary>
        private static void AddTone(double[] mix, int sampleRate, SoundTone tone)
        {
            var start = (int)Math.Round(tone.Delay * sampleRate);
            if (start >= mix.Length || tone.Decay <= 0d) return;

            var attack = Math.Max(tone.Attack, 1d / sampleRate);
            var step = 2d * Math.PI * tone.Frequency / sampleRate;
            for (var index = start; index < mix.Length; index++)
            {
                var time = (index - start) / (double)sampleRate;
                var rise = time < attack ? time / attack : 1d;
                var envelope = rise * Math.Exp(-time / tone.Decay);
                // A little second harmonic keeps a chime from sounding like a test tone.
                var wave = Math.Sin(step * (index - start)) + 0.3 * Math.Sin(2d * step * (index - start));
                mix[index] += wave * tone.Gain * envelope;
            }
        }

        /// <summary>The fade at both ends of the clip, so it opens and closes on silence.</summary>
        private static double FadeAt(int index, int count, int sampleRate)
        {
            var time = index / (double)sampleRate;
            var total = count / (double)sampleRate;
            if (time < Fade) return time / Fade;
            var left = total - time;
            if (left < Fade) return Math.Max(0d, left / Fade);
            return 1d;
        }

        /// <summary>A seed per sound and per strike: the same noise every run, none of it random at play time.</summary>
        private static uint SeedOf(int seedBase, int burst)
        {
            var value = (uint)(seedBase * 7919 + (burst + 1) * 104729 + 2463534242u);
            return value == 0 ? 2463534242u : value;
        }

        /// <summary>
        /// A seed for a piece of music, taken from its name: the same track is the same noise in every
        /// run and in every build, and two tracks cannot share a texture by accident.
        /// </summary>
        private static int SeedOf(string id)
        {
            var hash = 2166136261u;
            foreach (var letter in id) hash = (hash ^ letter) * 16777619u;
            return (int)(hash % 100000u) + 1000;
        }

        /// <summary>
        /// Xorshift32 and not <see cref="Random"/>: the sequence of <c>System.Random</c> differs between
        /// runtimes, and a clip that sounds different inside Unity and outside it would be a bug nobody
        /// could hear in a test.
        /// </summary>
        private sealed class Noise32
        {
            private uint state;

            public Noise32(uint seed) => state = seed;

            /// <summary>Next sample in −1…1.</summary>
            public double Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state / (double)uint.MaxValue * 2d - 1d;
            }
        }
    }
}
