using System;
using System.Collections.Generic;

namespace Mahjong.Controls
{
    /// <summary>
    /// A piece of music as data: how long one turn of the loop lasts, how loud it peaks, and the tones
    /// and strikes inside it. The same shape as <see cref="SoundRecipe"/>, minus the cue — music is not
    /// an event of the table, it is a mood under everything the table says.
    ///
    /// A track is written as a loop, so its length is exactly one bar of the tune: what the player
    /// hears after six seconds is the first six seconds again. The last note therefore has to die
    /// before the loop ends, otherwise the seam would cut a ringing string in half.
    /// </summary>
    public sealed record MusicTrack(string Id, double Seconds, double Gain,
        IReadOnlyList<SoundBurst> Noise, IReadOnlyList<SoundTone> Tones);

    /// <summary>
    /// The music of the table (4.10, remainder): one theme, synthesised like the cues, so a build cannot
    /// lose it and a test can measure it without ears.
    ///
    /// The tune is a slow pentatonic figure in A — the five notes a mahjong table would be played with
    /// on a zither: a bass note that rings under each half of the loop and four plucked notes over it.
    /// Nothing is sampled and nothing is streamed: the whole theme is six seconds of arithmetic.
    /// </summary>
    public static class MusicTracks
    {
        /// <summary>One beat of the theme in seconds: eighty beats a minute, slow enough to read by.</summary>
        public const double Beat = 0.75d;

        /// <summary>The theme the table plays. Eight beats, then the same eight again.</summary>
        public static readonly MusicTrack Table = new("table", 8d * Beat, 0.55d,
            new[]
            {
                // A brush of air on the first beat of each half: texture, not rhythm.
                new SoundBurst(0.000, 1.00, 0.050, 0.14),
                new SoundBurst(0.000 + 4d * Beat, 0.70, 0.045, 0.12)
            },
            new[]
            {
                // The two bass notes: A under the first half, G under the second, each ringing on.
                new SoundTone(110.00, 0.000, 0.30, 1.400, 0.010),
                new SoundTone(98.00, 4d * Beat, 0.26, 0.900, 0.010),

                // The melody: A C D E G, the pentatonic the theme never leaves.
                new SoundTone(329.63, 0.000, 0.50, 0.300, 0.006), // E4
                new SoundTone(440.00, 1d * Beat, 0.50, 0.300, 0.006), // A4
                new SoundTone(392.00, 2d * Beat, 0.48, 0.300, 0.006), // G4
                new SoundTone(329.63, 3d * Beat, 0.46, 0.300, 0.006), // E4
                new SoundTone(293.66, 4d * Beat, 0.50, 0.300, 0.006), // D4
                new SoundTone(329.63, 5d * Beat, 0.48, 0.300, 0.006), // E4
                new SoundTone(261.63, 6d * Beat, 0.46, 0.280, 0.006), // C4
                // The last note starts more than a second before the loop ends and is short on purpose:
                // the seam has to fall on silence, not on a string still swinging.
                new SoundTone(220.00, 6.6d * Beat, 0.44, 0.260, 0.006) // A3
            });

        /// <summary>Every track the game has; one for now, and a test checks the catalogue by it.</summary>
        public static readonly IReadOnlyList<MusicTrack> All = new[] { Table };

        /// <summary>The theme a table is built with.</summary>
        public static MusicTrack Default => Table;

        /// <summary>The track with a name, or null when no track is called that.</summary>
        public static MusicTrack? Find(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            foreach (var track in All)
                if (string.Equals(track.Id, id.Trim(), StringComparison.OrdinalIgnoreCase)) return track;
            return null;
        }
    }

    /// <summary>
    /// How loud the music is (4.10, remainder). It is a setting of its own, not a share of the table
    /// volume: a player who wants the knocks of a discard and no tune under them would otherwise have
    /// to choose. The ladder is the same one the table uses — off, then a quarter at a time — so there
    /// is one way to write down a volume in this game.
    ///
    /// The default is half: a theme is a background, and the first thing a player does to a game that
    /// plays music louder than its own sounds is look for the setting that turns it off.
    /// </summary>
    public static class MusicVolumeSettings
    {
        /// <summary>The same five steps the table volume walks; silence is the first of them.</summary>
        public static readonly double[] Steps = SoundVolumeSettings.Steps;

        /// <summary>Half volume: audible under the table, never over it.</summary>
        public const double Default = 0.5d;

        /// <summary>True when the setting means "no music at all"; the player then asks for no clip.</summary>
        public static bool IsMuted(double volume) => SoundVolumeSettings.IsMuted(volume);

        /// <summary>Keeps a volume inside 0…1, which is what an audio source takes.</summary>
        public static double Clamp(double volume) => SoundVolumeSettings.Clamp(volume);

        /// <summary>How the music row prints the setting: a word for silence, a percentage otherwise.</summary>
        public static string Text(double volume) => SoundVolumeSettings.Text(volume);

        /// <summary>Steps one notch up or down; the value stays where it is at either end.</summary>
        public static double Step(double volume, int delta) => SoundVolumeSettings.Step(volume, delta);

        /// <summary>A volume from text ("off", "50%", "0.5"); null when the text means nothing.</summary>
        public static double? TryParse(string? text) => SoundVolumeSettings.TryParse(text);
    }
}
