using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Controls;
using Mahjong.Localization;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the table sounds (4.10): every cue renders to a clip of the right length that opens
    /// and closes in silence, the cue catalogue is deterministic, and the volume is one setting with
    /// one ladder that the welcome screen and the menu both step.
    ///
    /// The clips are synthesised in the rules assembly, so this runs headless: the play-mode test only
    /// has to check that Unity turns the samples into an <c>AudioClip</c> and that the table plays it.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void SoundGroups(SelfTestReport report)
        {
            Run(report, "sound: every cue is a clip that opens and closes in silence", () => SoundClips(report));
            Run(report, "sound: the volume steps from silence to full volume", () => SoundVolumeRules(report));
            Run(report, "sound: the two screens step the volume of the table", () => SoundRows(report));
        }

        private static void SoundClips(SelfTestReport report)
        {
            var cues = (SoundCue[])Enum.GetValues(typeof(SoundCue));
            report.Equal(cues.Length, SoundCues.Count, "every cue has a recipe");
            report.Equal(cues.Length, SoundCues.All.Select(recipe => recipe.Cue).Distinct().Count(),
                "a cue appears in the catalogue once");

            var rendered = new Dictionary<SoundCue, float[]>();
            foreach (var cue in cues)
            {
                var recipe = SoundCues.Recipe(cue);
                var samples = SoundSynth.Render(recipe);
                rendered[cue] = samples;

                // The clip lasts what the recipe says, and Unity gets exactly this array as a clip.
                report.Equal((int)Math.Round(recipe.Seconds * SoundCues.SampleRate), samples.Length,
                    $"{cue} renders a clip of its own length");
                report.Check(recipe.Seconds is > 0.05 and < 2.5, $"{cue} is a short cue, not a piece of music");
                report.Check(recipe.Noise.Count + recipe.Tones.Count > 0, $"{cue} is made of something");

                // It sounds: a clip that renders silence would be a cue the player never hears.
                var peak = SoundSynth.Peak(samples);
                report.Check(peak > 0.1, $"{cue} is audible (peak {peak:0.000})");
                report.Check(peak <= 1d, $"{cue} does not clip (peak {peak:0.000})");
                report.Check(Math.Abs(peak - recipe.Gain) < 0.02, $"{cue} peaks at the gain of its recipe");

                // Both ends rest on silence: a clip that starts or stops mid-swing clicks. The fade
                // itself is walked sample by sample — a jump between two neighbouring samples is the
                // click, not the level they are at.
                report.Check(Math.Abs(samples[0]) < 0.01, $"{cue} starts in silence");
                report.Check(Math.Abs(samples[^1]) < 0.01, $"{cue} ends in silence");
                for (var index = 0; index < 16; index++)
                {
                    report.Check(Math.Abs(samples[index + 1] - samples[index]) < 0.05,
                        $"{cue} opens without a jump");
                    var tail = samples.Length - 1 - index;
                    report.Check(Math.Abs(samples[tail] - samples[tail - 1]) < 0.05,
                        $"{cue} closes without a jump");
                }

                // One recipe, one clip: the noise is seeded, so a run and a build sound the same.
                var again = SoundSynth.Render(recipe);
                report.Check(samples.SequenceEqual(again), $"{cue} renders the same samples twice");
            }

            // Two cues that render the same samples would be one sound with two names.
            foreach (var pair in cues.SelectMany(first => cues.Where(second => second > first)
                         .Select(second => (first, second))))
            {
                report.Check(!rendered[pair.first].SequenceEqual(rendered[pair.second]),
                    $"{pair.first} and {pair.second} sound different");
            }
        }

        private static void SoundVolumeRules(SelfTestReport report)
        {
            var previous = Loc.Current;
            try
            {
                Loc.Current = Language.Ru;

                report.Equal(SoundVolumeSettings.Default, new GameSettings().SoundVolume,
                    "a fresh table is not at full volume");
                report.Check(!SoundVolumeSettings.IsMuted(SoundVolumeSettings.Default),
                    "the default volume is audible");
                report.Equal(0d, SoundVolumeSettings.Steps[0], "the first step of the ladder is silence");
                report.Equal(1d, SoundVolumeSettings.Steps[^1], "the last step of the ladder is full volume");
                for (var index = 1; index < SoundVolumeSettings.Steps.Length; index++)
                    report.Check(SoundVolumeSettings.Steps[index] > SoundVolumeSettings.Steps[index - 1],
                        "the ladder only goes up");

                // The text of the row: silence has a word of its own, everything else is a percentage.
                report.Equal(Loc.Get("sound.off"), SoundVolumeSettings.Text(0), "silence prints as \"off\"");
                report.Equal("50%", SoundVolumeSettings.Text(0.5), "half volume prints as 50%");
                report.Equal("75%", SoundVolumeSettings.Text(SoundVolumeSettings.Default),
                    "the default prints as 75 %");
                report.Equal("100%", SoundVolumeSettings.Text(1), "full volume prints as 100%");
                Loc.Current = Language.En;
                report.Equal("off", SoundVolumeSettings.Text(0), "the English row says off");

                // Stepping: one notch at a time, and the ends hold instead of wrapping.
                Loc.Current = Language.Ru;
                var volume = SoundVolumeSettings.Default;
                volume = SoundVolumeSettings.Step(volume, 1);
                report.Equal(1d, volume, "one step up from the default is full volume");
                report.Equal(1d, SoundVolumeSettings.Step(volume, 1), "the top of the ladder holds");
                volume = 0.5d;
                volume = SoundVolumeSettings.Step(volume, -1);
                report.Equal(0.25d, volume, "one step down from a half is a quarter");
                volume = SoundVolumeSettings.Step(volume, -1);
                report.Equal(0d, volume, "a step down from the quietest step is silence");
                report.Check(SoundVolumeSettings.IsMuted(volume), "zero volume is silence");
                report.Check(!SoundVolumeSettings.IsMuted(0.25d), "a quarter is still audible");
                report.Equal(0d, SoundVolumeSettings.Step(0d, -1), "the bottom of the ladder holds");
                report.Equal(0.5d, SoundVolumeSettings.Step(0.5d, 0), "a step of zero changes nothing");

                // A volume from elsewhere: the environment of a capture run or a settings file later.
                report.Equal<double?>(0d, SoundVolumeSettings.TryParse("off"), "\"off\" is silence");
                report.Equal<double?>(1d, SoundVolumeSettings.TryParse("on"), "\"on\" is full volume");
                report.Equal<double?>(0.5d, SoundVolumeSettings.TryParse("0.5"), "a fraction is a fraction");
                report.Equal<double?>(0.5d, SoundVolumeSettings.TryParse("50%"), "a percentage is a fraction");
                report.Equal<double?>(0.75d, SoundVolumeSettings.TryParse("75"), "a number above one is a percentage");
                report.Equal<double?>(null, SoundVolumeSettings.TryParse("громко"), "nonsense is no volume");
                report.Equal<double?>(null, SoundVolumeSettings.TryParse(""), "an empty value is no volume");
                report.Equal<double?>(null, SoundVolumeSettings.TryParse(null), "a missing value is no volume");
                report.Equal(1d, SoundVolumeSettings.Clamp(4d), "a volume above full is full volume");
                report.Equal(0d, SoundVolumeSettings.Clamp(-2d), "a negative volume is silence");

                // The setting reaches the table from the environment, like every other table setting.
                var previousSound = Environment.GetEnvironmentVariable("MAHJONG_SOUND");
                try
                {
                    Environment.SetEnvironmentVariable("MAHJONG_SOUND", "25%");
                    report.Equal(0.25d, GameSettings.FromEnvironment().SoundVolume,
                        "MAHJONG_SOUND=25% is a quarter volume");
                    Environment.SetEnvironmentVariable("MAHJONG_SOUND", "какой-то текст");
                    report.Equal(SoundVolumeSettings.Default, GameSettings.FromEnvironment().SoundVolume,
                        "a broken MAHJONG_SOUND keeps the default volume");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("MAHJONG_SOUND", previousSound);
                }
            }
            finally
            {
                Loc.Current = previous;
            }
        }

        private static void SoundRows(SelfTestReport report)
        {
            var previous = Loc.Current;
            try
            {
                Loc.Current = Language.Ru;
                var settings = new GameSettings();
                var title = new TitleScreen(settings);

                // The welcome screen: the row is there, it prints the volume and it steps it.
                var row = title.AllRows().FirstOrDefault(candidate => candidate.Item == TitleItem.Sound);
                report.Check(row != null, "the welcome screen offers the sound of the table");
                report.Equal(Loc.Get("title.sound"), row!.Label, "the row is called \"Звук\"");
                report.Equal(SoundVolumeSettings.Text(settings.SoundVolume), row.Value,
                    "the row shows the volume the table has");

                var steps = 0;
                while (title.Selected != TitleItem.Sound && steps++ < TitleScreen.MaxRows) title.Move(1);
                var before = settings.SoundVolume;
                report.Check(title.Adjust(-1), "the sound row can be stepped");
                report.Check(settings.SoundVolume < before, "stepping the row changes the volume of the table");
                var quietest = settings.SoundVolume;
                while (title.Adjust(-1)) { }
                report.Equal(0d, settings.SoundVolume, "the row steps down to silence");
                report.Check(settings.SoundVolume < quietest, "silence is quieter than a quarter");
                report.Check(!title.Adjust(-1), "the row holds at silence");
                report.Equal(SoundVolumeSettings.Text(0d),
                    title.RowAt(title.Index).Value, "the row prints silence");

                // The menu: the same setting, the same ladder, its own row.
                var menuSettings = new GameSettings();
                var menu = new GameMenu(menuSettings);
                var rows = menu.AllRows();
                var soundRow = -1;
                for (var index = 0; index < rows.Count; index++)
                    if (rows[index].Item == MenuItem.Sound) soundRow = index;
                report.Check(soundRow >= 0, "the in-game menu offers the sound of the table");
                report.Equal(Loc.Get("menu.sound"), rows[soundRow].Label, "the menu row is called \"Звук\"");
                report.Equal(SoundVolumeSettings.Text(menuSettings.SoundVolume), rows[soundRow].Value,
                    "the menu row shows the same volume");

                menuSettings.SoundVolume = 0.5d;
                menu.Move(soundRow);
                report.Equal(MenuItem.Sound, menu.Selected, "the cursor stands on the sound row");
                report.Equal(GameMenu.TotalRows, menu.RowCount, "the panel and the model agree on the row count");
                report.Equal(MenuCommand.None, menu.Activate(), "picking a setting row opens nothing");
                report.Equal(0.75d, menuSettings.SoundVolume, "picking the sound row steps it up");
                report.Check(menu.Adjust(-1), "the menu row can be stepped");
                report.Equal(0.5d, menuSettings.SoundVolume, "the menu steps the volume of the table down");
                menu.Adjust(-1);
                menu.Adjust(-1);
                report.Equal(0d, menuSettings.SoundVolume, "the menu steps the volume down to silence");
                report.Check(!menu.Adjust(-1), "the menu row holds at silence");
                report.Check(!menu.Adjust(0), "a step of zero changes nothing");
            }
            finally
            {
                Loc.Current = previous;
            }
        }
    }
}
