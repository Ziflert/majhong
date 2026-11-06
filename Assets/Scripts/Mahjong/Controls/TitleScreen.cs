using System;
using System.Collections.Generic;
using Mahjong.Bots;
using Mahjong.Localization;
using Mahjong.Replay;
using Mahjong.Rules;

namespace Mahjong.Controls
{
    /// <summary>Which page of the welcome screen is on the screen.</summary>
    public enum TitlePage
    {
        /// <summary>The rows a player sees on launch: start, continue, load, settings.</summary>
        Main,

        /// <summary>The list of saved games, opened by the "load game" row.</summary>
        Load
    }

    /// <summary>The rows of the welcome screen; a row is what it is, not what it does.</summary>
    public enum TitleItem
    {
        NewGame = 0,
        Continue = 1,
        LoadGame = 2,
        LocalGame = 3,
        Multiplayer = 4,
        Variant = 5,
        Language = 6,
        Level = 7,
        Speed = 8,
        Result = 9,
        Rules = 10,
        Quit = 11,
        SaveSlot = 12,
        Back = 13,

        /// <summary>У-1: how long the player gets for each decision of a hand.</summary>
        Clock = 14,

        /// <summary>8.7: how long the whole session runs (3.5.6).</summary>
        Session = 15,

        /// <summary>8.7: which deal a new game starts from.</summary>
        Seed = 16,

        /// <summary>4.10: how loud the table is, from silence to full volume.</summary>
        Sound = 17,

        /// <summary>Which palette the interface is painted with (headless: <see cref="UiThemes"/>).</summary>
        Look = 18,

        /// <summary>How big the interface is drawn, one step of <see cref="UiThemeSettings.Scales"/>.</summary>
        Scale = 19,

        /// <summary>4.10: how loud the music under the table is.</summary>
        Music = 20,

        /// <summary>Composition of the interface, independent from its palette.</summary>
        Profile = 21
    }

    /// <summary>
    /// Whether picking a row does anything. A feature that is planned but not built stands on the
    /// screen as <see cref="Unavailable"/> and says so when picked: the screen never pretends to have
    /// a mode the engine cannot play (docs/journal.md, 2026-11-05). A save that does not parse stands
    /// the same way, with the reason in the status line.
    /// </summary>
    public enum TitleRowState
    {
        Ready = 0,
        Unavailable = 1
    }

    /// <summary>What picking a row asks the game to do.</summary>
    public enum TitleCommand
    {
        /// <summary>Nothing to do: a setting steps its own value, or the row is not built yet.</summary>
        None,
        NewGame,
        Continue,
        OpenLoad,
        LoadSave,
        Rules,
        Quit,
        Back
    }

    /// <summary>One row of the welcome screen: caption, the value it shows and what picking it means.</summary>
    public sealed record TitleRow(TitleItem Item, string Label, string Value, TitleCommand Command,
        TitleRowState State)
    {
        public bool Ready => State == TitleRowState.Ready;

        public override string ToString() => Value.Length == 0 ? Label : $"{Label}: {Value}";
    }

    /// <summary>
    /// The welcome screen as a model: the rows, the cursor, the two pages and what a step and a pick
    /// mean. Everything a player can decide before the first hand — the rules profile, the language,
    /// the bots, the result screen — stands here as a row, next to the ways into a game.
    ///
    /// Knows nothing about Unity, so the whole screen is checked headless; the panel only draws
    /// <see cref="RowAt"/> and reports the row a click landed on.
    /// </summary>
    public sealed class TitleScreen
    {
        /// <summary>
        /// Rows the panel draws; the main page uses them all, the load page never has more than
        /// <see cref="MaxSaves"/> saves plus "back".
        /// </summary>
        // Keep this in sync with MainRows: every visible row must have a collider. If the panel has
        // fewer strips than the model, the lower settings appear to change selection but cannot be
        // clicked, which is especially confusing on the welcome screen.
        public const int MaxRows = 20;

        /// <summary>Saved games a single page can list; more than that would not fit the frame.</summary>
        public const int MaxSaves = 10;

        /// <summary>The main page in the order a player reads it: play, then the table, then the door.</summary>
        private static readonly TitleItem[] MainRows =
        {
            TitleItem.NewGame, TitleItem.Continue, TitleItem.LoadGame,
            TitleItem.LocalGame, TitleItem.Multiplayer,
            TitleItem.Variant, TitleItem.Language, TitleItem.Level, TitleItem.Speed, TitleItem.Result,
            TitleItem.Clock, TitleItem.Session, TitleItem.Seed, TitleItem.Sound, TitleItem.Music,
            TitleItem.Look, TitleItem.Scale, TitleItem.Profile,
            TitleItem.Rules, TitleItem.Quit
        };

        private readonly GameSettings settings;
        private readonly List<SaveSlot> saves;
        private int mainIndex;
        private int loadIndex;

        public TitleScreen(GameSettings settings, IReadOnlyList<SaveSlot>? saves = null)
        {
            this.settings = settings ?? new GameSettings();
            this.saves = new List<SaveSlot>(saves ?? Array.Empty<SaveSlot>());
        }

        public GameSettings Settings => settings;

        public TitlePage Page { get; private set; } = TitlePage.Main;

        /// <summary>One line under the rows: why a row did nothing, or how many saves there are.</summary>
        public string Status { get; private set; } = string.Empty;

        /// <summary>Path of the save the last pick chose; empty until "continue" or a save row is picked.</summary>
        public string PendingSave { get; private set; } = string.Empty;

        /// <summary>The saves this screen offers, newest first.</summary>
        public IReadOnlyList<SaveSlot> Saves => saves;

        /// <summary>Rows of the page that is open.</summary>
        public int RowCount => Page == TitlePage.Main ? MainRows.Length : Math.Min(saves.Count, MaxSaves) + 1;

        /// <summary>Row under the cursor; it wraps, so one key press always lands somewhere.</summary>
        public int Index => Page == TitlePage.Main ? mainIndex : loadIndex;

        public TitleItem Selected => RowAt(Index).Item;

        /// <summary>The newest save that can be opened, or an empty path when there is none.</summary>
        public string NewestSave => FirstOpenable?.Path ?? string.Empty;

        /// <summary>True when "continue" has something to continue.</summary>
        public bool CanContinue => NewestSave.Length > 0;

        private SaveSlot? FirstOpenable
        {
            get
            {
                foreach (var slot in saves)
                    if (slot.IsOpenable) return slot;
                return null;
            }
        }

        /// <summary>Replaces the save list after a scan, e.g. when the screen opens or a game is saved.</summary>
        public void SetSaves(IReadOnlyList<SaveSlot>? slots)
        {
            saves.Clear();
            if (slots != null) saves.AddRange(slots);
            loadIndex = Math.Clamp(loadIndex, 0, RowCount - 1);
        }

        public void SetStatus(string status) => Status = status ?? string.Empty;

        /// <summary>Moves the cursor by whole rows, wrapping at both ends.</summary>
        public void Move(int delta)
        {
            var count = RowCount;
            if (count <= 0) return;
            var next = ((Index + delta) % count + count) % count;
            if (Page == TitlePage.Main) mainIndex = next;
            else loadIndex = next;
        }

        /// <summary>
        /// Steps the value of the selected row. On the load page there is nothing to step; on the main
        /// page a row that is not a setting does nothing and says so.
        /// </summary>
        public bool Adjust(int delta)
        {
            if (delta == 0 || Page != TitlePage.Main) return false;

            switch (Selected)
            {
                case TitleItem.Variant:
                    var next = StepVariant(settings.Variant, delta);
                    if (next.Id == settings.Variant.Id) return false;
                    settings.Variant = next;
                    return true;
                case TitleItem.Language:
                    settings.Language = settings.Language == Language.Ru ? Language.En : Language.Ru;
                    return true;
                case TitleItem.Level:
                    var level = (BotLevel)Math.Clamp((int)settings.Level + Math.Sign(delta),
                        (int)BotLevel.Fast, (int)BotLevel.Strategic);
                    if (level == settings.Level) return false;
                    settings.Level = level;
                    return true;
                case TitleItem.Speed:
                    return delta > 0 ? settings.Faster() : settings.Slower();
                case TitleItem.Result:
                    var advance = ResultAdvanceSettings.Step(settings.Result, delta);
                    if (advance == settings.Result) return false;
                    settings.Result = advance;
                    return true;
                case TitleItem.Clock:
                    var pace = ClockPaceSettings.Step(settings.ClockPace, delta);
                    if (pace == settings.ClockPace) return false;
                    settings.ClockPace = pace;
                    return true;
                case TitleItem.Session:
                    var limit = SessionLengthSettings.Step(settings.SessionLimit, delta);
                    if (limit == settings.SessionLimit) return false;
                    settings.SessionLimit = limit;
                    return true;
                case TitleItem.Seed:
                    var seed = DealSeedSettings.Step(settings.Seed, delta, settings.LastSeed);
                    if (seed == settings.Seed) return false;
                    settings.Seed = seed;
                    return true;
                case TitleItem.Sound:
                    var volume = SoundVolumeSettings.Step(settings.SoundVolume, delta);
                    if (volume == settings.SoundVolume) return false;
                    settings.SoundVolume = volume;
                    return true;
                case TitleItem.Music:
                    var music = MusicVolumeSettings.Step(settings.MusicVolume, delta);
                    if (music == settings.MusicVolume) return false;
                    settings.MusicVolume = music;
                    return true;
                case TitleItem.Profile:
                    var profile = UiProfiles.Step(settings.Profile, delta);
                    if (profile.Id == settings.Profile.Id) return false;
                    settings.Profile = profile;
                    return true;
                case TitleItem.Look:
                    var theme = UiThemeSettings.Step(settings.Theme, delta);
                    if (theme.Id == settings.Theme.Id) return false;
                    settings.Theme = theme;
                    return true;
                case TitleItem.Scale:
                    var scale = UiThemeSettings.StepScale(settings.UiScale, delta);
                    if (Math.Abs(scale - settings.UiScale) < 0.0001d) return false;
                    settings.UiScale = scale;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Picks the row under the cursor. A setting steps itself, a built row returns its command, a
        /// planned row explains itself and returns nothing.
        /// </summary>
        public TitleCommand Activate()
        {
            var row = RowAt(Index);
            Status = string.Empty;

            if (Page == TitlePage.Load) return ActivateLoad(row);

            if (!row.Ready)
            {
                Status = row.Item == TitleItem.SaveSlot
                    ? Loc.Get("title.save.broken")
                    : Loc.Format("title.planned", row.Label);
                return TitleCommand.None;
            }

            switch (row.Item)
            {
                case TitleItem.Continue:
                    PendingSave = NewestSave;
                    if (PendingSave.Length == 0)
                    {
                        Status = Loc.Get("title.noSave");
                        return TitleCommand.None;
                    }
                    return TitleCommand.Continue;

                case TitleItem.LoadGame:
                    if (saves.Count == 0)
                    {
                        Status = Loc.Get("title.noSave");
                        return TitleCommand.None;
                    }
                    Page = TitlePage.Load;
                    loadIndex = 0;
                    return TitleCommand.OpenLoad;

                case TitleItem.NewGame:
                    return TitleCommand.NewGame;
                case TitleItem.Rules:
                    return TitleCommand.Rules;
                case TitleItem.Quit:
                    return TitleCommand.Quit;
                default:
                    // A setting: picking it steps it, which is what a click on a slider should do.
                    Adjust(1);
                    return TitleCommand.None;
            }
        }

        private TitleCommand ActivateLoad(TitleRow row)
        {
            if (row.Item == TitleItem.Back)
            {
                Page = TitlePage.Main;
                return TitleCommand.Back;
            }

            if (!row.Ready || Index >= saves.Count)
            {
                Status = Loc.Get("title.save.broken");
                return TitleCommand.None;
            }

            PendingSave = saves[Index].Path;
            return TitleCommand.LoadSave;
        }

        /// <summary>Leaves the load page; false on the main page, where there is nothing to go back to.</summary>
        public bool Back()
        {
            if (Page == TitlePage.Main) return false;
            Page = TitlePage.Main;
            Status = string.Empty;
            return true;
        }

        public TitleRow RowAt(int index)
        {
            if (Page == TitlePage.Load) return LoadRowAt(index);

            var item = MainRows[((index % MainRows.Length) + MainRows.Length) % MainRows.Length];
            return item switch
            {
                TitleItem.NewGame => new TitleRow(item, Loc.Get("title.new"), string.Empty,
                    TitleCommand.NewGame, TitleRowState.Ready),
                TitleItem.Continue => new TitleRow(item, Loc.Get("title.continue"), ContinueValue(),
                    TitleCommand.Continue, TitleRowState.Ready),
                TitleItem.LoadGame => new TitleRow(item, Loc.Get("title.load"), LoadValue(),
                    TitleCommand.OpenLoad, TitleRowState.Ready),
                // Two modes that are planned and not built: they stand greyed out with a reason.
                TitleItem.LocalGame => new TitleRow(item, Loc.Get("title.local"), Loc.Get("title.value.planned"),
                    TitleCommand.None, TitleRowState.Unavailable),
                TitleItem.Multiplayer => new TitleRow(item, Loc.Get("title.multiplayer"), Loc.Get("title.value.planned"),
                    TitleCommand.None, TitleRowState.Unavailable),
                TitleItem.Variant => new TitleRow(item, Loc.Get("title.variant"), Text.VariantName(settings.Variant),
                    TitleCommand.None, TitleRowState.Ready),
                TitleItem.Language => new TitleRow(item, Loc.Get("title.language"),
                    GameMenu.LanguageText(settings.Language), TitleCommand.None, TitleRowState.Ready),
                TitleItem.Level => new TitleRow(item, Loc.Get("title.level"),
                    GameMenu.LevelText(settings.Level), TitleCommand.None, TitleRowState.Ready),
                TitleItem.Speed => new TitleRow(item, Loc.Get("title.speed"), settings.SpeedText(),
                    TitleCommand.None, TitleRowState.Ready),
                TitleItem.Result => new TitleRow(item, Loc.Get("title.result"),
                    ResultAdvanceSettings.Text(settings.Result), TitleCommand.None, TitleRowState.Ready),
                TitleItem.Clock => new TitleRow(item, Loc.Get("title.clock"),
                    ClockPaceSettings.Text(settings.ClockPace), TitleCommand.None, TitleRowState.Ready),
                TitleItem.Session => new TitleRow(item, Loc.Get("title.session"),
                    SessionLengthSettings.Text(settings.SessionLimit), TitleCommand.None, TitleRowState.Ready),
                TitleItem.Seed => new TitleRow(item, Loc.Get("title.seed"),
                    DealSeedSettings.Text(settings.Seed), TitleCommand.None, TitleRowState.Ready),
                TitleItem.Sound => new TitleRow(item, Loc.Get("title.sound"),
                    SoundVolumeSettings.Text(settings.SoundVolume), TitleCommand.None, TitleRowState.Ready),
                TitleItem.Music => new TitleRow(item, Loc.Get("title.music"),
                    MusicVolumeSettings.Text(settings.MusicVolume), TitleCommand.None, TitleRowState.Ready),
                TitleItem.Look => new TitleRow(item, Loc.Get("title.look"), settings.Theme.Name,
                    TitleCommand.None, TitleRowState.Ready),
                TitleItem.Profile => new TitleRow(item, Loc.Get("title.profile"), settings.Profile.Name,
                    TitleCommand.None, TitleRowState.Ready),
                TitleItem.Scale => new TitleRow(item, Loc.Get("title.scale"),
                    UiThemeSettings.ScaleText(settings.UiScale), TitleCommand.None, TitleRowState.Ready),
                TitleItem.Rules => new TitleRow(item, Loc.Get("title.rules"), string.Empty,
                    TitleCommand.Rules, TitleRowState.Ready),
                _ => new TitleRow(item, Loc.Get("title.quit"), string.Empty,
                    TitleCommand.Quit, TitleRowState.Ready)
            };
        }

        private TitleRow LoadRowAt(int index)
        {
            var listed = Math.Min(saves.Count, MaxSaves);
            if (index >= listed || index < 0)
                return new TitleRow(TitleItem.Back, Loc.Get("title.back"), string.Empty, TitleCommand.Back,
                    TitleRowState.Ready);

            var slot = saves[index];
            return new TitleRow(TitleItem.SaveSlot, slot.Name, slot.Summary(), TitleCommand.LoadSave,
                slot.IsOpenable ? TitleRowState.Ready : TitleRowState.Unavailable);
        }

        public IReadOnlyList<TitleRow> AllRows()
        {
            var rows = new List<TitleRow>(RowCount);
            for (var index = 0; index < RowCount; index++) rows.Add(RowAt(index));
            return rows;
        }

        /// <summary>What "continue" says: the newest save or a plain "there is nothing saved yet".</summary>
        private string ContinueValue()
        {
            var slot = FirstOpenable;
            return slot == null ? Loc.Get("title.value.none") : slot.Summary();
        }

        /// <summary>How many games are saved; the row that lists them is the only place to look.</summary>
        private string LoadValue() => saves.Count == 0
            ? Loc.Get("title.value.none")
            : Loc.Format("title.value.count", saves.Count);

        /// <summary>Next preset in the order of <see cref="RuleVariant.Presets"/>; it stops at both ends.</summary>
        public static RuleVariant StepVariant(RuleVariant current, int delta)
        {
            var presets = RuleVariant.Presets;
            var index = 0;
            for (var i = 0; i < presets.Count; i++)
                if (presets[i].Id == current.Id) index = i;

            var next = Math.Clamp(index + Math.Sign(delta), 0, presets.Count - 1);
            return presets[next];
        }
    }
}
