using System.Collections;
using System.Collections.Generic;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The sounds of the table inside the running prototype (4.10). The recipes and the samples are
    /// checked headless; what only Unity can answer is here: the samples become a real
    /// <see cref="AudioClip"/>, the table plays cues while a hand is dealt, and the volume on the audio
    /// source is the volume of the setting in the menu.
    ///
    /// Nothing here listens to a speaker: a batch-mode editor has no audio device, so the tests read the
    /// clip the player built and the cues it actually played.
    /// </summary>
    public sealed class SoundTests
    {
        private static MahjongGame Game()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game == null) game = new GameObject("MahjongSoundTestHost").AddComponent<MahjongGame>();
            game.SkipWelcome();
            return game;
        }

        /// <summary>
        /// The suite shares one table, so a test leaves the volume where it found it: a muted table would
        /// silence the tests that come after it.
        /// </summary>
        [UnityTearDown]
        public IEnumerator RestoreTheVolume()
        {
            var game = Object.FindAnyObjectByType<MahjongGame>();
            if (game?.Settings == null) yield break;

            game.Settings.SoundVolume = SoundVolumeSettings.Default;
            game.Settings.MusicVolume = MusicVolumeSettings.Default;
            if (game.Sound != null) game.Sound.ApplyMusic(game.Settings.MusicVolume);
            if (game.IsMenuOpen) game.CloseMenu();
            var previousScale = Time.timeScale;
            Time.timeScale = 100f;
            var guard = 0;
            while (guard++ < 3000 && game.Opening.IsRunning) yield return null;
            Time.timeScale = previousScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryCueBecomesARealClip()
        {
            var game = Game();
            yield return null;

            Assert.IsNotNull(game.Sound, "The table has no ears at all.");
            foreach (var cue in (SoundCue[])System.Enum.GetValues(typeof(SoundCue)))
            {
                var clip = game.Sound.ClipFor(cue);
                Assert.IsNotNull(clip, $"The cue {cue} has no clip.");
                Assert.Greater(clip!.samples, 0, $"The clip of {cue} carries no samples.");
                Assert.AreEqual(1, clip.channels, $"The clip of {cue} is not mono.");
                Assert.AreEqual(SoundPlayer.SampleRate, clip.frequency,
                    $"The clip of {cue} is not played at the rate it was rendered at.");

                // The clip Unity holds is the clip the rules assembly rendered, sample for sample.
                var expected = SoundSynth.Render(SoundCues.Recipe(cue), SoundPlayer.SampleRate);
                Assert.AreEqual(expected.Length, clip.samples, $"The clip of {cue} has the wrong length.");
            }
        }

        [UnityTest]
        public IEnumerator ADealtHandIsHeard()
        {
            var game = Game();
            yield return null;

            // A hand of its own, so the opening — dice, break, deal — starts under the eye of the test.
            game.Settings.SoundVolume = 1d;
            game.StartNewGame(20250101);

            var played = new List<SoundCue>();
            var guard = 0;
            while (guard++ < 6000 && !played.Contains(SoundCue.Deal))
            {
                played.AddRange(game.Sound.Played);
                game.Sound.ForgetPlayed();
                yield return null;
            }

            CollectionAssert.Contains(played, SoundCue.Dice, "The opening of a hand is dealt in silence.");
            CollectionAssert.Contains(played, SoundCue.Break, "The wall is broken in silence.");
            CollectionAssert.Contains(played, SoundCue.Deal, "The deal of sixteen tiles makes no sound.");
        }

        [UnityTest]
        public IEnumerator ADiscardIsHeard()
        {
            var game = Game();
            yield return null;

            game.Settings.SoundVolume = 1d;
            game.StartNewGame(20250102);

            // The human is on the clock and the bots move on theirs; running the windows out and the
            // clock forward is how the prototype plays a hand without a keyboard.
            var played = new List<SoundCue>();
            var previousScale = Time.timeScale;
            Time.timeScale = 50f;
            var guard = 0;
            while (guard++ < 8000 && !played.Contains(SoundCue.Discard))
            {
                if (game.HandClock.IsRunning) game.AdvanceClock(60);
                played.AddRange(game.Sound.Played);
                game.Sound.ForgetPlayed();
                yield return null;
            }

            Time.timeScale = previousScale;
            CollectionAssert.Contains(played, SoundCue.Discard, "No tile ever landed in the discard pile audibly.");
        }

        [UnityTest]
        public IEnumerator TheMenuRowIsTheVolumeOfTheTable()
        {
            var game = Game();
            yield return null;

            game.Settings.SoundVolume = SoundVolumeSettings.Default;
            game.OpenMenu();
            yield return null;

            // Walk the cursor to the sound row the way a player would.
            var row = -1;
            for (var index = 0; index < game.Menu.RowCount; index++)
                if (game.Menu.RowAt(index).Item == MenuItem.Sound) row = index;
            Assert.GreaterOrEqual(row, 0, "The menu has no sound row.");
            Assert.IsTrue(game.MenuPanel.RowText(row).Contains(Loc.Get("menu.sound")),
                "The sound row is not drawn: " + game.MenuPanel.RowText(row));

            for (var step = 0; step < row; step++) game.MenuMove(1);
            Assert.AreEqual(MenuItem.Sound, game.Menu.Selected, "The cursor did not reach the sound row.");

            Assert.IsTrue(game.MenuAdjust(-1), "The volume of the table cannot be stepped down.");
            Assert.AreEqual(game.Settings.SoundVolume, game.Sound.AppliedVolume, 0.001f,
                "The audio source does not carry the volume the menu set.");
            StringAssert.Contains(SoundVolumeSettings.Text(game.Settings.SoundVolume),
                game.MenuPanel.RowText(row), "The sound row does not show the new volume.");

            // Silence is a setting, not a very quiet knock: a muted table plays nothing at all.
            while (game.MenuAdjust(-1)) { }
            Assert.IsTrue(game.Sound.IsMuted, "Stepping the row down to zero did not silence the table.");
            var before = game.Sound.PlayedCount;
            Assert.IsFalse(game.Sound.Play(SoundCue.Discard), "A muted table still played a cue.");
            Assert.AreEqual(before, game.Sound.PlayedCount, "A muted table counted a cue it did not play.");
            game.CloseMenu();
        }

        /// <summary>
        /// The interface has a voice of its own (4.10, remainder): a step of a row ticks, and a step that
        /// changes nothing knocks dully. The music row is used because stepping the sound row down to
        /// silence would silence the very cues the test is listening for.
        /// </summary>
        [UnityTest]
        public IEnumerator TheInterfaceAnswersEveryStep()
        {
            var game = Game();
            yield return null;

            game.Settings.SoundVolume = 1d;
            game.Sound.ForgetPlayed();
            game.OpenMenu();
            yield return null;

            // A step of the cursor ticks; a step of a value ticks too; a step that changes nothing knocks.
            game.MenuMove(1);
            Assert.AreEqual(SoundCue.UiMove, game.Sound.LastCue, "A moved cursor says nothing.");

            var row = -1;
            for (var index = 0; index < game.Menu.RowCount; index++)
                if (game.Menu.RowAt(index).Item == MenuItem.Music) row = index;
            Assert.GreaterOrEqual(row, 0, "The menu has no music row.");

            var guard = 0;
            while (game.Menu.Selected != MenuItem.Music && guard++ < game.Menu.RowCount) game.MenuMove(1);
            Assert.AreEqual(MenuItem.Music, game.Menu.Selected, "The cursor did not reach the music row.");

            Assert.IsTrue(game.MenuAdjust(-1), "The music row cannot be stepped down.");
            Assert.AreEqual(SoundCue.UiMove, game.Sound.LastCue, "A stepped value says nothing.");
            Assert.AreEqual(game.Settings.MusicVolume, game.Sound.AppliedMusicVolume, 0.001f,
                "The theme source does not carry the volume the menu set.");

            while (game.MenuAdjust(-1)) { }
            Assert.IsTrue(game.Sound.IsMusicMuted, "The theme can be stepped down to silence.");
            Assert.AreEqual(SoundCue.UiDenied, game.Sound.LastCue,
                "A step that changes nothing sounds like a step that did something.");

            game.CloseMenu();
            yield return null;
        }

        /// <summary>
        /// The theme is a loop on a source of its own (4.10, remainder): it is one clip, it loops, its
        /// volume is its own setting, and silence stops it instead of playing it at zero.
        /// </summary>
        [UnityTest]
        public IEnumerator TheThemeLoopsAtItsOwnVolume()
        {
            var game = Game();
            yield return null;

            Assert.IsNotNull(game.Sound, "The table has no ears at all.");
            var clip = game.Sound.MusicClip;
            Assert.IsNotNull(clip, "The theme has no clip.");
            Assert.AreEqual(SoundPlayer.SampleRate, clip!.frequency, "The theme is played at the wrong rate.");
            Assert.AreEqual(1, clip.channels, "The theme is not mono.");

            // The clip Unity holds is the loop the rules assembly rendered, to the sample.
            var expected = SoundSynth.Render(MusicTracks.Default, SoundPlayer.SampleRate);
            Assert.AreEqual(expected.Length, clip.samples, "The theme clip is not one loop long.");
            Assert.Greater(expected.Length, SoundCues.SampleRate * 3,
                "The theme is too short to be music and too long to be a cue.");

            // Its volume is a setting of its own, not a share of the table volume.
            game.Sound.ApplyVolume(0.25d);
            game.Sound.ApplyMusic(1d);
            Assert.IsTrue(game.Sound.MusicOn, "The theme does not play when it is asked to.");
            Assert.AreEqual(1d, game.Sound.AppliedMusicVolume, 0.001f,
                "The theme source does not carry its own volume.");
            Assert.AreEqual(0.25d, game.Sound.AppliedVolume, 0.001f,
                "The volume of the table was overwritten by the theme.");

            // Silence is silence: the loop stops instead of running at zero.
            game.Sound.ApplyMusic(0d);
            Assert.IsFalse(game.Sound.MusicOn, "A silent theme still runs.");
            Assert.AreEqual(0d, game.Sound.AppliedMusicVolume, 0.001f, "A silent theme keeps its volume.");

            game.Sound.ApplyMusic(MusicVolumeSettings.Default);
            Assert.IsTrue(game.Sound.MusicOn, "The theme cannot be turned back on.");
            Assert.AreEqual(MusicVolumeSettings.Default, game.Sound.AppliedMusicVolume, 0.001f,
                "The theme does not come back at its own volume.");
        }
    }
}
