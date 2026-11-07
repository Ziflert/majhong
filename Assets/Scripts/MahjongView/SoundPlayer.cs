using System.Collections.Generic;
using Mahjong.Controls;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The ears of the table (4.10). A cue is a name from <see cref="SoundCues"/>; this component turns
    /// the recipe into a real <see cref="AudioClip"/> once, keeps it and plays it through an
    /// <see cref="AudioSource"/>. The recipes and the samples are synthesised in the rules assembly, so
    /// the sounds are checked headless and no audio asset can go missing from a build.
    ///
    /// One-shot playback, not <c>Play</c>: the dice rattle while the deal starts, and a discard can land
    /// inside the win chime — sounds of a table overlap, so they mix instead of cutting each other off.
    ///
    /// The theme is the exception (4.10): it is one clip played in a loop on a source of its own, with a
    /// volume of its own, so a player can keep the tiles and drop the music.
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        /// <summary>Samples per second of the generated clips.</summary>
        public const int SampleRate = SoundCues.SampleRate;

        /// <summary>Cues remembered in <see cref="Played"/>; a capture run or a test reads the tail.</summary>
        private const int History = 128;

        private AudioSource source = null!;
        private AudioSource theme = null!;
        private readonly Dictionary<SoundCue, AudioClip> clips = new();
        private readonly List<SoundCue> played = new();
        private AudioClip? themeClip;

        /// <summary>Volume the table was built with; the settings field is the only thing that changes it.</summary>
        public double Volume { get; private set; } = SoundVolumeSettings.Default;

        /// <summary>True when the volume is zero: the clip is not even asked for.</summary>
        public bool IsMuted => SoundVolumeSettings.IsMuted(Volume);

        /// <summary>The cue that sounded last; nothing until the first one plays.</summary>
        public SoundCue? LastCue => played.Count == 0 ? null : played[^1];

        /// <summary>How many cues actually sounded in this session.</summary>
        public int PlayedCount => played.Count;

        /// <summary>Cues that sounded, oldest first, at most <see cref="History"/> of them.</summary>
        public IReadOnlyList<SoundCue> Played => played;

        /// <summary>How many cues were skipped because the table is silent.</summary>
        public int SkippedCount { get; private set; }

        /// <summary>Forgets the cues that have sounded; a test reads the history one window at a time.</summary>
        public void ForgetPlayed() => played.Clear();

        /// <summary>The theme the table plays under everything else.</summary>
        public MusicTrack Track => MusicTracks.Default;

        /// <summary>How loud the theme is: a setting of its own, not a share of <see cref="Volume"/>.</summary>
        public double MusicVolume { get; private set; } = MusicVolumeSettings.Default;

        /// <summary>True while the theme is meant to be heard: the setting is not silence and it plays.</summary>
        public bool MusicOn { get; private set; }

        /// <summary>True when the music setting is silence.</summary>
        public bool IsMusicMuted => MusicVolumeSettings.IsMuted(MusicVolume);

        /// <summary>The theme source volume as Unity sees it; the tests compare it with the setting.</summary>
        public double AppliedMusicVolume => theme != null ? theme.volume : 0d;

        /// <summary>The theme as a clip, rendered on first use and kept for the session.</summary>
        public AudioClip? MusicClip => themeClip ??= MakeClip(Track, "music-" + Track.Id);

        /// <summary>True when Unity itself says the theme source is playing (not read in batch mode).</summary>
        public bool IsMusicPlaying => theme != null && theme.isPlaying;

        /// <summary>The audio source volume as Unity sees it; the tests compare it with the setting.</summary>
        public double AppliedVolume => source != null ? source.volume : 0d;

        /// <summary>Creates the ears of the table under the object that owns it.</summary>
        public static SoundPlayer Create(Transform parent)
        {
            var host = new GameObject("Sounds");
            host.transform.SetParent(parent, false);

            var player = host.AddComponent<SoundPlayer>();
            player.source = host.AddComponent<AudioSource>();
            player.source.playOnAwake = false;
            // The table sounds the same from every seat: no distance falloff, no panning.
            player.source.spatialBlend = 0f;
            player.source.volume = (float)player.Volume;

            // The theme sits on a source of its own: it loops, it is never one-shot, and its volume is
            // a setting the table volume must not overwrite.
            var themeHost = new GameObject("Music");
            themeHost.transform.SetParent(host.transform, false);
            player.theme = themeHost.AddComponent<AudioSource>();
            player.theme.playOnAwake = false;
            player.theme.loop = true;
            player.theme.spatialBlend = 0f;
            player.theme.volume = (float)player.MusicVolume;

            // A scene built code-first has no listener; without one Unity plays nothing and says so
            // once per clip. The camera of a real scene usually ships with one, and then ours is skipped.
            if (FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();

            return player;
        }

        /// <summary>Sets the volume from the table settings; zero is silence, not a quiet knock.</summary>
        public void ApplyVolume(double volume)
        {
            Volume = SoundVolumeSettings.Clamp(volume);
            if (source != null) source.volume = (float)Volume;
        }

        /// <summary>
        /// Sets the volume of the theme. Silence stops it rather than playing it at zero: a muted loop
        /// still burns a mixer channel and still asks Unity for a clip.
        /// </summary>
        public void ApplyMusic(double volume)
        {
            MusicVolume = MusicVolumeSettings.Clamp(volume);
            if (theme == null) return;
            theme.volume = (float)MusicVolume;

            if (MusicVolumeSettings.IsMuted(MusicVolume))
            {
                StopMusic();
                return;
            }

            PlayMusic();
        }

        /// <summary>
        /// Starts the theme, or keeps it running where it is. False when nothing sounded: the music is
        /// muted or the clip could not be made. A muted theme is a setting, not an error.
        /// </summary>
        public bool PlayMusic()
        {
            if (IsMusicMuted || theme == null) return false;
            if (MusicOn) return true;

            var clip = MusicClip;
            if (clip == null) return false;

            theme.clip = clip;
            theme.loop = true;
            theme.Play();
            MusicOn = true;
            return true;
        }

        /// <summary>Stops the theme; the clip stays rendered, so turning it back on is instant.</summary>
        public void StopMusic()
        {
            MusicOn = false;
            if (theme == null) return;
            theme.Stop();
            theme.clip = null;
        }

        /// <summary>
        /// Plays a cue. Returns false when nothing sounded — the table is silent, or the cue has no
        /// recipe. A silent table is a setting, so this is not an error and nothing is logged.
        /// </summary>
        public bool Play(SoundCue cue)
        {
            if (IsMuted)
            {
                SkippedCount++;
                return false;
            }

            var clip = ClipFor(cue);
            if (clip == null) return false;

            source.PlayOneShot(clip);
            if (played.Count >= History) played.RemoveAt(0);
            played.Add(cue);
            return true;
        }

        /// <summary>The clip of a cue, rendered on first use and kept for the session.</summary>
        public AudioClip? ClipFor(SoundCue cue)
        {
            if (clips.TryGetValue(cue, out var ready)) return ready;

            var recipe = SoundCues.Recipe(cue);
            var clip = MakeClip(recipe, "cue-" + recipe.Name);
            if (clip == null) return null;

            clips[cue] = clip;
            return clip;
        }

        /// <summary>Turns a recipe or a theme into a clip Unity can play; the samples come from the core.</summary>
        private static AudioClip? MakeClip(SoundRecipe recipe, string name) =>
            MakeClip(SoundSynth.Render(recipe, SampleRate), name);

        private static AudioClip? MakeClip(MusicTrack track, string name) =>
            MakeClip(SoundSynth.Render(track, SampleRate), name);

        private static AudioClip? MakeClip(float[] samples, string name)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            if (clip == null) return null;
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
