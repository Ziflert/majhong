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
            Run(report, "sound: the interface answers in a voice of its own", () => InterfaceCues(report));
            Run(report, "sound: the music is a loop with a seam you cannot hear", () => MusicLoop(report));
            Run(report, "sound: the two screens step the theme as well", () => MusicRows(report));
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
                title.OpenSettings();

                // The settings page: the row is there, it prints the volume and it steps it.
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

        /// <summary>
        /// The interface is not the table (4.10, remainder). The menus, the tray and the rulebook answer
        /// a click with a click: shorter, quieter and higher than anything a tile makes, so a player
        /// never mistakes a row he stepped for a hand he dealt.
        /// </summary>
        private static void InterfaceCues(SelfTestReport report)
        {
            var cues = (SoundCue[])Enum.GetValues(typeof(SoundCue));
            report.Equal(4, SoundCues.Interface.Count, "the interface has four things to say");
            report.Equal(SoundCues.Interface.Count, SoundCues.Interface.Distinct().Count(),
                "no cue of the interface appears twice");

            foreach (var cue in cues)
            {
                var recipe = SoundCues.Recipe(cue);
                report.Equal(SoundCues.IsInterface(cue), SoundCues.Interface.Contains(cue),
                    $"{cue} is listed as an interface cue exactly when it says so");
                if (!SoundCues.IsInterface(cue)) continue;

                // A click is a click: it must not last like a chime or the knock of a won hand.
                report.Check(recipe.Seconds < 0.35, $"{cue} is a click, not a chime ({recipe.Seconds:0.00} s)");
                report.Check(recipe.Noise.Count + recipe.Tones.Count > 0, $"{cue} is made of something");
            }

            // Loudness and length follow the meaning: the tick of a moved cursor is the smallest thing
            // the game says, and a picked row answers louder and longer than it.
            var move = SoundCues.Recipe(SoundCue.UiMove);
            var select = SoundCues.Recipe(SoundCue.UiSelect);
            report.Check(move.Gain < select.Gain, "the cursor ticks quieter than a picked row answers");
            report.Check(move.Seconds < select.Seconds, "and shorter");
            report.Check(SoundCues.Interface.All(cue => SoundCues.Recipe(cue).Seconds >= move.Seconds),
                "nothing the interface says is shorter than the tick of the cursor");

            // Every interface cue is audible and sounds like none of the table.
            var discard = SoundSynth.Render(SoundCues.Recipe(SoundCue.Discard));
            foreach (var cue in SoundCues.Interface)
            {
                var samples = SoundSynth.Render(SoundCues.Recipe(cue));
                report.Check(SoundSynth.Peak(samples) > 0.1, $"{cue} is audible");
                report.Check(!samples.SequenceEqual(discard), $"{cue} does not sound like a discard");
            }
        }

        /// <summary>
        /// The music (4.10, remainder) is a loop, and a loop is only music if its seam cannot be heard:
        /// the clip has to end on silence and start on silence, or the player hears a click every turn.
        /// Nothing here listens — the samples are measured instead.
        /// </summary>
        private static void MusicLoop(SelfTestReport report)
        {
            report.Equal(1, MusicTracks.All.Count, "the table has one theme");
            report.Equal(MusicTracks.Table.Id, MusicTracks.Default.Id, "and it is the theme a table plays");
            report.Equal(MusicTracks.Table.Id, MusicTracks.Find("TABLE")?.Id, "a theme is found by its name");
            report.Equal<MusicTrack?>(null, MusicTracks.Find("марш"), "a theme that does not exist is no theme");
            report.Equal<MusicTrack?>(null, MusicTracks.Find(""), "an empty name is no theme");

            var track = MusicTracks.Default;
            report.Equal(8d * MusicTracks.Beat, track.Seconds, "the theme is eight beats long");
            report.Check(track.Seconds is > 3d and < 12d,
                "a loop is long enough to breathe and short enough to keep");

            var samples = SoundSynth.Render(track);
            report.Equal((int)Math.Round(track.Seconds * SoundCues.SampleRate), samples.Length,
                "the loop is as long as the theme says, to the sample");
            var peak = SoundSynth.Peak(samples);
            report.Check(peak > 0.1, "the theme is audible");
            report.Check(Math.Abs(peak - track.Gain) < 0.02, "the theme peaks at the gain of its recipe");

            // The seam: both ends rest on silence, so the last sample and the first are both nothing.
            report.Check(Math.Abs(samples[0]) < 0.01, "the theme opens in silence");
            report.Check(Math.Abs(samples[^1]) < 0.02, "and ends in silence, where the loop starts again");
            report.Check(Math.Abs(samples[^1] - samples[0]) < 0.02,
                "the two sides of the seam are the same level");

            // A tail that is still ringing would be cut off by the loop; every note dies before it.
            foreach (var tone in track.Tones)
            {
                report.Check(tone.Delay < track.Seconds,
                    $"the note at {tone.Delay:0.00} s starts inside the loop");
                var left = tone.Gain * Math.Exp(-(track.Seconds - tone.Delay) / tone.Decay);
                report.Check(left < 0.01,
                    $"the note at {tone.Delay:0.00} s has died before the loop ends ({left:0.000})");
            }

            var quiet = 0d;
            for (var index = samples.Length - 1 - SoundCues.SampleRate / 20; index < samples.Length; index++)
                quiet = Math.Max(quiet, Math.Abs(samples[index]));
            report.Check(quiet < 0.05, $"the last fiftieth of a second of the loop is quiet ({quiet:0.000})");

            report.Check(samples.SequenceEqual(SoundSynth.Render(track)), "the theme renders the same samples twice");
            var win = SoundSynth.Render(SoundCues.Recipe(SoundCue.Win));
            report.Check(samples.Length > win.Length, "the theme is longer than any cue of the table");
        }

        /// <summary>
        /// The volume of the theme is a setting of its own on the same ladder: a player who wants the
        /// tiles and no tune can have exactly that, and the two screens that step the table volume step
        /// this one too.
        /// </summary>
        private static void MusicRows(SelfTestReport report)
        {
            var previous = Loc.Current;
            try
            {
                Loc.Current = Language.Ru;
                report.Equal(MusicVolumeSettings.Default, new GameSettings().MusicVolume,
                    "a fresh table has music at half volume");
                report.Check(!MusicVolumeSettings.IsMuted(MusicVolumeSettings.Default), "the theme is audible");
                report.Check(MusicVolumeSettings.Default < SoundVolumeSettings.Default,
                    "the theme starts under the sound of the table");
                report.Equal(SoundVolumeSettings.Steps.Length, MusicVolumeSettings.Steps.Length,
                    "the theme steps on the ladder the table uses");
                report.Equal(0d, MusicVolumeSettings.Steps[0], "the first step of the music is silence");
                report.Equal(1d, MusicVolumeSettings.Steps[^1], "the last step is full volume");

                report.Equal(Loc.Get("sound.off"), MusicVolumeSettings.Text(0d), "silence prints as off");
                report.Equal("50%", MusicVolumeSettings.Text(MusicVolumeSettings.Default),
                    "the default prints as 50%");
                var volume = MusicVolumeSettings.Step(MusicVolumeSettings.Default, 1);
                report.Equal(0.75d, volume, "one step up from the default is three quarters");
                volume = MusicVolumeSettings.Step(MusicVolumeSettings.Step(volume, 1), 1);
                report.Equal(1d, volume, "the ladder holds at full volume");
                report.Check(MusicVolumeSettings.IsMuted(MusicVolumeSettings.Step(0.25d, -1)),
                    "a step down from a quarter is silence");
                report.Equal<double?>(0d, MusicVolumeSettings.TryParse("off"),
                    "the music can be turned off by name");
                report.Equal<double?>(0.5d, MusicVolumeSettings.TryParse("50%"), "and set by a percentage");
                report.Equal<double?>(null, MusicVolumeSettings.TryParse("громко"),
                    "nonsense is no music setting");

                // The welcome screen: the row is there, it shows the volume and it steps it.
                var settings = new GameSettings();
                var title = new TitleScreen(settings);
                title.OpenSettings();
                var row = title.AllRows().FirstOrDefault(candidate => candidate.Item == TitleItem.Music);
                report.Check(row != null, "the welcome screen offers the music of the table");
                report.Equal(Loc.Get("title.music"), row!.Label, "the row has a name of its own");
                report.Equal(MusicVolumeSettings.Text(settings.MusicVolume), row.Value,
                    "the row shows the volume the theme has");

                var steps = 0;
                while (title.Selected != TitleItem.Music && steps++ < TitleScreen.MaxRows) title.Move(1);
                report.Equal(TitleItem.Music, title.Selected, "the cursor can walk to the music row");
                report.Check(title.Adjust(-1), "the music row can be stepped");
                report.Check(settings.MusicVolume < MusicVolumeSettings.Default,
                    "stepping the row turns the theme down");
                while (title.Adjust(-1)) { }
                report.Equal(0d, settings.MusicVolume, "the row steps down to silence");
                report.Check(!title.Adjust(-1), "the row holds at silence");

                // The menu: the same setting, the same ladder, its own row.
                var menuSettings = new GameSettings();
                var menu = new GameMenu(menuSettings);
                var rows = menu.AllRows();
                var musicRow = -1;
                for (var index = 0; index < rows.Count; index++)
                    if (rows[index].Item == MenuItem.Music) musicRow = index;
                report.Check(musicRow >= 0, "the in-game menu offers the music of the table");
                report.Equal(Loc.Get("menu.music"), rows[musicRow].Label, "the menu row has a name of its own");
                menu.Move(musicRow);
                report.Equal(MenuItem.Music, menu.Selected, "the cursor stands on the music row");
                report.Equal(MenuCommand.None, menu.Activate(), "picking the music row opens nothing");
                report.Equal(0.75d, menuSettings.MusicVolume, "picking the music row steps it up");
                while (menu.Adjust(-1)) { }
                report.Equal(0d, menuSettings.MusicVolume, "the menu steps the theme down to silence");
                report.Check(!menu.Adjust(-1), "the menu music row holds at silence");

                // MAHJONG_MUSIC goes through one parse, like every other table setting.
                var previousMusic = Environment.GetEnvironmentVariable("MAHJONG_MUSIC");
                try
                {
                    Environment.SetEnvironmentVariable("MAHJONG_MUSIC", "off");
                    report.Equal(0d, GameSettings.FromEnvironment().MusicVolume,
                        "MAHJONG_MUSIC=off silences a capture run");
                    Environment.SetEnvironmentVariable("MAHJONG_MUSIC", "25%");
                    report.Equal(0.25d, GameSettings.FromEnvironment().MusicVolume,
                        "MAHJONG_MUSIC=25% is a quarter of the theme");
                    Environment.SetEnvironmentVariable("MAHJONG_MUSIC", "какая-то музыка");
                    report.Equal(MusicVolumeSettings.Default, GameSettings.FromEnvironment().MusicVolume,
                        "a broken MAHJONG_MUSIC keeps the default");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("MAHJONG_MUSIC", previousMusic);
                }
            }
            finally
            {
                Loc.Current = previous;
            }
        }
    }
}
