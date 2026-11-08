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
        /// <summary>The seven rows a player sees on launch: play, the table, the settings page and the door.</summary>
        Main,

        /// <summary>The settings page: the game, the sound and the interface, one section each.</summary>
        Settings,

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
        Variant = 5,
        Language = 6,
        Level = 7,
        Speed = 8,
        Result = 9,
        Rules = 10,
        Quit = 11,
        Multiplayer = 12,
        SaveSlot = 13,
        Back = 14,

        /// <summary>У-1: how long the player gets for each decision of a hand.</summary>
        Clock = 15,

        /// <summary>8.7: how long the whole session runs (3.5.6).</summary>
        Session = 16,

        /// <summary>8.7: which deal a new game starts from.</summary>
        Seed = 17,

        /// <summary>4.10: how loud the table is, from silence to full volume.</summary>
        Sound = 18,

        /// <summary>Which palette the interface is painted with (headless: <see cref="UiThemes"/>).</summary>
        Look = 19,

        /// <summary>How big the interface is drawn, one step of <see cref="UiThemeSettings.Scales"/>.</summary>
        Scale = 20,

        /// <summary>4.10: how loud the music under the table is.</summary>
        Music = 21,

        /// <summary>Composition of the interface, independent from its palette.</summary>
        Profile = 22,

        /// <summary>Which typeface the interface is drawn with.</summary>
        Font = 23,

        /// <summary>How big the text is drawn, apart from the size of the plates.</summary>
        FontSize = 24,

        /// <summary>The row that opens the settings page.</summary>
        Settings = 25,

        /// <summary>Section titles of the settings page; the cursor steps over them.</summary>
        SectionGame = 26,
        SectionSound = 27,
        SectionInterface = 28
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
        Unavailable = 1,

        /// <summary>A section title: it is read, not picked, and the cursor steps over it.</summary>
        Header = 2
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
        Multiplayer,
        Quit,
        Back
    }

    /// <summary>One row of the welcome screen: caption, the value it shows and what picking it means.</summary>
    public sealed record TitleRow(TitleItem Item, string Label, string Value, TitleCommand Command,
        TitleRowState State)
    {
        public bool Ready => State == TitleRowState.Ready;

        /// <summary>A section title is drawn as a caption over the rows that belong to it.</summary>
        public bool IsHeader => State == TitleRowState.Header;

        public override string ToString() => Value.Length == 0 ? Label : $"{Label}: {Value}";
    }

    /// <summary>
    /// The welcome screen as a model: three pages, a cursor on each of them and what a step and a pick
    /// mean. The main page is the short one a player reads on launch (task-1.md, раздел 7): play, the
    /// rules profile, the settings page and the door. Everything the player tunes lives one page
    /// deeper, in the three sections of the settings page — the game, the sound and the interface.
    ///
    /// Knows nothing about Unity, so the whole screen is checked headless; the panel only draws
    /// <see cref="RowAt"/> and reports the row a click landed on.
    /// </summary>
    public sealed class TitleScreen
    {
        /// <summary>
        /// Rows the panel draws. The settings page is the longest one: three section titles, fourteen
        /// settings and the way back. Keep this in sync with the arrays below: every visible row needs
        /// a collider, or a lower setting would answer the arrow keys and nothing else.
        /// </summary>
        public const int MaxRows = 18;

        /// <summary>Saved games a single page can list; more than that would not fit the frame.</summary>
        public const int MaxSaves = 10;

        /// <summary>
        /// The main page in the order a player reads it: how to start, what table it will be, and the
        /// two doors — the settings page and the exit. Two modes that are still being built stay out
        /// of the way and are only listed when <see cref="ShowUnfinishedModes"/> is asked for.
        /// </summary>
        private static readonly TitleItem[] MainRows =
        {
            TitleItem.NewGame, TitleItem.Continue, TitleItem.LoadGame,
            TitleItem.Variant, TitleItem.Settings, TitleItem.Rules, TitleItem.Quit
        };

        /// <summary>The main page with the two unfinished modes, for a build that wants to show them.</summary>
        private static readonly TitleItem[] MainRowsWithModes =
        {
            TitleItem.NewGame, TitleItem.Continue, TitleItem.LoadGame,
            TitleItem.LocalGame, TitleItem.Multiplayer,
            TitleItem.Variant, TitleItem.Settings, TitleItem.Rules, TitleItem.Quit
        };

        /// <summary>The settings page: three titled sections, then the way back.</summary>
        private static readonly TitleItem[] SettingsRows =
        {
            TitleItem.SectionGame,
            TitleItem.Level, TitleItem.Speed, TitleItem.Result, TitleItem.Clock, TitleItem.Session,
            TitleItem.Seed,
            TitleItem.SectionSound,
            TitleItem.Sound, TitleItem.Music,
            TitleItem.SectionInterface,
            TitleItem.Language, TitleItem.Look, TitleItem.Scale, TitleItem.Profile,
            TitleItem.Font, TitleItem.FontSize,
            TitleItem.Back
        };

        private readonly GameSettings settings;
        private readonly List<SaveSlot> saves;
        private int mainIndex;
        private int settingsIndex;
        private int loadIndex;

        public TitleScreen(GameSettings settings, IReadOnlyList<SaveSlot>? saves = null)
        {
            this.settings = settings ?? new GameSettings();
            this.saves = new List<SaveSlot>(saves ?? Array.Empty<SaveSlot>());
        }

        public GameSettings Settings => settings;

        /// <summary>
        /// True when the two modes that are not built yet are listed. Off by default: in a normal build
        /// a player sees no greyed row at all (task-1.md, M-1).
        /// </summary>
        public bool ShowUnfinishedModes { get; set; }

        public TitlePage Page { get; private set; } = TitlePage.Main;

        /// <summary>One line under the rows: why a row did nothing, or how many saves there are.</summary>
        public string Status { get; private set; } = string.Empty;

        /// <summary>Path of the save the last pick chose; empty until "continue" or a save row is picked.</summary>
        public string PendingSave { get; private set; } = string.Empty;

        /// <summary>The saves this screen offers, newest first.</summary>
        public IReadOnlyList<SaveSlot> Saves => saves;

        private TitleItem[] MainItems => ShowUnfinishedModes ? MainRowsWithModes : MainRows;

        /// <summary>Rows of the page that is open.</summary>
        public int RowCount => Page switch
        {
            TitlePage.Main => MainItems.Length,
            TitlePage.Settings => SettingsRows.Length,
            _ => Math.Min(saves.Count, MaxSaves) + 1
        };

        /// <summary>Row under the cursor; it wraps, so one key press always lands somewhere.</summary>
        public int Index => Page switch
        {
            TitlePage.Main => mainIndex,
            TitlePage.Settings => settingsIndex,
            _ => loadIndex
        };

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
            loadIndex = Math.Clamp(loadIndex, 0, Math.Max(0, Math.Min(saves.Count, MaxSaves)));
        }

        public void SetStatus(string status) => Status = status ?? string.Empty;

        /// <summary>
        /// Opens a page directly. The main page is where the screen starts; the settings page and the
        /// list of saves are reached by picking their rows, but a tool, a test and a keyboard shortcut
        /// may ask for one of them without walking the cursor there.
        /// </summary>
        public void OpenSettings()
        {
            Page = TitlePage.Settings;
            settingsIndex = 0;
            Status = string.Empty;
        }

        /// <summary>Moves the cursor by whole rows, wrapping at both ends and stepping over titles.</summary>
        public void Move(int delta)
        {
            if (delta == 0) return;

            if (Page == TitlePage.Load)
            {
                var count = RowCount;
                if (count <= 0) return;
                loadIndex = ((loadIndex + delta) % count + count) % count;
                return;
            }

            var rows = Page == TitlePage.Main ? MainItems : SettingsRows;
            var step = Math.Sign(delta);
            var index = Index;
            for (var move = 0; move < Math.Abs(delta); move++)
            {
                for (var guard = 0; guard < rows.Length; guard++)
                {
                    index = ((index + step) % rows.Length + rows.Length) % rows.Length;
                    if (!RowOf(rows[index]).IsHeader) break;
                }
            }

            SetIndex(index);
        }

        /// <summary>The row as a record, without going through the page the cursor is on.</summary>
        private TitleRow RowOf(TitleItem item) => BuildRow(item);

        private void SetIndex(int index)
        {
            if (Page == TitlePage.Main) mainIndex = index;
            else if (Page == TitlePage.Settings) settingsIndex = index;
            else loadIndex = index;
        }

        /// <summary>
        /// Steps the value of the selected row. On the load page there is nothing to step; a section
        /// title has no value either, and says so.
        /// </summary>
        public bool Adjust(int delta)
        {
            if (delta == 0 || Page == TitlePage.Load) return false;

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
                case TitleItem.Font:
                    var font = UiFontSettings.StepFamily(settings.Font, delta);
                    if (font == settings.Font) return false;
                    settings.Font = font;
                    return true;
                case TitleItem.FontSize:
                    var fontSize = UiFontSettings.StepSize(settings.FontSize, delta);
                    if (Math.Abs(fontSize - settings.FontSize) < 0.0001d) return false;
                    settings.FontSize = fontSize;
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
        /// planned row explains itself and returns nothing. The "settings" row opens its page here, so
        /// the caller does not have to know the pages of the screen.
        /// </summary>
        public TitleCommand Activate()
        {
            var row = RowAt(Index);
            Status = string.Empty;

            if (Page == TitlePage.Load) return ActivateLoad(row);

            if (row.IsHeader) return TitleCommand.None;

            if (!row.Ready)
            {
                Status = row.Item == TitleItem.SaveSlot
                    ? Loc.Get("title.save.broken")
                    : Loc.Format("title.planned", row.Label);
                return TitleCommand.None;
            }

            switch (row.Item)
            {
                case TitleItem.Settings:
                    OpenSettings();
                    return TitleCommand.None;

                case TitleItem.Back:
                    Back();
                    return TitleCommand.Back;

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
                case TitleItem.Multiplayer:
                    return TitleCommand.Multiplayer;
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
                Back();
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

        /// <summary>
        /// Goes one page back: the list of saves and the settings page return to the main page, and the
        /// cursor is put on the row that opened them. False on the main page, where there is nothing
        /// to go back to.
        /// </summary>
        public bool Back()
        {
            var page = Page;
            if (page == TitlePage.Main) return false;

            Page = TitlePage.Main;
            Status = string.Empty;
            var target = page == TitlePage.Load ? TitleItem.LoadGame : TitleItem.Settings;
            for (var index = 0; index < MainItems.Length; index++)
                if (MainItems[index] == target) mainIndex = index;
            return true;
        }

        public TitleRow RowAt(int index)
        {
            if (Page == TitlePage.Load) return LoadRowAt(index);

            var rows = Page == TitlePage.Main ? MainItems : SettingsRows;
            var item = rows[((index % rows.Length) + rows.Length) % rows.Length];
            return BuildRow(item);
        }

        private TitleRow BuildRow(TitleItem item) => item switch
        {
            TitleItem.NewGame => new TitleRow(item, Loc.Get("title.new"), string.Empty,
                TitleCommand.NewGame, TitleRowState.Ready),
            TitleItem.Continue => new TitleRow(item, Loc.Get("title.continue"), ContinueValue(),
                TitleCommand.Continue, TitleRowState.Ready),
            TitleItem.LoadGame => new TitleRow(item, Loc.Get("title.load"), LoadValue(),
                TitleCommand.OpenLoad, TitleRowState.Ready),

            // Two modes that are planned and not built: they stand greyed out with a reason. They are
            // not part of the main page unless a build asks for them.
            TitleItem.LocalGame => new TitleRow(item, Loc.Get("title.local"), Loc.Get("title.value.planned"),
                TitleCommand.None, TitleRowState.Unavailable),
            TitleItem.Settings => new TitleRow(item, Loc.Get("title.settings"), SettingsValue(),
                TitleCommand.None, TitleRowState.Ready),
            TitleItem.Variant => new TitleRow(item, Loc.Get("title.variant"), Text.VariantName(settings.Variant),
                TitleCommand.None, TitleRowState.Ready),

            TitleItem.SectionGame => Header(item, Loc.Get("title.section.game")),
            TitleItem.SectionSound => Header(item, Loc.Get("title.section.sound")),
            TitleItem.SectionInterface => Header(item, Loc.Get("title.section.interface")),

            TitleItem.Back => new TitleRow(item, Loc.Get("title.back"), string.Empty,
                TitleCommand.Back, TitleRowState.Ready),

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
            TitleItem.Font => new TitleRow(item, Loc.Get("title.font"), UiFontSettings.FamilyName(settings.Font),
                TitleCommand.None, TitleRowState.Ready),
            TitleItem.FontSize => new TitleRow(item, Loc.Get("title.fontSize"),
                UiFontSettings.SizeText(settings.FontSize), TitleCommand.None, TitleRowState.Ready),
            TitleItem.Scale => new TitleRow(item, Loc.Get("title.scale"),
                UiThemeSettings.ScaleText(settings.UiScale), TitleCommand.None, TitleRowState.Ready),
            TitleItem.Rules => new TitleRow(item, Loc.Get("title.rules"), string.Empty,
                TitleCommand.Rules, TitleRowState.Ready),
            TitleItem.Multiplayer => new TitleRow(item, Loc.Get("title.multiplayer"),
                Loc.Get("title.value.planned"), TitleCommand.Multiplayer, TitleRowState.Ready),
            _ => new TitleRow(item, Loc.Get("title.quit"), string.Empty,
                TitleCommand.Quit, TitleRowState.Ready)
        };

        private static TitleRow Header(TitleItem item, string label) =>
            new(item, label, string.Empty, TitleCommand.None, TitleRowState.Header);

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

        /// <summary>
        /// Puts the cursor on a row, whichever page it lives on, and opens that page. A tool, a test and
        /// a keyboard shortcut use it instead of counting rows; the player reaches the same place with
        /// the arrows.
        /// </summary>
        public bool Select(TitleItem item)
        {
            for (var index = 0; index < MainItems.Length; index++)
                if (MainItems[index] == item)
                {
                    Page = TitlePage.Main;
                    mainIndex = index;
                    return true;
                }

            for (var index = 0; index < SettingsRows.Length; index++)
                if (SettingsRows[index] == item)
                {
                    Page = TitlePage.Settings;
                    settingsIndex = index;
                    return true;
                }

            return false;
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

        /// <summary>The settings row says what is behind it: the three sections, one word each.</summary>
        private static string SettingsValue() => Loc.Get("title.settings.value");

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
