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
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        /// <summary>Samples per second of the generated clips.</summary>
        public const int SampleRate = SoundCues.SampleRate;

        /// <summary>Cues remembered in <see cref="Played"/>; a capture run or a test reads the tail.</summary>
        private const int History = 128;

        private AudioSource source = null!;
        private readonly Dictionary<SoundCue, AudioClip> clips = new();
        private readonly List<SoundCue> played = new();

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
            var samples = SoundSynth.Render(recipe, SampleRate);
            var clip = AudioClip.Create($"cue-{recipe.Name}", samples.Length, 1, SampleRate, false);
            if (clip == null) return null;
            clip.SetData(samples, 0);

            clips[cue] = clip;
            return clip;
        }
    }
}
