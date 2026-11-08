using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Controls;
using Mahjong.Localization;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the look of the interface (8.10): the palettes, the size ladder and the two screens
    /// that step them.
    ///
    /// A palette is data — fourteen roles with a colour each — so it is checked without a screen: every
    /// role is painted, the body text stays readable on its own panels (WCAG 2.1 contrast, not taste),
    /// and the light theme really is light. The view only turns these colours into Unity colours, which
    /// is what the play-mode test has to watch.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void LookGroups(SelfTestReport report)
        {
            Run(report, "look: every palette paints all its roles and stays readable", () => LookPalettes(report));
            Run(report, "look: the size ladder steps and both ends hold", () => LookScaleRules(report));
            Run(report, "look: the two screens step the palette and the size", () => LookRows(report));
        }

        /// <summary>
        /// A palette is only usable if nothing is missing from it and if a player can read what it
        /// prints. The contrast ratios are the ones WCAG asks for: 4.5 for body text on a panel, 3 for
        /// large text and for a marker that is not text (a border, a refusal).
        /// </summary>
        private static void LookPalettes(SelfTestReport report)
        {
            var roles = (UiRole[])Enum.GetValues(typeof(UiRole));
            report.Check(UiThemes.All.Count >= 3, "the game ships more than one look");
            report.Equal(UiThemes.All.Count, UiThemes.All.Select(theme => theme.Id).Distinct().Count(),
                "every palette has its own id");
            report.Check(UiThemes.All.Contains(UiThemes.Default), "the default palette is one of the presets");
            report.Equal(UiThemeSettings.DefaultTheme.Id, UiThemes.Default.Id,
                "the look setting starts on the default palette");

            foreach (var theme in UiThemes.All)
            {
                foreach (var role in roles)
                    report.Check(theme.Paints(role), $"{theme.Id}: the palette paints {role}");

                // Nothing is left black by accident: a forgotten role draws pure black on screen.
                report.Check(roles.All(role => theme.Of(role) != UiColour.FromHex("#000000")),
                    $"{theme.Id}: no role falls back to black");

                // Body text on the panels it stands on, labels second, text on a filled row third.
                report.Check(UiColour.Contrast(theme.Of(UiRole.TextMain), theme.Of(UiRole.Surface)) >= 7d,
                    $"{theme.Id}: the body text is readable on a panel");
                report.Check(UiColour.Contrast(theme.Of(UiRole.TextMain), theme.Of(UiRole.SurfaceHigh)) >= 7d,
                    $"{theme.Id}: the body text is readable on a row");
                report.Check(UiColour.Contrast(theme.Of(UiRole.TextMuted), theme.Of(UiRole.Surface)) >= 4d,
                    $"{theme.Id}: a label is still readable on a panel");
                report.Check(UiColour.Contrast(theme.Of(UiRole.TextOnAccent), theme.Of(UiRole.Primary)) >= 4d,
                    $"{theme.Id}: the text on a chosen row is readable");
                report.Check(UiColour.Contrast(theme.Of(UiRole.Critical), theme.Of(UiRole.Surface)) >= 2.5d,
                    $"{theme.Id}: a refusal stands out from a panel");
                report.Check(UiColour.Contrast(theme.Of(UiRole.Border), theme.Of(UiRole.Surface)) >= 1.15d,
                    $"{theme.Id}: the hairline is visible on a panel");
                report.Check(UiColour.Contrast(theme.Of(UiRole.Accent), theme.Of(UiRole.Surface)) >= 1.3d,
                    $"{theme.Id}: the gold line is visible on a panel");

                // A tile keeps its ivory face in every palette: the faces are painted by the atlas, and
                // a dark tile face would make the whole table unreadable.
                var face = theme.Of(UiRole.TileFace);
                report.Check(UiColour.Contrast(face, theme.Of(UiRole.TileBody)) >= 1.05d,
                    $"{theme.Id}: the face of a tile differs from its body");
                report.Check(face.Luminance() > theme.Of(UiRole.TextMain).Luminance() * 0.5d,
                    $"{theme.Id}: the face of a tile stays pale");

                // The name of a dark theme is darker than its text, a light one the other way round.
                var background = theme.Of(UiRole.Background).Luminance();
                var text = theme.Of(UiRole.TextMain).Luminance();
                report.Equal(theme.IsDark, background < text, $"{theme.Id}: the theme knows whether it is dark");
                report.Check(theme.NameRu.Length > 0 && theme.NameEn.Length > 0,
                    $"{theme.Id}: the palette is named in both languages");
            }

            // The same palette answers with a name in the language the player is reading now.
            var previous = Loc.Current;
            try
            {
                Loc.Current = Language.Ru;
                report.Equal(UiThemes.Night.NameRu, UiThemes.Night.Name, "a Russian player reads the Russian name");
                Loc.Current = Language.En;
                report.Equal(UiThemes.Night.NameEn, UiThemes.Night.Name, "an English player reads the English name");
            }
            finally
            {
                Loc.Current = previous;
            }

            report.Equal(UiThemes.Night, UiThemes.Find("night"), "a palette is found by its id");
            report.Equal(UiThemes.Ivory, UiThemes.Find(" Ivory "), "the id ignores case and spaces");
            report.Equal(UiThemes.Default, UiThemes.Find("нет такой"), "an unknown id is the default palette");
            report.Equal(UiThemes.Default, UiThemes.Find(null), "a missing id is the default palette");

            // Colour as text: the docs write palettes in hex, so hex is what the catalogue reads.
            report.Equal(new UiColour(0x2E, 0x8B, 0x70), UiColour.FromHex("#2E8B70"), "hex with a hash is a colour");
            report.Equal(new UiColour(0x2E, 0x8B, 0x70), UiColour.FromHex("2e8b70"), "hex without a hash is a colour");
            report.Equal("#2E8B70", new UiColour(0x2E, 0x8B, 0x70).ToHex(), "a colour prints as hex");
            report.Equal(new UiColour(0, 0, 0), UiColour.FromHex("не цвет"), "broken hex is black, not a crash");
            report.Equal(new UiColour(0, 0, 0), UiColour.FromHex("#12345"), "short hex is black");
            report.Equal(21d, Math.Round(UiColour.Contrast(UiColour.FromHex("#FFFFFF"), UiColour.FromHex("#000000"))),
                "white on black is the widest contrast there is");
            report.Equal(1d, UiColour.Contrast(UiColour.FromHex("#2E8B70"), UiColour.FromHex("#2E8B70")),
                "a colour against itself has no contrast");
        }

        /// <summary>
        /// The size of the interface is one number off a short ladder, the same on both screens. The
        /// layout numbers stay written for 100% and are multiplied by this, so a bigger interface is the
        /// same interface; the ladder therefore holds four steps and never a value in between.
        /// </summary>
        private static void LookScaleRules(SelfTestReport report)
        {
            report.Equal(4, UiThemeSettings.Scales.Length, "the size ladder has four steps");
            report.Equal(1d, UiThemeSettings.DefaultScale, "the layout numbers are written for 100 %");
            report.Check(UiThemeSettings.Scales.Contains(UiThemeSettings.DefaultScale),
                "the default size is one of the steps");
            report.Check(UiThemeSettings.Scales.SequenceEqual(UiThemeSettings.Scales.OrderBy(step => step)),
                "the ladder runs from small to large");

            // Stepping: one notch at a time, both ends hold, a step of zero changes nothing.
            report.Equal(1.15d, UiThemeSettings.StepScale(1d, 1), "one step up from 100 % is 115 %");
            report.Equal(0.85d, UiThemeSettings.StepScale(1d, -1), "one step down from 100 % is 85 %");
            var largest = UiThemeSettings.Scales[^1];
            report.Equal(largest, UiThemeSettings.StepScale(largest, 1), "the top of the ladder holds");
            var smallest = UiThemeSettings.Scales[0];
            report.Equal(smallest, UiThemeSettings.StepScale(smallest, -1), "the bottom of the ladder holds");
            report.Equal(1.15d, UiThemeSettings.StepScale(1.15d, 0), "a step of zero changes nothing");
            report.Equal(1.3d, UiThemeSettings.StepScale(1.27d, 0), "a value off the ladder snaps to the nearest step");
            report.Equal(1d, UiThemeSettings.StepScale(double.NaN, 0), "a broken size is the default size");

            report.Equal("100%", UiThemeSettings.ScaleText(1d), "the default size prints as 100%");
            report.Equal("85%", UiThemeSettings.ScaleText(0.85d), "the smallest step prints as 85%");
            report.Equal("130%", UiThemeSettings.ScaleText(5d), "a size past the ladder prints as the largest step");

            // A size from elsewhere: the environment of a capture run or a settings file later.
            report.Equal<double?>(1.15d, UiThemeSettings.TryParseScale("1.15"), "a fraction is a fraction");
            report.Equal<double?>(1.15d, UiThemeSettings.TryParseScale("115%"), "a percentage is a fraction");
            report.Equal<double?>(1.15d, UiThemeSettings.TryParseScale("115"), "a number above three is a percentage");
            report.Equal<double?>(1.3d, UiThemeSettings.TryParseScale("2"), "a size past the ladder snaps to its top");
            report.Equal<double?>(null, UiThemeSettings.TryParseScale("крупно"), "nonsense is no size");
            report.Equal<double?>(null, UiThemeSettings.TryParseScale(""), "an empty value is no size");
            report.Equal<double?>(null, UiThemeSettings.TryParseScale(null), "a missing value is no size");

            // A palette from elsewhere: its id or either of its names.
            report.Equal(UiThemes.Ivory, UiThemeSettings.TryParseTheme("ivory"), "a palette is asked for by id");
            report.Equal(UiThemes.Ivory, UiThemeSettings.TryParseTheme("Слоновая кость"), "or by its Russian name");
            report.Equal(UiThemes.Dawn, UiThemeSettings.TryParseTheme("amber dawn"), "or by its English name");
            report.Equal<UiTheme?>(null, UiThemeSettings.TryParseTheme("нефрит"), "an unknown name is no palette");
            report.Equal<UiTheme?>(null, UiThemeSettings.TryParseTheme(null), "a missing name is no palette");

            // Both settings reach the table from the environment, like every other table setting.
            var previousTheme = Environment.GetEnvironmentVariable("MAHJONG_THEME");
            var previousScale = Environment.GetEnvironmentVariable("MAHJONG_UI_SCALE");
            try
            {
                Environment.SetEnvironmentVariable("MAHJONG_THEME", "ivory");
                Environment.SetEnvironmentVariable("MAHJONG_UI_SCALE", "115%");
                var settings = GameSettings.FromEnvironment();
                report.Equal("ivory", settings.Theme.Id, "MAHJONG_THEME=ivory paints a capture run light");
                report.Equal(1.15d, settings.UiScale, "MAHJONG_UI_SCALE=115% draws the interface bigger");

                Environment.SetEnvironmentVariable("MAHJONG_THEME", "какая-то тема");
                Environment.SetEnvironmentVariable("MAHJONG_UI_SCALE", "огромный");
                var broken = GameSettings.FromEnvironment();
                report.Equal(UiThemes.Default.Id, broken.Theme.Id, "a broken MAHJONG_THEME keeps the default palette");
                report.Equal(UiThemeSettings.DefaultScale, broken.UiScale, "a broken MAHJONG_UI_SCALE keeps the default size");
            }
            finally
            {
                Environment.SetEnvironmentVariable("MAHJONG_THEME", previousTheme);
                Environment.SetEnvironmentVariable("MAHJONG_UI_SCALE", previousScale);
            }
        }

        /// <summary>
        /// The palette and the size stand on both screens — the welcome screen and the menu under Esc —
        /// and both step the same settings object, so a look chosen before a game is the look of the game.
        /// </summary>
        private static void LookRows(SelfTestReport report)
        {
            var previous = Loc.Current;
            try
            {
                Loc.Current = Language.Ru;

                // The welcome screen.
                var settings = new GameSettings();
                var title = new TitleScreen(settings);
                title.OpenSettings();
                var look = title.AllRows().FirstOrDefault(row => row.Item == TitleItem.Look);
                var scale = title.AllRows().FirstOrDefault(row => row.Item == TitleItem.Scale);
                var steps = 0;
                report.Check(look != null, "the welcome screen offers the palette of the interface");
                report.Check(scale != null, "the welcome screen offers the size of the interface");
                report.Equal(Loc.Get("title.look"), look!.Label, "the row is called \"Тема оформления\"");
                report.Equal(UiThemes.Default.Name, look.Value, "the row shows the palette in effect");
                report.Equal(UiThemeSettings.ScaleText(UiThemeSettings.DefaultScale), scale!.Value,
                    "the size row shows the size in effect");

                while (title.Selected != TitleItem.Look && steps++ < TitleScreen.MaxRows) title.Move(1);
                report.Equal(TitleItem.Look, title.Selected, "the cursor stands on the palette row");
                report.Equal(TitleCommand.None, title.Activate(), "picking the palette row opens nothing");
                report.Check(settings.Theme.Id != UiThemes.Default.Id, "picking the row changes the palette");
                report.Equal(settings.Theme.Name, title.RowAt(title.Index).Value,
                    "and the row shows the palette it changed to");
                while (title.Adjust(1)) { }
                report.Equal(UiThemes.All[^1].Id, settings.Theme.Id, "the palette ladder ends at the last preset");
                report.Check(!title.Adjust(1), "there is nothing past the last palette");
                while (title.Adjust(-1)) { }
                report.Equal(UiThemes.All[0].Id, settings.Theme.Id, "and starts at the first");

                steps = 0;
                while (title.Selected != TitleItem.Scale && steps++ < TitleScreen.MaxRows) title.Move(1);
                report.Equal(TitleItem.Scale, title.Selected, "the size has its own row right under the palette");
                var before = settings.UiScale;
                report.Check(title.Adjust(1), "the size row can be stepped");
                report.Check(settings.UiScale > before, "one step up draws the interface bigger");
                while (title.Adjust(1)) { }
                report.Equal(UiThemeSettings.Scales[^1], settings.UiScale, "the size ladder ends at the largest step");
                report.Check(!title.Adjust(1), "there is nothing past the largest size");
                while (title.Adjust(-1)) { }
                report.Equal(UiThemeSettings.Scales[0], settings.UiScale, "and starts at the smallest");
                report.Check(!title.Adjust(-1), "the size row holds at the smallest step");

                // The menu under Esc: the same two settings, the same rows, the same object.
                var menuSettings = new GameSettings();
                var menu = new GameMenu(menuSettings);
                var rows = menu.AllRows();
                var lookRow = -1;
                var scaleRow = -1;
                for (var index = 0; index < rows.Count; index++)
                {
                    if (rows[index].Item == MenuItem.Look) lookRow = index;
                    if (rows[index].Item == MenuItem.Scale) scaleRow = index;
                }

                report.Check(lookRow >= 0, "the in-game menu offers the palette");
                report.Equal(Loc.Get("menu.look"), rows[lookRow].Label, "the menu row is called \"Тема\"");
                report.Equal(UiThemes.Default.Name, rows[lookRow].Value, "the menu row shows the palette in effect");
                report.Check(scaleRow == lookRow + 1, "the size follows the palette in the menu");
                report.Equal(UiThemeSettings.ScaleText(UiThemeSettings.DefaultScale), rows[scaleRow].Value,
                    "the menu row shows the size in effect");

                menu.Move(lookRow);
                report.Equal(MenuItem.Look, menu.Selected, "the cursor stands on the palette row of the menu");
                report.Equal(MenuCommand.None, menu.Activate(), "picking a look setting opens nothing");
                report.Equal(UiThemes.All[1].Id, menuSettings.Theme.Id, "picking the menu row changes the palette");
                report.Equal(menuSettings.Theme.Name, menu.RowAt(lookRow).Value,
                    "and the menu row shows the palette it changed to");
                report.Check(menu.Adjust(-1), "the menu row can be stepped back");
                report.Equal(UiThemes.All[0].Id, menuSettings.Theme.Id, "back to the first palette");
                report.Check(!menu.Adjust(-1), "the menu row holds at the first palette");

                menu.Move(1);
                report.Equal(MenuItem.Scale, menu.Selected, "the size row follows the palette row");
                report.Check(menu.Adjust(1), "the menu steps the size");
                report.Equal(1.15d, menuSettings.UiScale, "one step up in the menu is 115 %");
                report.Check(!menu.Adjust(0), "a step of zero changes nothing");

                // Both screens step the same field: a look chosen on the welcome screen is the look of
                // the game, because the session is created from this one settings object.
                report.Equal(GameMenu.TotalRows, menu.RowCount, "the panel and the model agree on the row count");
            }
            finally
            {
                Loc.Current = previous;
            }
        }
    }
}
