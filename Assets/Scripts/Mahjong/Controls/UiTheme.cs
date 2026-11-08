using System;
using System.Collections.Generic;
using System.Globalization;
using Mahjong.Localization;

namespace Mahjong.Controls
{
    /// <summary>
    /// What a colour is used for. The interface never names a colour directly: a panel asks for
    /// <see cref="UiRole.Surface"/>, a caption for <see cref="UiRole.TextMain"/>, and the theme decides
    /// what that is. Swapping the theme therefore repaints the whole screen without touching a single
    /// layout number.
    /// </summary>
    public enum UiRole
    {
        /// <summary>What stands behind the table: the camera clears to it.</summary>
        Background = 0,

        /// <summary>Panels that hold text and rows.</summary>
        Surface = 1,

        /// <summary>Rows, buttons and cards that sit on a panel.</summary>
        SurfaceHigh = 2,

        /// <summary>The fitted colour: the cursor, a chosen row, a turn of the player.</summary>
        Primary = 3,

        /// <summary>The thin gold line: borders, the value of a row, a title.</summary>
        Accent = 4,

        /// <summary>Body text.</summary>
        TextMain = 5,

        /// <summary>Labels, hints, anything the player reads second.</summary>
        TextMuted = 6,

        /// <summary>Text printed on a <see cref="Primary"/> or <see cref="Accent"/> fill.</summary>
        TextOnAccent = 7,

        /// <summary>Hairline around a panel or a row.</summary>
        Border = 8,

        /// <summary>Refusals, losses, forbidden moves.</summary>
        Critical = 9,

        /// <summary>The cloth of the table.</summary>
        Felt = 10,

        /// <summary>The wooden rim of the table.</summary>
        Wood = 11,

        /// <summary>The lacquer back of a face-down tile.</summary>
        TileBody = 12,

        /// <summary>The ivory field a tile face is printed on.</summary>
        TileFace = 13
    }

    /// <summary>
    /// One colour as the rules assembly keeps it: three bytes, no Unity. The interface catalogue is a
    /// plain C# table so the palettes, their contrast and their names are checked headless; the view
    /// turns a <see cref="UiColour"/> into a <c>UnityEngine.Color</c> and never invents a colour itself.
    /// </summary>
    public readonly struct UiColour : IEquatable<UiColour>
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;

        public UiColour(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>Reads <c>#RRGGBB</c> or <c>RRGGBB</c>; a broken string is black, never an exception.</summary>
        public static UiColour FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return new UiColour(0, 0, 0);
            var value = hex.Trim().TrimStart('#');
            if (value.Length != 6 ||
                !byte.TryParse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
                !byte.TryParse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
                !byte.TryParse(value[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return new UiColour(0, 0, 0);

            return new UiColour(r, g, b);
        }

        /// <summary>The colour as <c>#RRGGBB</c>, which is how a palette is written down in the docs.</summary>
        public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

        /// <summary>
        /// Relative luminance of WCAG 2.1 (sRGB), 0 for black and 1 for white. Used to check that a
        /// palette keeps a readable text colour on its own panels, whatever a player picks.
        /// </summary>
        public double Luminance()
        {
            static double Channel(byte value)
            {
                var c = value / 255d;
                return c <= 0.03928d ? c / 12.92d : Math.Pow((c + 0.055d) / 1.055d, 2.4d);
            }

            return 0.2126d * Channel(R) + 0.7152d * Channel(G) + 0.0722d * Channel(B);
        }

        /// <summary>Contrast ratio between two colours, 1 (same colour) to 21 (black on white).</summary>
        public static double Contrast(UiColour a, UiColour b)
        {
            var first = a.Luminance();
            var second = b.Luminance();
            var light = Math.Max(first, second);
            var dark = Math.Min(first, second);
            return (light + 0.05d) / (dark + 0.05d);
        }

        public bool Equals(UiColour other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object? obj) => obj is UiColour other && Equals(other);
        public override int GetHashCode() => (R << 16) | (G << 8) | B;
        public override string ToString() => ToHex();
        public static bool operator ==(UiColour a, UiColour b) => a.Equals(b);
        public static bool operator !=(UiColour a, UiColour b) => !a.Equals(b);
    }

    /// <summary>
    /// One palette of the interface: the fourteen roles a screen asks for, under one name. The presets
    /// are the answer to "make it look right, and let me change it" — a player picks a name, not eleven
    /// colours, and every panel follows on the next frame.
    /// </summary>
    public sealed class UiTheme
    {
        public UiTheme(string id, string nameRu, string nameEn, bool dark, IReadOnlyDictionary<UiRole, UiColour> roles)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            IsDark = dark;
            Roles = roles;
        }

        /// <summary>The name a settings file or <c>MAHJONG_THEME</c> uses: <c>night</c>, <c>dawn</c>…</summary>
        public string Id { get; }

        public string NameRu { get; }
        public string NameEn { get; }

        /// <summary>True when the background is darker than the text; a light theme inverts that.</summary>
        public bool IsDark { get; }

        private IReadOnlyDictionary<UiRole, UiColour> Roles { get; }

        /// <summary>The colour of a role. An unknown role is black, which shows up at once on screen.</summary>
        public UiColour Of(UiRole role) => Roles.TryGetValue(role, out var colour) ? colour : new UiColour(0, 0, 0);

        /// <summary>True when this palette paints the role; a palette that forgets one draws black.</summary>
        public bool Paints(UiRole role) => Roles.ContainsKey(role);

        /// <summary>The name of the theme in the language the player is reading now.</summary>
        public string Name => Loc.Current == Language.Ru ? NameRu : NameEn;

        public override string ToString() => $"{Id} ({NameRu})";
    }

    /// <summary>
    /// The palettes the game ships with, in the order the settings row walks them.
    ///
    /// Two dark ones (jade, amber), a deep green one and a light one: a player who reads the table at
    /// night and a player who reads it on a laptop in daylight need different screens, and the light
    /// theme is not a filter over the dark one — its text and border roles are chosen for paper.
    /// </summary>
    public static class UiThemes
    {
        /// <summary>Dark jade and gold: the table at night, the default.</summary>
        public static readonly UiTheme Night = Palette("night", "Нефритовая ночь", "Jade night", true,
            background: "#0F1A17", surface: "#1A2A24", surfaceHigh: "#26382F",
            primary: "#2E8B70", accent: "#C9A250", textMain: "#E8E4D9", textMuted: "#93A095",
            textOnAccent: "#0F1A17", border: "#3D5246", critical: "#C4453A",
            felt: "#1C4A3A", wood: "#4A3526", tileBody: "#2F5B46", tileFace: "#F6F1E4");

        /// <summary>Warm amber on dark walnut: the same table under a lamp.</summary>
        public static readonly UiTheme Dawn = Palette("dawn", "Янтарный рассвет", "Amber dawn", true,
            background: "#16110C", surface: "#251A12", surfaceHigh: "#35261A",
            primary: "#B0722C", accent: "#E2B760", textMain: "#F1E7D8", textMuted: "#B09A82",
            textOnAccent: "#1B120B", border: "#54402C", critical: "#C0512F",
            felt: "#3A2A1E", wood: "#5A3E27", tileBody: "#5A4030", tileFace: "#FBF5E8");

        /// <summary>Deep bamboo green and pale gold: the quietest of the dark ones.</summary>
        public static readonly UiTheme Bamboo = Palette("bamboo", "Бамбуковая роща", "Bamboo grove", true,
            background: "#0D1919", surface: "#16292A", surfaceHigh: "#1F3A3B",
            primary: "#3F9E8A", accent: "#D8C27A", textMain: "#E4F0E9", textMuted: "#8FA8A0",
            textOnAccent: "#0C1717", border: "#35534F", critical: "#CB4B3C",
            felt: "#16503E", wood: "#4B3A2A", tileBody: "#2E5A4C", tileFace: "#F7FAF0");

        /// <summary>Cream paper, forest green and amber: for a bright room.</summary>
        public static readonly UiTheme Ivory = Palette("ivory", "Слоновая кость", "Ivory paper", false,
            background: "#E8E1D0", surface: "#F7F3E8", surfaceHigh: "#FFFCF3",
            primary: "#1E6B4C", accent: "#9A6A22", textMain: "#24312A", textMuted: "#6E756E",
            textOnAccent: "#F9F6EC", border: "#BFAF97", critical: "#A83B2E",
            felt: "#2F6B54", wood: "#8A6A45", tileBody: "#3E6B4E", tileFace: "#FFFCF3");

        /// <summary>Every preset, in the order the settings row steps through them.</summary>
        public static readonly IReadOnlyList<UiTheme> All = new[] { Night, Dawn, Bamboo, Ivory };

        /// <summary>The palette a fresh table is painted with.</summary>
        public static UiTheme Default => Night;

        /// <summary>The preset with this id; the default when the id means nothing.</summary>
        public static UiTheme Find(string? id)
        {
            foreach (var theme in All)
                if (string.Equals(theme.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return theme;
            return Default;
        }

        private static UiTheme Palette(string id, string ru, string en, bool dark,
            string background, string surface, string surfaceHigh, string primary, string accent,
            string textMain, string textMuted, string textOnAccent, string border, string critical,
            string felt, string wood, string tileBody, string tileFace) =>
            new(id, ru, en, dark, new Dictionary<UiRole, UiColour>
            {
                [UiRole.Background] = UiColour.FromHex(background),
                [UiRole.Surface] = UiColour.FromHex(surface),
                [UiRole.SurfaceHigh] = UiColour.FromHex(surfaceHigh),
                [UiRole.Primary] = UiColour.FromHex(primary),
                [UiRole.Accent] = UiColour.FromHex(accent),
                [UiRole.TextMain] = UiColour.FromHex(textMain),
                [UiRole.TextMuted] = UiColour.FromHex(textMuted),
                [UiRole.TextOnAccent] = UiColour.FromHex(textOnAccent),
                [UiRole.Border] = UiColour.FromHex(border),
                [UiRole.Critical] = UiColour.FromHex(critical),
                [UiRole.Felt] = UiColour.FromHex(felt),
                [UiRole.Wood] = UiColour.FromHex(wood),
                [UiRole.TileBody] = UiColour.FromHex(tileBody),
                [UiRole.TileFace] = UiColour.FromHex(tileFace)
            });
    }

    /// <summary>
    /// The two look settings of the table — which palette and how big the interface is — as one ladder
    /// with one text and one place where a variable from the environment becomes a value. The welcome
    /// screen and the in-game menu both step these, so a player can change the look without leaving the
    /// table, and a capture run can ask for a palette from the command line.
    /// </summary>
    public static class UiThemeSettings
    {
        /// <summary>
        /// How big the interface is drawn: the smallest fits a small window, the largest a player who
        /// reads the captions from a sofa. Layout numbers stay as they are — every panel multiplies
        /// them by this factor, so a bigger interface is the same interface, not a different one.
        /// </summary>
        public static readonly double[] Scales = { 0.85d, 1d, 1.15d, 1.3d };

        /// <summary>The scale a fresh table is drawn at: the layout numbers are written for it.</summary>
        public const double DefaultScale = 1d;

        /// <summary>The theme a fresh table is painted with.</summary>
        public static UiTheme DefaultTheme => UiThemes.Default;

        /// <summary>Nearest step of <see cref="Scales"/>; a NaN keeps the default.</summary>
        public static double ClampScale(double scale)
        {
            if (double.IsNaN(scale)) return DefaultScale;
            var best = Scales[0];
            foreach (var step in Scales)
                if (Math.Abs(step - scale) < Math.Abs(best - scale)) best = step;
            return best;
        }

        /// <summary>Index of the nearest scale step, which is what the ladder steps from.</summary>
        public static int ScaleIndex(double scale)
        {
            var clamped = ClampScale(scale);
            for (var index = 0; index < Scales.Length; index++)
                if (Math.Abs(Scales[index] - clamped) < 0.0001d) return index;
            return Array.IndexOf(Scales, DefaultScale);
        }

        /// <summary>Steps the size one notch; the value stays where it is at either end.</summary>
        public static double StepScale(double scale, int delta)
        {
            if (delta == 0) return ClampScale(scale);
            var index = Math.Clamp(ScaleIndex(scale) + Math.Sign(delta), 0, Scales.Length - 1);
            return Scales[index];
        }

        /// <summary>The size as a row prints it: <c>85%</c>, <c>100%</c>, the same in both languages.</summary>
        public static string ScaleText(double scale) =>
            ((int)Math.Round(ClampScale(scale) * 100d)).ToString(CultureInfo.InvariantCulture) + "%";

        /// <summary>Next preset in the order of <see cref="UiThemes.All"/>; it stops at both ends.</summary>
        public static UiTheme Step(UiTheme current, int delta)
        {
            if (delta == 0 || current == null) return current ?? DefaultTheme;
            var index = 0;
            for (var i = 0; i < UiThemes.All.Count; i++)
                if (UiThemes.All[i].Id == current.Id) index = i;

            var next = Math.Clamp(index + Math.Sign(delta), 0, UiThemes.All.Count - 1);
            return UiThemes.All[next];
        }

        /// <summary>
        /// A palette from text: <c>night</c>, <c>ivory</c>, or a Russian name. Null when the text means
        /// nothing, so a broken <c>MAHJONG_THEME</c> keeps the default instead of stopping the table.
        /// </summary>
        public static UiTheme? TryParseTheme(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var value = text!.Trim().ToLowerInvariant();
            foreach (var theme in UiThemes.All)
                if (string.Equals(theme.Id, value, StringComparison.Ordinal) ||
                    theme.NameRu.ToLowerInvariant() == value ||
                    theme.NameEn.ToLowerInvariant() == value)
                    return theme;

            return null;
        }

        /// <summary>
        /// A size from text: <c>1.15</c>, <c>115%</c> or <c>115</c>. Null when the text means nothing.
        /// A number off the ladder snaps to the nearest step, so a hand-written 1.1 is the 1.15 look.
        /// </summary>
        public static double? TryParseScale(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var value = text!.Trim().ToLowerInvariant();
            if (value.EndsWith("%", StringComparison.Ordinal)) value = value[..^1].Trim();

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return null;

            if (number > 3d) number /= 100d;
            return ClampScale(number);
        }
    }
}
