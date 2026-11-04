using System;
using System.Collections.Generic;
using Mahjong.Bots;
using Mahjong.Localization;

namespace Mahjong.Controls
{
    /// <summary>What activating a menu row asks the game to do.</summary>
    public enum MenuCommand
    {
        /// <summary>Nothing to do: the row is a setting that only steps its own value.</summary>
        None,
        Save,
        Resume,
        Review,
        Rules,
        Close
    }

    /// <summary>One row of the menu: what it is called, what it is set to, and what picking it does.</summary>
    public sealed record MenuRow(MenuItem Item, string Label, string Value, MenuCommand Command, bool Adjustable)
    {
        public override string ToString() => Value.Length == 0 ? Label : $"{Label}: {Value}";
    }

    public enum MenuItem
    {
        Language = 0,
        Speed = 1,
        Level = 2,
        Result = 3,
        Save = 4,
        Resume = 5,
        Review = 6,
        Rules = 7,
        Close = 8
    }

    /// <summary>
    /// The in-game menu as a model: a list of rows, a cursor and the two things a player can do — step
    /// a setting and pick a row. The panel only draws what this returns, so the behaviour (what "left"
    /// does on the speed row, what Enter does on the save row) is checked headless.
    /// </summary>
    public sealed class GameMenu
    {
        private readonly GameSettings settings;

        public GameMenu(GameSettings settings) => this.settings = settings;

        public int RowCount => Rows.Length;

        /// <summary>How many rows the menu has; the panel needs it before it has a settings object.</summary>
        public static int TotalRows => Rows.Length;

        private static readonly MenuItem[] Rows =
        {
            MenuItem.Language, MenuItem.Speed, MenuItem.Level, MenuItem.Result,
            MenuItem.Save, MenuItem.Resume, MenuItem.Review, MenuItem.Rules, MenuItem.Close
        };

        /// <summary>Row under the cursor; it wraps around, so one key press always lands somewhere.</summary>
        public int Index { get; private set; }

        public MenuItem Selected => Rows[Index];

        /// <summary>Moves the cursor by whole rows, wrapping at both ends.</summary>
        public void Move(int delta)
        {
            if (Rows.Length == 0) return;
            Index = ((Index + delta) % Rows.Length + Rows.Length) % Rows.Length;
        }

        /// <summary>
        /// Steps the value of the selected row: the language flips, the speed and the level go one
        /// notch. A row that is not a setting does nothing and says so.
        /// </summary>
        public bool Adjust(int delta)
        {
            switch (Selected)
            {
                case MenuItem.Language:
                    if (delta == 0) return false;
                    settings.Language = settings.Language == Language.Ru ? Language.En : Language.Ru;
                    return true;
                case MenuItem.Speed:
                    return delta > 0 ? settings.Faster() : delta < 0 && settings.Slower();
                case MenuItem.Level:
                    if (delta == 0) return false;
                    var next = (BotLevel)NextLevel(settings.Level, delta);
                    if (next == settings.Level) return false;
                    settings.Level = next;
                    return true;
                case MenuItem.Result:
                    if (delta == 0) return false;
                    var advance = ResultAdvanceSettings.Step(settings.Result, delta);
                    if (advance == settings.Result) return false;
                    settings.Result = advance;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Picks the selected row; the caller performs the command it gets back.</summary>
        public MenuCommand Activate()
        {
            var command = RowAt(Index).Command;
            // A setting row has nothing to open: picking it steps it, which is what a click should do.
            if (command == MenuCommand.None && ActivateSteps(Selected)) Adjust(1);
            return command;
        }

        public MenuRow RowAt(int index)
        {
            var item = Rows[((index % Rows.Length) + Rows.Length) % Rows.Length];
            return item switch
            {
                MenuItem.Language => new MenuRow(item, Loc.Get("menu.language"), LanguageText(settings.Language),
                    MenuCommand.None, true),
                MenuItem.Speed => new MenuRow(item, Loc.Get("menu.speed"), settings.SpeedText(),
                    MenuCommand.None, true),
                MenuItem.Level => new MenuRow(item, Loc.Get("menu.level"), LevelText(settings.Level),
                    MenuCommand.None, true),
                MenuItem.Result => new MenuRow(item, Loc.Get("menu.result"),
                    ResultAdvanceSettings.Text(settings.Result), MenuCommand.None, true),
                MenuItem.Save => new MenuRow(item, Loc.Get("menu.save"), string.Empty, MenuCommand.Save, false),
                MenuItem.Resume => new MenuRow(item, Loc.Get("menu.resume"), string.Empty, MenuCommand.Resume, false),
                MenuItem.Review => new MenuRow(item, Loc.Get("menu.review"), string.Empty, MenuCommand.Review, false),
                MenuItem.Rules => new MenuRow(item, Loc.Get("menu.rules"), string.Empty, MenuCommand.Rules, false),
                _ => new MenuRow(item, Loc.Get("menu.close"), string.Empty, MenuCommand.Close, false)
            };
        }

        public IReadOnlyList<MenuRow> AllRows()
        {
            var rows = new List<MenuRow>(RowCount);
            for (var index = 0; index < RowCount; index++) rows.Add(RowAt(index));
            return rows;
        }

        /// <summary>The name of the language a player reads, in the language he is reading now.</summary>
        public static string LanguageText(Language language) =>
            Loc.Get(language == Language.Ru ? "menu.value.ru" : "menu.value.en");

        /// <summary>How the three bot levels are called in the menu.</summary>
        public static string LevelText(BotLevel level) => Loc.Get(level switch
        {
            BotLevel.Fast => "menu.value.fast",
            BotLevel.Cautious => "menu.value.cautious",
            _ => "menu.value.strategic"
        });

        private static bool ActivateSteps(MenuItem item) =>
            item is MenuItem.Language or MenuItem.Speed or MenuItem.Level or MenuItem.Result;

        /// <summary>Next of the three levels, clamped: the menu has no fourth setting to step into.</summary>
        private static int NextLevel(BotLevel level, int delta)
        {
            var value = (int)level + Math.Sign(delta);
            return Math.Clamp(value, (int)BotLevel.Fast, (int)BotLevel.Strategic);
        }
    }
}
